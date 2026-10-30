import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';

const DURATION_MS = 3000;
const UNDO_MS = 6000;

/** A message with an undo button. */
export interface UndoOffer {
  /** Resolves when the message is gone: true if the person pressed the button, false if it timed out or was dismissed. */
  readonly undone: Promise<boolean>;
  /** Closes the message now, as if its time had run out. */
  dismiss(): void;
}

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

  /** Says what was done and offers to take it back; the caller does the real work only when `undone` is false. */
  offerUndo(messageKey: string, actionKey: string): UndoOffer {
    const ref = this.snackBar.open(
      this.transloco.translate(messageKey),
      this.transloco.translate(actionKey),
      { duration: UNDO_MS },
    );
    return {
      undone: firstValueFrom(ref.afterDismissed()).then((result) => result.dismissedByAction),
      dismiss: () => ref.dismiss(),
    };
  }
}
