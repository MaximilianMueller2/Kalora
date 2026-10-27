import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { BookingService } from './booking.service';

function slugOf(route: Parameters<CanActivateFn>[0]): string {
  return (
    route.pathFromRoot
      .map((snapshot) => snapshot.params['slug'] as string | undefined)
      .find(Boolean) ?? ''
  );
}

/** A step may only open when what it needs has been chosen before; otherwise (reload, shared link) start at the beginning. */
const requires =
  (isReady: (booking: BookingService) => boolean): CanActivateFn =>
  (route) => {
    const booking = inject(BookingService);
    const router = inject(Router);
    const slug = slugOf(route).toLowerCase();
    booking.setSlug(slug);
    return isReady(booking) || router.createUrlTree(['/', slug]);
  };

export const requireService = requires((booking) => booking.selectedService() !== null);
export const requireSlot = requires(
  (booking) => booking.selectedService() !== null && booking.selectedSlot() !== null,
);
export const requireReceipt = requires((booking) => booking.receipt() !== null);
