import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { LocaleFormatService } from '../../core/i18n/locale-format.service';
import { nowTop } from '../calendar/calendar-geometry';
import { DashboardPerson } from './dashboard.models';
import { placeOnTimeline, timelineMarks, timelineWindow } from './timeline';

interface Place {
  left: number;
  width: number;
}

/** Only what lies on the timeline, typed as such. */
function placed<T extends { place: Place | null }>(items: T[]): (T & { place: Place })[] {
  return items.filter((item): item is T & { place: Place } => item.place !== null);
}

/**
 * Today as a timeline per person: working time, the appointments in the color of the person (open requests dashed),
 * and the free gaps that are left, highlighted. Below it the next appointments as text.
 */
@Component({
  selector: 'app-today-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, TranslocoPipe],
  templateUrl: './today-card.html',
  styleUrl: './today-card.scss',
})
export class TodayCard {
  private readonly format = inject(LocaleFormatService);

  readonly people = input.required<DashboardPerson[]>();
  readonly date = input.required<string>();
  readonly zone = input.required<string>();
  readonly now = input.required<number>();

  protected readonly window = computed(() =>
    timelineWindow(this.people(), this.date(), this.zone()),
  );
  protected readonly marks = computed(() => timelineMarks(this.window()));
  protected readonly nowLeft = computed(() => {
    const window = this.window();
    const top = nowTop(this.now(), this.date(), this.zone(), window, 1);
    return top === null ? null : (top / (window.endMinute - window.startMinute)) * 100;
  });

  protected readonly rows = computed(() => {
    const date = this.date();
    const zone = this.zone();
    const window = this.window();
    const place = (span: { startUtc: string; endUtc: string }) =>
      placeOnTimeline(span, date, zone, window);
    return this.people().map((person) => ({
      person,
      working: person.working.map(place).filter((p) => p !== null),
      gaps: placed(person.gaps.map((gap) => ({ gap, place: place(gap) }))),
      appointments: placed(
        person.appointments.map((appointment) => ({ appointment, place: place(appointment) })),
      ),
      free: person.working.length === 0,
    }));
  });

  /** The appointments that have not ended yet, in order, with the person: the "next" list. */
  protected readonly upcoming = computed(() => {
    const now = this.now();
    const zone = this.zone();
    return this.people()
      .flatMap((person) => person.appointments.map((appointment) => ({ person, appointment })))
      .filter(
        ({ appointment }) =>
          Date.parse(appointment.endUtc) > now &&
          appointment.status !== 'Completed' &&
          appointment.status !== 'NoShow',
      )
      .sort((a, b) => Date.parse(a.appointment.startUtc) - Date.parse(b.appointment.startUtc))
      .slice(0, 6)
      .map(({ person, appointment }) => ({
        ...appointment,
        person,
        time: this.format.formatDate(appointment.startUtc, 'time', zone),
        running: Date.parse(appointment.startUtc) <= now,
      }));
  });

  protected gapText(startUtc: string, endUtc: string): string {
    const zone = this.zone();
    return `${this.format.formatDate(startUtc, 'time', zone)} – ${this.format.formatDate(endUtc, 'time', zone)}`;
  }
}
