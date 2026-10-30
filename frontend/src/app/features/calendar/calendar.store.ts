import { Injectable, computed, inject, resource, signal } from '@angular/core';
import { AuthService } from '../auth/auth.service';
import { todayInZone } from '../../shared/dates/today';
import { addDays, startOfWeek, visibleWindow } from './calendar-geometry';
import { CalendarService } from './calendar.service';
import { CalendarColumn, CalendarView } from './calendar.models';

const FALLBACK_ZONE = 'UTC';

/** One column of the grid: a person on a day. The day view has one lane per person, the week view one per day. */
export interface Lane {
  key: string;
  date: string;
  column: CalendarColumn;
  isToday: boolean;
}

/**
 * The state of the calendar page: which day or week, which view, which people, and the data of that range.
 * One instance per page (provided by the page), gone when the page is left.
 */
@Injectable()
export class CalendarStore {
  private readonly service = inject(CalendarService);
  private readonly auth = inject(AuthService);

  /** The salon's own zone; every day in the calendar is a day there. */
  readonly zone = computed(() => this.auth.currentUser()?.salon?.timeZone ?? FALLBACK_ZONE);

  readonly date = signal(todayInZone(this.zone()));
  readonly view = signal<CalendarView>('day');
  /** People shown in the day view; null for everybody. */
  readonly hiddenStaff = signal<ReadonlySet<string>>(new Set());
  /** The person of the week view, and of the day view on a phone. */
  private readonly focus = signal<string | null>(null);
  /** Moves forward once a minute, so the "now" line and "today" follow the clock. */
  readonly now = signal(Date.now());

  readonly days = computed(() => {
    const date = this.date();
    return this.view() === 'week'
      ? Array.from({ length: 7 }, (_, offset) => addDays(startOfWeek(date), offset))
      : [date];
  });
  private readonly range = computed(() => ({
    from: this.days()[0],
    to: this.days().at(-1) as string,
  }));

  private readonly data = resource({
    params: () => this.range(),
    loader: ({ params }) => this.service.get(params.from, params.to),
  });

  readonly loading = computed(() => this.data.isLoading() && !this.data.hasValue());
  readonly failed = computed(() => this.data.status() === 'error');
  readonly slotMinutes = computed(() =>
    this.data.hasValue() ? this.data.value().slotMinutes : 15,
  );
  /** Everybody the server sent (all active people the caller may see). */
  readonly people = computed(() => (this.data.hasValue() ? this.data.value().columns : []));
  readonly shownPeople = computed(() =>
    this.people().filter((p) => !this.hiddenStaff().has(p.staffId)),
  );
  readonly focused = computed(() => {
    const people = this.people();
    return people.find((p) => p.staffId === this.focus()) ?? people.at(0) ?? null;
  });

  /** The columns of the grid for the current view; `singlePerson` is the phone's day view. */
  lanes(singlePerson: boolean): Lane[] {
    const today = todayInZone(this.zone(), new Date(this.now()));
    if (this.view() === 'week' || singlePerson) {
      const person = this.focused();
      return person
        ? this.days().map((date) => ({
            key: `${person.staffId}-${date}`,
            date,
            column: person,
            isToday: date === today,
          }))
        : [];
    }
    const date = this.date();
    return this.shownPeople().map((column) => ({
      key: `${column.staffId}-${date}`,
      date,
      column,
      isToday: date === today,
    }));
  }

  window(singlePerson: boolean) {
    const columns =
      this.view() === 'week' || singlePerson
        ? [this.focused()].filter((c) => c !== null)
        : this.shownPeople();
    return visibleWindow(columns, this.days(), this.zone());
  }

  setView(view: CalendarView): void {
    this.view.set(view);
  }

  goTo(date: string): void {
    this.date.set(date);
  }

  today(): void {
    this.date.set(todayInZone(this.zone()));
  }

  step(direction: -1 | 1): void {
    this.date.update((date) => addDays(date, direction * (this.view() === 'week' ? 7 : 1)));
  }

  focusOn(staffId: string): void {
    this.focus.set(staffId);
  }

  /** The next (or previous) person, for swiping on a phone; stays at the ends. */
  stepPerson(direction: -1 | 1): void {
    const people = this.people();
    const index = people.findIndex((p) => p.staffId === this.focused()?.staffId);
    const next = people[index + direction];
    if (next) {
      this.focus.set(next.staffId);
    }
  }

  toggleStaff(staffId: string): void {
    this.hiddenStaff.update((hidden) => {
      const next = new Set(hidden);
      if (!next.delete(staffId)) {
        next.add(staffId);
      }
      return next;
    });
  }

  reload(): void {
    this.data.reload();
  }

  /** Called by the page every minute. */
  tick(): void {
    this.now.set(Date.now());
  }
}
