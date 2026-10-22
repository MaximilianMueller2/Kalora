import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslocoService } from '@jsverse/transloco';

const DURATION_MS = 3000;

/** Short confirmations ("Saved"); errors go through the ErrorNotifier. */
@Injectable({ providedIn: 'root' })
export class Toast {
  private readonly snackBar = inject(MatSnackBar);
  private readonly transloco = inject(TranslocoService);

  show(translationKey: string): void {
    this.snackBar.open(this.transloco.translate(translationKey), undefined, {
      duration: DURATION_MS,
    });
  }
}
