import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  resource,
  signal,
  untracked,
} from '@angular/core';
import { FormField, FormRoot, form, validate } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiError } from '../../../core/http/api-error';
import { Toast } from '../../../core/ui/toast';
import { fieldErrorText } from '../../../shared/forms/field-error';
import { injectErrorMessage } from '../../../shared/forms/server-error';
import { BookingRules, SLOT_INTERVALS } from '../settings.models';
import { SettingsService } from '../settings.service';

/** Allowed values, the same limits the API enforces (see BookingRulesValidator). */
export const RULE_LIMITS = {
  minLeadTimeMinutes: { min: 0, max: 7 * 24 * 60 },
  bookingHorizonDays: { min: 1, max: 365 },
  cancellationDeadlineHours: { min: 0, max: 7 * 24 },
  requestExpiryHours: { min: 1, max: 72 },
  retentionMonths: { min: 1, max: 120 },
} as const;

type NumericRule = keyof typeof RULE_LIMITS;

// Numbers are edited as text (an input can be empty while typing) and converted when saving.
interface RulesModel {
  slotIntervalMinutes: string;
  autoConfirm: boolean;
  minLeadTimeMinutes: string;
  bookingHorizonDays: string;
  cancellationDeadlineHours: string;
  requestExpiryHours: string;
  retentionMonths: string;
}

const toModel = (rules: BookingRules): RulesModel => ({
  slotIntervalMinutes: String(rules.slotIntervalMinutes),
  autoConfirm: rules.autoConfirm,
  minLeadTimeMinutes: String(rules.minLeadTimeMinutes),
  bookingHorizonDays: String(rules.bookingHorizonDays),
  cancellationDeadlineHours: String(rules.cancellationDeadlineHours),
  requestExpiryHours: String(rules.requestExpiryHours),
  retentionMonths: String(rules.retentionMonths),
});

const toRules = (model: RulesModel): BookingRules => ({
  slotIntervalMinutes: Number(model.slotIntervalMinutes),
  autoConfirm: model.autoConfirm,
  minLeadTimeMinutes: Number(model.minLeadTimeMinutes),
  bookingHorizonDays: Number(model.bookingHorizonDays),
  cancellationDeadlineHours: Number(model.cancellationDeadlineHours),
  requestExpiryHours: Number(model.requestExpiryHours),
  retentionMonths: Number(model.retentionMonths),
});

export function isWholeNumberInRange(text: string, min: number, max: number): boolean {
  const value = Number(text);
  return text.trim() !== '' && Number.isInteger(value) && value >= min && value <= max;
}

