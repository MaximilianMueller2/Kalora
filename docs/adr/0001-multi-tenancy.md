# ADR 0001: Mandantenfähigkeit über gemeinsame Datenbank, `SalonId` und globalen Query-Filter

- Status: akzeptiert
- Datum: 2026-10-21

## Kontext

Kalora bedient viele unabhängige Salons mit einer Installation. Kein Salon darf Daten eines anderen lesen, ändern oder löschen – auch nicht durch einen Fehler im Code oder eine manipulierte Anfrage. Gleichzeitig soll der Betrieb für eine einzelne Person auf einem Heimserver machbar bleiben (ein Server, eine Datenbank, ein Backup).

## Entscheidung

Alle Salons teilen sich eine PostgreSQL-Datenbank und dieselben Tabellen. Isolation entsteht in mehreren Schichten:

1. **`SalonId` überall.** Jede salonbezogene Entität implementiert `ISalonOwned`. Die Tabelle `salons` ist der Mandant selbst; ihre `id` ist die `SalonId`.
2. **`ITenantContext`** liefert die `SalonId` der laufenden Anfrage:
   - angemeldete Nutzer: aus dem Claim `salon_id`,
   - öffentliche Endpunkte (`/api/public/salons/{slug}/…`): aus dem per Slug aufgelösten Salon. Öffentliche Pfade lesen nie Claims; ein unbekannter oder ungültiger Slug ergibt `404 salon.not_found`.
   - Der Mandant wird pro Anfrage genau einmal gesetzt (`TenantResolutionMiddleware`) und kann danach nicht gewechselt werden. Eine `SalonId` aus dem Request-Body wird nie verwendet.
3. **Globaler Query-Filter** (`"Tenant"`) auf allen `ISalonOwned`-Entitäten und auf `Salon` selbst. Ohne Mandant (`null`) liefert jede Abfrage nichts (fail closed). Der Filter wirkt auch bei `ExecuteUpdate`/`ExecuteDelete`.
4. **Schreibschutz im `SaveChanges`** (`TenantSaveGuard`):
   - Neue Entitäten bekommen die `SalonId` des Mandanten, eine abweichende wird mit `TenantViolationException` abgelehnt.
   - Die `SalonId` einer bestehenden Entität darf nie geändert werden. Ein losgelöstes `Update()` markiert sie als geändert und wird deshalb abgelehnt; Services laden, ändern und speichern.
   - Geänderte oder gelöschte Entitäten müssen zum Mandanten gehören (relevant, wenn sie über die Bypass-Methoden geladen wurden).
   - Ohne Mandant (Registrierung, Hintergrunddienste) muss die `SalonId` explizit gesetzt sein.
5. **`SalonId` als Concurrency-Token.** `UPDATE` und `DELETE` enthalten `AND salon_id = …`. Eine gefälschte Entität mit fremder ID trifft keine Zeile (`DbUpdateConcurrencyException`) und ändert nichts.
6. **Explizite Umgehung.** Nur `IAppDbContext.AllSalons()` und `AllTenants<T>()` umgehen den Filter. Erlaubt sind sie für Plattform-Admin-Services, die Slug-Auflösung, die Registrierung und Hintergrunddienste. Ein Architekturtest (`ArchitectureTests`) lässt die Methoden nur in einer festen Dateiliste zu; neue Verwendungen erfordern eine bewusste Änderung dieser Liste im Review.

Zusätzlich verhindert ein Test, dass Controller einen `DbContext` nutzen, und ein Strukturtest stellt sicher, dass jede neue `ISalonOwned`-Entität Filter und Concurrency-Token erhält.

## Konsequenzen

- Ein vergessenes `Where(s => s.SalonId == …)` ist ausgeschlossen; neue Entitäten sind durch Schnittstelle und Strukturtest automatisch abgedeckt.
- Services dürfen für Änderungen kein losgelöstes `Update()` verwenden.
- Hintergrunddienste und Plattform-Admin-Code müssen den Mandanten bewusst wählen und die Bypass-Methoden nutzen.
- Fremde IDs verhalten sich wie nicht vorhandene Datensätze (404), es gibt keinen Hinweis auf deren Existenz.
- Der Filter ersetzt keine Rollenprüfung innerhalb eines Salons (Owner, Manager, Staff); die folgt mit Autorisierung in Prompt 04.

## Verworfene Alternativen

- **Eine Datenbank pro Salon.** Stärkste Trennung, aber Migrationen, Backups und Verbindungen skalieren mit der Salonzahl; für die Zielgröße und einen Heimserver unverhältnismäßig.
- **Ein Schema pro Salon.** Gleiche Migrations- und Betriebsprobleme wie oben, dazu aufwendiges Schema-Routing in EF Core.
- **Nur manuelle `Where`-Bedingungen in jedem Service.** Ein einziges vergessenes `Where` wäre ein Datenleck; nicht verlässlich testbar.
- **PostgreSQL Row-Level-Security als alleiniger Schutz.** Wirksam als zweite Verteidigungslinie, braucht aber pro Verbindung gesetzte Sitzungsvariablen, die mit Connection-Pooling fehleranfällig sind. Bleibt als mögliche Härtung für Prompt 22 (Sicherheits-Härtung) offen.
- **Getrennte Deployments pro Salon.** Höchster Betriebsaufwand, widerspricht dem Ziel „radikal einfach“.
