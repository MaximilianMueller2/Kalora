import { HttpInterceptorFn } from '@angular/common/http';
import { isApiUrl } from './api-client';

/** API calls always carry the session cookie. */
export const credentialsInterceptor: HttpInterceptorFn = (req, next) =>
  isApiUrl(req.url) ? next(req.clone({ withCredentials: true })) : next(req);
