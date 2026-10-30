import { Injectable, inject } from '@angular/core';
import { WEEKDAYS, Weekday } from '../../shared/week-schedule/week-schedule.model';
import { LanguageService } from './language.service';

export type DateStyle =
  'date' | 'time' | 'dateTime' | 'weekdayDate' | 'fullDate' | 'monthYear' | 'weekdayDay';

const DATE_OPTIONS: Record<DateStyle, Intl.DateTimeFormatOptions> = {
  date: { day: '2-digit', month: '2-digit', year: 'numeric' },
  time: { hour: '2-digit', minute: '2-digit' },
  dateTime: {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  },
  weekdayDate: { weekday: 'long', day: 'numeric', month: 'long' },
  fullDate: { weekday: 'short', day: 'numeric', month: 'long', year: 'numeric' },
  monthYear: { month: 'long', year: 'numeric' },
  weekdayDay: { weekday: 'short', day: 'numeric' },
};

/** Dates, times, numbers and money in the active language's conventions (Intl, no locale data bundles needed). */
@Injectable({ providedIn: 'root' })
export class LocaleFormatService {
  private readonly language = inject(LanguageService);

  /** `timeZone` is the salon's IANA zone; times are stored in UTC and shown in the salon's local time. */
  formatDate(value: Date | string | number, style: DateStyle = 'date', timeZone?: string): string {
    return new Intl.DateTimeFormat(this.language.current(), {
      ...DATE_OPTIONS[style],
      timeZone,
    }).format(new Date(value));
  }

  /** Name of a weekday in the active language ("Montag", "Monday", "Pazartesi"). */
  weekdayName(weekday: Weekday, style: 'long' | 'short' = 'long'): string {
    // 1 Jan 2024 is a Monday, so the index in the Monday-first list is the day offset.
    const reference = new Date(Date.UTC(2024, 0, 1 + WEEKDAYS.indexOf(weekday)));
    return new Intl.DateTimeFormat(this.language.current(), {
      weekday: style,
      timeZone: 'UTC',
    }).format(reference);
  }

  formatNumber(value: number, options?: Intl.NumberFormatOptions): string {
    return new Intl.NumberFormat(this.language.current(), options).format(value);
  }

  /** Lengths of time in the language's unit names ("1 Std. 30 Min.", "1 hr 30 min", "1 sa 30 dk"). */
  formatDuration(totalMinutes: number): string {
    const unit = (value: number, name: 'hour' | 'minute') =>
      new Intl.NumberFormat(this.language.current(), {
        style: 'unit',
        unit: name,
        unitDisplay: 'short',
      }).format(value);
    const hours = Math.floor(totalMinutes / 60);
    const minutes = totalMinutes % 60;
    return [
      hours > 0 ? unit(hours, 'hour') : '',
      minutes > 0 || hours === 0 ? unit(minutes, 'minute') : '',
    ]
      .filter(Boolean)
      .join(' ');
  }

  /** Prices are stored as cents (integers) of a currency. */
  formatMoney(cents: number, currency = 'EUR'): string {
    return new Intl.NumberFormat(this.language.current(), { style: 'currency', currency }).format(
      cents / 100,
    );
  }
}
