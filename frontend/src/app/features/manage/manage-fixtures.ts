import { vi } from 'vitest';
import { ManagedAppointment } from './manage.models';

/** A confirmed appointment on Thursday 5 November 2026, 10:00 in Berlin, cancellable until the day before. */
export function managed(overrides: Partial<ManagedAppointment> = {}): ManagedAppointment {
  return {
    status: 'Confirmed',
    startUtc: '2026-11-05T09:00:00Z',
    endUtc: '2026-11-05T10:00:00Z',
    serviceName: 'Haarschnitt',
    salonName: 'Haarwerk Müller',
    salonSlug: 'haarwerk',
    salonPhone: '+49 30 123456',
    salonAddress: 'Hauptstraße 1, 10115 Berlin',
    timeZone: 'Europe/Berlin',
    cancelUntilUtc: '2026-11-04T09:00:00Z',
    canCancel: true,
    canAddToCalendar: true,
    canRebook: false,
    ...overrides,
  };
}

/** Stands in for ManageService. */
export function fakeManage(appointment: ManagedAppointment = managed()) {
  return {
    get: vi.fn().mockResolvedValue(appointment),
    cancel: vi.fn().mockResolvedValue(
      managed({
        status: 'Cancelled',
        canCancel: false,
        canAddToCalendar: false,
        canRebook: true,
        cancelUntilUtc: null,
      }),
    ),
  };
}
