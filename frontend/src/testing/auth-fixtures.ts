import { signal } from '@angular/core';
import { vi } from 'vitest';
import { Me, Role } from '../app/features/auth/auth.models';

export const OWNER: Me = {
  userId: 'u1',
  email: 'maria@example.com',
  displayName: 'Maria Müller',
  emailConfirmed: true,
  preferredLanguage: 'de',
  twoFactorEnabled: false,
  isPlatformAdmin: false,
  salon: {
    id: 's1',
    name: 'Haarwerk',
    slug: 'haarwerk',
    status: 'Trial',
    planCode: 'Solo',
    trialEndsAtUtc: null,
  },
  role: 'Owner',
  staffId: null,
};

export const STAFF: Me = {
  ...OWNER,
  userId: 'u2',
  displayName: 'Tom',
  role: 'Staff',
  staffId: 'st1',
};

/** Stands in for AuthService in component and guard specs. */
export function fakeAuth(user: Me | null = null) {
  const current = signal<Me | null>(user);
  return {
    currentUser: current.asReadonly(),
    isLoggedIn: () => current() !== null,
    ensureLoaded: vi.fn().mockResolvedValue(undefined),
    hasAnyRole: (allowed: readonly Role[]) => {
      const me = current();
      const mine: Role[] = me
        ? [
            ...(me.role ? [me.role] : []),
            ...(me.isPlatformAdmin ? (['PlatformAdmin'] as Role[]) : []),
          ]
        : [];
      return allowed.some((role) => mine.includes(role));
    },
    login: vi.fn(),
    register: vi.fn(),
    logout: vi.fn().mockResolvedValue(undefined),
    reload: vi.fn().mockResolvedValue(undefined),
    confirmEmail: vi.fn(),
    forgotPassword: vi.fn(),
    resetPassword: vi.fn(),
    getInvitation: vi.fn(),
    acceptInvitation: vi.fn(),
  };
}
