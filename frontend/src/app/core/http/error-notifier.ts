import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslocoService } from '@jsverse/transloco';
import { ApiError } from './api-error';

const SNACKBAR_DURATION_MS = 6000;

@Injectable({ providedIn: 'root' })
export class ErrorNotifier {
  private readonly snackBar = inject(MatSnackBar);
  private readonly transloco = inject(TranslocoService);

  show(error: ApiError): void {
    this.snackBar.open(this.messageFor(error), this.transloco.translate('common.close'), {
      duration: SNACKBAR_DURATION_MS,
    });
  }

  /** `errors.<code>` if the code is translated, else a generic text for its HTTP class. */
  messageFor(error: ApiError): string {
    const specific = `errors.${error.code}`;
    const translated = this.transloco.translate(specific);
    if (translated !== specific) {
      return translated;
    }

    return this.transloco.translate(
      error.isNetworkError ? 'errors.network.unreachable' : 'errors.unknown',
    );
  }
}
