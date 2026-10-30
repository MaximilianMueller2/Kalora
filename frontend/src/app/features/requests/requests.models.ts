export type AppointmentStatus =
  'Requested' | 'Confirmed' | 'Declined' | 'Expired' | 'Cancelled' | 'Completed' | 'NoShow';

export interface AppointmentStaff {
  id: string;
  displayName: string;
  color: string;
}

export interface AppointmentCustomer {
  name: string;
  email: string | null;
  phone: string | null;
}

/** An appointment as the salon sees it (the API's AppointmentDto). */
export interface Appointment {
  id: string;
  status: AppointmentStatus;
  source: 'Online' | 'Manual';
  startUtc: string;
  endUtc: string;
  serviceName: string;
  priceCents: number;
  priceIsFrom: boolean;
  currency: string;
  staff: AppointmentStaff;
  customer: AppointmentCustomer;
  customerNote: string | null;
  internalNote: string | null;
  /** Until when an open request may be answered. */
  expiresAtUtc: string | null;
  createdAtUtc: string;
  nextStates: AppointmentStatus[];
}

export interface OpenRequestCount {
  count: number;
}

/** Longest reason the API accepts. */
export const MAX_REASON_LENGTH = 500;
