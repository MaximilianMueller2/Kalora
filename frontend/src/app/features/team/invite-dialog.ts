import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormField, FormRoot, form, required, validate } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiError } from '../../core/http/api-error';
import { fieldErrorText } from '../../shared/forms/field-error';
import { injectErrorMessage } from '../../shared/forms/server-error';
import { SalonRole } from '../auth/auth.models';
import { StaffMember } from './team.models';
import { TeamService } from './team.service';

export interface InviteDialogData {
  staff: StaffMember;
  canInviteManager: boolean;
}

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** Sends (or re-sends) the invitation to create a login for an existing calendar column. Closes with `true`. */
@Component({
  selector: 'app-invite-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormField,
    FormRoot,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    TranslocoPipe,
  ],
  styleUrl: '../../shared/dialogs/dialog.scss',
  template: `
    <h2 mat-dialog-title>
      {{ 'team.invite.title' | transloco: { name: data.staff.displayName } }}
    </h2>
    <form [formRoot]="form">
      <mat-dialog-content>
        <p class="lead">{{ 'team.invite.lead' | transloco }}</p>
        @if (errorMessage(); as message) {
          <p class="alert" role="alert">{{ message }}</p>
        }
        <mat-form-field appearance="outline">
          <mat-label>{{ 'team.dialog.inviteEmail' | transloco }}</mat-label>
          <input matInput type="email" autocomplete="off" [formField]="form.email" />
          @if (errorText(form.email().errors()); as error) {
            <mat-error>{{ error.key | transloco: error.params }}</mat-error>
          }
        </mat-form-field>
        @if (data.canInviteManager) {
          <mat-form-field appearance="outline">
            <mat-label>{{ 'team.dialog.inviteRole' | transloco }}</mat-label>
            <select matNativeControl [formField]="form.role">
              <option value="Staff">{{ 'team.role.Staff' | transloco }}</option>
              <option value="Manager">{{ 'team.role.Manager' | transloco }}</option>
            </select>
          </mat-form-field>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>
          {{ 'common.cancel' | transloco }}
        </button>
        <button mat-flat-button type="submit" [disabled]="form().submitting()">
          {{ 'team.invite.send' | transloco }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .lead {
      margin: 0 0 12px;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class InviteDialog {
  protected readonly data = inject<InviteDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<InviteDialog, boolean>>(MatDialogRef);
  private readonly team = inject(TeamService);

  protected readonly errorText = fieldErrorText;
  protected readonly errorCode = signal<string | null>(null);
  protected readonly errorMessage = injectErrorMessage(this.errorCode);

  private readonly model = signal<{ email: string; role: Exclude<SalonRole, 'Owner'> }>({
    email: '',
    role: 'Staff',
  });

  protected readonly form = form(
    this.model,
    (path) => {
      required(path.email);
      validate(path.email, ({ value }) =>
        value().trim() === '' || EMAIL_PATTERN.test(value().trim()) ? undefined : { kind: 'email' },
      );
    },
    { submission: { action: () => this.send() } },
  );

  private async send(): Promise<undefined> {
    this.errorCode.set(null);
    const { email, role } = this.model();
    try {
      await this.team.invite({ staffId: this.data.staff.id, email: email.trim(), role });
      this.dialogRef.close(true);
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    }
    return undefined;
  }
}
