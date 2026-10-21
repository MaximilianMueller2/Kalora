import { Type } from '@angular/core';
import { Route, Routes } from '@angular/router';
import { provideTranslocoScope } from '@jsverse/transloco';

/** Salon-area pages. `title` is a translation key; each feature loads its own texts together with its code. */
const areaRoute = (path: string, load: () => Promise<Type<unknown>>, scope: string): Route => ({
  path,
  title: `nav.${scope}`,
  loadComponent: load,
  providers: [provideTranslocoScope(scope)],
});

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'app' },
  {
    path: 'app',
    loadComponent: () => import('./core/layout/shell/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      areaRoute(
        'dashboard',
        () => import('./features/dashboard/dashboard-page').then((m) => m.DashboardPage),
        'dashboard',
      ),
      areaRoute(
        'calendar',
        () => import('./features/calendar/calendar-page').then((m) => m.CalendarPage),
        'calendar',
      ),
      areaRoute(
        'requests',
        () => import('./features/requests/requests-page').then((m) => m.RequestsPage),
        'requests',
      ),
      areaRoute(
        'services',
        () => import('./features/services/services-page').then((m) => m.ServicesPage),
        'services',
      ),
      areaRoute('team', () => import('./features/team/team-page').then((m) => m.TeamPage), 'team'),
      areaRoute(
        'settings',
        () => import('./features/settings/settings-page').then((m) => m.SettingsPage),
        'settings',
      ),
    ],
  },
  // Public booking page of a salon: kalora.app/<salon-slug>. Static routes above win; reserved slugs
  // (admin, app, auth, api, m, ...) are blocked in the backend (SlugGenerator).
  {
    path: ':slug',
    loadComponent: () =>
      import('./core/layout/public-layout/public-layout').then((m) => m.PublicLayout),
    children: [
      {
        path: '',
        title: 'booking.title',
        loadComponent: () => import('./features/booking/booking-page').then((m) => m.BookingPage),
        providers: [provideTranslocoScope('booking')],
      },
    ],
  },
  {
    path: '**',
    title: 'notFound.title',
    loadComponent: () => import('./features/not-found/not-found-page').then((m) => m.NotFoundPage),
  },
];
