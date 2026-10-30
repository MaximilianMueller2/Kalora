import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TranslocoPipe } from '@jsverse/transloco';
import { MAX_REASON_LENGTH } from './requests.models';

/** Texts of the dialog; the defaults are those of declining a request. */
export interface ReasonDialogData {
  titleKey: string;
  leadKey: string;
  confirmKey: string;
}

const DECLINE_TEXTS: ReasonDialogData = {
  titleKey: 'requests.decline.title',
  leadKey: 'requests.decline.lead',
  confirmKey: 'requests.decline.confirm',
};

/** Asks for an optional reason before a request is declined or an appointment cancelled. Closes with the reason ('' for none) or nothing on cancel. */
@Component({
  selector: 'app-decline-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    TranslocoPipe,
  ],
  styleUrl: '../../shared/dialogs/dialog.scss',
  template: `
    <h2 mat-dialog-title>{{ texts.titleKey | transloco }}</h2>
    <mat-dialog-content>
      <p class="lead">{{ texts.leadKey | transloco }}</p>
      <mat-form-field appearance="outline">
        <mat-label>{{ 'requests.decline.reason' | transloco }}</mat-label>
        <textarea
          matInput
          rows="3"
          [maxLength]="maxLength"
          [ngModel]="reason()"
          (ngModelChange)="reason.set($event)"
        ></textarea>
        <mat-hint align="end">{{ reason().length }} / {{ maxLength }}</mat-hint>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>
        {{ 'common.cancel' | transloco }}
      </button>
      <button mat-flat-button type="button" [mat-dialog-close]="reason().trim()">
        {{ texts.confirmKey | transloco }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .lead {
      margin: 0 0 12px;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class DeclineDialog {
  protected readonly texts =
    inject<ReasonDialogData | null>(MAT_DIALOG_DATA, { optional: true }) ?? DECLINE_TEXTS;
  protected readonly maxLength = MAX_REASON_LENGTH;
  protected readonly reason = signal('');
}
