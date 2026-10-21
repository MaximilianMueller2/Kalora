import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from './api-client';

/**
 * Fetches the antiforgery cookie. Angular's built-in XSRF support then copies it into the X-XSRF-TOKEN header of
 * every writing request. Tokens are bound to the signed-in user, so a new one is needed after login and logout;
 * the retry interceptor asks for it whenever the server rejects a request for a missing or stale token.
 */
@Injectable({ providedIn: 'root' })
export class AntiforgeryService {
  private readonly api = inject(ApiClient);

  refresh(): Observable<void> {
    return this.api.get<void>('/antiforgery', { silent: true });
  }
}
