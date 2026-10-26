import { Routes } from '@angular/router';

/** The tabs of the settings page. */
export const SETTINGS_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'salon' },
  { path: 'salon', loadComponent: () => import('./tabs/salon-tab').then((m) => m.SalonTab) },
  { path: 'hours', loadComponent: () => import('./tabs/hours-tab').then((m) => m.HoursTab) },
  { path: 'booking', loadComponent: () => import('./tabs/rules-tab').then((m) => m.RulesTab) },
  {
    path: 'closures',
    loadComponent: () => import('./tabs/closures-tab').then((m) => m.ClosuresTab),
  },
];
