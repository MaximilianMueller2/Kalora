import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormField, FormRoot, form, maxLength, required, validate } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiError } from '../../core/http/api-error';
import { fieldErrorText } from '../../shared/forms/field-error';
import { injectErrorMessage } from '../../shared/forms/server-error';
import { SalonRole } from '../auth/auth.models';
import { STAFF_COLORS, StaffMember } from './team.models';
import { TeamService } from './team.service';

export interface StaffDialogData {
  /** Set when editing. */
  staff?: StaffMember;
  /** The signed-in user has no calendar column yet and may take this one. */
  canLinkSelf: boolean;
  /** Only owners may invite managers. */
  canInviteManager: boolean;
}

export interface StaffDialogResult {
  staff: StaffMember;
  /** Creating worked, but the invitation e-mail could not be queued; the person can be invited again later. */
  inviteError: ApiError | null;
}

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

interface StaffModel {
  displayName: string;
  color: string;
  isBookableOnline: boolean;
  autoConfirm: boolean;
  isActive: boolean;
  linkToMe: boolean;
  inviteEmail: string;
  inviteRole: Exclude<SalonRole, 'Owner'>;
}

/** Creates a person (optionally inviting them by e-mail) or edits one. Closes with a {@link StaffDialogResult}. */
@Component({
  selector: 'app-staff-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormField,
    FormRoot,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    TranslocoPipe,
  ],
  styleUrl: '../../shared/dialogs/dialog.scss',
  template: `
    <h2 mat-dialog-title>
      {{ (data.staff ? 'team.dialog.editTitle' : 'team.dialog.addTitle') | transloco }}
    </h2>
    <form [formRoot]="form">
      <mat-dialog-content>
        @if (errorMessage(); as message) {
          <p class="alert" role="alert">{{ message }}</p>
        }

        <mat-form-field appearance="outline">
          <mat-label>{{ 'team.dialog.name' | transloco }}</mat-label>
          <input matInput type="text" autocomplete="off" [formField]="form.displayName" />
          @if (errorText(form.displayName().errors()); as error) {
            <mat-error>{{ error.key | transloco: error.params }}</mat-error>
          }
        </mat-form-field>

        <div role="group" class="colors" [attr.aria-label]="'team.dialog.color' | transloco">
          @for (color of colors; track color) {
            <button
              type="button"
              class="swatch"
              [style.background]="color"
              [attr.aria-pressed]="model().color === color"
              [attr.aria-label]="color"
              (click)="chooseColor(color)"
            ></button>
          }
        </div>

        <mat-checkbox [formField]="form.isBookableOnline">{{
          'team.dialog.bookableOnline' | transloco
        }}</mat-checkbox>
        <mat-checkbox [formField]="form.autoConfirm">{{
          'team.dialog.autoConfirm' | transloco
        }}</mat-checkbox>
        <p class="hint">{{ 'team.dialog.autoConfirmHint' | transloco }}</p>
        @if (data.staff) {
          <mat-checkbox [formField]="form.isActive">{{
            'team.dialog.active' | transloco
          }}</mat-checkbox>
          <p class="hint">{{ 'team.dialog.activeHint' | transloco }}</p>
        } @else {
          @if (data.canLinkSelf) {
            <mat-checkbox [formField]="form.linkToMe">{{
              'team.dialog.linkToMe' | transloco
            }}</mat-checkbox>
          }
          @if (!model().linkToMe) {
            <h3>{{ 'team.dialog.inviteTitle' | transloco }}</h3>
            <p class="hint">{{ 'team.dialog.inviteHint' | transloco }}</p>
            <mat-form-field appearance="outline">
              <mat-label>{{ 'team.dialog.inviteEmail' | transloco }}</mat-label>
              <input matInput type="email" autocomplete="off" [formField]="form.inviteEmail" />
              @if (errorText(form.inviteEmail().errors()); as error) {
                <mat-error>{{ error.key | transloco: error.params }}</mat-error>
              }
            </mat-form-field>
            @if (data.canInviteManager) {
              <mat-form-field appearance="outline">
                <mat-label>{{ 'team.dialog.inviteRole' | transloco }}</mat-label>
                <select matNativeControl [formField]="form.inviteRole">
                  <option value="Staff">{{ 'team.role.Staff' | transloco }}</option>
                  <option value="Manager">{{ 'team.role.Manager' | transloco }}</option>
                </select>
              </mat-form-field>
            }
          }
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>
          {{ 'common.cancel' | transloco }}
        </button>
        <button mat-flat-button type="submit" [disabled]="form().submitting()">
          {{ 'team.dialog.save' | transloco }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    mat-checkbox {
      display: block;
    }

    h3 {
      margin: 16px 0 4px;
      font: var(--mat-sys-title-small);
    }

    .hint {
      margin: 0 0 8px;
      padding-left: 16px;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }

    h3 + .hint {
      padding-left: 0;
    }

    .colors {
      display: flex;
      flex-wrap: wrap;
      gap: 4px;
      margin: 0 0 12px;
    }

    .swatch {
      width: 44px;
      height: 44px;
      border: 3px solid transparent;
      border-radius: 50%;
      background-clip: content-box;
      padding: 4px;
      cursor: pointer;

      &[aria-pressed='true'] {
        border-color: var(--mat-sys-on-surface);
      }

      &:focus-visible {
        outline: 2px solid var(--mat-sys-primary);
        outline-offset: 2px;
      }
    }
  `,
})
export class StaffDialog {
  protected readonly data = inject<StaffDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<StaffDialog, StaffDialogResult>>(MatDialogRef);
  private readonly team = inject(TeamService);

