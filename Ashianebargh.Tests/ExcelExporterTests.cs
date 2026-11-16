using ClosedXML.Excel;
using Ashianebargh.Application.Exports;
using Ashianebargh.Domain.Queries;
using Xunit;

namespace Ashianebargh.Tests;

/// <summary>
/// خروجی اکسل: رگرسیونِ یافتهٔ «تزریق فرمول و setterِ .Value هدرها».
/// متنِ شروع‌شده با = باید literal بماند (نه فرمول) و هدرها هم با SetValue نوشته شوند.
/// </summary>
public class ExcelExporterTests
{
    private static ReportTable TableWithText(string value) => new(
        new[] { new ReportColumn("name", "نام") },
        new[] { new Dictionary<string, object?> { ["name"] = value } });

    [Fact]
    public void ClosedXml_SetValue_LeadingEquals_IsLiteralText_NotFormula()
    {
        // تستِ رفتار ClosedXML 0.104.2: SetValue رشتهٔ «=…» را متنِ literal می‌گذارد،
        // برخلاف .Value که آن را فرمول می‌کند. مبنای امن‌بودنِ مسیر داده.
        using var workbook = new XLWorkbook();
        var cell = workbook.Worksheets.Add("s").Cell(1, 1);

        cell.SetValue("=1+1");

        Assert.Equal(XLDataType.Text, cell.DataType);
        Assert.True(string.IsNullOrEmpty(cell.FormulaA1));
        Assert.Equal("=1+1", cell.GetString());
    }

    [Fact]
    public void Export_TextStartingWithEquals_IsNeutralized_AndNotFormula()
    {
        var exporter = new ExcelExporter();
        var bytes = exporter.Export(TableWithText("=HYPERLINK(\"http://evil\",\"click\")"));

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var cell = workbook.Worksheet(1).Cell(2, 1);

        Assert.Equal(XLDataType.Text, cell.DataType);
        Assert.True(string.IsNullOrEmpty(cell.FormulaA1));
        // آپاستروفِ پیشوند مثل اکسل به‌صورت QuotePrefix ذخیره می‌شود (نه بخشی از
        // متنِ نمایشی) و سلول را literal نگه می‌دارد؛ پس GetString بدون آن برمی‌گردد.
        Assert.True(cell.Style.IncludeQuotePrefix);
    }

    [Fact]
    public void Export_HeaderTitle_IsLiteralText_NotFormula()
    {
        var exporter = new ExcelExporter();
        var table = new ReportTable(
            new[] { new ReportColumn("name", "=عنوان") },
            Array.Empty<IReadOnlyDictionary<string, object?>>());

        var bytes = exporter.Export(table);

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var header = workbook.Worksheet(1).Cell(1, 1);

        Assert.Equal(XLDataType.Text, header.DataType);
        Assert.True(string.IsNullOrEmpty(header.FormulaA1));
    }

    [Fact]
    public void Export_PlainText_IsNotAltered()
    {
        var exporter = new ExcelExporter();
        var bytes = exporter.Export(TableWithText("محصول سالم"));

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);

        Assert.Equal("محصول سالم", workbook.Worksheet(1).Cell(2, 1).GetString());
    }

    [Fact]
    public void Export_NumericColumn_StaysTypedNumber()
    {
        var exporter = new ExcelExporter();
        var table = new ReportTable(
            new[] { new ReportColumn("price", "قیمت", ReportColumnKind.Money) },
            new[] { new Dictionary<string, object?> { ["price"] = "1234" } });

        var bytes = exporter.Export(table);

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var cell = workbook.Worksheet(1).Cell(2, 1);

        Assert.Equal(XLDataType.Number, cell.DataType);
        Assert.Equal(1234d, cell.GetDouble());
    }
}