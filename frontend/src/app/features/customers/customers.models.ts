import { AppointmentStatus } from '../requests/requests.models';

export interface CustomerListItem {
  id: string;
  name: string;
  email: string | null;
  phone: string | null;
  lastVisitAtUtc: string | null;
  appointments: number;
  noShows: number;
}

export interface CustomerList {
  items: CustomerListItem[];
  /** There are more customers than shown; narrow the search. */
  hasMore: boolean;
}

export interface CustomerAppointment {
  id: string;
  status: AppointmentStatus;
  startUtc: string;
  endUtc: string;
  serviceName: string;
  staffName: string | null;
  priceCents: number;
  priceIsFrom: boolean;
  currency: string;
  customerNote: string | null;
  internalNote: string | null;
}

export interface CustomerDetail {
  id: string;
  name: string;
  email: string | null;
  phone: string | null;
  preferredLanguage: string;
  createdAtUtc: string;
  lastVisitAtUtc: string | null;
  noShows: number;
  marketingConsentAtUtc: string | null;
  anonymized: boolean;
  appointments: CustomerAppointment[];
}
