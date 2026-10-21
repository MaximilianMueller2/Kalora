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

## Status

Backend-Grundgerüst und Datenbank stehen. Mandantenfähigkeit, Authentifizierung und Frontend folgen in den nächsten Schritten.
