import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule } from '@angular/material/dialog';
import { TranslocoPipe } from '@jsverse/transloco';

/** "Really cancel?" Closes with `true` if the customer confirms. */
@Component({
  selector: 'app-cancel-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatDialogModule, TranslocoPipe],
  template: `
    <h2 mat-dialog-title>{{ 'manage.cancel.title' | transloco }}</h2>
    <mat-dialog-content>{{ 'manage.cancel.text' | transloco }}</mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close cdkFocusInitial>
        {{ 'manage.cancel.keep' | transloco }}
      </button>
      <button mat-flat-button type="button" [mat-dialog-close]="true">
        {{ 'manage.cancel.confirm' | transloco }}
      </button>
    </mat-dialog-actions>
  `,
})
export class CancelDialog {}
