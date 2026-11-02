import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { BookingLinkCard } from '../dashboard/booking-link-card';
import { OnboardingStatus, OnboardingStepKey } from './onboarding.models';

/** Step 5: the salon is bookable. Shows the link and the QR code, and offers a test booking as a customer would make it. */
@Component({
  selector: 'app-onboarding-done-step',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BookingLinkCard, MatButtonModule, MatIconModule, RouterLink, TranslocoPipe],
  styleUrl: './step.scss',
  styles: `
    .actions {
      margin-bottom: 16px;
    }
  `,
  template: `
    @if (status().complete) {
      <h2>{{ 'onboarding.done.title' | transloco }}</h2>
      <p class="lead">{{ 'onboarding.done.lead' | transloco }}</p>
      <div class="actions">
        <a
          mat-flat-button
          class="test-booking"
          [href]="status().bookingUrl"
          target="_blank"
          rel="noopener noreferrer"
        >
          <mat-icon aria-hidden="true">open_in_new</mat-icon>
          {{ 'onboarding.done.testBooking' | transloco }}
          <span class="visually-hidden">{{ 'onboarding.newTab' | transloco }}</span>
        </a>
        <a mat-stroked-button class="dashboard" routerLink="/app/dashboard">
          {{ 'onboarding.toDashboard' | transloco }}
        </a>
      </div>
      <app-booking-link-card [url]="status().bookingUrl" [salonName]="salonName()" />
    } @else {
      <h2>{{ 'onboarding.done.missingTitle' | transloco }}</h2>
      <p class="lead">{{ 'onboarding.done.missingLead' | transloco }}</p>
      <ul class="missing">
        @for (key of missing(); track key) {
          <li>
            <button mat-button type="button" (click)="goTo.emit(key)">
              {{ 'onboarding.steps.' + key | transloco }}
            </button>
          </li>
        }
      </ul>
    }
  `,
})
export class DoneStep {
  readonly status = input.required<OnboardingStatus>();
  readonly salonName = input.required<string>();
  /** Asks the assistant to open one of the steps that is still missing. */
  readonly goTo = output<OnboardingStepKey>();

  protected readonly missing = computed(() =>
    this.status()
      .steps.filter((step) => !step.done)
      .map((step) => step.key),
  );
}
