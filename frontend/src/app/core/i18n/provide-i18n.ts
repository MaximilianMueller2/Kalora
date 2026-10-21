import {
  EnvironmentProviders,
  isDevMode,
  makeEnvironmentProviders,
  provideAppInitializer,
  inject,
} from '@angular/core';
import { provideTransloco } from '@jsverse/transloco';
import { DEFAULT_LANGUAGE, SUPPORTED_LANGUAGES } from './languages';
import { LanguageService } from './language.service';
import { TranslocoHttpLoader } from './transloco-loader';

export function provideI18n(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideTransloco({
      config: {
        availableLangs: [...SUPPORTED_LANGUAGES],
        defaultLang: DEFAULT_LANGUAGE,
        fallbackLang: DEFAULT_LANGUAGE,
        reRenderOnLangChange: true,
        prodMode: !isDevMode(),
      },
      loader: TranslocoHttpLoader,
    }),
    provideAppInitializer(() => inject(LanguageService).initialize()),
  ]);
}
