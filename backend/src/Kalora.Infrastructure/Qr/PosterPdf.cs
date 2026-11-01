using System.Globalization;
using System.Text;

namespace Kalora.Infrastructure.Qr;

/// <summary>
/// A one-page A4 PDF for printing: the salon's name, a headline, the QR code as vector squares, a hint and the link. It is written
/// by hand because only a few standard elements are needed and no PDF library is allowed; the text uses the built-in Helvetica
/// fonts (Latin-1), so letters outside that range are replaced by plain ones.
/// </summary>
internal static class PosterPdf
{
    private const double PageWidth = 595.28;
    private const double PageHeight = 841.89;
    private const double MaxTextWidth = 480;
    private const double QrSize = 400;

    public static byte[] Create(bool[][] modules, string salonName, string headline, string hint, string url)
    {
        var content = new StringBuilder();
        Text(content, "F2", 34, salonName, 740, MaxTextWidth, bold: true);
        Text(content, "F1", 22, headline, 700, MaxTextWidth, bold: false);

        var left = (PageWidth - QrSize) / 2;
        var top = 640.0; // top edge of the QR code
        Qr(content, modules, left, top);

        Text(content, "F1", 16, hint, top - QrSize - 40, MaxTextWidth, bold: false);
        Text(content, "F2", 18, url, top - QrSize - 80, MaxTextWidth, bold: true);
        return Write(content.ToString());
    }

    /// <summary>The dark modules as filled rectangles; neighbours in a row become one rectangle. The quiet zone around the code is part of the matrix and stays white.</summary>
    private static void Qr(StringBuilder content, bool[][] modules, double left, double top)
    {
        var size = QrSize / modules.Length;
        content.Append("0 0 0 rg\n");
        for (var row = 0; row < modules.Length; row++)
        {
            var column = 0;
            while (column < modules[row].Length)
            {
                if (!modules[row][column])
                {
                    column++;
                    continue;
                }

                var start = column;
                while (column < modules[row].Length && modules[row][column])
                {
                    column++;
                }

                content.Append(Number(left + start * size)).Append(' ')
                    .Append(Number(top - (row + 1) * size)).Append(' ')
                    .Append(Number((column - start) * size)).Append(' ')
                    .Append(Number(size)).Append(" re f\n");
            }
        }
    }

    private static void Text(StringBuilder content, string font, double size, string text, double y, double maxWidth, bool bold)
    {
        var clean = Sanitize(text);
        var width = Width(clean, size, bold);
        if (width > maxWidth)
        {
            size *= maxWidth / width; // long names and links shrink instead of running off the page
            width = maxWidth;
        }

        content.Append("BT /").Append(font).Append(' ').Append(Number(size)).Append(" Tf ")
            .Append(Number((PageWidth - width) / 2)).Append(' ').Append(Number(y)).Append(" Td (")
            .Append(Escape(clean)).Append(") Tj ET\n");
    }

    /// <summary>Letters the standard font cannot show are replaced by the closest plain one.</summary>
    internal static string Sanitize(string text)
    {
        var result = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            result.Append(character switch
            {
                'ğ' => 'g', 'Ğ' => 'G', 'ş' => 's', 'Ş' => 'S', 'ı' => 'i', 'İ' => 'I', '–' or '—' => '-', '’' or '‘' => '\'', '“' or '”' => '"',
                < ' ' => ' ',
                > 'ÿ' => '?',
                _ => character,
            });
        }

        return result.ToString();
    }

    private static string Escape(string text) => text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);

    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    // Widths of Helvetica and Helvetica-Bold in 1/1000 em for the characters 32 to 126 (Adobe font metrics).
    private static readonly int[] Regular =
    [
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556,
        1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556,
        333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556,
        556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584,
    ];

    private static readonly int[] Bold =
    [
        278, 333, 474, 556, 556, 889, 722, 238, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 333, 333, 584, 584, 584, 611,
        975, 722, 722, 722, 722, 667, 611, 778, 722, 278, 556, 722, 611, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 333, 278, 333, 584, 556,
        333, 556, 611, 556, 611, 556, 333, 611, 611, 278, 278, 556, 278, 889, 611, 611,
        611, 611, 389, 556, 333, 611, 556, 778, 556, 556, 500, 389, 280, 389, 584,
    ];

    /// <summary>Width of a text in points. Letters beyond ASCII (umlauts) are measured like their base letter, which is close enough for centering.</summary>
    internal static double Width(string text, double size, bool bold)
    {
        var table = bold ? Bold : Regular;
        var total = 0;
        foreach (var character in text)
        {
            total += character is >= ' ' and <= '~' ? table[character - ' '] : bold ? 611 : 556;
        }

        return total * size / 1000.0;
    }

    private static byte[] Write(string pageContent)
    {
        var stream = Encoding.Latin1.GetBytes(pageContent);
        var objects = new List<byte[]>
        {
            Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
            Encoding.ASCII.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            Encoding.ASCII.GetBytes($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Number(PageWidth)} {Number(PageHeight)}] /Resources << /Font << /F1 5 0 R /F2 6 0 R >> >> /Contents 4 0 R >>"),
            Encoding.ASCII.GetBytes($"<< /Length {stream.Length} >>\nstream\n").Concat(stream).Concat(Encoding.ASCII.GetBytes("endstream")).ToArray(),
            Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"),
            Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"),
        };

        using var file = new MemoryStream();
        void Append(string text) => file.Write(Encoding.ASCII.GetBytes(text));

        Append("%PDF-1.4\n");
        file.Write([0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A]); // marks the file as binary for transfer tools
        var offsets = new List<long>();
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(file.Position);
            Append($"{index + 1} 0 obj\n");
            file.Write(objects[index]);
            Append("\nendobj\n");
        }

        var xref = file.Position;
        Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            Append($"{offset:D10} 00000 n \n");
        }

        Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return file.ToArray();
    }
}
