using System.Text;
using AdPulse.Api.Contracts;
using AdPulse.Domain.Tenancy;
using AdPulse.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

#pragma warning disable CA1716

namespace AdPulse.Api.Imports;

public static class ImportEndpoints
{
    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup(ImportContract.Route).RequireAuthorization();
        group.MapGet("/template", (TenantContext tenant) =>
            ImportContract.HasImportPermission(tenant)
                ? Results.File(
                    Encoding.UTF8.GetBytes(ImportTemplate.Content),
                    ImportContract.TemplateContentType,
                    ImportContract.TemplateFileName)
                : Results.Forbid());

        group.MapGet("", ListAsync);
        group.MapPost("", UploadAsync);
        group.MapPost("/{importBatchId:guid}/confirm", ConfirmAsync);
        group.MapGet("/{importBatchId:guid}", GetResultAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        string? cursor,
        int? limit,
        AdPulseDbContext db,
        TenantContext tenant,
        CancellationToken cancellationToken)
    {
        if (!ImportContract.HasImportPermission(tenant))
        {
            return Results.Forbid();
        }

        try
        {
            var page = await new ImportWorkflow().ListAsync(
                db,
                tenant,
                cursor,
                limit ?? PageQuery.DefaultLimit,
                cancellationToken);
            return Results.Ok(page);
        }
        catch (ImportValidationException exception)
        {
            return Results.BadRequest(new { code = exception.Code, message = exception.Message });
        }
    }

    private static async Task<IResult> UploadAsync(
        HttpContext httpContext,
        AdPulseDbContext db,
        TenantContext tenant,
        CancellationToken cancellationToken)
    {
        if (!ImportContract.HasImportPermission(tenant))
        {
            return Results.Forbid();
        }

        var form = await httpContext.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file") ?? (form.Files.Count == 1 ? form.Files[0] : null);
        if (file is null)
        {
            return Results.BadRequest(new { code = "file_required", message = "A CSV file is required." });
        }

        if (file.Length > ImportContract.MaxFileBytes)
        {
            return Results.StatusCode(StatusCodes.Status413RequestEntityTooLarge);
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await new ImportWorkflow().UploadAsync(
                db,
                tenant,
                file.FileName,
                stream,
                cancellationToken);
            return Results.Created($"{ImportContract.Route}/{result.ImportBatchId}", result);
        }
        catch (ImportFileTooLargeException)
        {
            return Results.StatusCode(StatusCodes.Status413RequestEntityTooLarge);
        }
        catch (ImportValidationException exception)
        {
            return Results.BadRequest(new { code = exception.Code, message = exception.Message });
        }
    }

    private static async Task<IResult> ConfirmAsync(
        Guid importBatchId,
        AdPulseDbContext db,
        TenantContext tenant,
        CancellationToken cancellationToken)
    {
        if (!ImportContract.HasImportPermission(tenant))
        {
            return Results.Forbid();
        }

        try
        {
            var result = await new ImportWorkflow().ConfirmAsync(db, tenant, importBatchId, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (ImportConflictException exception)
        {
            return Results.Conflict(new { code = "import_conflict", message = exception.Message });
        }
    }

    private static async Task<IResult> GetResultAsync(
        Guid importBatchId,
        AdPulseDbContext db,
        TenantContext tenant,
        CancellationToken cancellationToken)
    {
        if (!ImportContract.HasImportPermission(tenant))
        {
            return Results.Forbid();
        }

        var result = await new ImportWorkflow().GetResultAsync(db, tenant, importBatchId, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }
}

public static class ImportTemplate
{
    public const string Content =
        "campaign_id,metric_date,currency,spend,revenue,impressions,clicks,conversions\n";
}
