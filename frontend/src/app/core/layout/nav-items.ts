import { Role } from '../../features/auth/auth.models';

export interface NavItem {
  path: string;
  icon: string;
  /** Translation key of the label (shared texts, see public/i18n/<lang>.json). */
  labelKey: string;
  /** Visible only for these roles; without it everybody who is signed in sees the item. */
  roles?: readonly Role[];
  /** A number shown next to the label. */
  badge?: 'openRequests';
}

const MANAGEMENT: readonly Role[] = ['Owner', 'Manager'];

export const NAV_ITEMS: readonly NavItem[] = [
  { path: 'dashboard', icon: 'dashboard', labelKey: 'nav.dashboard' },
  { path: 'calendar', icon: 'calendar_month', labelKey: 'nav.calendar' },
  { path: 'requests', icon: 'inbox', labelKey: 'nav.requests', badge: 'openRequests' },
  { path: 'services', icon: 'content_cut', labelKey: 'nav.services', roles: MANAGEMENT },
  { path: 'team', icon: 'group', labelKey: 'nav.team', roles: MANAGEMENT },
  { path: 'absences', icon: 'event_busy', labelKey: 'nav.absences', roles: ['Staff'] },
  { path: 'settings', icon: 'settings', labelKey: 'nav.settings', roles: MANAGEMENT },
];
