import { describe, expect, it } from 'vitest';
import {
  MINUTES_PER_DAY,
  PX_PER_MINUTE,
  addDays,
  instantAtMinutes,
  localDate,
  minutesAtPixel,
  minutesOfDay,
  nowTop,
  offRects,
  pixelOfMinute,
  rectOf,
  snap,
  startOfWeek,
  visibleWindow,
  windowHeight,
} from './calendar-geometry';
import { CalendarColumn } from './calendar.models';

const ZONE = 'Europe/Berlin';
const DAY = '2026-11-04'; // Wednesday, winter time (UTC+1)
const WINDOW = { startMinute: 8 * 60, endMinute: 18 * 60 };

const column = (overrides: Partial<CalendarColumn> = {}): CalendarColumn => ({
  staffId: 's1',
  displayName: 'Anna',
  color: '#2563EB',
  working: [],
  absences: [],
  blocks: [],
  appointments: [],
  ...overrides,
});

describe('minutes of the day', () => {
  it('reads an instant in the salons zone, not in UTC', () => {
    expect(minutesOfDay('2026-11-04T08:00:00Z', DAY, ZONE)).toBe(9 * 60);
    expect(minutesOfDay('2026-11-04T08:00:00Z', DAY, 'UTC')).toBe(8 * 60);
  });

  it('follows summer time', () => {
    expect(minutesOfDay('2026-07-01T07:00:00Z', '2026-07-01', ZONE)).toBe(9 * 60);
  });

  it('counts across midnight: before the day is negative, after it above 1440', () => {
    expect(minutesOfDay('2026-11-03T22:30:00Z', DAY, ZONE)).toBe(-30); // 23:30 the day before
    expect(minutesOfDay('2026-11-04T23:30:00Z', DAY, ZONE)).toBe(MINUTES_PER_DAY + 30); // 00:30 the day after
  });

  it('uses the wall clock on the day the clocks change', () => {
    // 29 March 2026: 02:00 -> 03:00. 01:30 is 00:30Z, 03:30 is 01:30Z.
    expect(minutesOfDay('2026-03-29T00:30:00Z', '2026-03-29', ZONE)).toBe(90);
    expect(minutesOfDay('2026-03-29T01:30:00Z', '2026-03-29', ZONE)).toBe(210);
  });

  it('turns local minutes back into the instant', () => {
    expect(instantAtMinutes(DAY, 9 * 60, ZONE)).toBe('2026-11-04T08:00:00.000Z');
    expect(instantAtMinutes('2026-07-01', 9 * 60, ZONE)).toBe('2026-07-01T07:00:00.000Z');
    expect(instantAtMinutes(DAY, 9 * 60 + 15, 'UTC')).toBe('2026-11-04T09:15:00.000Z');
  });

  it('round-trips every quarter hour of an ordinary and a clock-change day', () => {
    for (const date of [DAY, '2026-03-29', '2026-10-25']) {
      for (let minutes = 0; minutes < MINUTES_PER_DAY; minutes += 15) {
        const instant = instantAtMinutes(date, minutes, ZONE);
        const back = minutesOfDay(instant, date, ZONE);
        // The hour that does not exist (02:00-03:00 in spring) lands an hour later; everything else comes back unchanged.
        const skipped = date === '2026-03-29' && minutes >= 120 && minutes < 180;
        expect(back).toBe(skipped ? minutes + 60 : minutes);
      }
    }
  });
});

describe('days', () => {
  it('names the local day of an instant', () => {
    expect(localDate('2026-11-04T23:30:00Z', ZONE)).toBe('2026-11-05');
    expect(localDate('2026-11-04T22:59:00Z', ZONE)).toBe('2026-11-04');
  });

  it('adds days across month ends and gives Monday as the start of the week', () => {
    expect(addDays('2026-11-30', 1)).toBe('2026-12-01');
    expect(addDays('2026-03-01', -1)).toBe('2026-02-28');
    expect(startOfWeek('2026-11-04')).toBe('2026-11-02'); // Wednesday
    expect(startOfWeek('2026-11-08')).toBe('2026-11-02'); // Sunday
    expect(startOfWeek('2026-11-02')).toBe('2026-11-02');
  });
});

