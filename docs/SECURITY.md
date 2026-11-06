# Sicherheit in Kalora

Stand der Sicherheitsprüfung (Prompt 22): wie die Anwendung geschützt ist, wer welchen Endpunkt aufrufen darf, und alle Funde der Prüfung mit ihrem Status. Die Rechtstexte und der Datenschutz stehen in `docs/PRIVACY.md`.

## Grundsätze

- **Verboten, solange nichts erlaubt ist.** Ein Endpunkt ohne Angabe verlangt eine angemeldete Person (`FallbackPolicy`). Anonyme Endpunkte tragen ausdrücklich `[AllowAnonymous]`; ein Test (`SecurityTests`) listet alle auf und schlägt fehl, wenn einer dazukommt oder ohne Angabe bleibt.
- **Der Mandant kommt aus dem Cookie oder der URL, nie aus dem Body.** Jede salonbezogene Tabelle hat `SalonId` und einen globalen Filter; Schreiben prüft zusätzlich `TenantSaveGuard`. Ein Salon sieht einen fremden Datensatz als „nicht gefunden“, nicht als „verboten“.
- **Kunden ohne Konto sehen nur freie Zeiten** und über ihr geheimes Token genau ihren eigenen Termin.
- **Daten sparsam:** keine Cookies von Dritten, keine externen Skripte, Schriften und Tracker, keine personenbezogenen Daten in Logs.

## Header

Die **API** setzt sie auf jede Antwort (`SecurityHeadersMiddleware`, `UseHsts`); die **Web-App** liefert der Reverse-Proxy aus, dafür liegt die Vorlage in `deploy/security-headers.caddy`.

| Header | API | Web-App |
|---|---|---|
| `Content-Security-Policy` | `default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'` | `default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; manifest-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'` |
| `Strict-Transport-Security` | `max-age=31536000; includeSubDomains` (nicht in Development, nicht auf `localhost`) | dasselbe |
| `X-Content-Type-Options` | `nosniff` | `nosniff` |
| `X-Frame-Options` | `DENY` | `DENY` |
| `Referrer-Policy` | `no-referrer` | `no-referrer` |
| `Permissions-Policy` | Kamera, Mikrofon, Standort, Zahlung, USB, Sensoren aus | dasselbe |
| `Cross-Origin-Opener-Policy` / `-Resource-Policy` | `same-origin` | COOP `same-origin` |
| `Cache-Control` | `no-store` für alles unter `/api` | Standard des Proxys (gehashte Dateien dürfen lange gecacht werden) |
| `Server` | entfernt | entfernt |

**Skripte** sind in der Web-App streng (`script-src 'self'`, kein Inline, kein `eval`): Der Produktions-Build enthält kein Inline-Skript mehr (die Kritisch-CSS-Einbettung, die eines erzeugte, ist abgeschaltet). Das wurde im Browser gegen die echte App mit dieser Richtlinie geprüft (Registrierung, Einrichtung, Dashboard, Einstellungen mit QR-Code, öffentliche Buchungsseite): **keine Verstöße**. **Stile** brauchen `'unsafe-inline'`, weil Angular und Angular Material Komponentenstile und `style`-Attribute zur Laufzeit einfügen; das ist die einzige Stelle, an der die Richtlinie lockerer ist als möglich (Abhilfe wäre ein Nonce pro Anfrage, siehe F-03).

**CORS:** Es ist keines eingerichtet. Web-App und API haben denselben Ursprung (der Proxy leitet `/api` weiter), also braucht kein Browser einer anderen Seite Zugriff; ohne CORS-Header verweigert er ihn. Ein Test belegt, dass weder eine Anfrage noch ein Preflight von einer fremden Herkunft `Access-Control-Allow-*` bekommt.

## Raten- und Fehlversuch-Grenzen

Je IP-Adresse (hinter dem Proxy aus den `X-Forwarded-For`-Angaben), feste Fenster, konfigurierbar unter `RateLimiting` in `appsettings.json`. Zu viel ⇒ `429`, Code `rate_limited`, `Retry-After`.

