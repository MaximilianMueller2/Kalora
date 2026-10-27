import { describe, expect, it } from 'vitest';
import { addMonths, dayPartOf, groupByDayPart, monthGrid, monthOf } from './booking-calendar';

describe('monthGrid', () => {
  it('starts on Monday and fills up the first and last week with empty cells', () => {
    // 1 November 2026 is a Sunday: six empty cells first, 30 days, then the last week is filled up.
    const weeks = monthGrid('2026-11');

    expect(weeks).toHaveLength(6);
    expect(weeks.every((week) => week.length === 7)).toBe(true);
    expect(weeks[0].slice(0, 6)).toEqual([null, null, null, null, null, null]);
    expect(weeks[0][6]).toEqual({ date: '2026-11-01', day: 1 });
    expect(weeks[1][0]).toEqual({ date: '2026-11-02', day: 2 });
    expect(weeks[5][0]).toEqual({ date: '2026-11-30', day: 30 });
    expect(weeks[5].slice(1)).toEqual([null, null, null, null, null, null]);
  });

  it('needs no empty cells when the month starts on Monday and has exactly four weeks', () => {
    // February 2027 starts on a Monday and has 28 days.
    const weeks = monthGrid('2027-02');

    expect(weeks).toHaveLength(4);
    expect(weeks.flat().every((cell) => cell !== null)).toBe(true);
  });

  it('knows leap years and six-week months', () => {
    const february = monthGrid('2028-02').flat().filter(Boolean);
    expect(february).toHaveLength(29);

    // 1 March 2025 is a Saturday: 31 days need six weeks.
    expect(monthGrid('2025-03')).toHaveLength(6);
  });

  it('numbers every day once, in order', () => {
    const days = monthGrid('2026-12')
      .flat()
      .filter((cell) => cell !== null);

    expect(days.map((cell) => cell.day)).toEqual(
      Array.from({ length: 31 }, (_, index) => index + 1),
    );
    expect(days[0].date).toBe('2026-12-01');
  });
});

describe('addMonths', () => {
  it.each([
    ['2026-11', 1, '2026-12'],
    ['2026-12', 1, '2027-01'],
    ['2027-01', -1, '2026-12'],
    ['2026-11', 12, '2027-11'],
    ['2026-11', -11, '2025-12'],
    ['2026-11', 0, '2026-11'],
  ])('%s %i -> %s', (month, delta, expected) => {
    expect(addMonths(month, delta)).toBe(expected);
  });

  it('cuts the month out of a date', () => {
    expect(monthOf('2026-11-04')).toBe('2026-11');
  });
});

describe('day parts', () => {
  it.each([
    ['08:00', 'morning'],
    ['11:59', 'morning'],
    ['12:00', 'afternoon'],
    ['16:45', 'afternoon'],
    ['17:00', 'evening'],
    ['20:30', 'evening'],
  ])('%s is %s', (time, part) => {
    expect(dayPartOf(time)).toBe(part);
  });

  it('groups start times in order and leaves out empty parts', () => {
    const slots = [{ time: '09:00' }, { time: '09:30' }, { time: '17:00' }, { time: '18:15' }];

    const groups = groupByDayPart(slots);

    expect(groups.map((group) => group.part)).toEqual(['morning', 'evening']);
    expect(groups[1].slots.map((slot) => slot.time)).toEqual(['17:00', '18:15']);
    expect(groupByDayPart([])).toEqual([]);
  });
});
