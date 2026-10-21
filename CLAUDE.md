# Kalora – verbindliche Projektregeln

Kalora ist eine mandantenfähige Web-App (SaaS) für Online-Terminbuchung in Salons (Friseur, Barber, Nägel, Wimpern, Kosmetik). Viele Salons nutzen sie unabhängig voneinander. Kunden buchen ohne Konto, der Salon bestätigt jede Anfrage (oder schaltet Auto-Bestätigung ein). Keine Provision, keine Online-Zahlung.

Diese Regeln gelten für alle Schritte, ohne dass sie wiederholt werden müssen. Änderungen an Regeln erfolgen ausschließlich in dieser Datei.

## Architektur

- **Frontend:** neueste stabile Angular-Version (vor dem Anlegen per npm prüfen), Angular Material + CDK. Nur Standalone-Komponenten, `ChangeDetectionStrategy.OnPush`, zoneless. State mit Signals (`signal`, `computed`, `input()`, `output()`, `model()`, `resource` wo sinnvoll), `inject()` statt Konstruktor-Injection, Control Flow `@if/@for/@defer`, Lazy Loading.
- **Aufrufkette:** Component → Feature-Service → ApiClient → Backend-Controller. Komponenten nutzen nie `HttpClient`.
- **Backend:** .NET 10 LTS, ASP.NET Core Web API mit Controllern. Controller sind dumm: binden, genau einen Service über sein Interface aufrufen, `Result` in HTTP-Antwort übersetzen. Keine Logik und kein DbContext im Controller.
- **Schichten:** `Kalora.Api` → `Kalora.Application` (Service-Interfaces + Implementierungen, DTOs, Validatoren, `IAppDbContext`) → `Kalora.Domain` (Entitäten, reine Regeln, keine Abhängigkeiten). `Kalora.Infrastructure` implementiert die Interfaces (EF Core, Mail, Kalender). Jede Service-Klasse hat ein Interface und wird per DI registriert.
- **Fehler:** Services liefern `Result<T>` mit stabilen Fehlercodes (z. B. `booking.slot_unavailable`). Die API antwortet mit `ProblemDetails` inkl. Code; das Frontend übersetzt den Code.
- **Datenbank:** PostgreSQL, EF Core Code-First mit Migrationen. Zeitpunkte in UTC, jeder Salon hat eine IANA-Zeitzone. Geld als Cent (`int`) + Währung. Systemzeit nur über `TimeProvider`.
- **Mandantenfähigkeit:** jede salonbezogene Tabelle hat `SalonId`, globaler Query-Filter, `SalonId` nie aus dem Request-Body übernehmen.
- **Kein MediatR, kein AutoMapper:** einfache Services und Mapping-Extension-Methoden.

## Mehrsprachigkeit

- Keine fest verdrahteten Texte. Frontend: Transloco mit `de` (Standard), `en`, `tr`. Backend-Mails: Ressourcen pro Sprache.
- Code, Bezeichner und Commits auf Englisch.

## Lizenzen

- Erlaubt nur: MIT, Apache-2.0, BSD-2/3, ISC, PostgreSQL, SIL OFL (Schriften) sowie gleichwertig freizügige 0BSD, MIT-0, CC0-1.0, BlueOak-1.0.0 und Python-2.0 (u. a. `tslib`, ohne das Angular nicht läuft).
- Nur für Entwicklungswerkzeuge und Datenpakete, die nicht ausgeliefert werden: MPL-2.0 (Dateien werden nicht verändert) und CC-BY-4.0 (`caniuse-lite`).
- Verboten: kommerzielle oder dual lizenzierte Pakete (u. a. MediatR ≥ 13, AutoMapper ≥ 15, FluentAssertions ≥ 8, MassTransit ≥ 9, QuestPDF, Hangfire, FullCalendar Premium) sowie GPL, AGPL, LGPL, RPL, SSPL.
- Vor jedem neuen Paket die Lizenz prüfen und in `docs/LICENSES.md` eintragen.

## Datenschutz & Sicherheit

- Kunden sehen nie Daten anderer Kunden; öffentliche Endpunkte liefern nur freie Zeiten.
- Datenminimierung. Keine externen CDNs, Tracker oder Google Fonts – alles lokal.
- Keine personenbezogenen Daten in Logs.

## Qualität

- Kleine, gut benannte Klassen und Komponenten; Kommentare nur, wo das Warum nicht offensichtlich ist.
- Nullable an, Warnungen als Fehler, ESLint ohne Fehler.
- Unit-Tests für Logik, Integrationstests für Endpunkte.
- UI: simpel, modern, minimalistisch, mobil zuerst, barrierearm. Lieber eine Option weniger als ein überladener Bildschirm.

## Git & „Abschluss nach CLAUDE.md"

- Pro Schritt den im Prompt genannten Branch nutzen. Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`).
- Am Ende jedes Schritts: Backend und Frontend bauen, alle Tests grün, `docs/LICENSES.md` aktuell, committen, mit `--no-ff` nach `main` mergen, `main` und Branch pushen.
- Dann kurz zusammenfassen: was gebaut wurde, wie man es lokal testet, offene Punkte.
- Fragen, die Architektur oder Datenmodell betreffen, vor dem Umsetzen stellen.
- Ausnahme Cloud-Sessions (Claude Code im Web): Die Session gibt einen festen Branch vor. Dort wird nur auf diesen Branch gepusht; der Merge nach `main` erfolgt dann per Pull Request durch den Inhaber.
