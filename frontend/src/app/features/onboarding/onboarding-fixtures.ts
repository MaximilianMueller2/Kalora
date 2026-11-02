import { vi } from 'vitest';
import { Catalog } from '../services/catalog.models';
import { OnboardingStatus, OnboardingStepKey } from './onboarding.models';

const ORDER: OnboardingStepKey[] = ['profile', 'hours', 'services', 'team'];

/** Status with the given steps done; the rest is open. */
export function status(...done: OnboardingStepKey[]): OnboardingStatus {
  return {
    steps: ORDER.map((key) => ({ key, done: done.includes(key) })),
    complete: done.length === ORDER.length,
    bookingUrl: 'https://kalora.app/haarwerk',
  };
}

export function fakeOnboarding(current: OnboardingStatus = status()) {
  return { status: vi.fn().mockResolvedValue(current) };
}

export const CATALOG: Catalog = {
  categories: [
    {
      id: 'c1',
      name: 'Damen',
      sortOrder: 0,
      services: [
        {
          id: 's1',
          categoryId: 'c1',
          name: 'Haarschnitt',
          description: null,
          priceCents: 4500,
          priceIsFrom: false,
          currency: 'EUR',
          durationMinutes: 45,
          bufferBeforeMinutes: 0,
          bufferAfterMinutes: 5,
          isBookableOnline: true,
          isActive: true,
          sortOrder: 0,
          hasAppointments: false,
        },
        {
          id: 's2',
          categoryId: 'c1',
          name: 'Färben',
          description: 'Mit Pflege',
          priceCents: 8000,
          priceIsFrom: true,
          currency: 'EUR',
          durationMinutes: 120,
          bufferBeforeMinutes: 0,
          bufferAfterMinutes: 0,
          isBookableOnline: true,
          isActive: true,
          sortOrder: 1,
          hasAppointments: false,
        },
      ],
    },
  ],
};

export const EMPTY_CATALOG: Catalog = { categories: [] };

import { emptySchedule } from '../../shared/week-schedule/week-schedule.model';
import { SalonProfile } from '../settings/settings.models';

export const PROFILE: SalonProfile = {
  name: 'Haarwerk',
  slug: 'haarwerk',
  industry: 'Hair',
  street: '',
  postalCode: '',
  city: '',
  phone: '',
  timeZone: 'Europe/Berlin',
  defaultLanguage: 'de',
  imprintText: null,
  privacyPolicyUrl: null,
  privacyText: null,
};

export function fakeSettings(profile: SalonProfile = PROFILE) {
  return {
    getProfile: vi.fn().mockResolvedValue(profile),
    updateProfile: vi.fn().mockImplementation((saved: SalonProfile) => Promise.resolve(saved)),
    getOpeningHours: vi.fn().mockResolvedValue(emptySchedule()),
    updateOpeningHours: vi
      .fn()
      .mockImplementation((schedule: unknown) => Promise.resolve(schedule)),
  };
}

export function fakeCatalog(current = CATALOG) {
  return {
    get: vi.fn().mockResolvedValue(current),
    applyTemplate: vi.fn().mockResolvedValue(CATALOG),
    updateService: vi.fn().mockImplementation((id: string, request: { priceCents: number }) =>
      Promise.resolve({
        categories: CATALOG.categories.map((category) => ({
          ...category,
          services: category.services.map((service) =>
            service.id === id ? { ...service, priceCents: request.priceCents } : service,
          ),
        })),
      }),
    ),
  };
}
