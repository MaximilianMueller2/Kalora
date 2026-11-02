import { vi } from 'vitest';
import { AdminSalonDetail, AdminSalonItem, AdminSalonList } from './admin.models';

export function salonItem(overrides: Partial<AdminSalonItem> = {}): AdminSalonItem {
  return {
    id: 's1',
    name: 'Haarwerk Müller',
    slug: 'haarwerk',
    status: 'Trial',
    planCode: 'Solo',
    access: 'Full',
    staffCount: 2,
    staffLimit: 15,
    appointmentsThisMonth: 12,
    trialEndsAtUtc: '2026-12-01T09:00:00Z',
    createdAtUtc: '2026-11-01T09:00:00Z',
    ownerEmail: 'maria@example.com',
    hasNote: false,
    ...overrides,
  };
}

export function salonDetail(overrides: Partial<AdminSalonDetail> = {}): AdminSalonDetail {
  return {
    id: 's1',
    name: 'Haarwerk Müller',
    slug: 'haarwerk',
    city: 'Berlin',
    phone: '+49 30 123456',
    timeZone: 'Europe/Berlin',
    status: 'Trial',
    planCode: 'Solo',
    access: 'Full',
    staffCount: 2,
    staffLimit: 15,
    appointmentsThisMonth: 12,
    trialEndsAtUtc: '2026-12-01T09:00:00Z',
    createdAtUtc: '2026-11-01T09:00:00Z',
    ownerName: 'Maria Müller',
    ownerEmail: 'maria@example.com',
    adminNote: null,
    ...overrides,
  };
}

export function fakeAdmin(
  list: AdminSalonList = { items: [salonItem()], hasMore: false },
  detail: AdminSalonDetail = salonDetail(),
) {
  return {
    list: vi.fn().mockResolvedValue(list),
    get: vi.fn().mockResolvedValue(detail),
    setPlan: vi
      .fn()
      .mockImplementation((_id: string, planCode: string) =>
        Promise.resolve({ ...detail, planCode }),
      ),
    extendTrial: vi.fn().mockResolvedValue({ ...detail, trialEndsAtUtc: '2026-12-08T09:00:00Z' }),
    setStatus: vi
      .fn()
      .mockImplementation((_id: string, status: string) => Promise.resolve({ ...detail, status })),
    suspend: vi.fn().mockResolvedValue({ ...detail, status: 'Suspended', access: 'Suspended' }),
    unsuspend: vi.fn().mockResolvedValue({ ...detail, status: 'Trial' }),
    setNote: vi
      .fn()
      .mockImplementation((_id: string, adminNote: string) =>
        Promise.resolve({ ...detail, adminNote }),
      ),
  };
}
