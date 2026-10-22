# Architektur (Gerüst)

Wird mit jedem Schritt erweitert; die verbindlichen Regeln stehen in [CLAUDE.md](../CLAUDE.md).

## Überblick

Angular (Frontend) → ASP.NET Core 10 Web API → PostgreSQL. Auslieferung und HTTPS über Caddy.

## Aufrufkette

Browser: Komponente → Feature-Service → ApiClient → Caddy (`/api`) → Server: Controller → Service (hinter Interface) → EF Core (`IAppDbContext`) → PostgreSQL.

## Schichten (Backend)

- `Kalora.Api` – Controller, Middleware, `Program.cs`
- `Kalora.Application` – Service-Interfaces und Implementierungen, DTOs, Validatoren, `IAppDbContext`
- `Kalora.Domain` – Entitäten, reine Regeln (Verfügbarkeit, Statusautomat), keine Abhängigkeiten
- `Kalora.Infrastructure` – EF Core, Migrationen, Mail, Kalender, `TimeProvider`

## Mandantenfähigkeit

Alle Salons teilen sich eine Datenbank; jede salonbezogene Tabelle trägt `SalonId`. Begründung und verworfene Alternativen: [ADR 0001](adr/0001-multi-tenancy.md).

```mermaid
flowchart LR
    R[Request] --> M[TenantResolutionMiddleware]
    M -- "/api/public/salons/{slug}" --> S[SalonResolver: Slug → SalonId]
    M -- "angemeldet" --> C[Claim salon_id]
    S --> T[ITenantContext]
    C --> T
    T --> F[Query-Filter 'Tenant' + SaveChanges-Guard + Concurrency-Token]
    F --> DB[(PostgreSQL)]
```

- **Lesen:** globaler Query-Filter auf `Salon` und allen `ISalonOwned`-Entitäten; ohne Mandant keine Daten.
- **Schreiben:** `TenantSaveGuard` setzt/prüft `SalonId`, `SalonId` ist zusätzlich Concurrency-Token (gefälschte Entitäten treffen keine Zeile).
- **Umgehung:** nur `AllSalons()` / `AllTenants<T>()`, per Architekturtest auf eine feste Dateiliste beschränkt.

## Anmeldung und Rollen

Begründung: [ADR 0002](adr/0002-authentication.md). Cookie-Sitzung (ASP.NET Core Identity), ein Nutzer gehört zu genau einem Salon; Salon und Rolle stehen als Claims im Cookie.

| Endpunkt | Zugriff | Zweck |
|----------|---------|-------|
| `GET /api/antiforgery` | offen | setzt das `XSRF-TOKEN`-Cookie; schreibende Anfragen senden es als `X-XSRF-TOKEN` |
| `POST /api/auth/register` | offen | Inhaber:in + Salon (30 Tage Test) anlegen, anmelden |
| `POST /api/auth/login`, `/logout` | offen | Sitzung starten/beenden; mit aktiver 2FA zusätzlich `twoFactorCode` oder `recoveryCode` |
| `POST /api/auth/confirm-email`, `/forgot-password`, `/reset-password` | offen | E-Mail bestätigen, Passwort zurücksetzen |
| `GET /api/me` | angemeldet | Profil, Salon, Rolle, 2FA-Status |
| `POST /api/auth/two-factor/setup`, `/enable`, `/disable`, `/recovery-codes` | angemeldet | TOTP einrichten und verwalten |
| `POST /api/invitations` | Owner, Manager | Mitarbeitende einladen (Manager nur als Staff) |
| `GET /api/public/invitations/{token}`, `POST …/accept` | Token | Einladung ansehen und annehmen |

Policies: `SalonMember` (Owner, Manager, Staff), `SalonManager` (Owner, Manager), `SalonOwner`, `PlatformAdmin`. Fehlercodes u. a.: `auth.invalid_credentials`, `auth.locked_out`, `auth.two_factor_required`, `auth.email_taken`, `auth.invalid_token`, `invitation.invalid`, `invitation.expired`, `security.antiforgery_failed`.

