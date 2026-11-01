import { Appointment, AppointmentStatus } from '../requests/requests.models';

export interface Span {
  startUtc: string;
  endUtc: string;
}

/** An appointment of today, as much as a timeline needs. */
export interface DashboardAppointment extends Span {
  id: string;
  status: AppointmentStatus;
  serviceName: string;
  customerName: string;
}

export interface DashboardPerson {
  staffId: string;
  displayName: string;
  color: string;
  /** Working time of today. */
  working: Span[];
  /** Free time left today, from now on. */
  gaps: Span[];
  appointments: DashboardAppointment[];
}

/** The week from Monday to Sunday; the revenue is an estimate from list prices. */
export interface DashboardWeek {
  from: string;
  to: string;
  appointments: number;
  bookedMinutes: number;
  availableMinutes: number;
  utilizationPercent: number;
  noShows: number;
  estimatedRevenueCents: number;
  currency: string;
  /** Some prices are "from" prices, so the real amount may be higher. */
  includesFromPrices: boolean;
}

export interface Dashboard {
  today: { date: string; timeZone: string; people: DashboardPerson[] };
  requests: { count: number; items: Appointment[] };
  week: DashboardWeek;
  /** Only owners and managers get the link to hand out. */
  bookingLink: { url: string } | null;
}
