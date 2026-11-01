import { vi } from 'vitest';
import { request } from '../requests/requests-fixtures';
import { Dashboard, DashboardPerson, DashboardWeek } from './dashboard.models';

/** Wednesday 4 November 2026 in Berlin (UTC+1): 09:00 local is 08:00Z. */
export const DAY = '2026-11-04';
export const ZONE = 'Europe/Berlin';
export const at = (hour: number, minute = 0) =>
  `${DAY}T${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}:00Z`;

export function person(overrides: Partial<DashboardPerson> = {}): DashboardPerson {
  return {
    staffId: 's1',
    displayName: 'Anna',
    color: '#2563EB',
    working: [{ startUtc: at(8), endUtc: at(16) }], // 09:00-17:00 local
    gaps: [{ startUtc: at(11), endUtc: at(13) }], // 12:00-14:00 local
    appointments: [
      {
        id: 'a1',
        status: 'Confirmed',
        startUtc: at(9),
        endUtc: at(10),
        serviceName: 'Haarschnitt',
        customerName: 'Kim Kundin',
      },
      {
        id: 'a2',
        status: 'Requested',
        startUtc: at(13),
        endUtc: at(14),
        serviceName: 'Färben',
        customerName: 'Rita Request',
      },
    ],
    ...overrides,
  };
}

export const WEEK: DashboardWeek = {
  from: '2026-11-02',
  to: '2026-11-08',
  appointments: 12,
  bookedMinutes: 780,
  availableMinutes: 1680,
  utilizationPercent: 46,
  noShows: 1,
  estimatedRevenueCents: 54000,
  currency: 'EUR',
  includesFromPrices: false,
};

export function dashboard(overrides: Partial<Dashboard> = {}): Dashboard {
  return {
    today: {
      date: DAY,
      timeZone: ZONE,
      people: [
        person(),
        person({ staffId: 's2', displayName: 'Ben', color: '#DC2626', appointments: [], gaps: [] }),
      ],
    },
    requests: { count: 7, items: [request({ id: 'r1' }), request({ id: 'r2' })] },
    week: WEEK,
    bookingLink: { url: 'https://kalora.app/haarwerk' },
    ...overrides,
  };
}

/** Stands in for DashboardService. */
export function fakeDashboard(data: Dashboard = dashboard()) {
  return {
    get: vi.fn().mockResolvedValue(data),
    qrPngUrl: '/api/dashboard/booking-link/qr.png',
    posterPdfUrl: '/api/dashboard/booking-link/poster.pdf',
  };
}