## Salon-Einstellungen

Alle Endpunkte unter `/api/settings` für Owner und Manager (Policy `SalonManager`), jeder Salon sieht und ändert nur seine eigenen Daten (Mandantenfilter).

| Endpunkt | Zweck |
|----------|-------|
| `GET/PUT /api/settings/profile` | Name, Slug, Branche, Adresse, Telefon, Zeitzone, Standardsprache, Impressum, Link zur Datenschutzerklärung. Den Slug ändert nur der Owner (`settings.slug_owner_only`), er muss eindeutig, URL-tauglich (3–40 Zeichen) und nicht reserviert sein |
| `GET /api/settings/slug-suggestion?name=` | freier Slug aus dem Namen (`name`, `name-2`, …) |
| `GET/PUT /api/settings/booking-rules` | Zeitraster (5/10/15/30 min), Mindestvorlauf, Buchungshorizont, Stornofrist, Auto-Bestätigung, Frist für unbeantwortete Anfragen (`request_expiry_hours`), Löschfrist |
| `GET/PUT /api/settings/opening-hours` | Wochenplan, Montag zuerst, mehrere Zeiträume pro Tag (höchstens 4, Zeiträume dürfen sich berühren, nicht überlappen); `PUT` ersetzt die ganze Woche, nicht genannte Tage sind geschlossen |
| `GET/POST /api/settings/closures`, `DELETE …/{id}` | Schließtage: ein Tag oder ein Bereich (bis 62 Tage, schon geschlossene Tage werden übersprungen); „heute“ ist der Kalendertag in der Zeitzone des Salons, die Liste zeigt nur heutige und kommende |

Uhrzeiten reisen als `"HH:mm"` (`TimeOnlyJsonConverter`), Wochentage und Enums als Text. Prüfungen mit FluentValidation liefern stabile Codes (`settings.*`, `opening_hours.*`, `closure.*`). Der Slug-Dienst (`ISlugAllocator`) prüft die Eindeutigkeit über alle Salons und ist eine der wenigen Stellen mit Mandantenfilter-Umgehung. Die Zeitzonen-ID muss eine gültige IANA-ID sein; das Laufzeit-Image braucht dafür `tzdata` (Prompt 25).

## Leistungen

Alle Endpunkte unter `/api/catalog`. Lesen dürfen alle Rollen des Salons (die Terminbuchung im Kalender braucht die Liste), ändern nur Owner und Manager (Policy `SalonManager`). Jede Änderung antwortet mit dem ganzen neuen Katalog (Kategorien samt Leistungen in Anzeigereihenfolge), das Frontend ersetzt damit seinen Stand und braucht keine Einzelabgleiche.

| Endpunkt | Zweck |
|----------|-------|
| `GET /api/catalog` | Kategorien mit Leistungen; `hasAppointments` zeigt, ob die Leistung schon Termine hat |
| `POST/PUT/DELETE /api/catalog/categories[/{id}]` | Kategorie anlegen, umbenennen, löschen (nur leer, sonst `category.not_empty`) |
| `PUT /api/catalog/categories/order` | neue Reihenfolge aller Kategorien |
| `POST/PUT/DELETE /api/catalog/services[/{id}]` | Leistung anlegen, ändern (auch in andere Kategorie verschieben, aktivieren/deaktivieren), löschen |
| `PUT /api/catalog/categories/{id}/services/order` | neue Reihenfolge der Leistungen einer Kategorie |
| `POST /api/catalog/templates/apply` | Branchenvorlage übernehmen (Branche des Salons, wenn nicht angegeben) |