| Richtlinie | Gilt für | Standard |
|---|---|---|
| `Login` | Anmeldung, Zwei-Faktor-Einrichtung/-Abschalten/-Codes | 20 / Minute |
| `Register` | Registrierung | 10 / Stunde |
| `Recovery` | E-Mail bestätigen, Passwort vergessen, Passwort zurücksetzen | 10 / 15 Minuten |
| `Manage` | Verwaltungslink eines Termins, Kalenderdatei, Absage, Einladungslinks | 30 / Minute |
| `Read` | öffentliche Salon-Seite, Leistungen, Personen | 120 / Minute |
| `Availability` | freie Tage und Zeiten | 60 / Minute |
| `Booking` | Terminanfrage | 10 / 10 Minuten |
| `General` | alles andere (Auffangnetz, `/health` ausgenommen) | 600 / Minute |

**Kontosperre:** Fünf falsche Passwörter sperren ein Konto für 15 Minuten. Falsche Passwörter zählen auch bei den sicherheitsrelevanten Bestätigungen (Zwei-Faktor abschalten, neue Wiederherstellungscodes), damit eine gestohlene Sitzung nicht dazu dient, das Passwort zu erraten. Ein richtiges Passwort setzt den Zähler zurück. Während der Sperre wird auch das richtige abgewiesen.

**Anmeldung:** Passwort mindestens 10, höchstens 128 Zeichen (Länge zählt mehr als Zeichenklassen); Zwei-Faktor per Authenticator-App (TOTP) mit zehn Wiederherstellungscodes, in den Einstellungen unter „Sicherheit“ für Inhaber:innen und Manager:innen einzurichten, mit QR-Code, der vom eigenen Server stammt. Das Sitzungs-Cookie ist `HttpOnly`, `Secure`, `SameSite=Lax`; jede schreibende Anfrage braucht das Antiforgery-Token (außer den zwei öffentlichen Formularen: Terminanfrage und Absage).

## Wer darf welchen Endpunkt

`SalonMember` = Inhaber:in, Manager:in und Mitarbeitende; `SalonManager` = Inhaber:in und Manager:in. Die Rolle prüft die Richtlinie am Endpunkt; wo ein Dienst mehr einschränkt (Mitarbeitende nur ihre eigene Spalte), steht es in der letzten Spalte. Bei einem Salon, der nur lesen darf (gesperrt, gekündigt, Testphase vorbei), verweigert `ReadOnlySalonFilter` jede schreibende Anfrage mit `403 subscription.read_only`; ausgenommen sind Anmelden/Abmelden, das eigene Profil, Zwei-Faktor, die Admin-API und die Kundenakte (Auskunft und Löschung nach DSGVO).

