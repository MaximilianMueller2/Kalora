import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  output,
  resource,
  signal,
  untracked,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiError } from '../../core/http/api-error';
import { injectErrorMessage } from '../../shared/forms/server-error';
import { SALON_INDUSTRIES, SalonIndustry } from '../auth/auth.models';
import { SettingsService } from '../settings/settings.service';

const PHONE_PATTERN = /^[0-9+()./ -]{3,50}$/;

/** Step 1: industry, address and phone number, which the booking page shows and the salon needs to be reached. */
@Component({
  selector: 'app-onboarding-profile-step',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, TranslocoPipe],
  styleUrl: './step.scss',
  template: `
    <h2>{{ 'onboarding.profile.title' | transloco }}</h2>
    <p class="lead">{{ 'onboarding.profile.lead' | transloco }}</p>

    @if (profile.hasValue()) {
      @if (message(); as text) {
        <p class="alert" role="alert">{{ text }}</p>
      }
      <form class="form" (submit)="$event.preventDefault(); save()">
        <label class="field">
          <span>{{ 'onboarding.profile.industry' | transloco }}</span>
          <select name="industry" (change)="pickIndustry($event)">
            @for (option of industries; track option) {
              <option [value]="option" [selected]="industry() === option">
                {{ 'auth.register.industries.' + option | transloco }}
              </option>
            }
          </select>
        </label>
        <label class="field">
          <span>{{ 'onboarding.profile.street' | transloco }}</span>
          <input
            name="street"
            type="text"
            autocomplete="address-line1"
            [value]="street()"
            (input)="street.set(value($event))"
          />
        </label>
        <div class="row">
          <label class="field">
            <span>{{ 'onboarding.profile.postalCode' | transloco }}</span>
            <input
              name="postalCode"
              type="text"
              autocomplete="postal-code"
              [value]="postalCode()"
              (input)="postalCode.set(value($event))"
            />
          </label>
          <label class="field">
            <span>{{ 'onboarding.profile.city' | transloco }}</span>
            <input
              name="city"
              type="text"
              autocomplete="address-level2"
              [value]="city()"
              (input)="city.set(value($event))"
            />
          </label>
        </div>
        <label class="field">
          <span>{{ 'onboarding.profile.phone' | transloco }}</span>
          <input
            name="phone"
            type="tel"
            autocomplete="tel"
            [value]="phone()"
            (input)="phone.set(value($event))"
          />
        </label>
        <div class="actions">
          <button mat-flat-button type="submit" [disabled]="saving()">
            {{ 'onboarding.next' | transloco }}
          </button>
        </div>
      </form>
    } @else if (profile.error()) {
      <p class="alert" role="alert">{{ 'errors.unknown' | transloco }}</p>
    } @else {
      <div class="skeleton" aria-busy="true" aria-hidden="true"></div>
    }
  `,
})
export class ProfileStep {
  private readonly settings = inject(SettingsService);

  /** The data is saved; the assistant goes on. */
  readonly saved = output<void>();

  protected readonly industries = SALON_INDUSTRIES;
  protected readonly profile = resource({ loader: () => this.settings.getProfile() });

  protected readonly industry = signal<SalonIndustry>('Hair');
  protected readonly street = signal('');
  protected readonly postalCode = signal('');
  protected readonly city = signal('');
  protected readonly phone = signal('');
  protected readonly saving = signal(false);
  private readonly errorCode = signal<string | null>(null);
  protected readonly message = injectErrorMessage(this.errorCode);

  constructor() {
    effect(() => {
      if (this.profile.hasValue()) {
        const value = this.profile.value();
        untracked(() => {
          this.industry.set(value.industry);
          this.street.set(value.street);
          this.postalCode.set(value.postalCode);
          this.city.set(value.city);
          this.phone.set(value.phone);
        });
      }
    });
  }

  protected pickIndustry(event: Event): void {
    this.industry.set(this.value(event) as SalonIndustry);
  }

  protected value(event: Event): string {
    return (event.target as HTMLInputElement | HTMLSelectElement).value;
  }

  protected async save(): Promise<void> {
    this.errorCode.set(null);
    const filled = [this.street(), this.postalCode(), this.city(), this.phone()].every(
      (text) => text.trim().length > 0,
    );
    if (!filled) {
      this.errorCode.set('validation.required');
      return;
    }
    if (!PHONE_PATTERN.test(this.phone().trim())) {
      this.errorCode.set('validation.invalid');
      return;
    }

    const current = this.profile.value();
    if (!current) {
      return;
    }

    this.saving.set(true);
    try {
      await this.settings.updateProfile({
        ...current,
        industry: this.industry(),
        street: this.street().trim(),
        postalCode: this.postalCode().trim(),
        city: this.city().trim(),
        phone: this.phone().trim(),
      });
      this.saved.emit();
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    } finally {
      this.saving.set(false);
    }
  }
}