describe('time to pixel', () => {
  it('gives an hour 72 px and starts at the top of the window', () => {
    expect(PX_PER_MINUTE * 60).toBe(72);
    expect(pixelOfMinute(8 * 60, WINDOW)).toBe(0);
    expect(pixelOfMinute(9 * 60, WINDOW)).toBe(72);
    expect(windowHeight(WINDOW)).toBe(720);
  });

  it('turns a pixel back into minutes and snaps to the grid', () => {
    expect(minutesAtPixel(72, WINDOW)).toBe(9 * 60);
    expect(snap(minutesAtPixel(20, WINDOW), 15)).toBe(8 * 60 + 15);
    expect(snap(8 * 60 + 7, 15)).toBe(8 * 60);
    expect(snap(8 * 60 + 8, 15)).toBe(8 * 60 + 15);
  });

  it('places a span by its local start and length', () => {
    const rect = rectOf(
      { startUtc: '2026-11-04T09:00:00Z', endUtc: '2026-11-04T10:30:00Z' },
      DAY,
      ZONE,
      WINDOW,
    );

    expect(rect).toEqual({ top: 120 * PX_PER_MINUTE, height: 90 * PX_PER_MINUTE }); // 10:00-11:30
  });

  it('cuts a span at the edges of the window and drops one outside it', () => {
    const early = rectOf(
      { startUtc: '2026-11-04T05:00:00Z', endUtc: '2026-11-04T08:00:00Z' },
      DAY,
      ZONE,
      WINDOW,
    );
    const outside = rectOf(
      { startUtc: '2026-11-04T04:00:00Z', endUtc: '2026-11-04T06:00:00Z' },
      DAY,
      ZONE,
      WINDOW,
    );
    const otherDay = rectOf(
      { startUtc: '2026-11-05T08:00:00Z', endUtc: '2026-11-05T09:00:00Z' },
      DAY,
      ZONE,
      WINDOW,
    );

    expect(early).toEqual({ top: 0, height: 60 * PX_PER_MINUTE }); // 06:00-09:00 shown from 08:00
    expect(outside).toBeNull();
    expect(otherDay).toBeNull();
  });

  it('shows a span that reaches across midnight only on the day it covers', () => {
    const night = { startUtc: '2026-11-04T21:00:00Z', endUtc: '2026-11-05T05:00:00Z' }; // 22:00 - 06:00
    const evening = { startMinute: 20 * 60, endMinute: 24 * 60 };

    expect(rectOf(night, DAY, ZONE, evening)).toEqual({
      top: 120 * PX_PER_MINUTE,
      height: 120 * PX_PER_MINUTE,
    });
    expect(rectOf(night, '2026-11-05', ZONE, { startMinute: 0, endMinute: 8 * 60 })).toEqual({
      top: 0,
      height: 360 * PX_PER_MINUTE,
    });
  });
});

describe('time off', () => {
  it('greys everything in the window that is not working time', () => {
    const rects = offRects(
      [{ startUtc: '2026-11-04T08:00:00Z', endUtc: '2026-11-04T12:00:00Z' }],
      DAY,
      ZONE,
      WINDOW,
    );

    expect(rects).toEqual([
      { top: 0, height: 60 * PX_PER_MINUTE }, // 08:00-09:00
      { top: 300 * PX_PER_MINUTE, height: 300 * PX_PER_MINUTE }, // 13:00-18:00
    ]);
  });

  it('keeps the gap between two working blocks (a lunch break)', () => {
    const rects = offRects(
      [
        { startUtc: '2026-11-04T08:00:00Z', endUtc: '2026-11-04T11:00:00Z' },
        { startUtc: '2026-11-04T12:00:00Z', endUtc: '2026-11-04T16:00:00Z' },
      ],
      DAY,
      ZONE,
      WINDOW,
    );

    expect(rects).toContainEqual({ top: 240 * PX_PER_MINUTE, height: 60 * PX_PER_MINUTE }); // 12:00-13:00
  });

  it('is entirely grey without working time and not grey with all-day work', () => {
    expect(offRects([], DAY, ZONE, WINDOW)).toEqual([{ top: 0, height: 600 * PX_PER_MINUTE }]);
    expect(
      offRects(
        [{ startUtc: '2026-11-04T00:00:00Z', endUtc: '2026-11-05T00:00:00Z' }],
        DAY,
        ZONE,
        WINDOW,
      ),
    ).toEqual([]);
  });
});

describe('the now line', () => {
  const now = Date.parse('2026-11-04T09:30:00Z'); // 10:30 in Berlin

  it('is drawn at the local time on today only', () => {
    expect(nowTop(now, DAY, ZONE, WINDOW)).toBe(150 * PX_PER_MINUTE);
    expect(nowTop(now, '2026-11-05', ZONE, WINDOW)).toBeNull();
  });

  it('is not drawn outside the visible hours', () => {
    expect(nowTop(Date.parse('2026-11-04T05:00:00Z'), DAY, ZONE, WINDOW)).toBeNull();
    expect(nowTop(Date.parse('2026-11-04T20:00:00Z'), DAY, ZONE, WINDOW)).toBeNull();
  });
});

describe('visible hours', () => {
  it('shows 08:00 to 18:00 for an ordinary day', () => {
    expect(visibleWindow([column()], [DAY], ZONE)).toEqual(WINDOW);
  });

  it('stretches to full hours around early and late work', () => {
    const early = column({
      working: [{ startUtc: '2026-11-04T05:20:00Z', endUtc: '2026-11-04T19:10:00Z' }],
    }); // 06:20-20:10

    expect(visibleWindow([early], [DAY], ZONE)).toEqual({
      startMinute: 6 * 60,
      endMinute: 21 * 60,
    });
  });

  it('includes the buffers of appointments', () => {
    const late = column({
      appointments: [
        { blockedStartUtc: '2026-11-04T17:45:00Z', blockedEndUtc: '2026-11-04T19:15:00Z' } as never,
      ],
    });

    expect(visibleWindow([late], [DAY], ZONE).endMinute).toBe(21 * 60); // until 20:15
  });

  it('ignores what happens on other days', () => {
    const otherDay = column({
      working: [{ startUtc: '2026-11-05T04:00:00Z', endUtc: '2026-11-05T20:00:00Z' }],
    });

    expect(visibleWindow([otherDay], [DAY], ZONE)).toEqual(WINDOW);
  });

  it('considers all shown days of a week', () => {
    const week = column({
      working: [{ startUtc: '2026-11-06T05:00:00Z', endUtc: '2026-11-06T16:00:00Z' }],
    });

    expect(visibleWindow([week], [DAY, '2026-11-06'], ZONE).startMinute).toBe(6 * 60);
  });
});
