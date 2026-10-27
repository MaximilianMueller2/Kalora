import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { LocaleFormatService } from '../../core/i18n/locale-format.service';
import { WEEKDAYS } from '../../shared/week-schedule/week-schedule.model';
import { addMonths, monthGrid } from './booking-calendar';

/** How far ahead the visitor can page; the salon's booking horizon is shorter and shows as days without free times. */
const MAX_MONTHS_AHEAD = 12;

/**
 * A month of days as buttons. Days that are not in `availableDays` are disabled. `loading` shows a placeholder
 * instead of days while the free days of the month are fetched.
 */
@Component({
  selector: 'app-month-calendar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, TranslocoPipe],
  template: `
    <div class="head">
      <button
        type="button"
        class="nav"
        [disabled]="!canGoBack()"
        [attr.aria-label]="'booking.calendar.previous' | transloco"
        (click)="monthChange.emit(previousMonth())"
      >
        <mat-icon aria-hidden="true">chevron_left</mat-icon>
      </button>
      <h3 aria-live="polite">{{ title() }}</h3>
      <button
        type="button"
        class="nav"
        [disabled]="!canGoForward()"
        [attr.aria-label]="'booking.calendar.next' | transloco"
        (click)="monthChange.emit(nextMonth())"
      >
        <mat-icon aria-hidden="true">chevron_right</mat-icon>
      </button>
    </div>

    <div class="grid" role="grid" [attr.aria-label]="title()" [attr.aria-busy]="loading()">
      <div class="row weekdays" role="row">
        @for (name of weekdayNames(); track $index) {
          <span role="columnheader" class="weekday">{{ name }}</span>
        }
      </div>
      @for (week of weeks(); track $index) {
        <div class="row" role="row">
          @for (cell of week; track $index) {
            <span role="gridcell" class="cell">
              @if (cell; as day) {
                <button
                  type="button"
                  class="day"
                  [class.skeleton]="loading()"
                  [class.today]="day.date === today()"
                  [disabled]="loading() || !availableDays().has(day.date)"
                  [attr.aria-pressed]="day.date === selected()"
                  [attr.aria-label]="label(day.date)"
                  (click)="dayPick.emit(day.date)"
                >
                  {{ day.day }}
                </button>
              }
            </span>
          }
        </div>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 8px;

      h3 {
        margin: 0;
        font: var(--mat-sys-title-medium);
        text-transform: capitalize;
      }
    }

    .nav {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 48px;
      height: 48px;
      border: 0;
      border-radius: 50%;
      background: none;
      color: var(--mat-sys-on-surface);
      cursor: pointer;

      &:disabled {
        color: var(--mat-sys-outline);
        cursor: default;
      }

      &:focus-visible {
        outline: 2px solid var(--mat-sys-primary);
      }
    }

    .row {
      display: grid;
      grid-template-columns: repeat(7, 1fr);
      gap: 2px;
    }

    .weekday {
      padding: 4px 0;
      text-align: center;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-label-medium);
    }

    .cell {
      display: flex;
      justify-content: center;
    }

    .day {
      width: 100%;
      max-width: 52px;
      aspect-ratio: 1;
      min-height: 44px;
      border: 1px solid transparent;
      border-radius: 50%;
      background: none;
      color: var(--mat-sys-on-surface);
      font: var(--mat-sys-body-large);
      cursor: pointer;

      &:disabled {
        color: var(--mat-sys-outline);
        text-decoration: line-through;
        text-decoration-thickness: 1px;
        cursor: default;
      }

      &:not(:disabled) {
        background: var(--mat-sys-primary-container);
        color: var(--mat-sys-on-primary-container);
        font-weight: 600;
      }

      &.today {
        border-color: var(--mat-sys-outline);
      }

      &[aria-pressed='true'] {
        background: var(--mat-sys-primary);
        color: var(--mat-sys-on-primary);
      }

      &:focus-visible {
        outline: 2px solid var(--mat-sys-primary);
        outline-offset: 2px;
      }

      &.skeleton {
        text-decoration: none;
        color: transparent;
      }
    }
  `,
})
export class MonthCalendar {
  /** "yyyy-MM". */
  readonly month = input.required<string>();
  readonly availableDays = input<ReadonlySet<string>>(new Set());
  readonly selected = input<string | null>(null);
  /** The salon's current month; one cannot page before it. */
  readonly currentMonth = input.required<string>();
  /** Today ("yyyy-MM-dd") in the salon's time zone. */
  readonly today = input<string>('');
  readonly loading = input(false);

  readonly monthChange = output<string>();
  readonly dayPick = output<string>();

  private readonly format = inject(LocaleFormatService);

  protected readonly weeks = computed(() => monthGrid(this.month()));
  protected readonly previousMonth = computed(() => addMonths(this.month(), -1));
  protected readonly nextMonth = computed(() => addMonths(this.month(), 1));
  protected readonly canGoBack = computed(() => this.previousMonth() >= this.currentMonth());
  protected readonly canGoForward = computed(
    () => this.nextMonth() <= addMonths(this.currentMonth(), MAX_MONTHS_AHEAD),
  );
  protected readonly title = computed(() =>
    this.format.formatDate(`${this.month()}-01`, 'monthYear', 'UTC'),
  );
  protected readonly weekdayNames = computed(() =>
    WEEKDAYS.map((day) => this.format.weekdayName(day, 'short')),
  );

  protected label(date: string): string {
    return this.format.formatDate(date, 'fullDate', 'UTC');
  }
}
