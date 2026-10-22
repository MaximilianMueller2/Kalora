import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { provideTestI18n } from '../../../testing/test-i18n';
import { LocalDatePipe } from '../../shared/pipes/local-date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { LanguageService } from './language.service';
import { LocaleFormatService } from './locale-format.service';

const NON_BREAKING_SPACES = new RegExp('[\\u00a0\\u202f]', 'g');
const plain = (text: string) => text.replace(NON_BREAKING_SPACES, ' ');

describe('LocaleFormatService', () => {
  let format: LocaleFormatService;
  let language: LanguageService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideTestI18n()] });
    format = TestBed.inject(LocaleFormatService);
    language = TestBed.inject(LanguageService);
  });

  it('formats prices from cents in each language’s convention', async () => {
    await language.use('de');
    expect(plain(format.formatMoney(1999))).toBe('19,99 €');
    await language.use('en');
    expect(plain(format.formatMoney(1999))).toBe('€19.99');
    await language.use('tr');
    expect(plain(format.formatMoney(1999))).toBe('€19,99');
  });

  it('shows UTC times in the salon’s local time zone, including daylight saving time', async () => {
    await language.use('de');
    // 3 Nov 2026 09:00 UTC is winter time (UTC+1); 3 Aug 2026 08:00 UTC is summer time (UTC+2).
    expect(format.formatDate('2026-11-03T09:00:00Z', 'time', 'Europe/Berlin')).toBe('10:00');
    expect(format.formatDate('2026-08-03T08:00:00Z', 'time', 'Europe/Berlin')).toBe('10:00');
  });

  it('orders day and month per language', async () => {
    await language.use('de');
    expect(format.formatDate('2026-11-03T12:00:00Z', 'date', 'UTC')).toBe('03.11.2026');
    await language.use('en');
    expect(format.formatDate('2026-11-03T12:00:00Z', 'date', 'UTC')).toBe('11/03/2026');
  });

  it('names weekdays in the active language', async () => {
    await language.use('de');
    expect([format.weekdayName('Monday'), format.weekdayName('Sunday')]).toEqual([
      'Montag',
      'Sonntag',
    ]);
    await language.use('en');
    expect(format.weekdayName('Wednesday')).toBe('Wednesday');
    await language.use('tr');
    expect(format.weekdayName('Monday')).toBe('Pazartesi');
    expect(format.weekdayName('Monday', 'short').length).toBeLessThan(
      format.weekdayName('Monday').length,
    );
  });

  it('formats date-only values as the same calendar day, whatever the time zone of the device', async () => {
    await language.use('de');
    expect(format.formatDate('2026-12-24', 'fullDate', 'UTC')).toContain('24. Dezember 2026');
  });

  it('pipes follow a language change and tolerate missing values', async () => {
    const money = TestBed.runInInjectionContext(() => new MoneyPipe());
    const date = TestBed.runInInjectionContext(() => new LocalDatePipe());

    await language.use('de');
    expect(plain(money.transform(500))).toBe('5,00 €');
    await language.use('en');
    expect(plain(money.transform(500))).toBe('€5.00');
    expect(money.transform(null)).toBe('');
    expect(date.transform(undefined)).toBe('');
  });
});
