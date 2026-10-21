export interface NavItem {
  path: string;
  icon: string;
  /** Translation key of the label (shared texts, see public/i18n/<lang>.json). */
  labelKey: string;
}

export const NAV_ITEMS: readonly NavItem[] = [
  { path: 'dashboard', icon: 'dashboard', labelKey: 'nav.dashboard' },
  { path: 'calendar', icon: 'calendar_month', labelKey: 'nav.calendar' },
  { path: 'requests', icon: 'inbox', labelKey: 'nav.requests' },
  { path: 'services', icon: 'content_cut', labelKey: 'nav.services' },
  { path: 'team', icon: 'group', labelKey: 'nav.team' },
  { path: 'settings', icon: 'settings', labelKey: 'nav.settings' },
];
