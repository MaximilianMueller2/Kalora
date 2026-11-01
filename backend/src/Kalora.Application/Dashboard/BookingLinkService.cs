using Kalora.Application.Auth;
using Kalora.Application.Common;
using Kalora.Application.Configuration;
using Kalora.Application.Identity;
using Kalora.Domain.Salons;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kalora.Application.Dashboard;

internal sealed class BookingLinkService(IAppDbContext db, ICurrentUser currentUser, IQrCodeService qr, IOptions<AppOptions> appOptions) : IBookingLinkService
{
    /// <summary>
    /// Poster texts in the salon's language. They are printed with a standard PDF font (Latin-1), so the Turkish text avoids
    /// letters that font does not have.
    /// </summary>
    private static readonly Dictionary<string, (string Headline, string Hint)> Texts = new()
    {
        ["de"] = ("Jetzt online Termin buchen", "Einfach den QR-Code mit der Handykamera scannen"),
        ["en"] = ("Book your appointment online", "Just scan the QR code with your phone camera"),
        ["tr"] = ("Online randevu alin", "QR kodu telefonunuzun kamerasiyla okutun"),
    };

    public async Task<Result<BinaryFile>> QrPngAsync(CancellationToken cancellationToken = default)
    {
        var salon = await FindAsync(cancellationToken);
        return salon is null ? Error.Forbidden() : new BinaryFile($"{salon.Slug}-qr.png", "image/png", qr.CreatePng(Url(salon)));
    }

    public async Task<Result<BinaryFile>> PosterPdfAsync(CancellationToken cancellationToken = default)
    {
        var salon = await FindAsync(cancellationToken);
        if (salon is null)
        {
            return Error.Forbidden();
        }

        var (headline, hint) = Texts.GetValueOrDefault(salon.DefaultLanguage, Texts["de"]);
        return new BinaryFile($"{salon.Slug}-poster.pdf", "application/pdf", qr.CreatePosterPdf(new PosterContent(salon.Name, headline, hint, Url(salon))));
    }

    private string Url(Salon salon) => AppLinks.Booking(appOptions.Value.PublicBaseUrl, salon.Slug);

    /// <summary>Only owners and managers hand the link out; the check is repeated here because the controller only names a role.</summary>
    private async Task<Salon?> FindAsync(CancellationToken cancellationToken) =>
        currentUser.SalonRole is SalonRole.Owner or SalonRole.Manager ? await db.Salons.FirstOrDefaultAsync(cancellationToken) : null;
}
