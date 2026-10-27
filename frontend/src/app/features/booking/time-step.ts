import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  untracked,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { todayInZone } from '../../shared/dates/today';
import { groupByDayPart, monthOf } from './booking-calendar';
import { BookingService } from './booking.service';
import { MonthCalendar } from './month-calendar';
import { StepHeader } from './step-header';

@Component({
  selector: 'app-time-step',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MonthCalendar, StepHeader, TranslocoPipe],
  styleUrl: './skeleton.scss',
  styles: `
    h3.part {
      margin: 16px 0 8px;
      font: var(--mat-sys-title-small);
      color: var(--mat-sys-on-surface-variant);
    }

    .chips {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .chip {
      min-width: 72px;
      min-height: 44px;
      padding: 0 16px;
      border: 1px solid var(--mat-sys-outline);
      border-radius: 22px;
      background: var(--mat-sys-surface);
      color: var(--mat-sys-on-surface);
      font: var(--mat-sys-label-large);
      cursor: pointer;

      &[aria-pressed='true'] {
        border-color: var(--mat-sys-primary);
        background: var(--mat-sys-primary);
        color: var(--mat-sys-on-primary);
      }

      &:focus-visible {
        outline: 2px solid var(--mat-sys-primary);
        outline-offset: 2px;
      }
    }

    .times {
      margin-top: 8px;
      min-height: 96px;
    }

    .empty,
    .hint {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-medium);
    }

    .skeleton.chip-skeleton {
      display: inline-block;
      width: 72px;
      height: 44px;
      margin: 0 8px 8px 0;
      border-radius: 22px;
    }
  `,
  template: `
    <app-step-header
      [step]="3"
      titleKey="booking.steps.time.title"
      backLink="{{ '/' + booking.slug() + '/person' }}"
    />

    @if (booking.days.error()) {
      <p class="alert" role="alert">{{ 'errors.unknown' | transloco }}</p>
      <button mat-stroked-button type="button" (click)="booking.days.reload()">
        {{ 'booking.retry' | transloco }}
      </button>
    } @else if (booking.month()) {
      <app-month-calendar
        [month]="booking.month()"
        [currentMonth]="currentMonth()"
        [today]="today()"
        [availableDays]="availableDays()"
        [selected]="booking.selectedDate()"
        [loading]="booking.days.isLoading() || !booking.days.hasValue()"
        (monthChange)="booking.month.set($event)"
        (dayPick)="booking.selectDate($event)"
      />
      @if (booking.days.hasValue() && !booking.days.isLoading() && availableDays().size === 0) {
        <p class="empty" role="status">{{ 'booking.steps.time.noDays' | transloco }}</p>
      }
    }

    <div class="times" aria-live="polite">
      @if (booking.selectedDate()) {
        @if (booking.slots.hasValue() && !booking.slots.isLoading()) {
          @for (group of groups(); track group.part) {
            <h3 class="part">{{ 'booking.steps.time.' + group.part | transloco }}</h3>
            <ul class="chips">
              @for (slot of group.slots; track slot.startUtc) {
                <li>
                  <button
                    type="button"
                    class="chip"
                    [attr.aria-pressed]="booking.selectedSlot()?.startUtc === slot.startUtc"
                    (click)="booking.selectSlot(slot)"
                  >
                    {{ slot.time }}
                  </button>
                </li>
              }
            </ul>
          } @empty {
            <p class="empty">{{ 'booking.steps.time.noTimes' | transloco }}</p>
          }
        } @else if (booking.slots.error()) {
          <p class="alert" role="alert">{{ 'errors.unknown' | transloco }}</p>
        } @else {
          <div aria-busy="true" role="status">
            <span class="visually-hidden">{{ 'common.loading' | transloco }}</span>
            @for (n of [1, 2, 3, 4, 5, 6]; track n) {
              <span class="skeleton chip-skeleton"></span>
            }
          </div>
        }
      } @else if (booking.month()) {
        <p class="hint">{{ 'booking.steps.time.pickDay' | transloco }}</p>
      }
    </div>

    <div class="primary-action">
      <button mat-flat-button type="button" [disabled]="!booking.selectedSlot()" (click)="next()">
        {{ 'booking.next' | transloco }}
      </button>
    </div>
  `,
})
export class TimeStep {
  protected readonly booking = inject(BookingService);
  private readonly router = inject(Router);

  protected readonly today = computed(() =>
    todayInZone(this.booking.salon.hasValue() ? this.booking.salon.value().timeZone : 'UTC'),
  );
  protected readonly currentMonth = computed(() => monthOf(this.today()));
  protected readonly availableDays = computed(
    () => new Set(this.booking.days.hasValue() ? this.booking.days.value().days : []),
  );
  protected readonly groups = computed(() =>
    this.booking.slots.hasValue() ? groupByDayPart(this.booking.slots.value().slots) : [],
  );

  constructor() {
    // The calendar opens on the day already chosen, else on the salon's current month (once its time zone is known).
    effect(() => {
      if (this.booking.salon.hasValue()) {
        const fallback = this.currentMonth();
        untracked(() => this.booking.showMonth(fallback));
      }
    });
  }

  protected next(): void {
    void this.router.navigate(['/', this.booking.slug(), 'details']);
  }
}
