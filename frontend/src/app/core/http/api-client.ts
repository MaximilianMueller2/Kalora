import { HttpClient, HttpContext, HttpContextToken } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

/** Set on requests whose errors the caller handles itself (e.g. form validation); no snackbar is shown for them. */
export const SILENT_ERRORS = new HttpContextToken<boolean>(() => false);

export interface ApiRequestOptions {
  params?: Record<string, string | number | boolean | undefined>;
  /** Suppress the global error snackbar; the caller shows the error. */
  silent?: boolean;
}

export const API_PREFIX = '/api';

export function isApiUrl(url: string): boolean {
  return url === API_PREFIX || url.startsWith(`${API_PREFIX}/`);
}

/** The only class that uses HttpClient. Everything else goes through feature services that use this. */
@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);

  get<T>(path: string, options?: ApiRequestOptions): Observable<T> {
    return this.http.get<T>(apiUrl(path), this.toHttpOptions(options));
  }

  post<T>(path: string, body?: unknown, options?: ApiRequestOptions): Observable<T> {
    return this.http.post<T>(apiUrl(path), body ?? {}, this.toHttpOptions(options));
  }

  put<T>(path: string, body?: unknown, options?: ApiRequestOptions): Observable<T> {
    return this.http.put<T>(apiUrl(path), body ?? {}, this.toHttpOptions(options));
  }

  delete<T>(path: string, options?: ApiRequestOptions): Observable<T> {
    return this.http.delete<T>(apiUrl(path), this.toHttpOptions(options));
  }

  /** Loads a static file of the app itself (e.g. translations); not an API call, so no API interceptors apply. */
  getAsset<T>(path: string): Observable<T> {
    return this.http.get<T>(path);
  }

  private toHttpOptions(options?: ApiRequestOptions) {
    const params = Object.fromEntries(
      Object.entries(options?.params ?? {}).filter(([, value]) => value !== undefined),
    ) as Record<string, string | number | boolean>;
    return {
      params,
      context: new HttpContext().set(SILENT_ERRORS, options?.silent ?? false),
    };
  }
}

export function apiUrl(path: string): string {
  return `${API_PREFIX}${path.startsWith('/') ? path : `/${path}`}`;
}
