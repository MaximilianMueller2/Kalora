import { Appointment } from '../requests/requests.models';

export interface CalendarSpan {
  startUtc: string;
  endUtc: string;
}

export type AbsenceType = 'Vacation' | 'Sick' | 'Private';

export interface CalendarAbsence extends CalendarSpan {
  id: string;
  type: AbsenceType;
}

export interface CalendarBlock extends CalendarSpan {
  id: string;
  label: string | null;
}

/** One person's column: when they work, when they are away, blockers and appointments. */
export interface CalendarColumn {
  staffId: string;
  displayName: string;
  color: string;
  working: CalendarSpan[];
  absences: CalendarAbsence[];
  blocks: CalendarBlock[];
  appointments: Appointment[];
}

/** The API's answer for a range of local days. */
export interface CalendarData {
  from: string;
  to: string;
  timeZone: string;
  slotMinutes: number;
  columns: CalendarColumn[];
}

export interface CustomerHit {
  id: string;
  name: string;
  email: string | null;
  phone: string | null;
}

export interface CreateManualAppointmentRequest {
  serviceId: string;
  staffId: string;
  startUtc: string;
  customerId: string | null;
  customerName: string | null;
  customerEmail: string | null;
  customerPhone: string | null;
  note: string | null;
}

export interface MoveAppointmentRequest {
  startUtc: string;
  staffId: string | null;
  reason: string | null;
}

export interface CreateBlockRequest {
  staffId: string;
  startUtc: string;
  endUtc: string;
  label: string | null;
}

export type CalendarView = 'day' | 'week';
