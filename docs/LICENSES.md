# Lizenzen der Abhängigkeiten

Erlaubt: MIT, Apache-2.0, BSD-2/3, ISC, PostgreSQL, SIL OFL (Schriften). Regeln siehe [CLAUDE.md](../CLAUDE.md).

| Paket | Version | Lizenz | Zweck |
|-------|---------|--------|-------|
| Microsoft.EntityFrameworkCore | 10.0.13 | MIT | ORM, `IAppDbContext` in Application |
| Microsoft.EntityFrameworkCore.Relational | 10.0.13 | MIT | Zentral gepinnt, damit alle EF-Assemblies dieselbe Version haben |
| Microsoft.EntityFrameworkCore.Design | 10.0.13 | MIT | Migrationen (nur Build-Zeit, `PrivateAssets=all`) |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | PostgreSQL | EF-Core-Provider für PostgreSQL |
| EFCore.NamingConventions | 10.0.1 | Apache-2.0 | snake_case-Namen für Tabellen und Spalten |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.13 | MIT | Identity-Speicher (Nutzer, Tokens) in EF Core |
| Microsoft.AspNetCore.DataProtection.EntityFrameworkCore | 10.0.13 | MIT | Data-Protection-Schlüssel in der Datenbank |
| Microsoft.Extensions.Identity.Stores | 10.0.13 | MIT | `AppUser` und `UserManager` in Application |
| FluentValidation | 12.2.0 | Apache-2.0 | Validierung der Anfragen in Application |
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

## Frontend (npm)

Direkte Abhängigkeiten aus `frontend/package.json`. Transitive Pakete prüft `npx license-checker-rseidelsohn --summary` (Stand: 321× MIT, 27× Apache-2.0, 20× ISC, 14× BSD-2-Clause, 6× BSD-3-Clause, 1× OFL-1.1, dazu die unten genannten Sonderfälle). Die CI-Prüfung folgt in Prompt 24.

| Paket | Version | Lizenz | Zweck |
|-------|---------|--------|-------|
| @angular/core, common, compiler, forms, platform-browser, router | 22.2.3 | MIT | Framework |
| @angular/material, @angular/cdk | 22.2.4 | MIT | UI-Komponenten (Material 3), Layout-Helfer |
| @jsverse/transloco | 8.4.0 | MIT | Übersetzungen zur Laufzeit (de, en, tr) |
| rxjs | 7.8.2 | Apache-2.0 | Reaktive Streams (HttpClient, Router) |
| tslib | 2.8.1 | 0BSD | TypeScript-Laufzeithelfer (von Angular vorausgesetzt) |
| @fontsource-variable/inter | 5.3.0 | OFL-1.1 | Schrift Inter, lokal ausgeliefert |
| material-icons | 1.13.14 | Apache-2.0 | Icon-Schrift (Outlined), lokal ausgeliefert (155 kB statt 3,9 MB bei Material Symbols) |
| @angular/build, @angular/cli, @angular/compiler-cli | 22.2.3 | MIT | Build (Dev) |
| typescript | 6.0.3 | Apache-2.0 | Compiler (Dev) |
| vitest, jsdom | 5.0.5, 30.1.3 | MIT | Unit-Tests (Dev) |
| eslint, @eslint/js, typescript-eslint, angular-eslint | 10.13.0, 10.0.1, 8.71.0, 22.6.0 | MIT | Linting (Dev) |
| prettier | 3.9.10 | MIT | Formatierung (Dev) |

Transitive Sonderfälle: `lightningcss` (MPL-2.0, Build-Werkzeug, nicht ausgeliefert), `caniuse-lite` (CC-BY-4.0, Daten für den Build), `@csstools/*` (MIT-0), `argparse` (Python-2.0), `lru-cache` und `minimatch` (BlueOak-1.0.0), `mdn-data` (CC0-1.0), `@bufbuild/protobuf` (Apache-2.0 AND BSD-3-Clause). Ausgeliefert wird davon nur `tslib`.
