/** The appointment as an event of a calendar: what the prefilled "add event" links are made from. */
export interface CalendarEvent {
  title: string;
  startUtc: string;
  endUtc: string;
  location: string;
  details: string;
}

const GOOGLE = 'https://calendar.google.com/calendar/render';
const OUTLOOK = 'https://outlook.live.com/calendar/0/deeplink/compose';

/** Seconds precision without milliseconds, as calendars expect: 2026-11-04T08:00:00Z. */
function iso(instant: string): string {
  return new Date(instant).toISOString().replace(/\.\d{3}Z$/, 'Z');
}

function compact(instant: string): string {
  return iso(instant).replace(/[-:]/g, '');
}

export function googleCalendarUrl(event: CalendarEvent): string {
  const query = new URLSearchParams({
    action: 'TEMPLATE',
    text: event.title,
    dates: `${compact(event.startUtc)}/${compact(event.endUtc)}`,
    details: event.details,
    location: event.location,
  });
  return `${GOOGLE}?${query}`;
}

export function outlookCalendarUrl(event: CalendarEvent): string {
  const query = new URLSearchParams({
    path: '/calendar/action/compose',
    rru: 'addevent',
    subject: event.title,
    startdt: iso(event.startUtc),
    enddt: iso(event.endUtc),
    body: event.details,
    location: event.location,
  });
  return `${OUTLOOK}?${query}`;
}
