import { describe, expect, it } from 'vitest';
import { DAY, ZONE, at, person } from './dashboard-fixtures';
import { placeOnTimeline, timelineMarks, timelineWindow } from './timeline';

describe('timelineWindow', () => {
  it('runs from the first to the last working hour of everybody, in whole hours', () => {
    const early = person({ working: [{ startUtc: at(6, 20), endUtc: at(14) }] }); // 07:20-15:00 local
    const late = person({ working: [{ startUtc: at(10), endUtc: at(19, 10) }] }); // 11:00-20:10 local

    expect(timelineWindow([early, late], DAY, ZONE)).toEqual({
      startMinute: 7 * 60,
      endMinute: 21 * 60,
    });
  });

  it('shows 08:00 to 18:00 when nobody works today', () => {
    expect(timelineWindow([person({ working: [] })], DAY, ZONE)).toEqual({
      startMinute: 480,
      endMinute: 1080,
    });
    expect(timelineWindow([], DAY, ZONE)).toEqual({ startMinute: 480, endMinute: 1080 });
  });
});

describe('placeOnTimeline', () => {
  const window = { startMinute: 9 * 60, endMinute: 17 * 60 };

  it('gives the start and the length as percent of the width', () => {
    expect(placeOnTimeline({ startUtc: at(9), endUtc: at(10) }, DAY, ZONE, window)).toEqual({
      left: 12.5,
      width: 12.5,
    }); // 10:00-11:00 of 8 hours
  });

  it('cuts what reaches beyond the window and drops what lies outside', () => {
    expect(placeOnTimeline({ startUtc: at(6), endUtc: at(9) }, DAY, ZONE, window)).toEqual({
      left: 0,
      width: 12.5,
    }); // 07:00-10:00 -> 09:00-10:00
    expect(placeOnTimeline({ startUtc: at(3), endUtc: at(6) }, DAY, ZONE, window)).toBeNull();
    expect(placeOnTimeline({ startUtc: at(17), endUtc: at(18) }, DAY, ZONE, window)).toBeNull();
  });

  it('works in the zone of the salon, not in UTC', () => {
    const summer = placeOnTimeline(
      { startUtc: '2026-07-01T07:00:00Z', endUtc: '2026-07-01T08:00:00Z' },
      '2026-07-01',
      ZONE,
      window,
    ); // 09:00-10:00 local

    expect(summer).toEqual({ left: 0, width: 12.5 });
  });
});

describe('timelineMarks', () => {
  it('marks every second hour from the start to the end', () => {
    expect(timelineMarks({ startMinute: 8 * 60, endMinute: 18 * 60 })).toEqual([
      { label: '08:00', left: 0 },
      { label: '10:00', left: 20 },
      { label: '12:00', left: 40 },
      { label: '14:00', left: 60 },
      { label: '16:00', left: 80 },
      { label: '18:00', left: 100 },
    ]);
  });
});
