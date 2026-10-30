import { describe, expect, it } from 'vitest';
import { CalendarEvent, googleCalendarUrl, outlookCalendarUrl } from './calendar-links';

const EVENT: CalendarEvent = {
  title: 'Färben – Haarwerk Müller',
  startUtc: '2026-11-04T08:00:00Z',
  endUtc: '2026-11-04T09:00:00.000+00:00',
  location: 'Hauptstraße 1, 10115 Berlin',
  details: 'Färben\nAnna',
};

describe('calendar links', () => {
  it('prefills Google Calendar with title, UTC times, place and details', () => {
    const url = new URL(googleCalendarUrl(EVENT));

    expect(url.origin + url.pathname).toBe('https://calendar.google.com/calendar/render');
    expect(url.searchParams.get('action')).toBe('TEMPLATE');
    expect(url.searchParams.get('text')).toBe('Färben – Haarwerk Müller');
    expect(url.searchParams.get('dates')).toBe('20261104T080000Z/20261104T090000Z');
    expect(url.searchParams.get('location')).toBe('Hauptstraße 1, 10115 Berlin');
    expect(url.searchParams.get('details')).toBe('Färben\nAnna');
  });

  it('prefills Outlook with title, ISO UTC times, place and details', () => {
    const url = new URL(outlookCalendarUrl(EVENT));

    expect(url.origin + url.pathname).toBe('https://outlook.live.com/calendar/0/deeplink/compose');
    expect(url.searchParams.get('path')).toBe('/calendar/action/compose');
    expect(url.searchParams.get('rru')).toBe('addevent');
    expect(url.searchParams.get('subject')).toBe('Färben – Haarwerk Müller');
    expect(url.searchParams.get('startdt')).toBe('2026-11-04T08:00:00Z');
    expect(url.searchParams.get('enddt')).toBe('2026-11-04T09:00:00Z');
    expect(url.searchParams.get('location')).toBe('Hauptstraße 1, 10115 Berlin');
    expect(url.searchParams.get('body')).toBe('Färben\nAnna');
  });

  it('encodes characters that would break out of the query', () => {
    const url = googleCalendarUrl({ ...EVENT, title: 'A&B=C#D' });

    expect(url).not.toContain('A&B');
    expect(new URL(url).searchParams.get('text')).toBe('A&B=C#D');
  });
});
