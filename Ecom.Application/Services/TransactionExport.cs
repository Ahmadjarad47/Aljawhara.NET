using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Ecom.Application.DTOs.Order;

namespace Ecom.Application.Services;

internal static class TransactionExport
{
    private static readonly string[] Headers =
        ["ID", "Order Number", "Customer Name", "Amount", "Payment Method", "Status", "Transaction Date", "Refunded"];
    private const string DateFormat = "yyyy-MM-dd HH:mm:ss";

    internal static byte[] Csv(IEnumerable<TransactionAdvancedDto> transactions)
    {
        var csv = new StringBuilder();
        Row(Headers);
        foreach (var t in transactions)
            Row([Invariant(t.Id), Neutralize(t.OrderNumber), Neutralize(t.CustomerName), Invariant(t.Amount),
                Neutralize(t.PaymentMethodName), Neutralize(t.Status.ToString()), Invariant(t.TransactionDate, DateFormat),
                t.IsRefunded.ToString()]);
        return Encoding.UTF8.GetBytes(csv.ToString());

        void Row(IEnumerable<string> fields) =>
            csv.AppendJoin(',', fields.Select(field => $"\"{field.Replace("\"", "\"\"")}\"")).Append("\r\n");
    }

    internal static byte[] Excel(IEnumerable<TransactionAdvancedDto> transactions)
    {
        using var output = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(output, SpreadsheetDocumentType.Workbook))
        {
            var workbook = document.AddWorkbookPart();
            workbook.Workbook = new Workbook();
            var styles = workbook.AddNewPart<WorkbookStylesPart>();
            styles.Stylesheet = new Stylesheet(
                new NumberingFormats(
                    new NumberingFormat { NumberFormatId = 164U, FormatCode = DateFormat },
                    new NumberingFormat { NumberFormatId = 165U, FormatCode = "0.000" }),
                new Fonts(new Font()), new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }),
                    new Fill(new PatternFill { PatternType = PatternValues.Gray125 })), new Borders(new Border()),
                new CellStyleFormats(new CellFormat()),
                new CellFormats(new CellFormat(),
                    new CellFormat { NumberFormatId = 164U, ApplyNumberFormat = true },
                    new CellFormat { NumberFormatId = 165U, ApplyNumberFormat = true }));
            styles.Stylesheet.Save();

            var worksheet = workbook.AddNewPart<WorksheetPart>();
            using (var writer = OpenXmlWriter.Create(worksheet))
            {
                writer.WriteStartElement(new Worksheet());
                writer.WriteStartElement(new SheetData());
                writer.WriteElement(new Row(Headers.Select(TextCell)));
                foreach (var t in transactions)
                {
                    // Excel numeric dates cannot represent timestamps before its calendar epoch.
                    var date = t.TransactionDate >= new DateTime(1900, 3, 1)
                        ? NumberCell(Invariant(t.TransactionDate.ToOADate(), "R"), 1)
                        : TextCell(Invariant(t.TransactionDate, DateFormat));
                    writer.WriteElement(new Row(NumberCell(Invariant(t.Id)), TextCell(t.OrderNumber), TextCell(t.CustomerName),
                        AmountCell(t.Amount), TextCell(t.PaymentMethodName), TextCell(t.Status.ToString()), date,
                        new Cell { DataType = CellValues.Boolean, CellValue = new CellValue(t.IsRefunded ? "1" : "0") }));
                }
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            workbook.Workbook.AppendChild(new Sheets(new Sheet
            {
                Id = workbook.GetIdOfPart(worksheet), SheetId = 1U, Name = "Transactions"
            }));
            workbook.Workbook.Save();
        }
        return output.ToArray();
    }

    private static Cell TextCell(string? text)
    {
        // SpreadsheetML treats an existing _xHHHH_ sequence as an escape. Protect its
        // leading underscore, then encode control characters that XML cannot carry.
        var value = System.Text.RegularExpressions.Regex.Replace(text ?? "", @"_[xX][0-9a-fA-F]{4}_",
            match => "_x005F_" + match.Value[1..]);
        var escaped = new StringBuilder(value.Length);
        foreach (var rune in value.EnumerateRunes())
        {
            if (rune.Value is 13 or 0xFFFE or 0xFFFF || (rune.Value < 32 && rune.Value is not (9 or 10)))
                escaped.Append("_x").Append(Invariant(rune.Value, "X4")).Append('_');
            else
                escaped.Append(rune.ToString());
        }
        return new Cell
        {
            DataType = CellValues.InlineString,
            InlineString = new InlineString(new Text(escaped.ToString()) { Space = SpaceProcessingModeValues.Preserve })
        };
    }

    private static Cell NumberCell(string value, uint style = 0) =>
        new() { DataType = CellValues.Number, CellValue = new CellValue(value), StyleIndex = style };

    private static Cell AmountCell(decimal amount)
    {
        // Excel stores up to 15 significant decimal digits. Use text at the edge of the
        // database's range instead of silently rounding a ledger value on open.
        var value = Invariant(amount, "0.000");
        var digits = value.Replace(".", "").Trim('-').Trim('0').Length;
        return digits <= 15 ? NumberCell(value, 2) : TextCell(value);
    }

    private static string Invariant(IFormattable value, string? format = null) =>
        value.ToString(format, CultureInfo.InvariantCulture);

    private static string Neutralize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        foreach (var rune in value.EnumerateRunes())
        {
            if (rune.Value is '\t' or '\r' or '\n' or '=' or '+' or '-' or '@' or '＝' or '＋' or '－' or '＠')
                return "'" + value;
            if (!Rune.IsWhiteSpace(rune) && Rune.GetUnicodeCategory(rune) is not
                (UnicodeCategory.Control or UnicodeCategory.Format))
                break;
        }
        return value;
    }
}
