import { Weekday } from '../../shared/week-schedule/week-schedule.model';

export interface OpeningDay {
  weekday: Weekday;
  blocks: { start: string; end: string }[];
}

/** What the public API says about a salon. */
export interface PublicSalon {
  name: string;
  slug: string;
  street: string;
  postalCode: string;
  city: string;
  phone: string;
  timeZone: string;
  defaultLanguage: string;
  languages: string[];
  openingHours: OpeningDay[];
  imprintText: string | null;
  privacyPolicyUrl: string | null;
}

export interface PublicService {
  id: string;
  name: string;
  description: string | null;
  priceCents: number;
  priceIsFrom: boolean;
  currency: string;
  durationMinutes: number;
}

export interface PublicCategory {
  name: string;
  services: PublicService[];
}

export interface PublicStaff {
  id: string;
  displayName: string;
  color: string;
}

export interface PublicDays {
  month: string;
  days: string[];
}

export interface PublicSlot {
  startUtc: string;
  /** Local time at the salon, "09:30". */
  time: string;
}

export interface PublicSlots {
  date: string;
  slots: PublicSlot[];
}

export interface AppointmentRequest {
  serviceId: string;
  startUtc: string;
  staffId: string | null;
  name: string;
  email: string;
  phone: string | null;
  note: string | null;
  language: string;
  /** Honeypot: stays empty for people. */
  website: string;
}

export interface BookingReceipt {
  status: 'Requested' | 'Confirmed';
  startUtc: string;
  endUtc: string;
  serviceName: string;
  staffName: string;
  manageToken: string;
  respondByUtc: string | null;
}
