import { Observable, of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { ApiError } from '../../core/http/api-error';
import {
  BookingReceipt,
  PublicCategory,
  PublicDays,
  PublicSalon,
  PublicSlots,
  PublicStaff,
} from './booking.models';

export const SALON: PublicSalon = {
  name: 'Haarwerk Müller',
  slug: 'haarwerk',
  street: 'Hauptstraße 1',
  postalCode: '10115',
  city: 'Berlin',
  phone: '+49 30 123456',
  timeZone: 'Europe/Berlin',
  defaultLanguage: 'de',
  languages: ['de', 'en', 'tr'],
  openingHours: [
    { weekday: 'Monday', blocks: [{ start: '09:00', end: '18:00' }] },
    {
      weekday: 'Tuesday',
      blocks: [
        { start: '09:00', end: '12:00' },
        { start: '14:00', end: '18:00' },
      ],
    },
    { weekday: 'Wednesday', blocks: [] },
    { weekday: 'Thursday', blocks: [] },
    { weekday: 'Friday', blocks: [] },
    { weekday: 'Saturday', blocks: [] },
    { weekday: 'Sunday', blocks: [] },
  ],
  imprintText: 'Haarwerk Müller\nHauptstraße 1',
  privacyPolicyUrl: 'https://haarwerk.example/datenschutz',
};

export const CATEGORIES: PublicCategory[] = [
  {
    name: 'Damen',
    services: [
      {
        id: 's1',
        name: 'Haarschnitt',
        description: 'Mit Waschen',
        priceCents: 4500,
        priceIsFrom: false,
        currency: 'EUR',
        durationMinutes: 60,
      },
      {
        id: 's2',
        name: 'Färben',
        description: null,
        priceCents: 8000,
        priceIsFrom: true,
        currency: 'EUR',
        durationMinutes: 120,
      },
    ],
  },
  {
    name: 'Herren',
    services: [
      {
        id: 's3',
        name: 'Bart',
        description: null,
        priceCents: 1500,
        priceIsFrom: false,
        currency: 'EUR',
        durationMinutes: 30,
      },
    ],
  },
];

export const STAFF: PublicStaff[] = [
  { id: 'p1', displayName: 'Anna Beispiel', color: '#7C5CFC' },
  { id: 'p2', displayName: 'Ben', color: '#059669' },
];

export const DAYS: PublicDays = {
  month: '2026-11',
  days: ['2026-11-04', '2026-11-05', '2026-11-20'],
};

export const SLOTS: PublicSlots = {
  date: '2026-11-04',
  slots: [
    { startUtc: '2026-11-04T08:00:00Z', time: '09:00' },
    { startUtc: '2026-11-04T08:30:00Z', time: '09:30' },
    { startUtc: '2026-11-04T11:00:00Z', time: '12:00' },
    { startUtc: '2026-11-04T16:00:00Z', time: '17:00' },
  ],
};

export const RECEIPT: BookingReceipt = {
  status: 'Requested',
  startUtc: '2026-11-04T08:00:00Z',
  endUtc: '2026-11-04T09:00:00Z',
  serviceName: 'Haarschnitt',
  staffName: 'Anna Beispiel',
  manageToken: 'secret-token',
  respondByUtc: '2026-11-04T09:00:00Z',
};

/** Answers of the public API by path (without `/public/salons/<slug>`); a value can be an `ApiError` to make the call fail. */
export type Answers = Record<string, unknown>;

export function defaultAnswers(): Answers {
  return {
    '': SALON,
    '/services': CATEGORIES,
    '/staff': STAFF,
    '/availability/days': DAYS,
    '/availability/slots': SLOTS,
    '/appointment-requests': RECEIPT,
  };
}

/** Stands in for ApiClient: serves the answers synchronously and records the calls. */
export function fakeApi(answers: Answers = defaultAnswers()) {
  const respond = <T>(path: string): Observable<T> => {
    const key = path.replace(/^\/public\/salons\/[^/]+/, '');
    const answer = answers[key];
    return answer instanceof ApiError ? throwError(() => answer) : of(answer as T);
  };
  return {
    answers,
    get: vi.fn<(path: string, options?: unknown) => Observable<unknown>>((path) => respond(path)),
    post: vi.fn<(path: string, body?: unknown, options?: unknown) => Observable<unknown>>((path) =>
      respond(path),
    ),
  };
}
