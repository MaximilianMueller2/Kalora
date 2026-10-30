import { CalendarColumn, CalendarSpan } from './calendar.models';

/** Height of one minute in the grid; one hour is 72 px, so a 15-minute slot is a comfortable tap target. */
export const PX_PER_MINUTE = 1.2;
export const MINUTES_PER_DAY = 24 * 60;

const DEFAULT_START = 8 * 60;
const DEFAULT_END = 18 * 60;

/** The part of the day the grid shows, in minutes since local midnight. */
export interface DayWindow {
  startMinute: number;
  endMinute: number;
}

/** A box in the grid, in pixels from the top of the window. */
export interface Rect {
  top: number;
  height: number;
}

const FORMATTERS = new Map<string, Intl.DateTimeFormat>();

function formatter(zone: string): Intl.DateTimeFormat {
  let known = FORMATTERS.get(zone);
  if (!known) {
    known = new Intl.DateTimeFormat('en-CA', {
      timeZone: zone,
      hourCycle: 'h23',
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    });
    FORMATTERS.set(zone, known);
  }
  return known;
}

/** The wall clock of a zone at an instant, as if it were UTC (so differences between two such values are wall-clock differences). */
function wallClockMs(instantMs: number, zone: string): number {
  const parts: Record<string, number> = {};
  for (const part of formatter(zone).formatToParts(new Date(instantMs))) {
    if (part.type !== 'literal') {
      parts[part.type] = Number(part.value);
    }
  }
  return Date.UTC(
    parts['year'],
    parts['month'] - 1,
    parts['day'],
    parts['hour'],
    parts['minute'],
    parts['second'],
  );
}

/** Offset of the zone from UTC at an instant, in minutes (Berlin: 60 in winter, 120 in summer). */
export function zoneOffsetMinutes(instantMs: number, zone: string): number {
  return Math.round((wallClockMs(instantMs, zone) - instantMs) / 60_000);
}

function dayStartMs(date: string): number {
  return Date.parse(`${date}T00:00:00Z`);
}

/** Minutes from local midnight of `date` to the instant; negative before that day, above 1440 after it. */
export function minutesOfDay(instant: string | number, date: string, zone: string): number {
  const ms = typeof instant === 'number' ? instant : Date.parse(instant);
  return Math.round((wallClockMs(ms, zone) - dayStartMs(date)) / 60_000);
}

/** The instant at which the clocks of the zone show `minutes` after midnight of `date` (ISO string, UTC). */
export function instantAtMinutes(date: string, minutes: number, zone: string): string {
  const wall = dayStartMs(date) + minutes * 60_000;
  let guess = wall - zoneOffsetMinutes(wall, zone) * 60_000;
  // At the edge of a daylight saving change the offset of the first guess can be the wrong one; one more round settles it.
  guess = wall - zoneOffsetMinutes(guess, zone) * 60_000;
  return new Date(guess).toISOString();
}

/** The local calendar day ("yyyy-MM-dd") an instant falls on. */
export function localDate(instant: string | number, zone: string): string {
  const ms = typeof instant === 'number' ? instant : Date.parse(instant);
  return new Date(wallClockMs(ms, zone)).toISOString().slice(0, 10);
}

export function addDays(date: string, days: number): string {
  return new Date(dayStartMs(date) + days * 86_400_000).toISOString().slice(0, 10);
}

/** Monday of the week the day is in. */
export function startOfWeek(date: string): string {
  const weekday = new Date(dayStartMs(date)).getUTCDay(); // 0 = Sunday
  return addDays(date, -((weekday + 6) % 7));
}

export function snap(minutes: number, step: number): number {
  return Math.round(minutes / step) * step;
}

/** The minutes since midnight that a vertical position in the grid stands for. */
export function minutesAtPixel(
  pixel: number,
  window: DayWindow,
  pxPerMinute = PX_PER_MINUTE,
): number {
  return window.startMinute + pixel / pxPerMinute;
}

export function pixelOfMinute(
  minute: number,
  window: DayWindow,
  pxPerMinute = PX_PER_MINUTE,
): number {
  return (minute - window.startMinute) * pxPerMinute;
}

export function windowHeight(window: DayWindow, pxPerMinute = PX_PER_MINUTE): number {
  return (window.endMinute - window.startMinute) * pxPerMinute;
}

/** Where a span of time lies in the grid of `date`; null if it lies outside the window or the day. */
export function rectOf(
  span: CalendarSpan,
  date: string,
  zone: string,
  window: DayWindow,
  pxPerMinute = PX_PER_MINUTE,
): Rect | null {
  const start = Math.max(minutesOfDay(span.startUtc, date, zone), window.startMinute);
  const end = Math.min(minutesOfDay(span.endUtc, date, zone), window.endMinute);
  return end > start
    ? { top: (start - window.startMinute) * pxPerMinute, height: (end - start) * pxPerMinute }
    : null;
}

/** The parts of the window in which the person does not work: the grey areas of a column. */
export function offRects(
  working: CalendarSpan[],
  date: string,
  zone: string,
  window: DayWindow,
  pxPerMinute = PX_PER_MINUTE,
): Rect[] {
  const spans = working
    .map((span) => ({
      start: Math.max(minutesOfDay(span.startUtc, date, zone), window.startMinute),
      end: Math.min(minutesOfDay(span.endUtc, date, zone), window.endMinute),
    }))
    .filter((span) => span.end > span.start)
    .sort((a, b) => a.start - b.start);

  const gaps: Rect[] = [];
  let cursor = window.startMinute;
  for (const span of spans) {
    if (span.start > cursor) {
      gaps.push({
        top: (cursor - window.startMinute) * pxPerMinute,
        height: (span.start - cursor) * pxPerMinute,
      });
    }
    cursor = Math.max(cursor, span.end);
  }
  if (cursor < window.endMinute) {
    gaps.push({
      top: (cursor - window.startMinute) * pxPerMinute,
      height: (window.endMinute - cursor) * pxPerMinute,
    });
  }
  return gaps;
}

/** Position of the "now" line, or null if it is not today or outside the window. */
export function nowTop(
  now: number,
  date: string,
  zone: string,
  window: DayWindow,
  pxPerMinute = PX_PER_MINUTE,
): number | null {
  if (localDate(now, zone) !== date) {
    return null;
  }
  const minute = minutesOfDay(now, date, zone);
  return minute >= window.startMinute && minute <= window.endMinute
    ? pixelOfMinute(minute, window, pxPerMinute)
    : null;
}

/**
 * The hours the grid shows: 08:00 to 18:00 at least, stretched to full hours around everything that happens earlier
 * or later (working time, appointments, blockers) on the shown days.
 */
export function visibleWindow(columns: CalendarColumn[], dates: string[], zone: string): DayWindow {
  let start = DEFAULT_START;
  let end = DEFAULT_END;
  for (const date of dates) {
    for (const column of columns) {
      const spans: CalendarSpan[] = [
        ...column.working,
        ...column.blocks,
        ...column.appointments.map((a) => ({
          startUtc: a.blockedStartUtc,
          endUtc: a.blockedEndUtc,
        })),
      ];
      for (const span of spans) {
        const from = minutesOfDay(span.startUtc, date, zone);
        const to = minutesOfDay(span.endUtc, date, zone);
        if (to <= 0 || from >= MINUTES_PER_DAY) {
          continue;
        }
        start = Math.min(start, Math.floor(Math.max(from, 0) / 60) * 60);
        end = Math.max(end, Math.ceil(Math.min(to, MINUTES_PER_DAY) / 60) * 60);
      }
    }
  }
  return { startMinute: start, endMinute: end };
}
