import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule } from '@angular/material/dialog';
import { TranslocoPipe } from '@jsverse/transloco';

/** Says exactly what "delete customer" does before it does it. Closes with `true` if confirmed. */
@Component({
  selector: 'app-delete-customer-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatDialogModule, TranslocoPipe],
  template: `
    <h2 mat-dialog-title>{{ 'customers.delete.title' | transloco }}</h2>
    <mat-dialog-content>
      <p>{{ 'customers.delete.text' | transloco }}</p>
      <ul>
        <li>{{ 'customers.delete.removed' | transloco }}</li>
        <li>{{ 'customers.delete.kept' | transloco }}</li>
        <li>{{ 'customers.delete.final' | transloco }}</li>
      </ul>
      <p>{{ 'customers.delete.export' | transloco }}</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close cdkFocusInitial>
        {{ 'common.cancel' | transloco }}
      </button>
      <button mat-flat-button type="button" class="danger" [mat-dialog-close]="true">
        {{ 'customers.delete.confirm' | transloco }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    p,
    ul {
      margin: 0 0 12px;
    }

    .danger {
      --mat-button-filled-container-color: var(--mat-sys-error);
      --mat-button-filled-label-text-color: var(--mat-sys-on-error);
    }
  `,
})
export class DeleteCustomerDialog {}
