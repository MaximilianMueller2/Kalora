export const SUPPORTED_LANGUAGES = ['de', 'en', 'tr'] as const;
export type Language = (typeof SUPPORTED_LANGUAGES)[number];
export const DEFAULT_LANGUAGE: Language = 'de';

/** Language names are shown in their own language so everybody finds theirs, whatever language is active. */
export const LANGUAGE_NAMES: Record<Language, string> = {
  de: 'Deutsch',
  en: 'English',
  tr: 'Türkçe',
};

export function isLanguage(value: unknown): value is Language {
  return SUPPORTED_LANGUAGES.includes(value as Language);
}

/** First supported language in the browser's preference list ("tr-TR" counts as "tr"). */
export function pickBrowserLanguage(preferences: readonly string[]): Language | undefined {
  for (const preference of preferences) {
    const primary = preference.toLowerCase().split('-')[0];
    if (isLanguage(primary)) {
      return primary;
    }
  }
  return undefined;
}

/** Explicit choice wins, then the profile, then the browser, then German. */
export function resolveLanguage(sources: {
  stored?: string | null;
  profile?: string | null;
  browser?: readonly string[];
}): Language {
  if (isLanguage(sources.stored)) {
    return sources.stored;
  }
  if (isLanguage(sources.profile)) {
    return sources.profile;
  }
  return pickBrowserLanguage(sources.browser ?? []) ?? DEFAULT_LANGUAGE;
}
