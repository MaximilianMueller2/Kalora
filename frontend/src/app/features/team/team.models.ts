import { SalonRole } from '../auth/auth.models';
import { Weekday } from '../../shared/week-schedule/week-schedule.model';

export interface StaffMember {
  id: string;
  displayName: string;
  color: string;
  isBookableOnline: boolean;
  autoConfirm: boolean;
  isActive: boolean;
  sortOrder: number;
  hasLogin: boolean;
  role: SalonRole | null;
  isMe: boolean;
  invitationPending: boolean;
}

export interface CreateStaffRequest {
  displayName: string;
  color: string;
  isBookableOnline: boolean;
  autoConfirm: boolean;
  linkToCurrentUser: boolean;
}

export interface SaveStaffRequest {
  displayName: string;
  color: string;
  isBookableOnline: boolean;
  autoConfirm: boolean;
  isActive: boolean;
}

export interface InviteRequest {
  staffId: string;
  email: string;
  role: Exclude<SalonRole, 'Owner'>;
}

export interface StaffSkill {
  serviceId: string;
  durationOverrideMinutes: number | null;
}

export interface StaffSkills {
  skills: StaffSkill[];
}

export interface PlanDay {
  weekday: Weekday;
  blocks: { start: string; end: string }[];
}

/** A weekly plan valid from a date until the next version starts. */
export interface ScheduleVersion {
  validFrom: string;
  validTo: string | null;
  days: PlanDay[];
}

export interface StaffSchedule {
  versions: ScheduleVersion[];
}

export interface SaveScheduleRequest {
  validFrom: string;
  days: PlanDay[];
}

export type AbsenceType = 'Vacation' | 'Sick' | 'Private';
export const ABSENCE_TYPES: readonly AbsenceType[] = ['Vacation', 'Sick', 'Private'];

export interface Absence {
  id: string;
  startDate: string;
  endDate: string;
  type: AbsenceType;
  conflictCount: number;
}

export interface CreateAbsenceRequest {
  startDate: string;
  endDate: string;
  type: AbsenceType;
}

export interface AffectedAppointment {
  id: string;
  startUtc: string;
  endUtc: string;
  serviceName: string;
  customerName: string;
  status: 'Requested' | 'Confirmed';
}

export interface AbsenceCreated {
  absence: Absence;
  affectedAppointments: AffectedAppointment[];
}

/** Calendar colors to choose from; every one keeps readable text on top in light and dark mode. */
export const STAFF_COLORS = [
  '#7C5CFC',
  '#2563EB',
  '#0891B2',
  '#059669',
  '#65A30D',
  '#D97706',
  '#DC2626',
  '#DB2777',
  '#9333EA',
  '#475569',
] as const;

export const MAX_ABSENCE_DAYS = 366;