  protected readonly colors = STAFF_COLORS;
  protected readonly errorText = fieldErrorText;
  protected readonly errorCode = signal<string | null>(null);
  protected readonly errorMessage = injectErrorMessage(this.errorCode);

  protected readonly model = signal<StaffModel>({
    displayName: this.data.staff?.displayName ?? '',
    color: this.data.staff?.color ?? STAFF_COLORS[0],
    isBookableOnline: this.data.staff?.isBookableOnline ?? true,
    autoConfirm: this.data.staff?.autoConfirm ?? false,
    isActive: this.data.staff?.isActive ?? true,
    linkToMe: false,
    inviteEmail: '',
    inviteRole: 'Staff',
  });

  protected readonly form = form(
    this.model,
    (path) => {
      required(path.displayName);
      maxLength(path.displayName, 100);
      validate(path.inviteEmail, ({ value }) => {
        const email = value().trim();
        return email === '' || EMAIL_PATTERN.test(email) ? undefined : { kind: 'email' };
      });
    },
    { submission: { action: () => this.save() } },
  );

  protected chooseColor(color: string): void {
    this.model.update((current) => ({ ...current, color }));
  }

  private async save(): Promise<undefined> {
    this.errorCode.set(null);
    const model = this.model();
    const base = {
      displayName: model.displayName.trim(),
      color: model.color,
      isBookableOnline: model.isBookableOnline,
      autoConfirm: model.autoConfirm,
    };
    try {
      if (this.data.staff) {
        const staff = await this.team.update(this.data.staff.id, {
          ...base,
          isActive: model.isActive,
        });
        this.dialogRef.close({ staff, inviteError: null });
        return undefined;
      }

      const staff = await this.team.create({ ...base, linkToCurrentUser: model.linkToMe });
      this.dialogRef.close({ staff, inviteError: await this.inviteIfRequested(staff, model) });
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    }
    return undefined;
  }

  private async inviteIfRequested(staff: StaffMember, model: StaffModel): Promise<ApiError | null> {
    const email = model.inviteEmail.trim();
    if (model.linkToMe || email === '') {
      return null;
    }
    try {
      await this.team.invite({ staffId: staff.id, email, role: model.inviteRole });
      return null;
    } catch (error) {
      return error instanceof ApiError ? error : new ApiError(0, 'unknown', '');
    }
  }
}
