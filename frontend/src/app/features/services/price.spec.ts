import { describe, expect, it } from 'vitest';
import {
  centsToInputText,
  isValidBuffer,
  isValidDuration,
  minuteOptions,
  parsePriceToCents,
} from './price';

describe('parsePriceToCents', () => {
  it.each([
    ['25', 2500],
    ['0', 0],
    ['25,5', 2550],
    ['25.50', 2550],
    ['25,05', 2505],
    ['0,99', 99],
    [' € 25 ', 2500],
    ['25 EUR', 2500],
    ['1.250', 125000],
    ['1.250,00', 125000],
    ['1,250.50', 125050],
    ['10000', 1_000_000],
  ])('reads %j as %i cents', (text, cents) => {
    expect(parsePriceToCents(text)).toBe(cents);
  });

  it.each([
    '',
    '   ',
    'abc',
    '-5',
    '25,',
    ',5',
    '1,2,3',
    '25.555',
    '10000,01',
    '99999999999999999999',
  ])('rejects %j', (text) => {
    expect(parsePriceToCents(text)).toBeNull();
  });

  it('does not lose cents to floating point errors', () => {
    expect(parsePriceToCents('19,99')).toBe(1999);
    expect(parsePriceToCents('0,29')).toBe(29);
    expect(parsePriceToCents('1.15')).toBe(115);
  });
});

describe('centsToInputText', () => {
  it('omits decimals for whole euros and uses the given separator otherwise', () => {
    expect(centsToInputText(2500, ',')).toBe('25');
    expect(centsToInputText(2550, ',')).toBe('25,50');
    expect(centsToInputText(2505, '.')).toBe('25.05');
    expect(centsToInputText(0, ',')).toBe('0');
  });

  it('round-trips through the parser', () => {
    for (const cents of [0, 1, 99, 100, 1999, 125000, 1_000_000]) {
      expect(parsePriceToCents(centsToInputText(cents, ','))).toBe(cents);
    }
  });
});

describe('minute rules', () => {
  it('accepts 5-minute steps within the limits, like the API', () => {
    expect(isValidDuration(5)).toBe(true);
    expect(isValidDuration(720)).toBe(true);
    expect([0, 3, 32, 725, -5, 1.5].some(isValidDuration)).toBe(false);
    expect(isValidBuffer(0)).toBe(true);
    expect(isValidBuffer(240)).toBe(true);
    expect([3, -5, 245, 2.5].some(isValidBuffer)).toBe(false);
  });

  it('lists the selectable minutes', () => {
    expect(minuteOptions(0, 15)).toEqual([0, 5, 10, 15]);
    expect(minuteOptions(5, 720)).toHaveLength(144);
  });
});
