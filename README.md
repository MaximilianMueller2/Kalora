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

## Status

Repository-Gerüst. Backend und Frontend folgen in den nächsten Schritten.
