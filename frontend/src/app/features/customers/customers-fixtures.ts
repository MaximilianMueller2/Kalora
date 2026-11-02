import { vi } from 'vitest';
import {
  CustomerAppointment,
  CustomerDetail,
  CustomerList,
  CustomerListItem,
} from './customers.models';

export function customerItem(overrides: Partial<CustomerListItem> = {}): CustomerListItem {
  return {
    id: 'c1',
    name: 'Kim Klein',
    email: 'kim@example.com',
    phone: null,
    lastVisitAtUtc: '2026-10-20T08:00:00Z',
    appointments: 3,
    noShows: 0,
    ...overrides,
  };
}

export function customerAppointment(
  overrides: Partial<CustomerAppointment> = {},
): CustomerAppointment {
  return {
    id: 'a1',
    status: 'Completed',
    startUtc: '2026-10-20T08:00:00Z',
    endUtc: '2026-10-20T09:00:00Z',
    serviceName: 'Damenschnitt',
    staffName: 'Tom',
    priceCents: 4500,
    priceIsFrom: false,
    currency: 'EUR',
    customerNote: null,
    internalNote: null,
    ...overrides,
  };
}

export function customerDetail(overrides: Partial<CustomerDetail> = {}): CustomerDetail {
  return {
    id: 'c1',
    name: 'Kim Klein',
    email: 'kim@example.com',
    phone: '0170 1234567',
    preferredLanguage: 'de',
    createdAtUtc: '2026-01-05T10:00:00Z',
    lastVisitAtUtc: '2026-10-20T08:00:00Z',
    noShows: 0,
    marketingConsentAtUtc: null,
    anonymized: false,
    appointments: [customerAppointment()],
    ...overrides,
  };
}

export function fakeCustomers(
  list: CustomerList = { items: [customerItem()], hasMore: false },
  detail: CustomerDetail = customerDetail(),
) {
  return {
    list: vi.fn().mockResolvedValue(list),
    get: vi.fn().mockResolvedValue(detail),
    exportUrl: vi.fn((id: string, format: string) => `/api/customers/${id}/export.${format}`),
    anonymize: vi.fn().mockResolvedValue(undefined),
  };
}
