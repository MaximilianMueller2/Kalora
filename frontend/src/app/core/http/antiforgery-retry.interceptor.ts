import { HttpContextToken, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { isApiUrl } from './api-client';
import { AntiforgeryService } from './antiforgery.service';

const ANTIFORGERY_RETRIED = new HttpContextToken<boolean>(() => false);
const ANTIFORGERY_FAILED = 'security.antiforgery_failed';

/**
 * Innermost interceptor: when the server refuses a writing request because the antiforgery token is missing or
 * belongs to another user (first write, after login or logout), it fetches a fresh token and repeats the request once.
 */
export const antiforgeryRetryInterceptor: HttpInterceptorFn = (req, next) => {
  const antiforgery = inject(AntiforgeryService);

  return next(req).pipe(
    catchError((error: unknown) => {
      const rejected =
        error instanceof HttpErrorResponse &&
        isApiUrl(req.url) &&
        error.status === 400 &&
        (error.error as { code?: string } | null)?.code === ANTIFORGERY_FAILED &&
        !req.context.get(ANTIFORGERY_RETRIED);

      if (!rejected) {
        return throwError(() => error);
      }

      return antiforgery
        .refresh()
        .pipe(
          switchMap(() => next(req.clone({ context: req.context.set(ANTIFORGERY_RETRIED, true) }))),
        );
    }),
  );
};
