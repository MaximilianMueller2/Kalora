export type SalonRole = 'Owner' | 'Manager' | 'Staff';
export type Role = SalonRole | 'PlatformAdmin';
export type SalonIndustry = 'Hair' | 'Barber' | 'Nails' | 'Lashes' | 'Beauty';

export const SALON_INDUSTRIES: readonly SalonIndustry[] = [
  'Hair',
  'Barber',
  'Nails',
  'Lashes',
  'Beauty',
];

export type SalonAccess = 'Full' | 'TrialExpired' | 'Suspended' | 'Cancelled';

export interface MeSalon {
  id: string;
  name: string;
  slug: string;
  status: 'Trial' | 'Active' | 'PastDue' | 'Suspended' | 'Cancelled';
  planCode: 'Solo' | 'Team' | 'SalonPlus';
  trialEndsAtUtc: string | null;
  /**
   * What the salon may do right now. Anything but `Full` means it can only look at its data
   * (trial over, suspended, cancelled); the server refuses changes.
   */
  access: SalonAccess;
  /** IANA time zone of the salon; times from the API are UTC and shown in this zone. */
  timeZone: string;
}

/** The signed-in user as returned by `GET /api/me`. */
export interface Me {
  userId: string;
  email: string;
  displayName: string;
  emailConfirmed: boolean;
  preferredLanguage: string;
  twoFactorEnabled: boolean;
  isPlatformAdmin: boolean;
  salon: MeSalon | null;
  role: SalonRole | null;
  staffId: string | null;
}

export interface RegisterRequest {
  salonName: string;
  industry: SalonIndustry;
  name: string;
  email: string;
  password: string;
  language: string;
  acceptTerms: boolean;
}

export interface LoginRequest {
  email: string;
  password: string;
  rememberMe: boolean;
  twoFactorCode?: string;
  recoveryCode?: string;
}

export interface InvitationPreview {
  salonName: string;
  staffDisplayName: string;
  email: string;
  role: SalonRole;
  expiresAtUtc: string;
}
