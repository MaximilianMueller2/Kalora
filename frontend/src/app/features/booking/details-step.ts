import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import {
  FormField,
  FormRoot,
  email,
  form,
  maxLength,
  required,
  validate,
} from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiError } from '../../core/http/api-error';
import { LocaleFormatService } from '../../core/i18n/locale-format.service';
import { fieldErrorText } from '../../shared/forms/field-error';
import { injectErrorMessage } from '../../shared/forms/server-error';
import { BookingService } from './booking.service';
import { StepHeader } from './step-header';

const PHONE_PATTERN = /^[0-9+()./ -]{3,50}$/;
const SLOT_GONE = 'booking.slot_unavailable';

@Component({
  selector: 'app-details-step',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormField,
    FormRoot,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    StepHeader,
    TranslocoPipe,
  ],
  styleUrl: './skeleton.scss',
  styles: `
    .summary {
      display: grid;
      grid-template-columns: auto 1fr;
      gap: 4px 16px;
      margin: 0 0 24px;
      padding: 16px;
      border-radius: var(--mat-sys-corner-large);
      background: var(--mat-sys-surface-container-low);

      dt {
        color: var(--mat-sys-on-surface-variant);
      }

      dd {
        margin: 0;
        font-weight: 500;
      }
    }

    form {
      display: grid;
      gap: 8px;
    }

    mat-form-field {
      width: 100%;
    }

    .trap {
      position: absolute;
      left: -10000px;
      width: 1px;
      height: 1px;
      overflow: hidden;
    }

    .privacy {
      margin: 8px 0 0;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }

    .gone {
      display: grid;
      gap: 12px;
      margin: 0 0 16px;
      padding: 12px 16px;
      border-radius: var(--mat-sys-corner-medium);
      background: var(--mat-sys-tertiary-container);
      color: var(--mat-sys-on-tertiary-container);
    }

    .gone p {
      margin: 0;
    }
  `,
  template: `
    <app-step-header
      [step]="4"
      titleKey="booking.steps.details.title"
      backLink="{{ '/' + booking.slug() + '/time' }}"
    />

    <dl class="summary">
      <dt>{{ 'booking.summary.service' | transloco }}</dt>
      <dd>{{ booking.selectedService()?.name }}</dd>
      <dt>{{ 'booking.summary.person' | transloco }}</dt>
      <dd>
        {{ booking.selectedStaff()?.displayName ?? ('booking.steps.person.anyone' | transloco) }}
      </dd>
      <dt>{{ 'booking.summary.when' | transloco }}</dt>
      <dd>{{ when() }}</dd>
      <dt>{{ 'booking.summary.duration' | transloco }}</dt>
      <dd>{{ duration() }}</dd>
      <dt>{{ 'booking.summary.price' | transloco }}</dt>
      <dd>{{ price() }}</dd>
    </dl>

    @if (slotGone()) {
      <div class="gone" role="alert">
        <p>{{ 'booking.slotGone' | transloco }}</p>
        <button mat-flat-button type="button" (click)="chooseAnotherTime()">
          {{ 'booking.chooseAnotherTime' | transloco }}
        </button>
      </div>
    } @else if (errorMessage(); as message) {
      <p class="alert" role="alert">{{ message }}</p>
    }

    <form [formRoot]="form">
      <mat-form-field appearance="outline">
        <mat-label>{{ 'booking.fields.name' | transloco }}</mat-label>
        <input matInput type="text" autocomplete="name" [formField]="form.name" />
        @if (errorText(form.name().errors()); as error) {
          <mat-error>{{ error.key | transloco: error.params }}</mat-error>
        }
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>{{ 'booking.fields.email' | transloco }}</mat-label>
        <input
          matInput
          type="email"
          autocomplete="email"
          inputmode="email"
          [formField]="form.email"
        />
        <mat-hint>{{ 'booking.fields.emailHint' | transloco }}</mat-hint>
        @if (errorText(form.email().errors()); as error) {
          <mat-error>{{ error.key | transloco: error.params }}</mat-error>
        }
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>{{ 'booking.fields.phone' | transloco }}</mat-label>
        <input matInput type="tel" autocomplete="tel" inputmode="tel" [formField]="form.phone" />
        @if (errorText(form.phone().errors()); as error) {
          <mat-error>{{ error.key | transloco: error.params }}</mat-error>
        }
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>{{ 'booking.fields.note' | transloco }}</mat-label>
        <textarea matInput rows="3" [formField]="form.note"></textarea>
        @if (errorText(form.note().errors()); as error) {
          <mat-error>{{ error.key | transloco: error.params }}</mat-error>
        }
      </mat-form-field>

      <!-- Honeypot: people never see or fill this; bots that fill every field give themselves away. -->
      <div class="trap" aria-hidden="true">
        <label>
          Website
          <input type="text" tabindex="-1" autocomplete="off" [formField]="form.website" />
        </label>
      </div>

      <p class="privacy">
        {{ 'booking.privacy.text' | transloco }}
        @if (privacyUrl(); as url) {
          <a [href]="url" target="_blank" rel="noopener noreferrer">{{
            'booking.privacy.link' | transloco
          }}</a>
        }
      </p>

      <div class="primary-action">
        <button mat-flat-button type="submit" [disabled]="form().submitting()">
          {{ 'booking.submit' | transloco }}
        </button>
      </div>
    </form>
  `,
})
export class DetailsStep {
  protected readonly booking = inject(BookingService);
  private readonly format = inject(LocaleFormatService);
  private readonly router = inject(Router);

  protected readonly errorText = fieldErrorText;
  protected readonly errorCode = signal<string | null>(null);
  protected readonly errorMessage = injectErrorMessage(this.errorCode);
  protected readonly slotGone = computed(() => this.errorCode() === SLOT_GONE);
  protected readonly privacyUrl = computed(() =>
    this.booking.salon.hasValue() ? this.booking.salon.value().privacyPolicyUrl : null,
  );

  private readonly zone = computed(() =>
    this.booking.salon.hasValue() ? this.booking.salon.value().timeZone : 'UTC',
  );

  protected readonly when = computed(() => {
    const slot = this.booking.selectedSlot();
    return slot
      ? `${this.format.formatDate(slot.startUtc, 'fullDate', this.zone())}, ${slot.time}`
      : '';
  });
  protected readonly duration = computed(() => {
    const service = this.booking.selectedService();
    return service ? this.format.formatDuration(service.durationMinutes) : '';
  });
  protected readonly price = computed(() => {
    const service = this.booking.selectedService();
    return service ? this.format.formatMoney(service.priceCents, service.currency) : '';
  });

  protected readonly form = form(
    this.booking.contact,
    (path) => {
      required(path.name);
      maxLength(path.name, 100);
      required(path.email);
      email(path.email);
      validate(path.phone, ({ value }) =>
        value().trim() === '' || PHONE_PATTERN.test(value().trim())
          ? undefined
          : { kind: 'phoneInvalid' },
      );
      maxLength(path.note, 1000);
    },
    { submission: { action: () => this.submit() } },
  );

  protected chooseAnotherTime(): void {
    this.errorCode.set(null);
    this.booking.clearTime();
    void this.router.navigate(['/', this.booking.slug(), 'time']);
  }

  private async submit(): Promise<undefined> {
    this.errorCode.set(null);
    try {
      await this.booking.submit();
      await this.router.navigate(['/', this.booking.slug(), 'done']);
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    }
    return undefined;
  }
}