| Methode | Pfad | Wer darf | Mandant | Limit / Besonderheit |
|---|---|---|---|---|
| GET | `/api/admin/salons` | Plattform-Admin | keiner, jeder Salon wird ausdrücklich benannt | jede Änderung im Audit-Log; keine Kundendaten |
| GET | `/api/admin/salons/{id:guid}` | Plattform-Admin | keiner, jeder Salon wird ausdrücklich benannt | jede Änderung im Audit-Log; keine Kundendaten |
| PUT | `/api/admin/salons/{id:guid}/note` | Plattform-Admin | keiner, jeder Salon wird ausdrücklich benannt | jede Änderung im Audit-Log; keine Kundendaten |
| PUT | `/api/admin/salons/{id:guid}/plan` | Plattform-Admin | keiner, jeder Salon wird ausdrücklich benannt | jede Änderung im Audit-Log; keine Kundendaten |
| PUT | `/api/admin/salons/{id:guid}/status` | Plattform-Admin | keiner, jeder Salon wird ausdrücklich benannt | jede Änderung im Audit-Log; keine Kundendaten |
| POST | `/api/admin/salons/{id:guid}/suspend` | Plattform-Admin | keiner, jeder Salon wird ausdrücklich benannt | jede Änderung im Audit-Log; keine Kundendaten |
| POST | `/api/admin/salons/{id:guid}/trial/extend` | Plattform-Admin | keiner, jeder Salon wird ausdrücklich benannt | jede Änderung im Audit-Log; keine Kundendaten |
| POST | `/api/admin/salons/{id:guid}/unsuspend` | Plattform-Admin | keiner, jeder Salon wird ausdrücklich benannt | jede Änderung im Audit-Log; keine Kundendaten |
| GET | `/api/appointments/awaiting-outcome` | Inhaber:in, Manager:in, Mitarbeitende | Salon aus dem Cookie, nie aus dem Body | Mitarbeitende: nur Termine der eigenen Spalte |
| GET | `/api/appointments/requests` | Inhaber:in, Manager:in, Mitarbeitende | Salon aus dem Cookie, nie aus dem Body | Mitarbeitende: nur Termine der eigenen Spalte |
| GET | `/api/appointments/requests/count` | Inhaber:in, Manager:in, Mitarbeitende | Salon aus dem Cookie, nie aus dem Body | Mitarbeitende: nur Termine der eigenen Spalte |
| GET | `/api/appointments/{id:guid}` | Inhaber:in, Manager:in, Mitarbeitende | Salon aus dem Cookie, nie aus dem Body | Mitarbeitende: nur Termine der eigenen Spalte |
| POST | `/api/appointments/{id:guid}/attended` | Inhaber:in, Manager:in, Mitarbeitende | Salon aus dem Cookie, nie aus dem Body | Mitarbeitende: nur Termine der eigenen Spalte |
| POST | `/api/appointments/{id:guid}/cancel` | Inhaber:in, Manager:in, Mitarbeitende | Salon aus dem Cookie, nie aus dem Body | Mitarbeitende: nur Termine der eigenen Spalte |
| POST | `/api/appointments/{id:guid}/confirm` | Inhaber:in, Manager:in, Mitarbeitende | Salon aus dem Cookie, nie aus dem Body | Mitarbeitende: nur Termine der eigenen Spalte |
| POST | `/api/appointments/{id:guid}/decline` | Inhaber:in, Manager:in, Mitarbeitende | Salon aus dem Cookie, nie aus dem Body | Mitarbeitende: nur Termine der eigenen Spalte |
| POST | `/api/appointments/{id:guid}/no-show` | Inhaber:in, Manager:in, Mitarbeitende | Salon aus dem Cookie, nie aus dem Body | Mitarbeitende: nur Termine der eigenen Spalte |
| POST | `/api/auth/confirm-email` | **anonym** | keiner | Limit `Recovery` |
| POST | `/api/auth/forgot-password` | **anonym** | keiner | Limit `Recovery` |
| POST | `/api/auth/login` | **anonym** | keiner | Limit `Login` |
| POST | `/api/auth/logout` | **anonym** | keiner |  |
| POST | `/api/auth/register` | **anonym** | keiner | Limit `Register` |
| POST | `/api/auth/reset-password` | **anonym** | keiner | Limit `Recovery` |
| POST | `/api/auth/two-factor/disable` | jede angemeldete Person | keiner | Limit `Login`; Passwortfehler zählen für die Kontosperre |
| POST | `/api/auth/two-factor/enable` | jede angemeldete Person | keiner | Limit `Login`; Passwortfehler zählen für die Kontosperre |
| POST | `/api/auth/two-factor/recovery-codes` | jede angemeldete Person | keiner | Limit `Login`; Passwortfehler zählen für die Kontosperre |
| POST | `/api/auth/two-factor/setup` | jede angemeldete Person | keiner | Limit `Login`; Passwortfehler zählen für die Kontosperre |
| GET | `/api/calendar` | Inhaber:in, Manager:in, Mitarbeitende | Salon aus dem Cookie, nie aus dem Body | Mitarbeitende: nur die eigene Spalte |
| POST | `/api/calendar/appointments` | Inhaber:in, Manager:in, Mitarbeitende | Salon aus dem Cookie, nie aus dem Body | Mitarbeitende: nur die eigene Spalte |
| POST | `/api/calendar/appointments/{id:guid}/move` | Inhaber:in, Manager:in, Mitarbeitende | Salon aus dem Cookie, nie aus dem Body | Mitarbeitende: nur die eigene Spalte |
| POST | `/api/calendar/blocks` | Inhaber:in, Manager:in, Mitarbeitende | Salon aus dem Cookie, nie aus dem Body | Mitarbeitende: nur die eigene Spalte |
| DELETE | `/api/calendar/blocks/{id:guid}` | Inhaber:in, Manager:in, Mitarbeitende | Salon aus dem Cookie, nie aus dem Body | Mitarbeitende: nur die eigene Spalte |
| GET | `/api/calendar/customers` | Inhaber:in, Manager:in, Mitarbeitende | Salon aus dem Cookie, nie aus dem Body | Mitarbeitende: nur Kunden mit Termin bei ihnen |
| GET | `/api/catalog` | Inhaber:in, Manager:in, Mitarbeitende | Salon aus dem Cookie, nie aus dem Body |  |
| POST | `/api/catalog/categories` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body |  |
| PUT | `/api/catalog/categories/order` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body |  |
| PUT | `/api/catalog/categories/{categoryId:guid}/services/order` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body |  |
| DELETE | `/api/catalog/categories/{id:guid}` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body |  |
| PUT | `/api/catalog/categories/{id:guid}` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body |  |
| POST | `/api/catalog/services` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body |  |
| DELETE | `/api/catalog/services/{id:guid}` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body |  |
| PUT | `/api/catalog/services/{id:guid}` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body |  |
| POST | `/api/catalog/templates/apply` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body |  |
| GET | `/api/customers` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body | auch bei gesperrtem Salon nutzbar (DSGVO) |
| GET | `/api/customers/{id:guid}` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body | auch bei gesperrtem Salon nutzbar (DSGVO) |
| POST | `/api/customers/{id:guid}/anonymize` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body | auch bei gesperrtem Salon nutzbar (DSGVO) |
| GET | `/api/customers/{id:guid}/export.csv` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body | auch bei gesperrtem Salon nutzbar (DSGVO) |
| GET | `/api/customers/{id:guid}/export.json` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body | auch bei gesperrtem Salon nutzbar (DSGVO) |
| GET | `/api/dashboard` | Inhaber:in, Manager:in, Mitarbeitende | Salon aus dem Cookie, nie aus dem Body | Mitarbeitende: nur die eigene Spalte |
| GET | `/api/dashboard/booking-link/poster.pdf` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body | Mitarbeitende: nur die eigene Spalte |
| GET | `/api/dashboard/booking-link/qr.png` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body | Mitarbeitende: nur die eigene Spalte |
| POST | `/api/invitations` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body |  |
| GET | `/api/me` | jede angemeldete Person | Salon aus dem Cookie, nie aus dem Body |  |
| GET | `/api/onboarding` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body |  |
| GET | `/api/ping` | **anonym** | keiner |  |
| GET | `/api/public/appointments/{token}` | **anonym** | Termin über das Token (kein Salon nötig) | Limit `Manage` |
| GET | `/api/public/appointments/{token}/calendar.ics` | **anonym** | Termin über das Token (kein Salon nötig) | Limit `Manage` |
| POST | `/api/public/appointments/{token}/cancel` | **anonym** | Termin über das Token (kein Salon nötig) | Limit `Manage`; ohne Antiforgery-Token (öffentliches Formular) |
| GET | `/api/public/invitations/{token}` | **anonym** | Einladung über das Token | Limit `Manage` |
| POST | `/api/public/invitations/{token}/accept` | **anonym** | Einladung über das Token | Limit `Manage` |
| GET | `/api/public/salons/{slug}` | **anonym** | Salon aus dem Kürzel in der URL | Limit `Read` |
| POST | `/api/public/salons/{slug}/appointment-requests` | **anonym** | Salon aus dem Kürzel in der URL | Limit `Booking`; ohne Antiforgery-Token (öffentliches Formular) |
| GET | `/api/public/salons/{slug}/availability/days` | **anonym** | Salon aus dem Kürzel in der URL | Limit `Availability` |
| GET | `/api/public/salons/{slug}/availability/slots` | **anonym** | Salon aus dem Kürzel in der URL | Limit `Availability` |
| GET | `/api/public/salons/{slug}/services` | **anonym** | Salon aus dem Kürzel in der URL | Limit `Read` |
| GET | `/api/public/salons/{slug}/staff` | **anonym** | Salon aus dem Kürzel in der URL | Limit `Read` |
| GET | `/api/settings/booking-rules` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body |  |
| PUT | `/api/settings/booking-rules` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body |  |
| GET | `/api/settings/closures` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body |  |
| POST | `/api/settings/closures` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body |  |
| DELETE | `/api/settings/closures/{id:guid}` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body |  |
| GET | `/api/settings/opening-hours` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body |  |
| PUT | `/api/settings/opening-hours` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body |  |
| GET | `/api/settings/profile` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body |  |
| PUT | `/api/settings/profile` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body |  |
| GET | `/api/settings/slug-suggestion` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body |  |
| GET | `/api/staff` | Inhaber:in, Manager:in, Mitarbeitende | Salon aus dem Cookie, nie aus dem Body |  |
| POST | `/api/staff` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body |  |
| GET | `/api/staff/{id:guid}` | Inhaber:in, Manager:in, Mitarbeitende | Salon aus dem Cookie, nie aus dem Body |  |
| PUT | `/api/staff/{id:guid}` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body |  |
| GET | `/api/staff/{id:guid}/services` | Inhaber:in, Manager:in, Mitarbeitende | Salon aus dem Cookie, nie aus dem Body |  |
| PUT | `/api/staff/{id:guid}/services` | Inhaber:in, Manager:in | Salon aus dem Cookie, nie aus dem Body |  |
| GET | `/api/staff/{staffId:guid}/absences` | Inhaber:in, Manager:in, Mitarbeitende | Salon aus dem Cookie, nie aus dem Body | Mitarbeitende: nur die eigene Person, sonst Inhaber:in/Manager:in |
| POST | `/api/staff/{staffId:guid}/absences` | Inhaber:in, Manager:in, Mitarbeitende | Salon aus dem Cookie, nie aus dem Body | Mitarbeitende: nur die eigene Person, sonst Inhaber:in/Manager:in |
| DELETE | `/api/staff/{staffId:guid}/absences/{absenceId:guid}` | Inhaber:in, Manager:in, Mitarbeitende | Salon aus dem Cookie, nie aus dem Body | Mitarbeitende: nur die eigene Person, sonst Inhaber:in/Manager:in |
| GET | `/api/staff/{staffId:guid}/working-hours` | Inhaber:in, Manager:in, Mitarbeitende | Salon aus dem Cookie, nie aus dem Body | Ändern nur Inhaber:in/Manager:in; Mitarbeitende lesen die eigenen Zeiten |
| PUT | `/api/staff/{staffId:guid}/working-hours` | Inhaber:in, Manager:in, Mitarbeitende | Salon aus dem Cookie, nie aus dem Body | Ändern nur Inhaber:in/Manager:in; Mitarbeitende lesen die eigenen Zeiten |
| GET | `/api/antiforgery` | **anonym** | keiner | liefert das Antiforgery-Cookie |
| GET | `/health` | **anonym** | keiner | ohne Limit, zeigt nichts außer „Healthy“ |

