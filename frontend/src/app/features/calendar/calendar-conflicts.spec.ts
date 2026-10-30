import { describe, expect, it } from 'vitest';
import { request } from '../requests/requests-fixtures';
import { Appointment } from '../requests/requests.models';
import { findConflict } from './calendar-conflicts';
import { CalendarColumn } from './calendar.models';

const NOW = Date.parse('2026-11-03T09:00:00Z');
const at = (hour: number, minute = 0) =>
  `2026-11-04T${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}:00Z`;

const appointment = (
  id: string,
  start: number,
  end: number,
  extra: Partial<Appointment> = {},
): Appointment =>
  request({
    id,
    status: 'Confirmed',
    startUtc: at(start),
    endUtc: at(end),
    blockedStartUtc: at(start),
    blockedEndUtc: at(end),
    ...extra,
  });

/** Anna works 08:00-16:00Z (09:00-17:00 in Berlin). */
const column = (overrides: Partial<CalendarColumn> = {}): CalendarColumn => ({
  staffId: 's1',
  displayName: 'Anna',
  color: '#2563EB',
  working: [{ startUtc: at(8), endUtc: at(16) }],
  absences: [],
  blocks: [],
  appointments: [],
  ...overrides,
});

const moving = appointment('moving', 9, 10);

describe('findConflict', () => {
  it('lets an appointment move to free working time', () => {
    expect(findConflict(column({ appointments: [moving] }), moving, at(11), NOW)).toBeNull();
  });

  it('does not collide with itself when it moves a little', () => {
    expect(findConflict(column({ appointments: [moving] }), moving, at(9, 15), NOW)).toBeNull();
  });

  it('allows directly adjacent appointments', () => {
    const other = appointment('other', 11, 12);

    expect(findConflict(column({ appointments: [moving, other] }), moving, at(10), NOW)).toBeNull();
    expect(findConflict(column({ appointments: [moving, other] }), moving, at(12), NOW)).toBeNull();
  });

  it('finds another appointment, even a corner of it', () => {
    const other = appointment('other', 11, 12);

    expect(findConflict(column({ appointments: [moving, other] }), moving, at(11, 30), NOW)).toBe(
      'appointment',
    );
    expect(findConflict(column({ appointments: [moving, other] }), moving, at(10, 30), NOW)).toBe(
      'appointment',
    );
  });

  it('counts the buffers of both appointments', () => {
    const buffered = appointment('buffered', 11, 12, {
      blockedStartUtc: at(10, 45),
      blockedEndUtc: at(12, 15),
    });
    const mine = appointment('mine', 9, 10, {
      blockedStartUtc: at(8, 45),
      blockedEndUtc: at(10, 15),
    });

    expect(findConflict(column({ appointments: [mine, buffered] }), mine, at(10), NOW)).toBe(
      'appointment',
    ); // 09:45-11:15 hits 10:45
    expect(
      findConflict(column({ appointments: [mine, buffered] }), mine, at(9, 15), NOW),
    ).toBeNull(); // 09:00-10:30 vs 10:45
  });

  it('ignores appointments that no longer hold their time', () => {
    for (const status of ['Completed', 'NoShow', 'Cancelled', 'Declined', 'Expired'] as const) {
      const other = appointment('other', 11, 12, { status });

      expect(
        findConflict(column({ appointments: [moving, other] }), moving, at(11), NOW),
      ).toBeNull();
    }
  });

  it('counts open requests as taken', () => {
    const open = appointment('open', 11, 12, { status: 'Requested' });

    expect(findConflict(column({ appointments: [moving, open] }), moving, at(11), NOW)).toBe(
      'appointment',
    );
  });

  it('finds a blocker', () => {
    const blocked = column({
      appointments: [moving],
      blocks: [{ id: 'b', startUtc: at(12), endUtc: at(13), label: 'Pause' }],
    });

    expect(findConflict(blocked, moving, at(12, 30), NOW)).toBe('block');
    expect(findConflict(blocked, moving, at(13), NOW)).toBeNull();
  });

  it('finds an absence', () => {
    const away = column({
      appointments: [moving],
      absences: [{ id: 'a', startUtc: at(13), endUtc: at(15), type: 'Sick' }],
    });

    expect(findConflict(away, moving, at(14), NOW)).toBe('absence');
  });

  it('refuses time outside the working hours, also when it only reaches over the end', () => {
    expect(findConflict(column({ appointments: [moving] }), moving, at(7), NOW)).toBe('closed');
    expect(findConflict(column({ appointments: [moving] }), moving, at(15, 30), NOW)).toBe(
      'closed',
    );
    expect(findConflict(column({ appointments: [moving] }), moving, at(15), NOW)).toBeNull();
  });

  it('refuses a column in which the person does not work at all', () => {
    expect(findConflict(column({ working: [], appointments: [moving] }), moving, at(10), NOW)).toBe(
      'closed',
    );
  });

  it('keeps the buffers inside the working time', () => {
    const buffered = appointment('b', 9, 10, {
      blockedStartUtc: at(8, 45),
      blockedEndUtc: at(10, 15),
    });

    expect(findConflict(column({ appointments: [buffered] }), buffered, at(8), NOW)).toBe('closed'); // buffer would start at 07:45
  });

  it('refuses the past', () => {
    expect(
      findConflict(column({ appointments: [moving] }), moving, '2026-11-03T08:00:00Z', NOW),
    ).toBe('past');
  });

  it('reports the most basic problem first', () => {
    const crowded = column({
      appointments: [moving, appointment('other', 7, 8)],
      blocks: [{ id: 'b', startUtc: at(7), endUtc: at(8), label: null }],
    });

    expect(findConflict(crowded, moving, at(7), NOW)).toBe('closed');
    expect(findConflict(crowded, moving, '2026-11-03T07:00:00Z', NOW)).toBe('past');
  });

  it('works for a second person: other columns have their own appointments', () => {
    const ben = column({
      staffId: 's2',
      displayName: 'Ben',
      appointments: [appointment('bens', 11, 12)],
    });

    expect(findConflict(ben, moving, at(11), NOW)).toBe('appointment');
    expect(findConflict(ben, moving, at(13), NOW)).toBeNull();
  });
});
