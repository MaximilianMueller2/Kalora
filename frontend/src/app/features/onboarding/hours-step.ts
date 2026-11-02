import {
  ChangeDetectionStrategy,
  Component,
  computed,
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
import { WeekScheduleEditor } from '../../shared/week-schedule/week-schedule-editor';
import {
  DEFAULT_BLOCK,
  WEEKDAYS,
  WeekSchedule,
  emptySchedule,
  scheduleProblems,
} from '../../shared/week-schedule/week-schedule.model';
import { SettingsService } from '../settings/settings.service';

/** What most salons have: closed on Sunday and Monday, open from 9 to 18 on the other days. */
export function suggestedHours(): WeekSchedule {
  const schedule = emptySchedule();
  for (const day of ['Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'] as const) {
    schedule[day] = [{ ...DEFAULT_BLOCK }];
  }
  return schedule;
}

/** Step 2: the opening hours. A salon that has none yet gets a suggestion to adjust instead of an empty week. */
@Component({
  selector: 'app-onboarding-hours-step',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, TranslocoPipe, WeekScheduleEditor],
  styleUrl: './step.scss',
  template: `
    <h2>{{ 'onboarding.hours.title' | transloco }}</h2>
    <p class="lead">
      {{ (suggested() ? 'onboarding.hours.suggested' : 'onboarding.hours.lead') | transloco }}
    </p>

    @if (hours.hasValue()) {
      @if (message(); as text) {
        <p class="alert" role="alert">{{ text }}</p>
      }
      <app-week-schedule-editor [(schedule)]="schedule" />
      <div class="actions">
        <button
          mat-flat-button
          type="button"
          [disabled]="saving() || hasProblems() || isEmpty()"
          (click)="save()"
        >
          {{ 'onboarding.next' | transloco }}
        </button>
      </div>
    } @else if (hours.error()) {
      <p class="alert" role="alert">{{ 'errors.unknown' | transloco }}</p>
    } @else {
      <div class="skeleton" aria-busy="true" aria-hidden="true"></div>
    }
  `,
})
export class HoursStep {
  private readonly settings = inject(SettingsService);

  readonly saved = output<void>();

  protected readonly hours = resource({ loader: () => this.settings.getOpeningHours() });
  protected readonly schedule = signal<WeekSchedule>(emptySchedule());
  /** The week shown is our suggestion, not something the salon has saved. */
  protected readonly suggested = signal(false);
  protected readonly saving = signal(false);
  private readonly errorCode = signal<string | null>(null);
  protected readonly message = injectErrorMessage(this.errorCode);
  protected readonly hasProblems = computed(
    () => Object.keys(scheduleProblems(this.schedule())).length > 0,
  );
  protected readonly isEmpty = computed(() =>
    WEEKDAYS.every((day) => this.schedule()[day].length === 0),
  );

  constructor() {
    effect(() => {
      if (this.hours.hasValue()) {
        const saved = this.hours.value();
        untracked(() => {
          const empty = WEEKDAYS.every((day) => saved[day].length === 0);
          this.suggested.set(empty);
          this.schedule.set(empty ? suggestedHours() : saved);
        });
      }
    });
  }

  protected async save(): Promise<void> {
    this.saving.set(true);
    this.errorCode.set(null);
    try {
      await this.settings.updateOpeningHours(this.schedule());
      this.saved.emit();
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    } finally {
      this.saving.set(false);
    }
  }
}
