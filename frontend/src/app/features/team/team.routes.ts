import { Routes } from '@angular/router';
import { provideTranslocoScope } from '@jsverse/transloco';

/** `/app/team`: the list of people and, per person, tabs for skills, working hours and absences. */
export const TEAM_ROUTES: Routes = [
  {
    path: '',
    providers: [provideTranslocoScope('team'), provideTranslocoScope('services')],
    children: [
      {
        path: '',
        title: 'nav.team',
        loadComponent: () => import('./team-page').then((m) => m.TeamPage),
      },
      {
        path: ':id',
        title: 'nav.team',
        loadComponent: () => import('./team-detail-page').then((m) => m.TeamDetailPage),
        children: [
          { path: '', pathMatch: 'full', redirectTo: 'services' },
          {
            path: 'services',
            loadComponent: () => import('./skills-tab').then((m) => m.SkillsTab),
          },
          { path: 'hours', loadComponent: () => import('./hours-tab').then((m) => m.HoursTab) },
          {
            path: 'absences',
            loadComponent: () => import('./absences-tab').then((m) => m.AbsencesTab),
          },
        ],
      },
    ],
  },
];
