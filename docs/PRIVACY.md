# Datenschutz in Kalora – welche Daten, wo, wie lange, wer sieht sie

Dieses Dokument beschreibt den technischen Stand. Es ist **keine Rechtsberatung**; die Rechtstexte unter `frontend/public/legal/` sind Vorlagen mit `[BITTE PRÜFEN]`-Markierungen und müssen vor dem Betrieb von einer fachkundigen Person geprüft werden.

## Rollen

- **Salon = Verantwortlicher** für die Daten seiner Kundschaft und seines Teams.
- **Kalora = Auftragsverarbeiter** für diese Daten (Vorlage Auftragsverarbeitungsvertrag: `/legal/avv`, bei der Registrierung verlinkt).
- **Kalora = Verantwortlicher** nur für die Konten der Salon-Betreiber:innen und die technische Sicherheit der Plattform.

## Welche Daten, wo, wie lange

| Daten | Wo | Wer sieht sie | Wie lange |
|---|---|---|---|
| Name, E-Mail, Telefon der Kundschaft | `customers` | Inhaber:in, Manager:innen; Mitarbeitende nur im Kalender bei Terminen ihrer eigenen Spalte | Bis zum Ende der **Aufbewahrungsdauer des Salons** (`Salon.RetentionMonths`, Standard 24 Monate) nach dem letzten Termin, dann Anonymisierung |
| Einwilligung zu Werbung (Zeitpunkt) | `customers.marketing_consent_at_utc` | Inhaber:in, Manager:innen | Wie die Kundschaft; wird beim Anonymisieren gelöscht |
| Kundenwunsch („Hinweis“) und interne Notiz am Termin | `appointments` | Inhaber:in, Manager:innen, Person des Termins | Werden beim Anonymisieren der Person geleert. Hinweis in der Buchung: keine Gesundheitsdaten eintragen |
| Termine (Zeit, Leistung, Person, Preis, Status) | `appointments` | Salon-Team gemäß Rolle | Bleiben für die Statistik **ohne Personenbezug** erhalten, nachdem die Kundschaft anonymisiert wurde |
| Link zum Verwalten des Termins | `appointments.manage_token_hash` (nur der Hash) | niemand; der Link steht in den Mails | Der Link wirkt bis 30 Tage nach dem Termin; danach wird der Hash entfernt |
| Mails in der Warteschlange | `email_outbox` | niemand außer dem Versand | Inhalt, Betreff und Adresse werden **30 Tage** nach dem Anlegen geleert (versendet oder endgültig fehlgeschlagen) |
| Einladungen für Team-Mitglieder | `staff_invitations` | Inhaber:in | Werden 30 Tage nach Annahme oder Ablauf gelöscht |
| Team-Konten (Name, E-Mail, Passwort-Hash, 2FA) | `users`, `staff` | Inhaber:in, Manager:innen (Name, Rolle) | Solange das Konto besteht |
| Audit-Log (wer hat was geändert) | `audit_logs` | Salon-Inhaber:in | Enthält nur IDs und Aktionsschlüssel, **keine personenbezogenen Daten** |
| Anwendungs-Logs | Server | Betrieb | **Keine** personenbezogenen Daten (weder Namen noch Mail-Adressen oder Telefonnummern) |

Die öffentliche Buchungsseite zeigt nur freie Zeiten und die Daten des Salons selbst – nie Daten anderer Kunden. Es gibt keine externen CDNs, Tracker oder Google Fonts; Schriften und Skripte kommen vom eigenen Server.

## Rechte der betroffenen Personen (im Salon-Bereich unter „Kunden“)

- **Auskunft und Übertragbarkeit:** Auf der Seite der Person gibt es **Export als JSON oder CSV** (Stammdaten, Einwilligung, alle Termine mit Notizen). Die CSV-Datei ist gegen Formel-Einschleusung in Tabellenkalkulationen abgesichert und mit BOM für Excel.
- **Löschung („Kunde löschen“):** Kalora **anonymisiert** statt zu löschen – Name wird „Anonymisiert“, E-Mail, Telefon und Einwilligung werden entfernt, Notizen und Verwaltungs-Links der Termine geleert, wartende Mails an die Person geleert oder als fehlgeschlagen markiert. Die Termine bleiben ohne Person bestehen (Auslastung, Umsatzschätzung). Das ist **endgültig**. Solange Termine in der Zukunft offen sind, wird abgelehnt (`customer.has_open_appointments`): erst absagen oder abschließen.
- **Berichtigung:** Eine Bearbeitung der Stammdaten in der Kundenakte gibt es noch nicht (offener Punkt); bis dahin legt der Salon bei Bedarf einen neuen Termin mit den richtigen Daten an bzw. wendet sich an den Support.
- Jede Auskunft (`customer.exported`) und Anonymisierung (`customer.anonymized`) steht im Audit-Log, ohne Personendaten.

## Automatische Bereinigung

Der **RetentionWorker** (Hintergrunddienst, Abschnitt `Retention` in `appsettings.json`) läuft alle `IntervalHours` (24) und nur auf **einer** Instanz (`Retention:Enabled`):

1. Anonymisiert Kundschaft, deren letzter Termin länger zurückliegt als die Aufbewahrungsdauer des Salons (ohne Termine: seit dem Anlegen). Wer einen späteren oder künftigen Termin hat, bleibt unberührt. In Paketen zu `BatchSize` (100).
2. Entfernt Verwaltungs-Links `TokenDays` (30) nach dem Terminende.
3. Löscht benutzte oder abgelaufene Einladungen nach `TokenDays`.
4. Leert Inhalte versendeter oder fehlgeschlagener Mails nach `MailContentDays` (30).

Die Aufbewahrungsdauer stellt der Salon in den Buchungsregeln ein (`retentionMonths`). Das Audit-Log vermerkt die automatische Anonymisierung mit Grund `retention`.

## Rechtstexte im Produkt

| Seite | Pfad | Quelle |
|---|---|---|
| Impressum von Kalora | `/legal/imprint` | `public/legal/imprint.de.md` |
| Datenschutzerklärung von Kalora | `/legal/privacy` | `public/legal/privacy.de.md` |
| Nutzungsbedingungen | `/legal/terms` | `public/legal/terms.de.md` |
| Auftragsverarbeitungsvertrag | `/legal/avv` | `public/legal/avv.de.md` |
| Datenschutz eines Salons | `/{salon}/privacy` | Text des Salons (`Salon.PrivacyText`), Vorlage `public/legal/salon-privacy.de.md` |

Die Texte gibt es verbindlich nur auf Deutsch; in anderen Sprachen weist die Seite darauf hin. Offene Stellen sind sichtbar markiert (`[BITTE PRÜFEN: …]`). Markdown wird in einen Syntaxbaum gelesen und als Angular-Elemente gezeichnet – **kein `innerHTML`**, Links nur auf `https:`, `http:`, `mailto:` oder Seiten der App.
