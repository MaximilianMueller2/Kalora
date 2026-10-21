# Lizenzen der Abhängigkeiten

Erlaubt: MIT, Apache-2.0, BSD-2/3, ISC, PostgreSQL, SIL OFL (Schriften). Regeln siehe [CLAUDE.md](../CLAUDE.md).

| Paket | Version | Lizenz | Zweck |
|-------|---------|--------|-------|
| Microsoft.EntityFrameworkCore | 10.0.13 | MIT | ORM, `IAppDbContext` in Application |
| Microsoft.EntityFrameworkCore.Relational | 10.0.13 | MIT | Zentral gepinnt, damit alle EF-Assemblies dieselbe Version haben |
| Microsoft.EntityFrameworkCore.Design | 10.0.13 | MIT | Migrationen (nur Build-Zeit, `PrivateAssets=all`) |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | PostgreSQL | EF-Core-Provider für PostgreSQL |
| EFCore.NamingConventions | 10.0.1 | Apache-2.0 | snake_case-Namen für Tabellen und Spalten |
| Microsoft.AspNetCore.OpenApi | 10.0.13 | MIT | OpenAPI-Dokument (nur Development) |
| Scalar.AspNetCore | 2.18.2 | MIT | API-Doku-UI (nur Development) |
| Serilog.AspNetCore | 10.0.0 | Apache-2.0 | Strukturiertes Logging |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.13 | MIT | DI-Registrierung in Application |
| Microsoft.Extensions.Options | 10.0.13 | MIT | Options-Validierung |
| Microsoft.Extensions.Options.ConfigurationExtensions | 10.0.13 | MIT | Options an Konfiguration binden |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.13 | MIT | Integrationstests (Test) |
| Microsoft.Extensions.TimeProvider.Testing | 10.11.0 | MIT | `FakeTimeProvider` (Test) |
| Microsoft.NET.Test.Sdk | 18.11.0 | MIT | Testhost (Test) |
| Testcontainers.PostgreSql | 4.16.0 | MIT | Echte PostgreSQL-18-Container in Integrationstests (Test) |
| xunit | 2.9.3 | Apache-2.0 | Testframework (Test) |
| xunit.runner.visualstudio | 4.0.1 | Apache-2.0 | Test-Runner (Test) |

Transitive Pakete stammen ausschließlich von Microsoft (MIT), Npgsql (PostgreSQL), Serilog (Apache-2.0), xUnit (Apache-2.0), Testcontainers (MIT), Docker.DotNet (MIT) und Scalar (MIT). Versionen stehen zentral in `backend/Directory.Packages.props`.

## Werkzeuge und Container-Images

| Werkzeug / Image | Version | Lizenz | Zweck |
|------------------|---------|--------|-------|
| dotnet-ef (globales Tool) | 10.0.13 | MIT | Migrationen erzeugen |
| postgres (Docker) | 18 | PostgreSQL | Datenbank |
| axllent/mailpit (Docker) | v1.31 | MIT | Mail-Fänger, nur Entwicklung |
