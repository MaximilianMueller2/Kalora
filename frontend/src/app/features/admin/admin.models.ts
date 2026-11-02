import { SalonAccess } from '../auth/auth.models';

export type SalonStatus = 'Trial' | 'Active' | 'PastDue' | 'Suspended' | 'Cancelled';
export type PlanCode = 'Solo' | 'Team' | 'SalonPlus';

export const SALON_STATUSES: readonly SalonStatus[] = [
  'Trial',
  'Active',
  'PastDue',
  'Suspended',
  'Cancelled',
];
export const PLAN_CODES: readonly PlanCode[] = ['Solo', 'Team', 'SalonPlus'];

export interface AdminSalonItem {
  id: string;
  name: string;
  slug: string;
  status: SalonStatus;
  planCode: PlanCode;
  access: SalonAccess;
  staffCount: number;
  /** What the plan allows (during the trial: what the biggest plan allows). */
  staffLimit: number;
  appointmentsThisMonth: number;
  trialEndsAtUtc: string | null;
  createdAtUtc: string;
  ownerEmail: string | null;
  hasNote: boolean;
}

export interface AdminSalonList {
  items: AdminSalonItem[];
  /** There are more salons than shown; narrow the search. */
  hasMore: boolean;
}

/** Master data and subscription of one salon. No customer data. */
export interface AdminSalonDetail {
  id: string;
  name: string;
  slug: string;
  city: string;
  phone: string;
  timeZone: string;
  status: SalonStatus;
  planCode: PlanCode;
  access: SalonAccess;
  staffCount: number;
  staffLimit: number;
  appointmentsThisMonth: number;
  trialEndsAtUtc: string | null;
  createdAtUtc: string;
  ownerName: string | null;
  ownerEmail: string | null;
  adminNote: string | null;
}
