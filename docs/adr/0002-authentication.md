# ADR 0002: Cookie-Authentifizierung mit ASP.NET Core Identity, ein Salon pro Nutzer

- Status: akzeptiert
- Datum: 2026-10-21

## Kontext

Salon-Personal (Inhaber:in, Manager:in, Mitarbeitende) meldet sich im Salon-Bereich an; Kunden buchen ohne Konto. Das Backend und die Angular-App werden unter derselben Domain ausgeliefert (Caddy). Das Konto muss gegen Passwort-Raten, Sitzungsdiebstahl und CSRF geschützt sein und zu Mandantenfähigkeit ([ADR 0001](0001-multi-tenancy.md)) passen.

## Entscheidung

- **ASP.NET Core Identity** (`UserManager`, Passwort-Hashing, Token-Provider, Lockout, TOTP) mit **Cookie-Sitzung**: `HttpOnly`, `Secure`, `SameSite=Lax`, 14 Tage gleitend. Keine Tokens im Browser-Speicher. Nicht angemeldete Zugriffe auf geschützte Endpunkte antworten mit 401/403 statt Redirect.
- **Ein Nutzer gehört zu genau einem Salon** (eindeutiger Index auf `salon_members.user_id`). Der Salon und die Rolle (Owner, Manager, Staff) stehen als Claims `salon_id` und Rolle im Cookie; die Claims-Factory liest sie beim Anmelden. Plattform-Admin ist ein Flag am Nutzer und die globale Rolle `PlatformAdmin`. Die Sicherheitsstempel-Prüfung läuft jede Minute, Rollen- oder Passwortänderungen wirken also spätestens dann.
- **`AppUser` liegt in `Kalora.Application`**, nicht in der Domain, weil Identity seine Basisklasse verlangt und die Domain keine Abhängigkeiten haben soll. Die HTTP-nahen Teile (`ISessionService`, `ICurrentUser`) sind Interfaces in Application und werden in Api implementiert.
- **Rollen** sind Policies (`SalonMember`, `SalonManager`, `SalonOwner`, `PlatformAdmin`), keine verstreuten Strings. Feinere Regeln (z. B. Manager dürfen nur Mitarbeitende einladen) prüft der Service.
- **CSRF:** jede schreibende Anfrage braucht den Antiforgery-Header `X-XSRF-TOKEN`. `GET /api/antiforgery` setzt das Token als lesbares Cookie `XSRF-TOKEN`; das Frontend holt nach Login und Logout ein neues (Tokens sind an den Nutzer gebunden). Anonyme Endpunkte ohne Cookie-Sitzung (spätere Buchungsabsendung) dürfen mit `[IgnoreAntiforgeryToken]` ausgenommen werden.
- **Kontosperre:** 5 Fehlversuche, 15 Minuten. Falsche zweite Faktoren zählen mit. „Unbekannter Nutzer“ und „falsches Passwort“ antworten gleich; „Passwort vergessen“ verrät nie, ob ein Konto existiert.
- **2FA (TOTP):** Aktivieren, Deaktivieren (mit Passwort) und 10 Wiederherstellungscodes. Ist 2FA aktiv, verlangt der Login in derselben Anfrage `twoFactorCode` oder `recoveryCode` (Fehlercode `auth.two_factor_required`); der zweite Faktor wird erst nach richtigem Passwort abgefragt.
- **Einladungen:** 32 Byte Zufall, nur der SHA-256-Hash liegt in der Datenbank, 7 Tage gültig, einmal verwendbar, eine neue Einladung ersetzt die offene. Wer annimmt, bekommt ein Konto mit bestätigter E-Mail, die Rolle der Einladung und die Verknüpfung zum Mitarbeitenden. Niemand kann als Owner eingeladen werden.
- **Data-Protection-Schlüssel** liegen in der Datenbank, damit Cookies, Bestätigungs- und Reset-Links Neustarts und Redeployments überstehen.
- **Mails** landen mit `Type`, Sprache und JSON-Nutzdaten (Links) in der `email_outbox`; Rendern und Versand folgen mit Prompt 13.

## Konsequenzen

- Registrierung meldet sofort an, die E-Mail-Bestätigung ist nicht Voraussetzung (`emailConfirmed` im Profil). Eine spätere Pflicht (z. B. vor Veröffentlichung der Buchungsseite) ist möglich.
- Ein Nutzer kann nicht in mehreren Salons arbeiten; ein Salon-Wechsel wäre ein späteres Feature mit eigenem Datenmodell.
- Tokens für Reset- und Bestätigungslinks stehen bis zum Versand (und bis zur Bereinigung) in `payload_json` der Outbox. Prompt 13/19 sollen die Nutzdaten nach erfolgreichem Versand leeren.
- Konten ohne Salon (Plattform-Admins) können noch keine Mails über die Outbox erhalten, weil sie salonbezogen ist.
- Die Data-Protection-Schlüssel liegen unverschlüsselt in der Datenbank; Backups sind entsprechend zu schützen (Prompt 22/25).
- Rate-Limiting auf Login und Registrierung folgt in Prompt 22; bis dahin schützt die Kontosperre einzelne Konten, nicht vor breitem Durchprobieren.

## Verworfene Alternativen

- **JWT im Browser-Speicher.** Durch XSS auslesbar, Widerruf und Rotation aufwendig; Cookies passen besser, weil Frontend und API dieselbe Domain nutzen.
- **Externer Identity-Provider (z. B. Keycloak).** Zusätzlicher Dienst im Betrieb; widerspricht „radikal einfach“ auf einem Heimserver.
- **Mehrere Salons pro Nutzer.** Bräuchte Salon-Wechsel in Claims, UI und Autorisierung; für die Zielgruppe (1–15 Mitarbeitende pro Salon) nicht nötig.
- **Identity-Rollentabellen und Identity-UI.** Rollen gelten pro Salon und liegen deshalb in `salon_members`; die Identity-UI passt nicht zum Angular-Frontend.
