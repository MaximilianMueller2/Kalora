import { CATALOG_LIMITS } from './catalog.models';

const PLAIN_PRICE = /^(\d+)(?:[.,](\d{1,2}))?$/;
const GROUPED_PRICE = /^(\d{1,3}(?:[.,]\d{3})+)(?:[.,](\d{1,2}))?$/;

/**
 * Converts what a person typed ("25", "25,5", "25.50", "1.250,00", "€ 25") to cents, or null if it is not a price.
 * One or two digits after the last separator are the decimals; three digits mean thousands grouping.
 */
export function parsePriceToCents(text: string): number | null {
  const cleaned = text.replace(/[\s€$£₺]|EUR|TRY/gi, '');
  const match = PLAIN_PRICE.exec(cleaned) ?? GROUPED_PRICE.exec(cleaned);
  if (!match) {
    return null;
  }

  const euros = Number(match[1].replace(/[.,]/g, ''));
  const cents = euros * 100 + Number((match[2] ?? '').padEnd(2, '0'));
  return cents <= CATALOG_LIMITS.maxPriceCents ? cents : null;
}

/** Text for an input field: whole euros without decimals, else two decimals, with the language's separator. */
export function centsToInputText(cents: number, decimalSeparator: string): string {
  const euros = Math.floor(cents / 100);
  const rest = cents % 100;
  return rest === 0 ? String(euros) : `${euros}${decimalSeparator}${String(rest).padStart(2, '0')}`;
}

export function isValidDuration(minutes: number): boolean {
  return (
    Number.isInteger(minutes) &&
    minutes >= CATALOG_LIMITS.stepMinutes &&
    minutes <= CATALOG_LIMITS.maxDurationMinutes &&
    minutes % CATALOG_LIMITS.stepMinutes === 0
  );
}

export function isValidBuffer(minutes: number): boolean {
  return (
    Number.isInteger(minutes) &&
    minutes >= 0 &&
    minutes <= CATALOG_LIMITS.maxBufferMinutes &&
    minutes % CATALOG_LIMITS.stepMinutes === 0
  );
}

/** All selectable durations/buffers in 5-minute steps. */
export function minuteOptions(from: number, to: number): number[] {
  const options: number[] = [];
  for (let minutes = from; minutes <= to; minutes += CATALOG_LIMITS.stepMinutes) {
    options.push(minutes);
  }
  return options;
}
