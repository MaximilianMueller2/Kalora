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

export interface MeSalon {
  id: string;
  name: string;
  slug: string;
  status: 'Trial' | 'Active' | 'PastDue' | 'Suspended' | 'Cancelled';
  planCode: 'Solo' | 'Team' | 'SalonPlus';
  trialEndsAtUtc: string | null;
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
