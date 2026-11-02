import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  resource,
  signal,
  untracked,
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../auth/auth.service';
import { DoneStep } from './done-step';
import { HoursStep } from './hours-step';
import { ONBOARDING_STEPS, OnboardingStepKey } from './onboarding.models';
import { OnboardingService } from './onboarding.service';
import { ProfileStep } from './profile-step';
import { ServicesStep } from './services-step';
import { TeamStep } from './team-step';

/** The screens of the assistant: the four setup steps and the final one. */
const SCREENS = [...ONBOARDING_STEPS, 'done'] as const;
type Screen = (typeof SCREENS)[number];

/**
 * The setup assistant: five steps to a salon customers can book. It can be left at any time ("Later"); what was saved stays,
 * and the next visit starts at the first step that is still open. Which steps are done is read from the data, not remembered.
 */
@Component({
  selector: 'app-onboarding-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DoneStep,
    HoursStep,
    MatIconModule,
    ProfileStep,
    RouterLink,
    ServicesStep,
    TeamStep,
    TranslocoPipe,
  ],
  templateUrl: './onboarding-page.html',
  styleUrl: './onboarding-page.scss',
})
export class OnboardingPage {
  private readonly onboarding = inject(OnboardingService);
  private readonly route = inject(ActivatedRoute);
  protected readonly user = inject(AuthService).currentUser;

  protected readonly screens = SCREENS;
  protected readonly status = resource({ loader: () => this.onboarding.status() });

  /** Nothing is shown until the first open step is known (or the one named in the address, `?step=2`). */
  protected readonly current = signal<Screen | null>(null);
  protected readonly index = computed(() => {
    const current = this.current();
    return current ? SCREENS.indexOf(current) : -1;
  });

  constructor() {
    effect(() => {
      if (this.current() === null && this.status.hasValue()) {
        const status = this.status.value();
        untracked(() => this.current.set(this.startScreen(status.steps)));
      }
    });
  }

  protected isDone(screen: Screen): boolean {
    return (
      screen !== 'done' &&
      this.status.hasValue() &&
      this.status.value().steps.some((step) => step.key === screen && step.done)
    );
  }

  protected open(screen: Screen): void {
    this.current.set(screen);
  }

  /** A step was saved: read the progress again and show the next screen. */
  protected async next(): Promise<void> {
    this.status.reload();
    const index = this.index();
    this.current.set(SCREENS[Math.min(index + 1, SCREENS.length - 1)]);
  }

  protected back(): void {
    this.current.set(SCREENS[Math.max(this.index() - 1, 0)]);
  }

  private startScreen(steps: readonly { key: OnboardingStepKey; done: boolean }[]): Screen {
    const wanted = Number(this.route.snapshot.queryParamMap.get('step'));
    if (Number.isInteger(wanted) && wanted >= 1 && wanted <= SCREENS.length) {
      return SCREENS[wanted - 1];
    }
    return steps.find((step) => !step.done)?.key ?? 'done';
  }
}
