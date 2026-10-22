import { SalonIndustry } from '../auth/auth.models';
import { Weekday } from '../../shared/week-schedule/week-schedule.model';

export interface SalonProfile {
  name: string;
  slug: string;
  industry: SalonIndustry;
  street: string;
  postalCode: string;
  city: string;
  phone: string;
  timeZone: string;
  defaultLanguage: string;
  imprintText: string | null;
  privacyPolicyUrl: string | null;
}

export interface BookingRules {
  slotIntervalMinutes: number;
  minLeadTimeMinutes: number;
  bookingHorizonDays: number;
  cancellationDeadlineHours: number;
  autoConfirm: boolean;
  requestExpiryHours: number;
  retentionMonths: number;
}

export interface OpeningDay {
  weekday: Weekday;
  blocks: { start: string; end: string }[];
}

export interface OpeningHoursPlan {
  days: OpeningDay[];
}

export interface Closure {
  id: string;
  date: string;
  reason: string | null;
}

export interface CreateClosureRequest {
  date: string;
  endDate: string | null;
  reason: string | null;
}

export const SLOT_INTERVALS = [5, 10, 15, 30] as const;

/** Time zones offered in the UI; a salon's current zone is always added if it is not in this list. */
export const TIME_ZONES = [
  'Europe/Berlin',
  'Europe/Vienna',
  'Europe/Zurich',
  'Europe/Amsterdam',
  'Europe/Brussels',
  'Europe/Paris',
  'Europe/Madrid',
  'Europe/Rome',
  'Europe/Warsaw',
  'Europe/Athens',
  'Europe/Istanbul',
  'Europe/London',
  'UTC',
] as const;
