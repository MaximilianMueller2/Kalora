import { signal } from '@angular/core';
import { vi } from 'vitest';
import { Appointment } from './requests.models';

const HOUR = 3_600_000;

/** An open request that can be answered for another three hours. */
export function request(overrides: Partial<Appointment> = {}): Appointment {
  return {
    id: 'r1',
    status: 'Requested',
    source: 'Online',
    startUtc: '2026-11-04T08:00:00Z',
    endUtc: '2026-11-04T09:00:00Z',
    blockedStartUtc: '2026-11-04T08:00:00Z',
    blockedEndUtc: '2026-11-04T09:00:00Z',
    serviceId: 'sv1',
    serviceName: 'Haarschnitt',
    priceCents: 4500,
    priceIsFrom: false,
    currency: 'EUR',
    staff: { id: 'st1', displayName: 'Anna', color: '#2563EB' },
    customerId: 'c1',
    customer: { name: 'Kim Kundin', email: 'kim@example.com', phone: '+49 170 1234567' },
    customerNote: 'Bitte ohne Föhn',
    internalNote: null,
    expiresAtUtc: new Date(Date.now() + 3 * HOUR + 30_000).toISOString(),
    createdAtUtc: '2026-11-03T08:00:00Z',
    nextStates: ['Confirmed', 'Declined', 'Expired', 'Cancelled'],
    ...overrides,
  };
}

export function expiresIn(milliseconds: number): string {
  return new Date(Date.now() + milliseconds).toISOString();
}

/** Stands in for RequestsService. */
export function fakeRequests(list: Appointment[] = [request()]) {
  return {
    list: vi.fn().mockResolvedValue(list),
    count: vi.fn().mockResolvedValue(list.length),
    confirm: vi.fn().mockResolvedValue(request({ status: 'Confirmed' })),
    decline: vi.fn().mockResolvedValue(request({ status: 'Declined' })),
    cancel: vi.fn().mockResolvedValue(request({ status: 'Cancelled' })),
    markAttended: vi.fn().mockResolvedValue(request({ status: 'Completed' })),
    markNoShow: vi.fn().mockResolvedValue(request({ status: 'NoShow' })),
  };
}

/** Stands in for OpenRequestsBadge. */
export function fakeBadge() {
  return {
    count: signal(0).asReadonly(),
    watch: vi.fn(),
    refresh: vi.fn().mockResolvedValue(undefined),
  };
}

/** An undo message the test lets run out (`expire`) or presses the button of (`undo`). */
export function controllableOffer() {
  let resolve!: (undone: boolean) => void;
  const undone = new Promise<boolean>((done) => (resolve = done));
  return {
    undone,
    dismiss: vi.fn(() => resolve(false)),
    expire: () => resolve(false),
    undo: () => resolve(true),
  };
}
