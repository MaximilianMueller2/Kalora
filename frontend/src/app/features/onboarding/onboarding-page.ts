import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../auth/auth.service';

/** Placeholder: the setup assistant (salon, opening hours, services, team) follows in a later step. */
@Component({
  selector: 'app-onboarding-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslocoPipe],
  template: `
    <h1>{{ 'onboarding.title' | transloco: { name: user()?.displayName } }}</h1>
    <p>{{ 'onboarding.placeholder' | transloco: { salon: user()?.salon?.name } }}</p>
    <a routerLink="/app/dashboard">{{ 'onboarding.toDashboard' | transloco }}</a>
  `,
  styles: `
    h1 {
      margin: 0 0 8px;
      font: var(--mat-sys-headline-medium);
    }

    p {
      margin: 0 0 16px;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class OnboardingPage {
  protected readonly user = inject(AuthService).currentUser;
}
