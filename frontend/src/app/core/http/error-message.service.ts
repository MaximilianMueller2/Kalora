import { Injectable, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { ApiError, NETWORK_ERROR_CODE } from './api-error';

/** Turns the stable error codes of the API into text in the active language. */
@Injectable({ providedIn: 'root' })
export class ErrorMessageService {
  private readonly transloco = inject(TranslocoService);

  /** `errors.<code>` if the code is translated, else a generic text. */
  messageFor(error: ApiError | string): string {
    const code = typeof error === 'string' ? error : error.code;
    const specific = `errors.${code}`;
    const translated = this.transloco.translate(specific);
    if (translated !== specific) {
      return translated;
    }

    return this.transloco.translate(
      code === NETWORK_ERROR_CODE ? 'errors.network.unreachable' : 'errors.unknown',
    );
  }
}
