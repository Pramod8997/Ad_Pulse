using System.Globalization;
using System.Security;
using System.Text;
using System.Text.Json;
using AdPulse.Api.Contracts;
using AdPulse.Domain.Tenancy;
using AdPulse.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

#pragma warning disable CA1716

namespace AdPulse.Api.Imports;

public sealed class ImportWorkflow
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly CsvImportParser _parser;

    public ImportWorkflow()
        : this(new CsvImportParser())
    {
    }

    public ImportWorkflow(CsvImportParser parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public async Task<ImportResult> UploadAsync(
        AdPulseDbContext db,
        TenantContext tenant,
        string? fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantScope(db, tenant);
        ArgumentNullException.ThrowIfNull(content);
        var csv = _parser.Parse(await ReadUtf8Async(content, cancellationToken));
        var safeFileName = SafeFileName(fileName);
        var rejections = new Dictionary<int, ImportRowRejection>();
        var candidates = new List<ImportMetricCandidate>(csv.Rows.Count);

        foreach (var row in csv.Rows)
        {
            if (row.Fields.Count != ImportContract.Headers.Count)
            {
                AddRejection(
                    rejections,
                    row.RowNumber,
                    "malformed_row",
                    "The row does not contain the expected number of columns.");
                continue;
            }

            if (!ImportRowValidator.TryParse(row, out var candidate, out var rejection))
            {
                AddRejection(rejections, rejection!.RowNumber, rejection.ErrorCode, rejection.Message);
                continue;
            }

            candidates.Add(candidate!);
        }

        var campaigns = await LoadCampaignsAsync(db, candidates, cancellationToken);
        var existingKeys = await LoadExistingMetricKeysAsync(db, candidates, cancellationToken);
        var duplicateKeys = candidates
            .GroupBy(x => new MetricKey(x.CampaignId, x.MetricDate))
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();
        var acceptedKeys = new HashSet<MetricKey>();
        var accepted = new List<ImportMetricCandidate>(candidates.Count);

        foreach (var candidate in candidates)
        {
            if (!campaigns.TryGetValue(candidate.CampaignId, out var campaign))
            {
                AddRejection(
                    rejections,
                    candidate.RowNumber,
                    "campaign_not_found",
                    "The referenced campaign is not available in this tenant.");
                continue;
            }

            if (!string.Equals(candidate.Currency, campaign.Currency, StringComparison.Ordinal))
            {
                AddRejection(
                    rejections,
                    candidate.RowNumber,
                    "currency_mismatch",
                    "The row currency does not match the campaign currency.");
                continue;
            }

            if (candidate.MetricDate < campaign.StartsOn || candidate.MetricDate > campaign.EndsOn)
            {
                AddRejection(
                    rejections,
                    candidate.RowNumber,
                    "date_out_of_range",
                    "The metric date is outside the campaign date range.");
                continue;
            }

            var key = new MetricKey(candidate.CampaignId, candidate.MetricDate);
            if (duplicateKeys.Contains(key) || existingKeys.Contains(key) || !acceptedKeys.Add(key))
            {
                AddRejection(
                    rejections,
                    candidate.RowNumber,
                    "duplicate_daily_key",
                    "A metric already exists for this campaign and date.");
                continue;
            }

            accepted.Add(candidate);
        }

        var orderedRejections = rejections.Values.OrderBy(x => x.RowNumber).ToArray();
        var batch = new ImportBatch
        {
            TenantId = tenant.TenantId,
            Id = Guid.NewGuid(),
            UploadedBySubjectId = tenant.SubjectId,
            UploadedAt = DateTimeOffset.UtcNow,
            FileName = safeFileName,
            AcceptedRows = accepted.Count,
            RejectedRows = orderedRejections.Length,
            Status = accepted.Count > 0 ? ImportContract.ValidatedStatus : ImportContract.RejectedStatus
        };

        db.ImportBatches.Add(batch);
        db.ImportRowErrors.AddRange(orderedRejections.Select(error => new ImportRowError
        {
            TenantId = tenant.TenantId,
            ImportBatchId = batch.Id,
            RowNumber = error.RowNumber,
            ErrorCode = error.ErrorCode,
            Message = error.Message
        }));
        db.ImportPendingMetrics.AddRange(accepted.Select(row => new ImportPendingMetric
        {
            TenantId = tenant.TenantId,
            ImportBatchId = batch.Id,
            RowNumber = row.RowNumber,
            CampaignId = row.CampaignId,
            MetricDate = row.MetricDate,
            Spend = row.Spend,
            Revenue = row.Revenue,
            Impressions = row.Impressions,
            Clicks = row.Clicks,
            Conversions = row.Conversions
        }));
        await db.SaveChangesAsync(cancellationToken);

        return new ImportResult(
            batch.Id,
            batch.FileName,
            batch.UploadedAt,
            batch.Status,
            batch.AcceptedRows,
            batch.RejectedRows,
            orderedRejections);
    }

    public async Task<ImportResult?> ConfirmAsync(
        AdPulseDbContext db,
        TenantContext tenant,
        Guid importBatchId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantScope(db, tenant);
        var batch = await db.ImportBatches
            .SingleOrDefaultAsync(x => x.Id == importBatchId, cancellationToken);
        if (batch is null)
        {
            return null;
        }

        var errors = await ReadErrorsAsync(db, batch.Id, cancellationToken);
        if (batch.Status is ImportContract.CompletedStatus or ImportContract.RejectedStatus)
        {
            return ToResult(batch, errors);
        }

        if (!string.Equals(batch.Status, ImportContract.ValidatedStatus, StringComparison.Ordinal))
        {
            throw new ImportConflictException("The import is not available for confirmation.");
        }

        var pending = await db.ImportPendingMetrics
            .Where(x => x.ImportBatchId == batch.Id)
            .OrderBy(x => x.RowNumber)
            .ToArrayAsync(cancellationToken);
        if (pending.Length == 0 && batch.AcceptedRows > 0)
        {
            throw new ImportConflictException("The validation result is no longer available; upload the file again.");
        }

        var currentKeys = await LoadExistingMetricKeysAsync(
            db,
            pending.Select(row => new ImportMetricCandidate(
                row.RowNumber,
                row.CampaignId,
                row.MetricDate,
                string.Empty,
                row.Spend,
                row.Revenue,
                row.Impressions,
                row.Clicks,
                row.Conversions)),
            cancellationToken);
        if (currentKeys.Count > 0)
        {
            throw new ImportConflictException("The import changed before confirmation; upload the file again.");
        }

        foreach (var row in pending)
        {
            db.DailyMetrics.Add(new DailyMetric
            {
                TenantId = tenant.TenantId,
                CampaignId = row.CampaignId,
                MetricDate = row.MetricDate,
                Spend = row.Spend,
                Revenue = row.Revenue,
                Impressions = row.Impressions,
                Clicks = row.Clicks,
                Conversions = row.Conversions
            });
        }

        batch.Status = ImportContract.CompletedStatus;
        db.ImportPendingMetrics.RemoveRange(pending);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ImportConflictException("The import changed before confirmation; upload the file again.");
        }
        return ToResult(batch, errors);
    }

    public async Task<CursorPage<ImportResult>> ListAsync(
        AdPulseDbContext db,
        TenantContext tenant,
        string? cursor = null,
        int limit = PageQuery.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantScope(db, tenant);
        if (limit is < 1 or > PageQuery.MaxLimit)
        {
            throw new ImportValidationException(
                "invalid_limit",
                $"The page limit must be between 1 and {PageQuery.MaxLimit}.");
        }

        var decodedCursor = ImportCursorCodec.Decode(cursor, tenant.TenantId);
        var batches = db.ImportBatches
            .AsNoTracking()
            .Where(x => x.TenantId == tenant.TenantId);

        List<ImportBatch> rows;
        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            // SQLite cannot translate DateTimeOffset ordering/comparisons; SQL Server keeps this server-side.
            var candidates = await batches.ToListAsync(cancellationToken);
            rows = candidates
                .Where(x => IsAfterCursor(x, decodedCursor))
                .OrderByDescending(x => x.UploadedAt.ToUniversalTime())
                .ThenByDescending(x => x.Id)
                .Take(limit + 1)
                .ToList();
        }
        else
        {
            if (decodedCursor is not null)
            {
                batches = batches.Where(x =>
                    x.UploadedAt < decodedCursor.UploadedAt
                    || (x.UploadedAt == decodedCursor.UploadedAt && x.Id.CompareTo(decodedCursor.Id) < 0));
            }

            rows = await batches
                .OrderByDescending(x => x.UploadedAt)
                .ThenByDescending(x => x.Id)
                .Take(limit + 1)
                .ToListAsync(cancellationToken);
        }

        var hasMore = rows.Count > limit;
        var pageRows = hasMore ? rows.Take(limit).ToArray() : rows.ToArray();
        var errorsByBatch = await ReadErrorsForBatchesAsync(
            db,
            pageRows.Select(x => x.Id).ToArray(),
            cancellationToken);
        var items = pageRows
            .Select(batch => ToResult(
                batch,
                errorsByBatch.TryGetValue(batch.Id, out var errors) ? errors : []))
            .ToArray();

        return new CursorPage<ImportResult>
        {
            Items = items,
            NextCursor = hasMore && pageRows.Length > 0
                ? ImportCursorCodec.Encode(pageRows[^1].UploadedAt.ToUniversalTime(), pageRows[^1].Id, tenant.TenantId)
                : null,
            HasMore = hasMore,
            Limit = limit
        };
    }

    public async Task<ImportResult?> GetResultAsync(
        AdPulseDbContext db,
        TenantContext tenant,
        Guid importBatchId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantScope(db, tenant);
        var batch = await db.ImportBatches
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == importBatchId, cancellationToken);
        if (batch is null)
        {
            return null;
        }

        return ToResult(batch, await ReadErrorsAsync(db, batch.Id, cancellationToken));
    }

    private static async Task<string> ReadUtf8Async(Stream content, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        var total = 0L;
        int read;
        while ((read = await content.ReadAsync(chunk.AsMemory(), cancellationToken)) > 0)
        {
            total += read;
            if (total > ImportContract.MaxFileBytes)
            {
                throw new ImportFileTooLargeException();
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        try
        {
            return StrictUtf8.GetString(buffer.ToArray());
        }
        catch (DecoderFallbackException)
        {
            throw new ImportValidationException("invalid_encoding", "The CSV must be UTF-8 encoded.");
        }
    }

    private static async Task<Dictionary<Guid, Campaign>> LoadCampaignsAsync(
        AdPulseDbContext db,
        IEnumerable<ImportMetricCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var campaignIds = candidates.Select(x => x.CampaignId).Distinct().ToArray();
        var campaigns = new Dictionary<Guid, Campaign>();
        foreach (var chunk in campaignIds.Chunk(500))
        {
            var loaded = await db.Campaigns
                .AsNoTracking()
                .Where(x => chunk.Contains(x.Id))
                .ToArrayAsync(cancellationToken);
            foreach (var campaign in loaded)
            {
                campaigns[campaign.Id] = campaign;
            }
        }

        return campaigns;
    }

    private static async Task<HashSet<MetricKey>> LoadExistingMetricKeysAsync(
        AdPulseDbContext db,
        IEnumerable<ImportMetricCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var rows = candidates.ToArray();
        var campaignIds = rows.Select(x => x.CampaignId).Distinct().ToArray();
        if (campaignIds.Length == 0)
        {
            return [];
        }

        var dates = rows.Select(x => x.MetricDate).ToArray();
        var minimumDate = dates.Min();
        var maximumDate = dates.Max();
        var existing = new HashSet<MetricKey>();
        foreach (var chunk in campaignIds.Chunk(500))
        {
            var loaded = await db.DailyMetrics
                .AsNoTracking()
                .Where(x => chunk.Contains(x.CampaignId)
                    && x.MetricDate >= minimumDate
                    && x.MetricDate <= maximumDate)
                .Select(x => new { x.CampaignId, x.MetricDate })
                .ToArrayAsync(cancellationToken);
            foreach (var metric in loaded)
            {
                existing.Add(new MetricKey(metric.CampaignId, metric.MetricDate));
            }
        }

        return existing;
    }

    private static async Task<IReadOnlyList<ImportRowRejection>> ReadErrorsAsync(
        AdPulseDbContext db,
        Guid batchId,
        CancellationToken cancellationToken) =>
        await db.ImportRowErrors
            .AsNoTracking()
            .Where(x => x.ImportBatchId == batchId)
            .OrderBy(x => x.RowNumber)
            .Select(x => new ImportRowRejection(x.RowNumber, x.ErrorCode, x.Message))
            .ToArrayAsync(cancellationToken);

    private static async Task<Dictionary<Guid, IReadOnlyList<ImportRowRejection>>> ReadErrorsForBatchesAsync(
        AdPulseDbContext db,
        Guid[] batchIds,
        CancellationToken cancellationToken)
    {
        if (batchIds.Length == 0)
        {
            return [];
        }

        var errors = await db.ImportRowErrors
            .AsNoTracking()
            .Where(x => batchIds.Contains(x.ImportBatchId))
            .OrderBy(x => x.RowNumber)
            .Select(x => new { x.ImportBatchId, Rejection = new ImportRowRejection(x.RowNumber, x.ErrorCode, x.Message) })
            .ToArrayAsync(cancellationToken);

        return errors
            .GroupBy(x => x.ImportBatchId)
            .ToDictionary(
                group => group.Key,
                IReadOnlyList<ImportRowRejection> (group) => group.Select(x => x.Rejection).ToArray());
    }

    private static void AddRejection(
        IDictionary<int, ImportRowRejection> rejections,
        int rowNumber,
        string code,
        string message) =>
        rejections.TryAdd(rowNumber, Reject(rowNumber, code, message));

    private static ImportRowRejection Reject(int rowNumber, string code, string message) =>
        new(rowNumber, code, message);

    private static ImportResult ToResult(ImportBatch batch, IReadOnlyList<ImportRowRejection> errors) =>
        new(
            batch.Id,
            batch.FileName,
            batch.UploadedAt.ToUniversalTime(),
            batch.Status,
            batch.AcceptedRows,
            batch.RejectedRows,
            errors);

    private static string SafeFileName(string? fileName)
    {
        var safe = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(safe))
        {
            return "upload.csv";
        }

        return safe.Length <= ImportContract.MaxFileNameLength
            ? safe
            : safe[..ImportContract.MaxFileNameLength];
    }

    private static void EnsureTenantScope(AdPulseDbContext db, TenantContext tenant)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(tenant);
        if (db.Scope.IsSystemScope
            || string.IsNullOrWhiteSpace(tenant.TenantId)
            || !string.Equals(db.Scope.TenantId, tenant.TenantId, StringComparison.Ordinal))
        {
            throw new SecurityException("The import context is outside the authorized tenant.");
        }
    }

    private static bool IsAfterCursor(ImportBatch batch, ImportCursor? cursor)
    {
        if (cursor is null)
        {
            return true;
        }

        var uploadedAt = batch.UploadedAt.ToUniversalTime();
        return uploadedAt < cursor.UploadedAt
            || (uploadedAt == cursor.UploadedAt && batch.Id.CompareTo(cursor.Id) < 0);
    }

    private sealed record ImportCursor(DateTimeOffset UploadedAt, Guid Id, string TenantId);

    private static class ImportCursorCodec
    {
        private const int MaxCursorLength = 512;

        public static string Encode(DateTimeOffset uploadedAt, Guid id, string tenantId)
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(new ImportCursor(uploadedAt.ToUniversalTime(), id, tenantId));
            return Convert.ToBase64String(json)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        public static ImportCursor? Decode(string? encoded, string expectedTenantId)
        {
            if (encoded is null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(encoded) || encoded.Length > MaxCursorLength)
            {
                throw new ImportValidationException("invalid_cursor", "The import cursor is invalid.");
            }

            try
            {
                var base64 = encoded.Replace('-', '+').Replace('_', '/');
                base64 += (base64.Length % 4) switch
                {
                    0 => string.Empty,
                    2 => "==",
                    3 => "=",
                    _ => throw new FormatException()
                };

                var cursor = JsonSerializer.Deserialize<ImportCursor>(Convert.FromBase64String(base64));
                if (cursor is null
                    || cursor.Id == Guid.Empty
                    || cursor.UploadedAt == default
                    || cursor.UploadedAt.Offset != TimeSpan.Zero
                    || string.IsNullOrWhiteSpace(cursor.TenantId))
                {
                    throw new FormatException();
                }

                if (!string.Equals(cursor.TenantId, expectedTenantId, StringComparison.Ordinal))
                {
                    throw new ImportValidationException("invalid_cursor", "The import cursor is invalid for this tenant.");
                }

                return cursor;
            }
            catch (FormatException)
            {
                throw new ImportValidationException("invalid_cursor", "The import cursor is invalid.");
            }
            catch (JsonException)
            {
                throw new ImportValidationException("invalid_cursor", "The import cursor is invalid.");
            }
        }
    }

    private readonly record struct MetricKey(Guid CampaignId, DateOnly MetricDate);

}