- **Preis und Dauer:** Preis in Cent (`0` bis `1.000.000`, `0` = kostenlos), Währung fest `EUR`, „ab“-Preis über `priceIsFrom`. Dauer 5 bis 720 min und Puffer davor/danach 0 bis 240 min jeweils in 5-Minuten-Schritten. Das Frontend rechnet Euro-Eingaben (`25`, `25,5`, `25.50`, `1.250,00`) in Cent um (`parsePriceToCents`); die Prüfung liegt zusätzlich im Validator.
- **Löschen:** Eine Leistung mit Terminen lässt sich nicht löschen (`service.has_appointments`, Konflikt), nur deaktivieren: Termine behalten so ihre Leistung, und sie wird nicht mehr online angeboten. Eine Kategorie lässt sich nur löschen, wenn sie leer ist.
- **Reihenfolge:** Die Anfrage enthält die vollständige Liste der IDs; fehlende, doppelte oder fremde IDs ergeben `catalog.order_mismatch`. So überschreibt eine veraltete Ansicht nie neuere Änderungen eines anderen Geräts stillschweigend.
- **Mandanten:** Kategorie und Leistung werden immer über den Mandantenfilter geladen. Eine fremde Kategorie-ID ergibt `category.not_found` (404), auch beim Anlegen und Verschieben.
- **Vorlagen:** `Catalog/Templates/service-templates.json` (eingebettete Ressource, Schicht Application) enthält je Branche Kategorien und Leistungen mit Namen in `de`, `en` und `tr` und Richtpreisen zum Anpassen. Beim Übernehmen werden die Namen in der Standardsprache des Salons geschrieben (Rückfall `de`) und **hinten angehängt**, so entstehen bei zweimaligem Übernehmen Dopplungen, aber nie verlorene Daten. Ein Unit-Test prüft alle Vorlagen (Sprachen vollständig, Grenzen eingehalten).
- **Frontend (`features/services`):** Seite mit Kategorien und Leistungen, Sortieren per Ziehen (CDK Drag & Drop, Ziehgriff) und gleichwertig per Menü „Nach oben/unten“ für Tastatur und Screenreader. Die neue Reihenfolge erscheint sofort und wird gespeichert; schlägt das fehl, kommt die alte Reihenfolge zurück. Dialoge für Leistung (Puffer unter „Erweitert“), Kategorie und Bestätigung. Dauer wird mit `Intl` in der Sprache der Oberfläche geschrieben (`LocaleFormatService.formatDuration`).

## Frontend-Gerüst

Angular 22 (Standalone, zoneless, Signals, OnPush) mit Angular Material 3. Quelltext in `frontend/src/app`:

- `core/http` – `ApiClient` (einziger Nutzer von `HttpClient`, präfixt `/api`), `ApiError` (ProblemDetails → typisierter Fehler mit Code), `ErrorMessageService` (Code → Text). Interceptoren in dieser Reihenfolge, von außen nach innen: Credentials → Sitzung abgelaufen (401 bei angemeldetem Nutzer → Login-Seite mit Rücksprungadresse) → Fehler (Snackbar, außer 401 und `silent`) → Antiforgery-Wiederholung. Schreibende Anfragen tragen den Header `X-XSRF-TOKEN` (Angulars XSRF-Unterstützung). Lehnt der Server wegen eines fehlenden oder veralteten Tokens ab (`security.antiforgery_failed`), holt `AntiforgeryService` ein neues und die innerste Stufe wiederholt die Anfrage einmal; sie setzt den Header selbst, weil Angulars XSRF-Interceptor weiter außen schon gelaufen ist, bevor das Cookie existierte.
- `core/i18n` – Transloco (de, en, tr). Startsprache: gespeicherte Wahl → Profil → Browser → de. Texte unter `public/i18n/<sprache>.json` (gemeinsam) und `public/i18n/<feature>/<sprache>.json` (je Feature, per `provideTranslocoScope` mit dem Feature geladen). `LocaleFormatService` und die Pipes `localDate` / `money` formatieren Datum, Zeit (in der Zeitzone des Salons) und Geld nach Sprache.
- `core/layout` – `Shell` für den Salon-Bereich (Seitenleiste ab 900 px, sonst untere Navigation; Einträge je nach Rolle, Abmelden), `AuthLayout` (zentrierte Karte für Konto-Seiten) und `PublicLayout` für Kundenseiten; Seitentitel folgen der Sprache.
- `core/theme` – hell, dunkel oder wie das Gerät (CSS `color-scheme`).
- `shared/` – wiederverwendbare Bausteine: Sprach- und Theme-Umschalter, Pipes und der `WeekScheduleEditor` (Wochenplan mit mehreren Zeiträumen pro Tag, „wie Vortag“; zweiseitig gebunden per `[(schedule)]`, die Regeln stehen als reine Funktionen in `week-schedule.model.ts` und entsprechen den Backend-Regeln; wird für Öffnungszeiten und später für Dienstpläne genutzt). `features/<name>/` – Seite, Service und Modelle eines Bereichs, jeweils lazy geladen.

