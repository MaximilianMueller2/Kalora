import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Role } from './auth.models';
import { AuthService } from './auth.service';

// `inject()` only works synchronously, so the dependencies are fetched before the first `await` in every guard.

/** Signed-in users only; everybody else goes to the login page and comes back afterwards. */
export const authGuard: CanActivateFn = async (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  await auth.ensureLoaded();
  return auth.isLoggedIn()
    ? true
    : router.createUrlTree(['/auth/login'], { queryParams: { returnUrl: state.url } });
};

/** For login and registration: somebody who is already signed in has no business there. */
export const guestGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  await auth.ensureLoaded();
  return auth.isLoggedIn() ? router.createUrlTree(['/app']) : true;
};

/** Page is for the given roles only; signed-in users with another role land on the dashboard. */
export const roleGuard =
  (...allowed: Role[]): CanActivateFn =>
  async (_route, state) => {
    const auth = inject(AuthService);
    const router = inject(Router);
    await auth.ensureLoaded();
    if (!auth.isLoggedIn()) {
      return router.createUrlTree(['/auth/login'], { queryParams: { returnUrl: state.url } });
    }
    return auth.hasAnyRole(allowed) ? true : router.createUrlTree(['/app/dashboard']);
  };
