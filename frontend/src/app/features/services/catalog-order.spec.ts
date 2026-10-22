import { describe, expect, it } from 'vitest';
import { Catalog, Category, ServiceItem } from './catalog.models';
import { categoryIds, moveCategory, moveService, serviceIds } from './catalog-order';

const service = (id: string, categoryId: string, sortOrder: number): ServiceItem => ({
  id,
  categoryId,
  name: id,
  description: null,
  priceCents: 1000,
  priceIsFrom: false,
  currency: 'EUR',
  durationMinutes: 30,
  bufferBeforeMinutes: 0,
  bufferAfterMinutes: 0,
  isBookableOnline: true,
  isActive: true,
  sortOrder,
  hasAppointments: false,
});

const category = (id: string, sortOrder: number, services: string[]): Category => ({
  id,
  name: id,
  sortOrder,
  services: services.map((s, index) => service(s, id, index)),
});

const CATALOG: Catalog = {
  categories: [
    category('A', 0, ['a1', 'a2', 'a3']),
    category('B', 1, ['b1']),
    category('C', 2, []),
  ],
};

describe('catalog order', () => {
  it('moves a category and renumbers all', () => {
    const next = moveCategory(CATALOG, 0, 2);

    expect(categoryIds(next)).toEqual(['B', 'C', 'A']);
    expect(next.categories.map((c) => c.sortOrder)).toEqual([0, 1, 2]);
  });

  it('moves a service only inside its category', () => {
    const next = moveService(CATALOG, 'A', 2, 0);

    expect(serviceIds(next, 'A')).toEqual(['a3', 'a1', 'a2']);
    expect(next.categories[0].services.map((s) => s.sortOrder)).toEqual([0, 1, 2]);
    expect(next.categories[1]).toBe(CATALOG.categories[1]);
  });

  it('does not change the input', () => {
    moveCategory(CATALOG, 0, 1);
    moveService(CATALOG, 'A', 0, 1);

    expect(categoryIds(CATALOG)).toEqual(['A', 'B', 'C']);
    expect(serviceIds(CATALOG, 'A')).toEqual(['a1', 'a2', 'a3']);
  });

  it('ignores moves beyond the ends', () => {
    expect(categoryIds(moveCategory(CATALOG, 0, -1))).toEqual(['A', 'B', 'C']);
    expect(categoryIds(moveCategory(CATALOG, 2, 3))).toEqual(['A', 'B', 'C']);
    expect(serviceIds(moveService(CATALOG, 'A', 2, 3), 'A')).toEqual(['a1', 'a2', 'a3']);
  });

  it('returns no service IDs for an unknown category', () => {
    expect(serviceIds(CATALOG, 'X')).toEqual([]);
  });
});
