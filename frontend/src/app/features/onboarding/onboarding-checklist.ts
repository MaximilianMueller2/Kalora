import { ChangeDetectionStrategy, Component, computed, inject, resource } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../auth/auth.service';
import { ONBOARDING_STEPS } from './onboarding.models';
import { OnboardingService } from './onboarding.service';

/** Shown on the dashboard of owners and managers until the salon can be booked: what is still open, each leading to its step. */
@Component({
  selector: 'app-onboarding-checklist',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, RouterLink, TranslocoPipe],
  styles: `
    :host {
      display: block;
    }

    .card {
      margin: 0 0 16px;
      padding: 16px 20px;
      border: 1px solid var(--mat-sys-primary);
      border-radius: var(--mat-sys-corner-large);
      background: var(--mat-sys-surface-container-low);
    }

    h2 {
      margin: 0 0 4px;
      font: var(--mat-sys-title-medium);
    }

    p {
      margin: 0 0 12px;
      color: var(--mat-sys-on-surface-variant);
    }

    ul {
      display: grid;
      gap: 4px;
      margin: 0 0 12px;
      padding: 0;
      list-style: none;
    }

    li a,
    li span.item {
      display: flex;
      align-items: center;
      gap: 8px;
      min-height: 44px;
      color: inherit;
      text-decoration: none;
    }

    li a:hover {
      text-decoration: underline;
    }

    li a:focus-visible {
      outline: 3px solid var(--mat-sys-primary);
      outline-offset: 2px;
    }

    li.done {
      color: var(--mat-sys-on-surface-variant);
    }

    mat-icon {
      flex: none;
    }

    li.done mat-icon {
      color: var(--mat-sys-primary);
    }

    .continue {
      display: inline-flex;
      align-items: center;
      min-height: 44px;
      font: var(--mat-sys-label-large);
    }
  `,
  template: `
    @if (open() > 0) {
      <section class="card" [attr.aria-label]="'onboarding.checklist.title' | transloco">
        <h2>{{ 'onboarding.checklist.title' | transloco }}</h2>
        <p>{{ 'onboarding.checklist.lead' | transloco: { count: open() } }}</p>
        <ul>
          @for (step of steps(); track step.key) {
            <li [class.done]="step.done">
              @if (step.done) {
                <span class="item">
                  <mat-icon aria-hidden="true">check_circle</mat-icon>
                  {{ 'onboarding.steps.' + step.key | transloco }}
                  <span class="visually-hidden">{{ 'onboarding.stepDone' | transloco }}</span>
                </span>
              } @else {
                <a routerLink="/app/onboarding" [queryParams]="{ step: step.position }">
                  <mat-icon aria-hidden="true">radio_button_unchecked</mat-icon>
                  {{ 'onboarding.steps.' + step.key | transloco }}
                </a>
              }
            </li>
          }
        </ul>
        <a class="continue" routerLink="/app/onboarding">{{
          'onboarding.checklist.resume' | transloco
        }}</a>
      </section>
    }
  `,
})
export class OnboardingChecklist {
  private readonly onboarding = inject(OnboardingService);
  private readonly auth = inject(AuthService);

  /** Staff members do not set the salon up and are not allowed to ask. */
  private readonly status = resource({
    params: () => (this.auth.hasAnyRole(['Owner', 'Manager']) ? true : undefined),
    loader: () => this.onboarding.status(),
  });

  protected readonly steps = computed(() =>
    this.status.hasValue()
      ? this.status.value().steps.map((step) => ({
          ...step,
          position: ONBOARDING_STEPS.indexOf(step.key) + 1,
        }))
      : [],
  );
  protected readonly open = computed(() =>
    this.status.hasValue() && !this.status.value().complete
      ? this.steps().filter((step) => !step.done).length
      : 0,
  );
}
