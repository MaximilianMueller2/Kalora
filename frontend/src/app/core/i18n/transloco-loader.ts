import { Injectable, inject } from '@angular/core';
import { Translation, TranslocoLoader } from '@jsverse/transloco';
import { Observable } from 'rxjs';
import { ApiClient } from '../http/api-client';

/**
 * Texts live in public/i18n/<lang>.json (shared) and public/i18n/<feature>/<lang>.json (per feature, loaded with
 * the feature). Transloco passes "<feature>/<lang>" for scopes.
 */
@Injectable({ providedIn: 'root' })
export class TranslocoHttpLoader implements TranslocoLoader {
  private readonly api = inject(ApiClient);

  getTranslation(lang: string): Observable<Translation> {
    return this.api.getAsset<Translation>(`/i18n/${lang}.json`);
  }
}