- `features/auth` – `AuthService` (spiegelt `GET /api/me` in Signals: `currentUser`, `isLoggedIn`, `roles`; die Sitzung selbst ist das HttpOnly-Cookie), Guards `authGuard`, `guestGuard`, `roleGuard(...)`, `safeReturnUrl` (nur Pfade innerhalb der App als Rücksprungziel) und die Seiten Anmelden (mit zweitem Faktor und Wiederherstellungscode), Registrieren, Passwort vergessen/zurücksetzen, E-Mail bestätigen, Einladung annehmen. Formulare sind Signal Forms (`form()`, `[formField]`, `[formRoot]`); Fehler vom Server erscheinen inline als übersetzter Fehlercode.

- `features/settings` – vier Tabs als eigene Routen unter `/app/settings/{salon,hours,booking,closures}` (Salon, Öffnungszeiten, Buchungsregeln, Schließtage), nur für Owner und Manager. `SettingsService` kapselt die API; Zahlenfelder werden als Text bearbeitet und beim Speichern umgewandelt.

Routen: `/auth/*` (Konto-Seiten; Anmelden, Registrieren und „Passwort vergessen“ nur für Nicht-Angemeldete), `/app/*` (nur angemeldet: `onboarding`, `dashboard`, `calendar`, `requests` für alle Rollen; `services`, `team`, `settings` nur für Owner und Manager), `/:slug` (öffentliche Buchungsseite), sonst 404. Die Links in den E-Mails zeigen auf `/auth/confirm-email`, `/auth/reset-password` und `/auth/invitation/:token`. Reservierte Slugs (`app`, `admin`, `auth`, `api`, `m`, …) verhindert das Backend (`SlugGenerator`).

Schrift (Inter) und Icons (Material Icons Outlined) liegen lokal im Build, es gibt keine Anfragen an externe Hosts. `npm run i18n:check` meldet Schlüssel, die in einer Sprache fehlen.

## Backend-Gerüst

- **Projektreferenzen:** `Api → Application → Domain`; `Infrastructure → Application`; `Api` referenziert `Infrastructure` nur, um es per `AddInfrastructure` zu registrieren (Composition Root).
- **DI:** pro Schicht eine Extension-Methode (`AddApplication`, `AddInfrastructure`); `Program.cs` bleibt kurz. Service-Implementierungen sind `internal`.
- **Fehlerbehandlung:** Services liefern `Result<T>` / `Result` mit `Error(Type, Code, Parameter)`. `ProblemDetailsMapper` (Api) übersetzt `ErrorType` in 400, 401, 403, 404, 409, 422 und setzt `code` (und optional `parameter`) als Extension. Unerwartete Exceptions fängt `GlobalExceptionHandler` ab (500, `server.unexpected`, ohne Details).
- **Dumme Controller:** Beispiel `PingController` → `IPingService`; Ableitung von `ApiControllerBase` übersetzt das `Result`.
- **Konfiguration:** `appsettings.json` + Umgebungsvariablen (`App__PublicBaseUrl`). Options-Klassen werden beim Start validiert (`ValidateOnStart`); ungültige Konfiguration stoppt den Host.
- **Reverse Proxy:** TLS endet im Proxy. `UseForwardedHeaders` (X-Forwarded-Proto/-For) lässt die API das ursprüngliche Schema erkennen; das ist nur sicher, solange die API ausschließlich über den Proxy erreichbar ist (Prompt 25). Das Antiforgery-Cookie ist `SameAsRequest` (Secure hinter dem Proxy, lokal über `http://localhost` nutzbar), das Sitzungs-Cookie immer `Secure`.
- **Logging/Betrieb:** Serilog (Konsole, aus Konfiguration), Request-Logging nur mit Pfad, Health-Check `/health`. OpenAPI (`/openapi/v1.json`) und Scalar (`/scalar/v1`) nur in Development.
- **Zeit:** `TimeProvider` wird über DI geliefert (`TimeProvider.System`, in Tests `FakeTimeProvider`).

