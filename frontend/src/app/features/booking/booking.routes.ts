import { Routes } from '@angular/router';
import { provideTranslocoScope } from '@jsverse/transloco';
import { PublicLayout } from '../../core/layout/public-layout/public-layout';
import { requireReceipt, requireService, requireSlot } from './booking.guards';
import { BookingShell } from './booking-shell';
import { ServiceStep } from './service-step';

/**
 * `/:slug`: the plain public frame, the salon's header, and below it the four steps, the receipt and the imprint.
 * The frame, the header and the first step are imported directly, so the page the customer lands on arrives in one
 * download instead of one after the other; the later steps load when they are needed.
 */
export const BOOKING_ROUTES: Routes = [
  {
    path: '',
    component: PublicLayout,
    providers: [provideTranslocoScope('booking')],
    children: [
      {
        path: '',
        component: BookingShell,
        children: [
          { path: '', title: 'booking.steps.service.title', component: ServiceStep },
          {
            path: 'person',
            title: 'booking.steps.person.title',
            canActivate: [requireService],
            loadComponent: () => import('./person-step').then((m) => m.PersonStep),
          },
          {
            path: 'time',
            title: 'booking.steps.time.title',
            canActivate: [requireService],
            loadComponent: () => import('./time-step').then((m) => m.TimeStep),
          },
          {
            path: 'details',
            title: 'booking.steps.details.title',
            canActivate: [requireSlot],
            loadComponent: () => import('./details-step').then((m) => m.DetailsStep),
          },
          {
            path: 'done',
            title: 'booking.done.title',
            canActivate: [requireReceipt],
            loadComponent: () => import('./done-step').then((m) => m.DoneStep),
          },
          {
            path: 'imprint',
            title: 'booking.imprint',
            loadComponent: () => import('./imprint-page').then((m) => m.ImprintPage),
          },
        ],
      },
    ],
  },
];
