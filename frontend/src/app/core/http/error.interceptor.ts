import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { SILENT_ERRORS, isApiUrl } from './api-client';
import { toApiError } from './api-error';
import { ErrorNotifier } from './error-notifier';

const UNAUTHORIZED = 401;

/** Turns failed API calls into typed {@link ApiError}s and tells the user about them unless the caller opted out. */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const notifier = inject(ErrorNotifier);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || !isApiUrl(req.url)) {
        return throwError(() => error);
      }

      const apiError = toApiError(error);
      // 401 is not an error message but a state: the auth flow sends the user to the login page.
      if (!req.context.get(SILENT_ERRORS) && apiError.status !== UNAUTHORIZED) {
        notifier.show(apiError);
      }
      return throwError(() => apiError);
    }),
  );
};