## Datenbank

PostgreSQL 18, EF Core Code-First, Migrationen in `Kalora.Infrastructure/Persistence/Migrations`. Tabellen und Spalten in snake_case (`EFCore.NamingConventions`), Enums als Text, Geld als Cent, Zeitpunkte als `timestamptz` (UTC), Öffnungs-/Arbeitszeiten als `time` + Wochentag (`smallint`, 0 = Sonntag).

| Tabelle | Zweck |
|---------|-------|
| `salons` | Mandant; `id` ist die SalonId aller anderen Tabellen |
| `salon_opening_hours`, `salon_closures` | Öffnungszeiten (mehrere Blöcke pro Tag), Schließtage |
| `salon_members` | Nutzer ↔ Salon mit Rolle (Owner, Manager, Staff); FK auf `AppUser` folgt mit Identity |
| `staff_members`, `staff_working_hours`, `staff_absences` | Mitarbeitende, Dienstplan mit Gültigkeit, Abwesenheiten |
| `service_categories`, `services`, `staff_services` | Leistungskatalog, Fähigkeiten (optional mit eigener Dauer) |
| `customers` | Kunden, pro Salon getrennt; E-Mail eindeutig je Salon |
| `appointments`, `time_blocks` | Termine (mit Snapshot von Name/Preis/Dauer), Blocker |
| `email_outbox`, `audit_logs` | Mail-Warteschlange, Nachvollziehbarkeit |

- **Basisklasse `Entity`:** `Id` als GUID v7, `CreatedAtUtc`, `UpdatedAtUtc`. `ISalonOwned` markiert Entitäten mit `SalonId`.
- **Zeitstempel:** `AuditTimestampInterceptor` setzt sie bei jedem Speichern über `TimeProvider`.
- **Löschverhalten:** Kindtabellen eines Mitarbeitenden bzw. einer Leistung (Dienstplan, Abwesenheiten, Fähigkeiten, Blocker) werden mitgelöscht; alles andere ist `Restrict`. Mitarbeitende und Leistungen werden deaktiviert statt gelöscht, sobald Termine existieren.
- **Doppelbuchungsschutz:** `appointments.blocked_range` ist eine aus `blocked_start_utc`/`blocked_end_utc` (Termin plus Puffer) erzeugte `tstzrange`-Spalte (`[)`, Back-to-back erlaubt). Der Exclusion-Constraint `ex_appointments_no_overlap` (`btree_gist`) verbietet pro `staff_id` überlappende Termine mit Status `Requested` oder `Confirmed`. Bei Verletzung meldet PostgreSQL SQLSTATE `23P01`; der Buchungsservice übersetzt das später in `booking.slot_unavailable`.
- **Migrationen:** In Development beim Start automatisch (`MigrateDatabaseAsync`), in Produktion als eigener Schritt (Migrationsbundle, Prompt 25). `btree_gist` ist eine „trusted extension“, der Datenbankbesitzer darf sie anlegen.

## Offene Abschnitte

Statusautomat, Verfügbarkeitsberechnung, Hintergrunddienste.
