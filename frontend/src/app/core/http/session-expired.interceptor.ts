import { HttpInterceptorFn } from '@angular/common/http';
import { Injector, inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../../features/auth/auth.service';
import { isApiUrl } from './api-client';
import { ApiError } from './api-error';

/** Sits outside the error interceptor: a 401 for a signed-in user means the session is gone, so go to the login page. */
export const sessionExpiredInterceptor: HttpInterceptorFn = (req, next) => {
  // Resolved lazily: AuthService itself uses HttpClient, so injecting it eagerly here would be circular.
  const injector = inject(Injector);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (isApiUrl(req.url) && error instanceof ApiError && error.status === 401) {
        void injector.get(AuthService).sessionExpired();
      }
      return throwError(() => error);
    }),
  );
};
