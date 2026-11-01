using System.Text;
using System.Text.RegularExpressions;
using Kalora.Application.Dashboard;
using Kalora.Infrastructure.Qr;

namespace Kalora.UnitTests.Dashboard;

public partial class PosterPdfTests
{
    private static readonly IQrCodeService Service = new QrCodeService();

    private static readonly PosterContent Poster = new("Haarwerk Müller", "Jetzt online Termin buchen", "QR-Code scannen", "https://kalora.app/haarwerk");

    private static string Text(byte[] pdf) => Encoding.Latin1.GetString(pdf);

    [Fact]
    public void The_PNG_is_a_real_image_with_a_square_code_and_a_quiet_zone()
    {
        var png = Service.CreatePng("https://kalora.app/haarwerk");

        Assert.Equal([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], png[..8]);
        var width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        var height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        Assert.Equal(width, height);
        Assert.True(width >= 29 * 16, $"{width}px is too small for version 2 with the quiet zone"); // 25 modules + 8 quiet zone at 16 px
        Assert.NotEqual(Service.CreatePng("https://kalora.app/other"), png);
    }

    [Fact]
    public void The_PDF_has_the_structure_of_one_A4_page_and_a_consistent_cross_reference_table()
    {
        var pdf = Service.CreatePosterPdf(Poster);
        var text = Text(pdf);

        Assert.StartsWith("%PDF-1.4", text);
        Assert.EndsWith("%%EOF\n", text);
        Assert.Contains("/MediaBox [0 0 595.28 841.89]", text);
        Assert.Contains("/Count 1", text);

        // Every entry of the xref table points at the start of its object.
        var table = Regex.Matches(text[text.IndexOf("xref\n", StringComparison.Ordinal)..], @"(\d{10}) 00000 n ").Select(m => int.Parse(m.Groups[1].Value)).ToList();
        Assert.Equal(6, table.Count);
        for (var index = 0; index < table.Count; index++)
        {
            Assert.StartsWith($"{index + 1} 0 obj", text[table[index]..]);
        }

        var startxref = int.Parse(Regex.Match(text, @"startxref\n(\d+)").Groups[1].Value);
        Assert.StartsWith("xref", text[startxref..]);
    }

    [Fact]
    public void The_declared_length_of_the_page_stream_is_right()
    {
        var text = Text(Service.CreatePosterPdf(Poster));

        var match = Regex.Match(text, @"/Length (\d+) >>\nstream\n(?<body>.*?)endstream", RegexOptions.Singleline);
        Assert.True(match.Success);
        Assert.Equal(int.Parse(match.Groups[1].Value), match.Groups["body"].Length);
    }

    [Fact]
    public void The_poster_names_the_salon_and_carries_the_link_and_the_texts()
    {
        var text = Text(Service.CreatePosterPdf(Poster));

        Assert.Contains("(Haarwerk M\u00fcller) Tj", text); // umlauts are in the font's encoding (Latin-1)
        Assert.Contains("(Jetzt online Termin buchen) Tj", text);
        Assert.Contains("(QR-Code scannen) Tj", text);
        Assert.Contains("(https://kalora.app/haarwerk) Tj", text);
    }

    [Fact]
    public void The_code_is_drawn_as_filled_squares_inside_the_page()
    {
        var text = Text(Service.CreatePosterPdf(Poster));

        var rectangles = Regex.Matches(text, @"(?<x>[\d.]+) (?<y>[\d.]+) (?<w>[\d.]+) (?<h>[\d.]+) re f");
        Assert.True(rectangles.Count > 50);
        foreach (Match rectangle in rectangles)
        {
            var (x, y, w, h) = (double.Parse(rectangle.Groups["x"].Value, System.Globalization.CultureInfo.InvariantCulture), double.Parse(rectangle.Groups["y"].Value, System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(rectangle.Groups["w"].Value, System.Globalization.CultureInfo.InvariantCulture), double.Parse(rectangle.Groups["h"].Value, System.Globalization.CultureInfo.InvariantCulture));
            Assert.InRange(x, 0, 595.28 - w + 0.01);
            Assert.InRange(y, 0, 841.89 - h + 0.01);
        }
    }

    [Fact]
    public void Special_characters_in_texts_cannot_break_the_file()
    {
        var text = Text(Service.CreatePosterPdf(Poster with { SalonName = "Salon (Kuaf\u00f6r) \\ \u015e\u0131k \u011f \u2013 \u65e5\u672c" }));

        Assert.Contains("(Salon \\(Kuaf\u00f6r\\) \\\\ Sik g - ??) Tj", text);
    }

    [Theory]
    [InlineData("\u011f\u015f\u0131\u0130\u011e\u015e", "gsiIGS")]
    [InlineData("a\u2013b\u2019c", "a-b'c")]
    [InlineData("\u4e2d", "?")]
    [InlineData("Gr\u00fc\u00dfe", "Gr\u00fc\u00dfe")]
    public void Letters_the_standard_font_lacks_are_replaced(string input, string expected)
    {
        Assert.Equal(expected, PosterPdf.Sanitize(input));
    }

    [Fact]
    public void A_long_name_or_link_is_shrunk_to_fit_the_page()
    {
        var text = Text(Service.CreatePosterPdf(Poster with { Url = "https://kalora.app/" + new string('x', 120) }));

        var size = double.Parse(Regex.Match(text, @"/F2 ([\d.]+) Tf [\d.]+ [\d.]+ Td \(https").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(size < 18, $"the link keeps its size {size}");
        Assert.True(PosterPdf.Width("https://kalora.app/" + new string('x', 120), size, bold: true) <= 480.01);
    }

    [Fact]
    public void Widths_come_from_the_font_metrics()
    {
        Assert.Equal(278 * 10 / 1000.0, PosterPdf.Width(" ", 10, bold: false) * 1, 3); // a space in Helvetica is 278 units
        Assert.True(PosterPdf.Width("Haarwerk", 10, bold: true) > PosterPdf.Width("Haarwerk", 10, bold: false));
    }

    [Fact]
    public void Different_links_give_different_codes()
    {
        Assert.NotEqual(Service.CreatePosterPdf(Poster), Service.CreatePosterPdf(Poster with { Url = "https://kalora.app/other" }));
    }
}
