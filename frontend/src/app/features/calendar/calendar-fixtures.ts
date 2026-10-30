import { vi } from 'vitest';
import { request } from '../requests/requests-fixtures';
import { Appointment } from '../requests/requests.models';
import { CalendarColumn, CalendarData } from './calendar.models';

/** Wednesday 4 November 2026 in Berlin (UTC+1). Anna works 09:00-17:00, so 08:00Z-16:00Z. */
export const DAY = '2026-11-04';

export const at = (hour: number, minute = 0, day = DAY) =>
  `${day}T${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}:00Z`;

export function appointment(
  id: string,
  startHour: number,
  endHour: number,
  extra: Partial<Appointment> = {},
): Appointment {
  return request({
    id,
    status: 'Confirmed',
    startUtc: at(startHour),
    endUtc: at(endHour),
    blockedStartUtc: at(startHour),
    blockedEndUtc: at(endHour),
    expiresAtUtc: null,
    nextStates: ['Cancelled', 'Completed', 'NoShow'],
    ...extra,
  });
}

export function column(
  staffId: string,
  displayName: string,
  extra: Partial<CalendarColumn> = {},
): CalendarColumn {
  return {
    staffId,
    displayName,
    color: staffId === 's1' ? '#2563EB' : '#DC2626',
    working: [{ startUtc: at(8), endUtc: at(16) }],
    absences: [],
    blocks: [],
    appointments: [],
    ...extra,
  };
}

export const ANNA_COLUMN = column('s1', 'Anna', {
  appointments: [
    appointment('a1', 9, 10),
    appointment('a2', 11, 12, {
      status: 'Requested',
      nextStates: ['Confirmed', 'Declined', 'Expired', 'Cancelled'],
      expiresAtUtc: at(23),
    }),
  ],
  blocks: [{ id: 'b1', startUtc: at(12), endUtc: at(13), label: 'Pause' }],
  absences: [{ id: 'x1', startUtc: at(14), endUtc: at(15), type: 'Private' }],
});

export const BEN_COLUMN = column('s2', 'Ben', {
  appointments: [
    appointment('b-1', 10, 11, { staff: { id: 's2', displayName: 'Ben', color: '#DC2626' } }),
  ],
});

export function calendarData(
  columns: CalendarColumn[] = [ANNA_COLUMN, BEN_COLUMN],
  overrides: Partial<CalendarData> = {},
): CalendarData {
  return { from: DAY, to: DAY, timeZone: 'Europe/Berlin', slotMinutes: 15, columns, ...overrides };
}

/** Stands in for CalendarService. */
export function fakeCalendar(data: CalendarData = calendarData()) {
  return {
    get: vi.fn().mockResolvedValue(data),
    searchCustomers: vi.fn().mockResolvedValue([]),
    createAppointment: vi.fn().mockResolvedValue(appointment('new', 10, 11)),
    move: vi.fn().mockResolvedValue(appointment('a1', 10, 11)),
    createBlock: vi
      .fn()
      .mockResolvedValue({ id: 'nb', startUtc: at(12), endUtc: at(13), label: null }),
    deleteBlock: vi.fn().mockResolvedValue(undefined),
  };
}
