import { describe, expect, it } from 'vitest';
import { initials, readableTextColor } from './staff-display';
import { STAFF_COLORS } from '../../features/team/team.models';

describe('initials', () => {
  it.each([
    ['Anna Müller', 'AM'],
    ['anna', 'A'],
    ['  anna   maria   schmidt ', 'AS'],
    ['Élodie Ünal', 'ÉÜ'],
    ['İpek', 'İ'],
    ['', '?'],
    ['   ', '?'],
  ])('%j -> %j', (name, expected) => {
    expect(initials(name)).toBe(expected);
  });

  it('keeps characters outside the basic plane whole', () => {
    expect(initials('😀 Smile')).toBe('😀S');
  });
});

describe('readableTextColor', () => {
  it('uses white on dark and black on light colors', () => {
    expect(readableTextColor('#000000')).toBe('#FFFFFF');
    expect(readableTextColor('#FFFFFF')).toBe('#000000');
    expect(readableTextColor('#FFEB3B')).toBe('#000000');
    expect(readableTextColor('#1E3A8A')).toBe('#FFFFFF');
  });

  it('falls back to white for anything that is not a hex color', () => {
    expect(readableTextColor('red')).toBe('#FFFFFF');
    expect(readableTextColor('#12')).toBe('#FFFFFF');
  });

  it('gives every offered calendar color at least WCAG AA contrast (4.5:1) for its text', () => {
    const luminance = (hex: string) => {
      const channel = (offset: number) => {
        const c = parseInt(hex.slice(offset, offset + 2), 16) / 255;
        return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
      };
      return 0.2126 * channel(1) + 0.7152 * channel(3) + 0.0722 * channel(5);
    };
    for (const color of STAFF_COLORS) {
      const text = readableTextColor(color);
      const [light, dark] = [luminance(color), luminance(text)].sort((a, b) => b - a);
      expect((light + 0.05) / (dark + 0.05), color).toBeGreaterThanOrEqual(4.5);
    }
  });
});
