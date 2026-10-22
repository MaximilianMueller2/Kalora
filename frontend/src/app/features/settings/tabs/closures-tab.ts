import { ChangeDetectionStrategy, Component, inject, resource, signal } from '@angular/core';
import { FormField, FormRoot, form, maxLength, required, validate } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiError } from '../../../core/http/api-error';
import { LocaleFormatService } from '../../../core/i18n/locale-format.service';
import { Toast } from '../../../core/ui/toast';
import { fieldErrorText } from '../../../shared/forms/field-error';
import { injectErrorMessage } from '../../../shared/forms/server-error';
import { Closure } from '../settings.models';
import { SettingsService } from '../settings.service';

@Component({
  selector: 'app-closures-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormField,
    FormRoot,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    TranslocoPipe,
  ],
  styleUrl: '../settings-tabs.scss',
  template: `
    <h2>{{ 'settings.closures.title' | transloco }}</h2>
    <p class="lead">{{ 'settings.closures.lead' | transloco }}</p>

    @if (errorMessage(); as message) {
      <p class="alert" role="alert">{{ message }}</p>
    }

    <form [formRoot]="form">
      <div class="row">
        <mat-form-field appearance="outline">
          <mat-label>{{ 'settings.closures.date' | transloco }}</mat-label>
          <input matInput type="date" [formField]="form.date" />
          @if (errorText(form.date().errors()); as error) {
            <mat-error>{{ error.key | transloco: error.params }}</mat-error>
          }
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>{{ 'settings.closures.endDate' | transloco }}</mat-label>
          <input matInput type="date" [formField]="form.endDate" />
          <mat-hint>{{ 'settings.closures.endDateHint' | transloco }}</mat-hint>
          @if (errorText(form.endDate().errors()); as error) {
            <mat-error>{{ error.key | transloco: error.params }}</mat-error>
          }
        </mat-form-field>
      </div>
      <mat-form-field appearance="outline">
        <mat-label>{{ 'settings.closures.reason' | transloco }}</mat-label>
        <input matInput type="text" [formField]="form.reason" />
        @if (errorText(form.reason().errors()); as error) {
          <mat-error>{{ error.key | transloco: error.params }}</mat-error>
        }
      </mat-form-field>
      <div class="actions">
        <button mat-flat-button type="submit" [disabled]="form().submitting()">
          {{ 'settings.closures.add' | transloco }}
        </button>
      </div>
    </form>

    <h3>{{ 'settings.closures.upcoming' | transloco }}</h3>
    @if (closures.isLoading()) {
      <p role="status">{{ 'common.loading' | transloco }}</p>
    } @else if (closures.value()?.length === 0) {
      <p class="hint">{{ 'settings.closures.empty' | transloco }}</p>
    } @else {
      <ul class="list">
        @for (closure of closures.value(); track closure.id) {
          <li>
            <span class="date">{{ longDate(closure) }}</span>
            @if (closure.reason) {
              <span class="reason">{{ closure.reason }}</span>
            }
            <button
              mat-icon-button
              type="button"
              class="remove"
              [attr.aria-label]="
                'settings.closures.remove' | transloco: { date: longDate(closure) }
              "
              (click)="remove(closure)"
            >
              <mat-icon aria-hidden="true">delete</mat-icon>
            </button>
          </li>
        }
      </ul>
    }
  `,
  styles: `
    h3 {
      margin: 32px 0 8px;
      font: var(--mat-sys-title-medium);
    }

    .list {
      margin: 0;
      padding: 0;
      list-style: none;
    }

    li {
      display: flex;
      align-items: center;
      gap: 12px;
      min-height: 52px;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }

    .date {
      font-weight: 500;
    }

    .reason {
      flex: 1;
      color: var(--mat-sys-on-surface-variant);
    }

    .remove {
      width: 44px;
      height: 44px;
      margin-left: auto;
    }
  `,
})
export class ClosuresTab {
  private readonly settings = inject(SettingsService);
  private readonly toast = inject(Toast);
  private readonly format = inject(LocaleFormatService);

  protected readonly errorText = fieldErrorText;
  protected readonly closures = resource({ loader: () => this.settings.listClosures() });
  protected readonly errorCode = signal<string | null>(null);
  protected readonly errorMessage = injectErrorMessage(this.errorCode);

  private readonly model = signal({ date: '', endDate: '', reason: '' });

  protected readonly form = form(
    this.model,
    (path) => {
      required(path.date);
      maxLength(path.reason, 500);
      validate(path.endDate, ({ value, valueOf }) =>
        !value() || value() >= valueOf(path.date) ? undefined : { kind: 'endBeforeStart' },
      );
    },
    { submission: { action: () => this.add() } },
  );

  /** Dates travel as "yyyy-MM-dd" without a time zone, so they are formatted as UTC days. */
  protected longDate(closure: Closure): string {
    return this.format.formatDate(closure.date, 'fullDate', 'UTC');
  }

  protected async remove(closure: Closure): Promise<void> {
    await this.settings.deleteClosure(closure.id);
    this.closures.reload();
    this.toast.show('settings.removed');
  }

  private async add(): Promise<undefined> {
    this.errorCode.set(null);
    const { date, endDate, reason } = this.model();
    try {
      await this.settings.createClosures({
        date,
        endDate: endDate || null,
        reason: reason.trim() || null,
      });
      this.model.set({ date: '', endDate: '', reason: '' });
      this.closures.reload();
      this.toast.show('settings.saved');
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    }
    return undefined;
  }
}
