import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslocoService } from '@jsverse/transloco';
import { ApiError } from './api-error';
import { ErrorMessageService } from './error-message.service';

const SNACKBAR_DURATION_MS = 6000;

@Injectable({ providedIn: 'root' })
export class ErrorNotifier {
  private readonly snackBar = inject(MatSnackBar);
  private readonly transloco = inject(TranslocoService);
  private readonly messages = inject(ErrorMessageService);

  show(error: ApiError): void {
    this.snackBar.open(this.messageFor(error), this.transloco.translate('common.close'), {
      duration: SNACKBAR_DURATION_MS,
    });
  }

  messageFor(error: ApiError): string {
    return this.messages.messageFor(error);
  }
}
