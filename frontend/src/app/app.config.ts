import {
  provideHttpClient,
  withFetch,
  withInterceptors,
  withXsrfConfiguration,
} from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { MatIconRegistry } from '@angular/material/icon';
import { TitleStrategy, provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { antiforgeryRetryInterceptor } from './core/http/antiforgery-retry.interceptor';
import { credentialsInterceptor } from './core/http/credentials.interceptor';
import { errorInterceptor } from './core/http/error.interceptor';
import { provideI18n } from './core/i18n/provide-i18n';
import { AppTitleStrategy } from './core/layout/title-strategy';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    { provide: TitleStrategy, useExisting: AppTitleStrategy },
    provideHttpClient(
      withFetch(),
      // Order matters: the retry interceptor is innermost, so the error interceptor only sees the final outcome.
      withInterceptors([credentialsInterceptor, errorInterceptor, antiforgeryRetryInterceptor]),
      withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' }),
    ),
    provideI18n(),
    // Icons come from the locally bundled "Material Icons Outlined" font, not a CDN.
    provideAppInitializer(() => {
      inject(MatIconRegistry).setDefaultFontSetClass('material-icons-outlined');
    }),
  ],
};