Der Test `SecurityTests.Every_endpoint_says_who_may_call_it_and_the_anonymous_ones_are_the_known_ones` hält die Liste der anonymen Endpunkte aktuell; `Every_anonymous_endpoint_that_changes_something_has_its_own_rate_limit` verlangt für jeden anonymen schreibenden Endpunkt eine eigene Grenze (außer Abmelden).

## Eingaben

- **Längen:** Jede Texteingabe hat eine Obergrenze im Validator (Namen 100–200, Notizen 1000–2000, Datenschutztext 20 000, Anmeldung E-Mail 320 und Passwort 128, Token 2000, Suchbegriffe 100, Zwei-Faktor-Codes 20). Die ganze Anfrage darf höchstens 1 MB groß sein (Kestrel).
- **Escapen:** Mails kodieren jeden Freitext und jede Adresse für HTML (`WebUtility.HtmlEncode`); ein Test setzt in **jeder** Mailart und Sprache in jedes Freitextfeld einen Angriffstext (`"><svg onload=…>`, Anführungszeichen in Links) und prüft, dass nichts aus dem HTML ausbricht. Betreffzeilen können keine Kopfzeilen einschleusen. Im Frontend gibt es kein `innerHTML`; Markdown wird als Baum gelesen und als Elemente gezeichnet, Links nur auf `https:`, `http:`, `mailto:` oder App-Seiten. CSV-Exporte schützen vor Formel-Einschleusung. `returnUrl` nach der Anmeldung wird auf App-Pfade begrenzt.

