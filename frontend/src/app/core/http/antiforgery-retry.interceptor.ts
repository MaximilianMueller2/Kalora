import {
  HttpContextToken,
  HttpErrorResponse,
  HttpInterceptorFn,
  HttpXsrfTokenExtractor,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { isApiUrl } from './api-client';
import { AntiforgeryService } from './antiforgery.service';
import { XSRF_HEADER_NAME } from './xsrf';

const ANTIFORGERY_RETRIED = new HttpContextToken<boolean>(() => false);
const ANTIFORGERY_FAILED = 'security.antiforgery_failed';

/**
 * Innermost interceptor: when the server refuses a writing request because the antiforgery token is missing or
 * belongs to another user (first write, after login or logout), it fetches a fresh token and repeats the request once.
 */
export const antiforgeryRetryInterceptor: HttpInterceptorFn = (req, next) => {
  const antiforgery = inject(AntiforgeryService);
  const tokens = inject(HttpXsrfTokenExtractor);

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

      // Angular's own XSRF interceptor sits further out and has already run for this request, before the cookie
      // existed. So the fresh token is put into the repeated request here.
      return antiforgery.refresh().pipe(
        switchMap(() => {
          const token = tokens.getToken();
          return next(
            req.clone({
              headers: token ? req.headers.set(XSRF_HEADER_NAME, token) : req.headers,
              context: req.context.set(ANTIFORGERY_RETRIED, true),
            }),
          );
        }),
      );
    }),
  );
};
