import { Type } from '@angular/core';
import { Route, Routes } from '@angular/router';
import { provideTranslocoScope } from '@jsverse/transloco';
import { authGuard, guestGuard, roleGuard } from './features/auth/auth.guards';

/** Salon-area pages. `title` is a translation key; each feature loads its own texts together with its code. */
const areaRoute = (
  path: string,
  load: () => Promise<Type<unknown>>,
  scope: string,
  guard?: Route['canActivate'],
): Route => ({
  path,
  title: `nav.${scope}`,
  loadComponent: load,
  providers: [provideTranslocoScope(scope)],
  ...(guard ? { canActivate: guard } : {}),
});

/** Services, team and settings are for owners and managers; staff only work with the calendar and requests. */
const MANAGEMENT = [roleGuard('Owner', 'Manager')];

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'app' },
  {
    path: 'app',
    canActivate: [authGuard],
    loadComponent: () => import('./core/layout/shell/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'onboarding',
        title: 'title.onboarding',
        loadComponent: () =>
          import('./features/onboarding/onboarding-page').then((m) => m.OnboardingPage),
        providers: [provideTranslocoScope('onboarding')],
      },
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
        MANAGEMENT,
      ),
      areaRoute(
        'team',
        () => import('./features/team/team-page').then((m) => m.TeamPage),
        'team',
        MANAGEMENT,
      ),
      {
        path: 'settings',
        title: 'nav.settings',
        canActivate: MANAGEMENT,
        loadComponent: () =>
          import('./features/settings/settings-page').then((m) => m.SettingsPage),
        providers: [provideTranslocoScope('settings')],
        children: [
          { path: '', pathMatch: 'full', redirectTo: 'salon' },
          {
            path: 'salon',
            loadComponent: () =>
              import('./features/settings/tabs/salon-tab').then((m) => m.SalonTab),
          },
          {
            path: 'hours',
            loadComponent: () =>
              import('./features/settings/tabs/hours-tab').then((m) => m.HoursTab),
          },
          {
            path: 'booking',
            loadComponent: () =>
              import('./features/settings/tabs/rules-tab').then((m) => m.RulesTab),
          },
          {
            path: 'closures',
            loadComponent: () =>
              import('./features/settings/tabs/closures-tab').then((m) => m.ClosuresTab),
          },
        ],
      },
    ],
  },
  {
    path: 'auth',
    loadComponent: () => import('./core/layout/auth-layout').then((m) => m.AuthLayout),
    providers: [provideTranslocoScope('auth')],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'login' },
      {
        path: 'login',
        title: 'title.login',
        canActivate: [guestGuard],
        loadComponent: () => import('./features/auth/pages/login-page').then((m) => m.LoginPage),
      },
      {
        path: 'register',
        title: 'title.register',
        canActivate: [guestGuard],
        loadComponent: () =>
          import('./features/auth/pages/register-page').then((m) => m.RegisterPage),
      },
      {
        path: 'forgot-password',
        title: 'title.forgotPassword',
        canActivate: [guestGuard],
        loadComponent: () =>
          import('./features/auth/pages/forgot-password-page').then((m) => m.ForgotPasswordPage),
      },
      {
        path: 'reset-password',
        title: 'title.resetPassword',
        loadComponent: () =>
          import('./features/auth/pages/reset-password-page').then((m) => m.ResetPasswordPage),
      },
      {
        path: 'confirm-email',
        title: 'title.confirmEmail',
        loadComponent: () =>
          import('./features/auth/pages/confirm-email-page').then((m) => m.ConfirmEmailPage),
      },
      {
        path: 'invitation/:token',
        title: 'title.invitation',
        loadComponent: () =>
          import('./features/auth/pages/invitation-page').then((m) => m.InvitationPage),
      },
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