## Logs

Logs sagen, was passiert ist und mit welcher ID, nie wem. Alle Log-Aufrufe wurden geprüft; sie enthalten nur Zahlen, Typen und IDs.

- Die **Anfrage-Protokollierung** schreibt statt des Pfads einen bereinigten (`/api/public/appointments/{token}/…`, `/api/public/invitations/{token}/…`), damit keine Verwaltungs- oder Einladungslinks in Logdateien landen (`LogPaths`).
- **Ausnahmen** werden maskiert protokolliert (`PersonalData.ForLog`): E-Mail-Adressen werden zu `<address>`, die Werte verletzter Datenbankschlüssel (`Key (email)=(…)`) zu `<value>`. Typ, Stack und innere Ausnahmen bleiben erhalten.
- **SMTP-Fehler** werden schon beim Speichern von Adressen bereinigt (Spalte `last_error` und Log).

## Abhängigkeiten

- **NuGet** (`dotnet list package --vulnerable --include-transitive`): keine bekannten Schwachstellen.
- **npm** (`npm audit`): **Produktion 0** (`npm audit --omit=dev`). Entwicklungswerkzeuge: 17 Meldungen, alle nur im Werkzeug, nicht im ausgelieferten Build: der Entwicklungsserver `vite` 8.3.0 (steckt fest in `@angular/build` 22.2.3) und die Linter-Kette (`minimatch`, Verweigerung durch aufwendige Muster). Siehe F-24.

