# Kalora

Kalora ist eine mandantenfähige Web-App, mit der Salons (Friseur, Barber, Nägel, Wimpern, Kosmetik) Online-Terminanfragen annehmen und ihren Teamkalender führen – ohne Telefon, ohne Provision und ohne Zahlungsabwicklung. Kunden buchen kostenlos und ohne Konto; der Salon bestätigt jede Anfrage.

## Voraussetzungen

- Git
- Node.js (aktuelle LTS)
- .NET 10 SDK
- Docker

## Projektstruktur

```
frontend/   Angular-App (Salon-Backend + öffentliche Buchungsseite)
backend/    .NET-10-Solution (Api, Application, Domain, Infrastructure, Tests)
deploy/     docker-compose, Caddyfile, .env.example, Backup-Skripte
docs/       ARCHITECTURE.md, LICENSES.md, adr/
CLAUDE.md   verbindliche Projektregeln
```

Die verbindlichen Regeln stehen in [CLAUDE.md](CLAUDE.md), die Architektur in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), erlaubte Abhängigkeiten in [docs/LICENSES.md](docs/LICENSES.md).

## Backend lokal starten

```bash
# 1. Datenbank und Mailpit starten (Passwort in deploy/.env anpassen)
cp deploy/.env.example deploy/.env
docker compose -f deploy/docker-compose.dev.yml --env-file deploy/.env up -d

# 2. Verbindung für die API hinterlegen (landet nicht im Git)
cd backend
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=localhost;Port=5432;Database=kalora;Username=kalora;Password=<dein Passwort>" \
  --project src/Kalora.Api

# 3. Bauen, testen, starten (Development migriert die Datenbank automatisch)
dotnet build
dotnet test            # Integrationstests starten eigene PostgreSQL-Container (Docker nötig)
dotnet run --project src/Kalora.Api   # http://localhost:5080
```

- Mailpit-Oberfläche: http://localhost:8025
- Neue Migration: `dotnet ef migrations add <Name> -p src/Kalora.Infrastructure -s src/Kalora.Api -o Persistence/Migrations`

- Health-Check: `GET /health`, Beispiel-Endpunkt: `GET /api/ping`
- API-Doku (nur Development): `/scalar/v1`

## Frontend lokal starten

Voraussetzung: Node.js ≥ 22.22.3 (Angular 22). Das Backend (siehe oben) läuft auf Port 5080; `ng serve` leitet `/api` dorthin weiter (`frontend/proxy.conf.json`). Für den ersten Start also zuerst Datenbank und API starten, dann das Frontend.

```bash
cd frontend
npm install
npm start               # http://localhost:4200
npm test                # Unit-Tests (Vitest)
npm run lint            # ESLint
npm run i18n:check      # fehlende Übersetzungen de/en/tr
npm run build
```

Ausprobieren: Auf http://localhost:4200 ein Konto registrieren. Die Bestätigungs-, Passwort-Reset- und Einladungs-Mails werden vom Hintergrunddienst über Mailpit zugestellt und sind unter http://localhost:8025 zu sehen (die Links führen auf `/auth/confirm-email`, `/auth/reset-password` bzw. `/auth/invitation/<token>`).

Hinweis: Das Backend setzt `Secure`-Cookies. Chrome und Firefox akzeptieren sie auch auf `http://localhost`, Safari nicht – dort lokal besser Chrome oder Firefox nutzen.

## Status

Backend-Grundgerüst, Datenbank, Mandantenfähigkeit und Anmeldung stehen, ebenso das Frontend-Grundgerüst (Layout, Sprachen, HTTP-Schicht) und die Konto-Seiten (Anmelden, Registrieren, Passwort, Einladung). Die Fachbereiche folgen in den nächsten Schritten.
