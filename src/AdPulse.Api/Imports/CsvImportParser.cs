using System.Text;

#pragma warning disable CA1716

namespace AdPulse.Api.Imports;

public sealed record CsvImportRow(int RowNumber, IReadOnlyList<string> Fields);

public sealed record CsvImportFile(IReadOnlyList<string> Headers, IReadOnlyList<CsvImportRow> Rows);

public sealed class CsvImportParser
{
    public CsvImportFile Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var records = ParseRecords(content);
        if (records.Count == 0)
        {
            throw new ImportValidationException("malformed_headers", "The CSV header is required.");
        }

        var headers = records[0];
        if (headers.Count != ImportContract.Headers.Count
            || headers.Select((value, index) => index == 0 ? value.TrimStart('\uFEFF') : value)
                .Select(value => value.Trim())
                .Where((value, index) => index < ImportContract.Headers.Count)
                .Where((value, index) => !string.Equals(value, ImportContract.Headers[index], StringComparison.Ordinal))
                .Any())
        {
            throw new ImportValidationException(
                "malformed_headers",
                "The CSV header must match the download template exactly.");
        }

        var rows = records
            .Skip(1)
            .Select((fields, index) => new CsvImportRow(index + 2, fields))
            .ToArray();

        if (rows.Length > ImportContract.MaxDataRows)
        {
            throw new ImportValidationException(
                "too_many_rows",
                $"The CSV file contains more than {ImportContract.MaxDataRows:N0} data rows.");
        }

        return new CsvImportFile(ImportContract.Headers.ToArray(), rows);
    }

    private static List<IReadOnlyList<string>> ParseRecords(string content)
    {
        var records = new List<IReadOnlyList<string>>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var afterClosingQuote = false;
        var hasRecordContent = false;

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];
            if (inQuotes)
            {
                if (character == '"')
                {
                    if (index + 1 < content.Length && content[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                        afterClosingQuote = true;
                    }
                }
                else
                {
                    field.Append(character);
                }

                continue;
            }

            if (afterClosingQuote)
            {
                if (character == ',')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    afterClosingQuote = false;
                    hasRecordContent = true;
                    continue;
                }

                if (character is '\r' or '\n')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    AddRecord(records, fields, hasRecordContent: true);
                    fields = new List<string>();
                    afterClosingQuote = false;
                    hasRecordContent = false;
                    if (character == '\r' && index + 1 < content.Length && content[index + 1] == '\n')
                    {
                        index++;
                    }

                    continue;
                }

                throw new ImportValidationException("malformed_csv", "The CSV contains an invalid quoted field.");
            }

            if (character == '"')
            {
                if (field.Length != 0)
                {
                    throw new ImportValidationException("malformed_csv", "The CSV contains an invalid quoted field.");
                }

                inQuotes = true;
                hasRecordContent = true;
                continue;
            }

            if (character == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
                hasRecordContent = true;
                continue;
            }

            if (character is '\r' or '\n')
            {
                if (hasRecordContent || fields.Count > 0 || field.Length > 0)
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    AddRecord(records, fields, hasRecordContent);
                    fields = new List<string>();
                }

                hasRecordContent = false;
                if (character == '\r' && index + 1 < content.Length && content[index + 1] == '\n')
                {
                    index++;
                }

                continue;
            }

            field.Append(character);
            hasRecordContent = true;
        }

        if (inQuotes)
        {
            throw new ImportValidationException("malformed_csv", "The CSV contains an unterminated quoted field.");
        }

        if (afterClosingQuote || hasRecordContent || fields.Count > 0 || field.Length > 0)
        {
            fields.Add(field.ToString());
            AddRecord(records, fields, hasRecordContent: true);
        }

        return records;
    }

    private static void AddRecord(
        List<IReadOnlyList<string>> records,
        IReadOnlyList<string> fields,
        bool hasRecordContent)
    {
        if (hasRecordContent || fields.Count > 0)
        {
            records.Add(fields.ToArray());
        }
    }
}
