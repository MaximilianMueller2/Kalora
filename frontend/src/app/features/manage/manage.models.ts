import { AppointmentStatus } from '../requests/requests.models';

/** What the customer's own page shows and what they may do (the API's ManagedAppointmentDto). */
export interface ManagedAppointment {
  status: AppointmentStatus;
  startUtc: string;
  endUtc: string;
  serviceName: string;
  salonName: string;
  salonSlug: string;
  salonPhone: string;
  salonAddress: string;
  timeZone: string;
  /** The last moment to cancel online. */
  cancelUntilUtc: string | null;
  canCancel: boolean;
  canAddToCalendar: boolean;
  canRebook: boolean;
}
