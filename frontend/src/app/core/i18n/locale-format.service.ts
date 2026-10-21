import { Injectable, inject } from '@angular/core';
import { LanguageService } from './language.service';

export type DateStyle = 'date' | 'time' | 'dateTime' | 'weekdayDate';

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

  formatNumber(value: number, options?: Intl.NumberFormatOptions): string {
    return new Intl.NumberFormat(this.language.current(), options).format(value);
  }

  /** Prices are stored as cents (integers) of a currency. */
  formatMoney(cents: number, currency = 'EUR'): string {
    return new Intl.NumberFormat(this.language.current(), { style: 'currency', currency }).format(
      cents / 100,
    );
  }
}