## Funde und Status

| Nr. | Fund | Schwere | Status |
|---|---|---|---|
| F-01 | Keine Sicherheits-Header an API-Antworten | mittel | **behoben** (Middleware, Test) |
| F-02 | Kein HSTS | mittel | **behoben** (`UseHsts` außerhalb Development) |
| F-03 | Web-App: Produktions-`index.html` enthielt ein Inline-Skript, eine strenge CSP wäre blockiert worden; keine Header-Vorlage | mittel | **behoben** (Kritisch-CSS-Einbettung aus, `deploy/security-headers.caddy`, im Browser ohne Verstöße geprüft). **Offen (gering):** `style-src 'unsafe-inline'` bleibt, solange kein Nonce pro Anfrage ausgegeben wird |
| F-04 | CORS | – | **in Ordnung**: keines eingerichtet, per Test belegt |
| F-05 | `Server: Kestrel` verrät die Software | gering | **behoben** |
| F-06 | API-Antworten waren cachebar (nur einzelne Endpunkte hatten `no-store`) | mittel | **behoben** (`no-store` für alles unter `/api`) |
| F-07 | Weitergeleitete Header werden von jeder Quelle akzeptiert; wer die API direkt erreicht, kann seine IP und damit die Grenzen fälschen | mittel | **offen, bewusst**: die API darf nur hinter dem Proxy erreichbar sein (Netzwerk, nicht Code). Bei der Bereitstellung (Prompt 25) im internen Docker-Netz ohne veröffentlichten Port; sonst `KnownProxies` setzen |
| F-08 | `AllowedHosts: *` | gering | **offen, bewusst**: Links in Mails kommen aus `App:PublicBaseUrl`, nie aus dem Host-Header; in der Bereitstellung auf die eigene Domain setzen |
| F-09 | Keine Grenzen für Anmeldung, Registrierung, Passwort vergessen/zurücksetzen, E-Mail bestätigen, Einladungslinks | hoch | **behoben** (Richtlinien `Login`, `Register`, `Recovery`, `Manage`, Tests) |
| F-10 | Kein allgemeines Auffangnetz gegen Überflutung | gering | **behoben** (`General`, 600/Minute) |
| F-11 | Zwei-Faktor abschalten und neue Wiederherstellungscodes prüften das Passwort ohne Fehlversuchszähler: mit gestohlener Sitzung erratbar | hoch | **behoben** (zählt für die Kontosperre, Tests) |
| F-12 | Kontosperre (5 Fehlversuche, 15 Minuten) | – | **in Ordnung**, getestet |
| F-13 | Zwei-Faktor war nur in der API, keine Oberfläche | mittel | **behoben** (Einstellungen › Sicherheit mit QR-Code, Wiederherstellungscodes, Abschalten). **Offen (gering):** Mitarbeitende ohne Einstellungs-Zugriff können es noch nicht einschalten |
| F-14 | Die Registrierung verrät, ob eine Adresse schon ein Konto hat (`auth.email_taken`) | gering | **bewusst hingenommen** (bei Registrierung nicht zu vermeiden, begrenzt durch `Register`); „Passwort vergessen“ verrät es nicht |
| F-15 | Controller ohne Angabe waren anonym zugänglich (`AuthController`, Einladungsseiten, `PingController`); ein vergessenes Attribut hätte einen offenen Endpunkt ergeben | hoch | **behoben** (Verboten-als-Standard, ausdrückliches `[AllowAnonymous]`, Inventar-Test) |
| F-16 | Mitarbeitende konnten über die Kundensuche des Kalenders **alle** Kunden des Salons mit Name, E-Mail und Telefon finden | hoch | **behoben** (nur Kunden mit Termin bei ihnen, Test) |
| F-17 | Fehlende Längenbegrenzungen: Anmeldung (E-Mail, Passwort, Codes), Tokens, Zwei-Faktor-Codes, Suchbegriffe | mittel | **behoben** (Validatoren, Tests) |
| F-18 | Keine Obergrenze für die Größe einer Anfrage (Standard 30 MB) | mittel | **behoben** (1 MB) |
| F-19 | Freitext in Mails | – | **in Ordnung**, Test auf alle Mailarten und Felder erweitert |
| F-20 | Anfrage-Log enthielt Verwaltungs- und Einladungs-Token im Pfad | hoch | **behoben** (`LogPaths`, Tests) |
| F-21 | Ausnahmen konnten Adressen und Schlüsselwerte ins Log schreiben (Datenbankfehler nennen den doppelten Wert) | mittel | **behoben** (`PersonalData.ForLog`, Tests) |
| F-22 | Mandantentrennung, Antiforgery, Cookie-Flags, Passwort-Hashing | – | **in Ordnung**, durch bestehende Tests belegt; Test für fehlendes/falsches Antiforgery-Token ergänzt |
| F-23 | NuGet-Abhängigkeiten | – | **in Ordnung**: keine bekannten Schwachstellen |
| F-24 | npm-Entwicklungswerkzeuge (vite 8.3.0 im Entwicklungsserver, minimatch in der Linter-Kette) | gering | **offen, bewusst**: nur Werkzeug, nicht im Build. Die Behebung für vite braucht `@angular/build` ≥ 22.2.6, das Angular 22.2.4 voraussetzt, das noch nicht erschienen ist. Bis dahin den Entwicklungsserver nur lokal betreiben. Wieder prüfen, wenn Angular nachzieht |

## Wenn ein Endpunkt dazukommt

1. Richtlinie wählen (`SalonMember`, `SalonManager`, `SalonOwner`, `PlatformAdmin`); anonym nur mit `[AllowAnonymous]` und einer eigenen Rate-Limit-Richtlinie, dazu in der Liste im Test und in der Tabelle oben eintragen.
2. Den Salon nie aus dem Body übernehmen; Daten nur über den Kontext lesen (der Filter trägt die Trennung). Soll ein Dienst mandantenübergreifend lesen, muss die Datei in der Architektur-Testliste stehen.
3. Alle Texte im Validator begrenzen; Freitext in Mails über den Renderer (kodiert), nie als HTML zusammenbauen.
4. Nichts Persönliches loggen (IDs genügen); Ausnahmen über `PersonalData.ForLog`.
5. Schreibt der Endpunkt etwas, das ein gesperrter Salon dennoch dürfen muss (wie die DSGVO-Löschung), `[AllowWhenReadOnly]` setzen, sonst nicht.
