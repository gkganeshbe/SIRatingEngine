
using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Text.Json;

// Usage:
// dotnet run -- --excel ./Rating Example.xlsx --out ./data/rates --product HO-PRIMARY --version 2026.02 --to json
// dotnet run -- --excel ./Rating Example.xlsx --connection "Server=.;Database=Rating;Trusted_Connection=True;TrustServerCertificate=True" --to sql

var argsDict = Args.Parse(args);
if (!argsDict.TryGetValue("--excel", out var excelPath))
{
    Console.Error.WriteLine("--excel path is required");
    return;
}

var output = argsDict.GetValueOrDefault("--out", "./data/rates");
var mode = argsDict.GetValueOrDefault("--to", "json");
var product = argsDict.GetValueOrDefault("--product", "HO-PRIMARY");
var version = argsDict.GetValueOrDefault("--version", "2026.02");
var connection = argsDict.GetValueOrDefault("--connection", "");

Directory.CreateDirectory(output);

using var wb = new XLWorkbook(excelPath);

// Simple convention: first row = header, known sheets map to rate tables
var sheetMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    {"BaseLossCost", "BaseLossCost"},
    {"AmountOfInsuranceFactor", "AmountOfInsuranceFactor"},
    {"LCM", "LCM"},
    {"PerilSubzoneFactor", "PerilSubzoneFactor"},
    {"ProtectionClassFactor", "ProtectionClassFactor"}
};

foreach (var kv in sheetMap)
{
    if (!wb.Worksheets.TryGetWorksheet(kv.Key, out var ws)) continue;
    var rows = new List<RateRowImport>();
    var header = ws.FirstRowUsed()?.Cells().Select(c => c.GetString()).ToArray() ?? [];
    foreach (var row in ws.RowsUsed().Skip(1))
    {
        var cells = row.Cells(1, header.Length).Select(c => c.GetString()).ToArray();
        var dict = header.Zip(cells, (h, v) => new { h, v })
                         .ToDictionary(x => x.h, x => x.v, StringComparer.OrdinalIgnoreCase);
        var rr = RateRowImport.From(dict);
        rows.Add(rr);
    }

    if (mode.Equals("json", StringComparison.OrdinalIgnoreCase))
    {
        File.WriteAllText(Path.Combine(output, kv.Value + ".json"), JsonSerializer.Serialize(rows.Select(r => r.ToRateRow()), new JsonSerializerOptions{WriteIndented = true}));
        Console.WriteLine($"Wrote {rows.Count} rows to {kv.Value}.json");
    }
    else if (mode.Equals("sql", StringComparison.OrdinalIgnoreCase))
    {
        if (string.IsNullOrWhiteSpace(connection)) throw new Exception("--connection required for sql mode");
        using var con = new SqlConnection(connection);
        con.Open();
        foreach (var r in rows)
        {
            await con.ExecuteAsync(@"insert into RateRow (RateTableId, Key1, Key2, Key3, Key4, Key5, Factor, Additive, EffStart, EffEnd, Jurisdiction)
              select rt.RateTableId, @Key1,@Key2,@Key3,@Key4,@Key5,@Factor,@Additive,@EffStart,@EffEnd,@Jurisdiction
              from RateTable rt where rt.Name=@Name and rt.ProductVersionId = (select top 1 ProductVersionId from ProductVersion where ProductCode=@Product and Version=@Version)",
              new {
                Name = kv.Value, r.Key1, r.Key2, r.Key3, r.Key4, r.Key5, r.Factor, r.Additive, r.EffStart, r.EffEnd, Jurisdiction = r.Jurisdiction,
                Product = product, Version = version
              });
        }
        Console.WriteLine($"Inserted {rows.Count} rows into SQL for {kv.Value}");
    }
}

class Args
{
    public static Dictionary<string,string> Parse(string[] args)
    {
        var d = new Dictionary<string,string>();
        for (int i=0;i<args.Length;i++)
        {
            if (args[i].StartsWith("--"))
            {
                var key = args[i];
                var val = (i+1 < args.Length && !args[i+1].StartsWith("--")) ? args[++i] : "";
                d[key]=val;
            }
        }
        return d;
    }
}

record RateRowImport(
    string? Key1, string? Key2, string? Key3, string? Key4, string? Key5,
    decimal? Factor, decimal? Additive, DateOnly EffStart, DateOnly? EffEnd, string? Jurisdiction
)
{
    public static RateRowImport From(Dictionary<string,string> row)
    {
        return new RateRowImport(
            row.GetValueOrDefault("Key1"), row.GetValueOrDefault("Key2"), row.GetValueOrDefault("Key3"), row.GetValueOrDefault("Key4"), row.GetValueOrDefault("Key5"),
            ParseDecimal(row.GetValueOrDefault("Factor")), ParseDecimal(row.GetValueOrDefault("Additive")),
            DateOnly.Parse(row.GetValueOrDefault("EffStart") ?? DateTime.Today.ToString("yyyy-MM-dd")),
            string.IsNullOrWhiteSpace(row.GetValueOrDefault("EffEnd")) ? null : DateOnly.Parse(row.GetValueOrDefault("EffEnd")!),
            row.GetValueOrDefault("Jurisdiction")
        );
    }

    public object ToRateRow() => new { Key1, Key2, Key3, Key4, Key5, Factor, Additive, EffStart, EffEnd };
    static decimal? ParseDecimal(string? s) => decimal.TryParse(s, out var d) ? d : null;
}
