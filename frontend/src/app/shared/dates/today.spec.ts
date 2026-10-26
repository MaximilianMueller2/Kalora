import { describe, expect, it } from 'vitest';
import { todayInZone } from './today';

describe('todayInZone', () => {
  const instant = new Date('2026-11-03T23:30:00Z');

  it('is the calendar day in the given zone, not in UTC', () => {
    expect(todayInZone('Europe/Berlin', instant)).toBe('2026-11-04');
    expect(todayInZone('UTC', instant)).toBe('2026-11-03');
    expect(todayInZone('America/New_York', instant)).toBe('2026-11-03');
    expect(todayInZone('Pacific/Auckland', instant)).toBe('2026-11-04');
  });

  it('pads month and day', () => {
    expect(todayInZone('UTC', new Date('2026-01-05T12:00:00Z'))).toBe('2026-01-05');
  });
});