@Component({
  selector: 'app-rules-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormField,
    FormRoot,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    TranslocoPipe,
  ],
  styleUrl: '../settings-tabs.scss',
  template: `
    <h2>{{ 'settings.booking.title' | transloco }}</h2>
    <p class="lead">{{ 'settings.booking.lead' | transloco }}</p>

    @if (rules.isLoading()) {
      <p role="status">{{ 'common.loading' | transloco }}</p>
    } @else if (rules.error()) {
      <p class="alert" role="alert">{{ 'errors.unknown' | transloco }}</p>
    } @else {
      @if (errorMessage(); as message) {
        <p class="alert" role="alert">{{ message }}</p>
      }

      <form [formRoot]="form">
        <mat-form-field appearance="outline">
          <mat-label>{{ 'settings.booking.slotInterval' | transloco }}</mat-label>
          <select matNativeControl [formField]="form.slotIntervalMinutes">
            @for (minutes of slotIntervals; track minutes) {
              <option [value]="minutes">
                {{ 'settings.booking.minutes' | transloco: { count: minutes } }}
              </option>
            }
          </select>
          <mat-hint>{{ 'settings.booking.slotIntervalHint' | transloco }}</mat-hint>
        </mat-form-field>

        <mat-checkbox [formField]="form.autoConfirm">{{
          'settings.booking.autoConfirm' | transloco
        }}</mat-checkbox>
        <p class="hint">{{ 'settings.booking.autoConfirmHint' | transloco }}</p>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'settings.booking.minLead' | transloco }}</mat-label>
          <input matInput type="number" inputmode="numeric" [formField]="form.minLeadTimeMinutes" />
          <mat-hint>{{
            'settings.booking.minLeadHint' | transloco: limits.minLeadTimeMinutes
          }}</mat-hint>
          @if (errorText(form.minLeadTimeMinutes().errors()); as error) {
            <mat-error>{{ error.key | transloco: error.params }}</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'settings.booking.horizon' | transloco }}</mat-label>
          <input matInput type="number" inputmode="numeric" [formField]="form.bookingHorizonDays" />
          <mat-hint>{{
            'settings.booking.horizonHint' | transloco: limits.bookingHorizonDays
          }}</mat-hint>
          @if (errorText(form.bookingHorizonDays().errors()); as error) {
            <mat-error>{{ error.key | transloco: error.params }}</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'settings.booking.cancellation' | transloco }}</mat-label>
          <input
            matInput
            type="number"
            inputmode="numeric"
            [formField]="form.cancellationDeadlineHours"
          />
          <mat-hint>{{
            'settings.booking.cancellationHint' | transloco: limits.cancellationDeadlineHours
          }}</mat-hint>
          @if (errorText(form.cancellationDeadlineHours().errors()); as error) {
            <mat-error>{{ error.key | transloco: error.params }}</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'settings.booking.requestExpiry' | transloco }}</mat-label>
          <input matInput type="number" inputmode="numeric" [formField]="form.requestExpiryHours" />
          <mat-hint>{{
            'settings.booking.requestExpiryHint' | transloco: limits.requestExpiryHours
          }}</mat-hint>
          @if (errorText(form.requestExpiryHours().errors()); as error) {
            <mat-error>{{ error.key | transloco: error.params }}</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'settings.booking.retention' | transloco }}</mat-label>
          <input matInput type="number" inputmode="numeric" [formField]="form.retentionMonths" />
          <mat-hint>{{
            'settings.booking.retentionHint' | transloco: limits.retentionMonths
          }}</mat-hint>
          @if (errorText(form.retentionMonths().errors()); as error) {
            <mat-error>{{ error.key | transloco: error.params }}</mat-error>
          }
        </mat-form-field>

        <div class="actions">
          <button mat-flat-button type="submit" [disabled]="form().submitting()">
            {{ 'settings.save' | transloco }}
          </button>
        </div>
      </form>
    }
  `,
})
export class RulesTab {
  private readonly settings = inject(SettingsService);
  private readonly toast = inject(Toast);

  protected readonly slotIntervals = SLOT_INTERVALS;
  protected readonly limits = RULE_LIMITS;
  protected readonly errorText = fieldErrorText;

  protected readonly rules = resource({ loader: () => this.settings.getBookingRules() });
  protected readonly errorCode = signal<string | null>(null);
  protected readonly errorMessage = injectErrorMessage(this.errorCode);

  private readonly model = signal<RulesModel>({
    slotIntervalMinutes: '15',
    autoConfirm: false,
    minLeadTimeMinutes: '120',
    bookingHorizonDays: '60',
    cancellationDeadlineHours: '24',
    requestExpiryHours: '24',
    retentionMonths: '24',
  });

  protected readonly form = form(
    this.model,
    (path) => {
      for (const rule of Object.keys(RULE_LIMITS) as NumericRule[]) {
        const { min, max } = RULE_LIMITS[rule];
        validate(path[rule], ({ value }) =>
          isWholeNumberInRange(value(), min, max) ? undefined : { kind: 'range' },
        );
      }
    },
    { submission: { action: () => this.save() } },
  );

  constructor() {
    effect(() => {
      const loaded = this.rules.value();
      if (loaded) {
        untracked(() => this.model.set(toModel(loaded)));
      }
    });
  }

  private async save(): Promise<undefined> {
    this.errorCode.set(null);
    try {
      this.model.set(toModel(await this.settings.updateBookingRules(toRules(this.model()))));
      this.toast.show('settings.saved');
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    }
    return undefined;
  }
}
