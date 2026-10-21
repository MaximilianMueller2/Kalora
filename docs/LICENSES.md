# Lizenzen der Abhängigkeiten

Erlaubt: MIT, Apache-2.0, BSD-2/3, ISC, PostgreSQL, SIL OFL (Schriften). Regeln siehe [CLAUDE.md](../CLAUDE.md).

| Paket | Version | Lizenz | Zweck |
|-------|---------|--------|-------|
| Microsoft.AspNetCore.OpenApi | 10.0.13 | MIT | OpenAPI-Dokument (nur Development) |
| Scalar.AspNetCore | 2.18.2 | MIT | API-Doku-UI (nur Development) |
| Serilog.AspNetCore | 10.0.0 | Apache-2.0 | Strukturiertes Logging |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.13 | MIT | DI-Registrierung in Application |
| Microsoft.Extensions.Options | 10.0.13 | MIT | Options-Validierung |
| Microsoft.Extensions.Options.ConfigurationExtensions | 10.0.13 | MIT | Options an Konfiguration binden |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.13 | MIT | Integrationstests (Test) |
| Microsoft.Extensions.TimeProvider.Testing | 10.11.0 | MIT | `FakeTimeProvider` (Test) |
| Microsoft.NET.Test.Sdk | 18.11.0 | MIT | Testhost (Test) |
| xunit | 2.9.3 | Apache-2.0 | Testframework (Test) |
| xunit.runner.visualstudio | 4.0.1 | Apache-2.0 | Test-Runner (Test) |

Transitive Pakete stammen ausschließlich von Microsoft (MIT), Serilog (Apache-2.0), xUnit (Apache-2.0) und Scalar (MIT). Versionen stehen zentral in `backend/Directory.Packages.props`.
