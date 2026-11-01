using Kalora.Application.Common;

namespace Kalora.Application.Dashboard;

/// <summary>The salon's public booking link as something to hand out: a QR code as PNG and a printable poster as PDF.</summary>
public interface IBookingLinkService
{
    Task<Result<BinaryFile>> QrPngAsync(CancellationToken cancellationToken = default);

    Task<Result<BinaryFile>> PosterPdfAsync(CancellationToken cancellationToken = default);
}

/// <summary>Draws QR codes and the poster. Implemented by Infrastructure (QRCoder).</summary>
public interface IQrCodeService
{
    byte[] CreatePng(string text);

    byte[] CreatePosterPdf(PosterContent poster);
}

/// <param name="Headline">Under the salon name, e.g. "Book your appointment online". Must be plain text.</param>
/// <param name="Hint">Under the QR code, e.g. "Scan with your phone camera".</param>
public sealed record PosterContent(string SalonName, string Headline, string Hint, string Url);
