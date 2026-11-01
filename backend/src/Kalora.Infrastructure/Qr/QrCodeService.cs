using Kalora.Application.Dashboard;
using QRCoder;

namespace Kalora.Infrastructure.Qr;

/// <summary>QR codes with QRCoder (MIT): PNG from its byte renderer, which needs no graphics library, and the poster from its module matrix.</summary>
internal sealed class QrCodeService : IQrCodeService
{
    /// <summary>"Q" corrects up to a quarter of damage, good for a code on a window sticker.</summary>
    private const QRCodeGenerator.ECCLevel Correction = QRCodeGenerator.ECCLevel.Q;

    private const int PixelsPerModule = 16;

    public byte[] CreatePng(string text)
    {
        using var data = new QRCodeGenerator().CreateQrCode(text, Correction);
        return new PngByteQRCode(data).GetGraphic(PixelsPerModule);
    }

    public byte[] CreatePosterPdf(PosterContent poster)
    {
        using var data = new QRCodeGenerator().CreateQrCode(poster.Url, Correction);
        var modules = data.ModuleMatrix.Select(row => row.Cast<bool>().ToArray()).ToArray();
        return PosterPdf.Create(modules, poster.SalonName, poster.Headline, poster.Hint, poster.Url);
    }
}
