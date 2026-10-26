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
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiError } from '../../core/http/api-error';
import { LocaleFormatService } from '../../core/i18n/locale-format.service';
import { Toast } from '../../core/ui/toast';
import { todayInZone } from '../../shared/dates/today';
import { injectErrorMessage } from '../../shared/forms/server-error';
import { WeekScheduleEditor } from '../../shared/week-schedule/week-schedule-editor';
import {
  WEEKDAYS,
  WeekSchedule,
  emptySchedule,
  scheduleProblems,
} from '../../shared/week-schedule/week-schedule.model';
import { AuthService } from '../auth/auth.service';
import { injectStaffId } from './staff-id';
import { ScheduleVersion } from './team.models';
import { TeamService } from './team.service';

const FALLBACK_ZONE = 'UTC';

export const toWeekSchedule = (version: ScheduleVersion): WeekSchedule => {
  const schedule = emptySchedule();
  for (const day of version.days) {
    schedule[day.weekday] = day.blocks.map((block) => ({ start: block.start, end: block.end }));
  }
  return schedule;
};

export const toDays = (schedule: WeekSchedule) =>
  WEEKDAYS.map((weekday) => ({ weekday, blocks: schedule[weekday] }));

/** The version in effect on `today`: the latest one that has started. */
export function versionInEffect(
  versions: readonly ScheduleVersion[],
  today: string,
): ScheduleVersion | undefined {
  return versions.filter((v) => v.validFrom <= today).at(-1);
}

@Component({
  selector: 'app-staff-hours-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatFormFieldModule, MatInputModule, TranslocoPipe, WeekScheduleEditor],
  styleUrl: '../../shared/forms/panel-form.scss',
  styles: `
    .versions {
      margin: 0 0 24px;
      padding: 0;
      list-style: none;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--mat-sys-corner-large);
    }

    .versions li {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 4px 12px;
      padding: 8px 8px 8px 16px;
    }

    .versions li + li {
      border-top: 1px solid var(--mat-sys-outline-variant);
    }

    .versions .what {
      flex: 1;
      min-width: 220px;
    }

    .versions .when {
      font-weight: 500;
    }

    .versions .summary {
      display: block;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }

    .versions button {
      min-height: 44px;
    }

    .from {
      max-width: 360px;
      margin-bottom: 16px;
    }
  `,
  template: `
    <h2>{{ 'team.hours.title' | transloco }}</h2>
    <p class="lead">{{ 'team.hours.lead' | transloco }}</p>

    @if (schedule.hasValue()) {
      @if (versions().length > 0) {
        <ul class="versions">
          @for (version of versions(); track version.validFrom) {
            <li>
              <span class="what">
                <span class="when">
                  @if (version.validFrom <= today()) {
                    {{ 'team.hours.currentPlan' | transloco }}
                  } @else {
                    {{ 'team.hours.fromDate' | transloco: { date: date(version.validFrom) } }}
                  }
                  @if (version.validTo) {
                    · {{ 'team.hours.until' | transloco: { date: date(version.validTo) } }}
                  }
                </span>
                <span class="summary">{{ summary(version) }}</span>
              </span>
              <button mat-button type="button" (click)="edit(version)">
                {{ 'team.hours.edit' | transloco }}
              </button>
            </li>
          }
        </ul>
      } @else {
        <p class="hint">{{ 'team.hours.none' | transloco }}</p>
      }

      @if (errorMessage(); as message) {
        <p class="alert" role="alert">{{ message }}</p>
      }

      <mat-form-field appearance="outline" class="from">
        <mat-label>{{ 'team.hours.validFrom' | transloco }}</mat-label>
        <input
          matInput
          type="date"
          [min]="today()"
          [value]="validFrom()"
          (input)="setDate($any($event.target).value)"
        />
        <mat-hint>{{ 'team.hours.validFromHint' | transloco }}</mat-hint>
      </mat-form-field>

      <app-week-schedule-editor [(schedule)]="editor" />

      @if (isEmpty()) {
        <p class="hint">{{ 'team.hours.emptyHint' | transloco }}</p>
      }
      <div class="actions">
        <button
          mat-flat-button
          type="button"
          [disabled]="saving() || hasProblems() || isEmpty() || !validFrom()"
          (click)="save()"
        >
          {{ 'team.save' | transloco }}
        </button>
      </div>
    } @else if (schedule.error()) {
      <p class="alert" role="alert">{{ 'errors.unknown' | transloco }}</p>
    } @else {
      <p role="status">{{ 'common.loading' | transloco }}</p>
    }
  `,
})
export class HoursTab {
  private readonly team = inject(TeamService);
  private readonly toast = inject(Toast);
  private readonly format = inject(LocaleFormatService);
  private readonly auth = inject(AuthService);
  private readonly staffId = injectStaffId();

  protected readonly schedule = resource({
    params: () => this.staffId(),
    loader: ({ params }) => this.team.getSchedule(params),
  });
  protected readonly editor = signal<WeekSchedule>(emptySchedule());
  protected readonly validFrom = signal('');
  protected readonly saving = signal(false);
  protected readonly errorCode = signal<string | null>(null);
  protected readonly errorMessage = injectErrorMessage(this.errorCode);

  protected readonly today = computed(() =>
    todayInZone(this.auth.currentUser()?.salon?.timeZone ?? FALLBACK_ZONE),
  );
  protected readonly versions = computed(() =>
    this.schedule.hasValue() ? this.schedule.value().versions : [],
  );
  protected readonly hasProblems = computed(
    () => Object.keys(scheduleProblems(this.editor())).length > 0,
  );
  protected readonly isEmpty = computed(() => WEEKDAYS.every((d) => this.editor()[d].length === 0));

  constructor() {
    effect(() => {
      if (this.schedule.hasValue()) {
        const current = versionInEffect(this.schedule.value().versions, this.today());
        untracked(() => {
          this.editor.set(current ? toWeekSchedule(current) : emptySchedule());
          this.validFrom.set(this.today());
        });
      }
    });
  }

  protected date(isoDate: string): string {
    // Dates travel as "yyyy-MM-dd" without a time zone, so they are formatted as UTC days.
    return this.format.formatDate(isoDate, 'fullDate', 'UTC');
  }

  protected summary(version: ScheduleVersion): string {
    return version.days
      .filter((day) => day.blocks.length > 0)
      .map(
        (day) =>
          `${this.format.weekdayName(day.weekday, 'short')} ${day.blocks
            .map((b) => `${b.start}–${b.end}`)
            .join(', ')}`,
      )
      .join(' · ');
  }

  protected setDate(value: string): void {
    this.validFrom.set(value);
  }

  /** Loads a plan into the editor; a plan that already started can only be replaced from today on. */
  protected edit(version: ScheduleVersion): void {
    this.editor.set(toWeekSchedule(version));
    this.validFrom.set(version.validFrom > this.today() ? version.validFrom : this.today());
  }

  protected async save(): Promise<void> {
    this.errorCode.set(null);
    this.saving.set(true);
    try {
      const saved = await this.team.saveSchedule(this.staffId(), {
        validFrom: this.validFrom(),
        days: toDays(this.editor()),
      });
      this.schedule.set(saved);
      this.toast.show('team.saved');
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    } finally {
      this.saving.set(false);
    }
  }
}
