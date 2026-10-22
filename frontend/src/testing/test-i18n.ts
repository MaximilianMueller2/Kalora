import { Injectable } from '@angular/core';
import { Translation, TranslocoLoader, provideTransloco } from '@jsverse/transloco';
import { Observable, of } from 'rxjs';
import { DEFAULT_LANGUAGE, SUPPORTED_LANGUAGES } from '../app/core/i18n/languages';

export type TestTranslations = Record<string, Translation>;

/** Texts used by specs; any language not listed gets an empty dictionary. */
export const TEST_TRANSLATIONS: TestTranslations = {
  de: {
    common: {
      close: 'Schließen',
      loading: 'Wird geladen …',
      language: 'Sprache wählen',
      skipToContent: 'Zum Inhalt springen',
      poweredBy: 'Online-Buchung mit Kalora',
    },
    nav: {
      label: 'Hauptnavigation',
      dashboard: 'Dashboard',
      calendar: 'Kalender',
      requests: 'Anfragen',
      services: 'Leistungen',
      team: 'Team',
      settings: 'Einstellungen',
      logout: 'Abmelden',
    },
    schedule: {
      open: 'Geöffnet',
      closed: 'Geschlossen',
      addBlock: 'Zeitraum hinzufügen',
      removeBlock: 'Zeitraum entfernen',
      copyPrevious: 'Wie {{day}}',
      start: 'Von',
      end: 'Bis',
      problems: {
        block_invalid: 'Das Ende muss nach dem Beginn liegen.',
        overlap: 'Die Zeiträume überschneiden sich.',
      },
    },
    theme: {
      system: 'Darstellung: wie Gerät',
      light: 'Darstellung: hell',
      dark: 'Darstellung: dunkel',
    },
    validation: {
      required: 'Dieses Feld ist erforderlich.',
      email: 'Bitte gib eine gültige E-Mail-Adresse ein.',
      minLength: 'Mindestens {{minLength}} Zeichen.',
      passwordMismatch: 'Die Passwörter stimmen nicht überein.',
      termsRequired: 'Bitte stimme zu.',
      range: 'Bitte gib eine ganze Zahl im erlaubten Bereich ein.',
      slugInvalid: 'Ungültiger Link.',
      phoneInvalid: 'Ungültige Telefonnummer.',
      urlInvalid: 'Ungültige Adresse.',
      endBeforeStart: 'Ende vor Beginn.',
    },
    errors: {
      unknown: 'Unbekannter Fehler',
      network: { unreachable: 'Keine Verbindung' },
      booking: { slot_unavailable: 'Dieser Termin ist nicht mehr frei.' },
      auth: {
        invalid_credentials: 'E-Mail oder Passwort stimmt nicht.',
        email_taken: 'Konto existiert schon.',
      },
      invitation: { invalid: 'Einladung ungültig.' },
      settings: { slug_taken: 'Link schon vergeben.' },
      closure: { date_in_past: 'Datum liegt in der Vergangenheit.' },
    },
  },
  en: { nav: { dashboard: 'Dashboard' } },
  tr: { nav: { dashboard: 'Gösterge paneli' } },
};

@Injectable()
class InMemoryLoader implements TranslocoLoader {
  getTranslation(lang: string): Observable<Translation> {
    return of(TEST_TRANSLATIONS[lang] ?? {});
  }
}

export function provideTestI18n(): ReturnType<typeof provideTransloco> {
  return provideTransloco({
    config: {
      availableLangs: [...SUPPORTED_LANGUAGES],
      defaultLang: DEFAULT_LANGUAGE,
      fallbackLang: DEFAULT_LANGUAGE,
      reRenderOnLangChange: true,
      prodMode: true,
    },
    loader: InMemoryLoader,
  });
}
