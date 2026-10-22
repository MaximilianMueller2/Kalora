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
