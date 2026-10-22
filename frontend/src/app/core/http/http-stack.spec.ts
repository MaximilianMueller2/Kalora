import { provideHttpClient, withInterceptors, withXsrfConfiguration } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { provideTestI18n } from '../../../testing/test-i18n';
import { antiforgeryRetryInterceptor } from './antiforgery-retry.interceptor';
import { ApiClient } from './api-client';
import { ApiError } from './api-error';
import { credentialsInterceptor } from './credentials.interceptor';
import { ErrorNotifier } from './error-notifier';
import { errorInterceptor } from './error.interceptor';
import { XSRF_COOKIE_NAME, XSRF_HEADER_NAME } from './xsrf';

describe('HTTP stack', () => {
  let api: ApiClient;
  let http: HttpTestingController;
  let notifier: { show: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    notifier = { show: vi.fn() };
    TestBed.configureTestingModule({
      providers: [
        provideTestI18n(),
        provideHttpClient(
          withInterceptors([credentialsInterceptor, errorInterceptor, antiforgeryRetryInterceptor]),
          withXsrfConfiguration({ cookieName: XSRF_COOKIE_NAME, headerName: XSRF_HEADER_NAME }),
        ),
        provideHttpClientTesting(),
        { provide: ErrorNotifier, useValue: notifier },
      ],
    });
    api = TestBed.inject(ApiClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  describe('ApiClient', () => {
    it('prefixes API paths with /api and drops undefined query parameters', () => {
      api.get('/salons', { params: { month: '2026-11', staff: undefined } }).subscribe();

      const request = http.expectOne((r) => r.url === '/api/salons');
      expect(request.request.params.get('month')).toBe('2026-11');
      expect(request.request.params.has('staff')).toBe(false);
      request.flush({});
    });

    it('also accepts paths without a leading slash', () => {
      api.get('me').subscribe();

      http.expectOne('/api/me').flush({});
    });

    it('loads static assets without the API prefix', () => {
      api.getAsset('/i18n/de.json').subscribe();

      http.expectOne('/i18n/de.json').flush({});
    });
  });

  describe('credentials', () => {
    it('sends the session cookie with API calls only', () => {
      api.get('/me').subscribe();
      api.getAsset('/i18n/de.json').subscribe();

      expect(http.expectOne('/api/me').request.withCredentials).toBe(true);
      const asset = http.expectOne('/i18n/de.json');
      expect(asset.request.withCredentials).toBe(false);
      asset.flush({});
      http.match('/api/me').forEach((r) => r.flush({}));
    });
  });

  describe('errors', () => {
    it('turns a ProblemDetails response into an ApiError and tells the user', () => {
      let caught: unknown;
      api.get('/salons').subscribe({ error: (error: unknown) => (caught = error) });

      http
        .expectOne('/api/salons')
        .flush({ code: 'booking.slot_unavailable' }, { status: 409, statusText: 'Conflict' });

      expect(caught).toBeInstanceOf(ApiError);
      expect((caught as ApiError).code).toBe('booking.slot_unavailable');
      expect(notifier.show).toHaveBeenCalledOnce();
    });

    it('stays quiet when the caller handles errors itself', () => {
      api.post('/auth/login', {}, { silent: true }).subscribe({ error: () => undefined });

      http
        .expectOne('/api/auth/login')
        .flush({ code: 'auth.invalid_credentials' }, { status: 401, statusText: 'Unauthorized' });

      expect(notifier.show).not.toHaveBeenCalled();
    });

    it('does not show 401 as an error message', () => {
      api.get('/me').subscribe({ error: () => undefined });

      http.expectOne('/api/me').flush(null, { status: 401, statusText: 'Unauthorized' });

      expect(notifier.show).not.toHaveBeenCalled();
    });

    it('reports a lost connection with the network code', () => {
      let caught: ApiError | undefined;
      api.get('/me').subscribe({ error: (error: ApiError) => (caught = error) });

      http.expectOne('/api/me').error(new ProgressEvent('error'));

      expect(caught?.isNetworkError).toBe(true);
      expect(notifier.show).toHaveBeenCalledOnce();
    });
  });

  describe('antiforgery', () => {
    const rejection = { code: 'security.antiforgery_failed' };

    afterEach(() => {
      document.cookie = `${XSRF_COOKIE_NAME}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/`;
    });

    it('fetches a new token and repeats a rejected write once, with the fresh token in the header', () => {
      let result: unknown;
      api.post('/auth/login', { email: 'a@b.de' }).subscribe((value) => (result = value));

      const first = http.expectOne('/api/auth/login');
      expect(first.request.headers.has(XSRF_HEADER_NAME)).toBe(false);
      first.flush(rejection, { status: 400, statusText: 'Bad Request' });
      // The server's answer to the token request sets the cookie. Angular's XSRF interceptor already ran for the
      // first attempt, so only the retry interceptor can put the new value into the repeated request.
      document.cookie = `${XSRF_COOKIE_NAME}=fresh-token; path=/`;
      http.expectOne('/api/antiforgery').flush(null, { status: 204, statusText: 'No Content' });
      const retry = http.expectOne('/api/auth/login');
      expect(retry.request.body).toEqual({ email: 'a@b.de' });
      expect(retry.request.headers.get(XSRF_HEADER_NAME)).toBe('fresh-token');
      retry.flush({ ok: true });

      expect(result).toEqual({ ok: true });
      expect(notifier.show).not.toHaveBeenCalled();
    });

    it('gives up after one retry and reports the error', () => {
      let caught: ApiError | undefined;
      api.post('/auth/login', {}).subscribe({ error: (error: ApiError) => (caught = error) });

      http
        .expectOne('/api/auth/login')
        .flush(rejection, { status: 400, statusText: 'Bad Request' });
      http.expectOne('/api/antiforgery').flush(null, { status: 204, statusText: 'No Content' });
      http
        .expectOne('/api/auth/login')
        .flush(rejection, { status: 400, statusText: 'Bad Request' });

      expect(caught?.code).toBe('security.antiforgery_failed');
      expect(notifier.show).toHaveBeenCalledOnce();
    });

    it('does not retry other 400 errors', () => {
      api.post('/auth/login', {}, { silent: true }).subscribe({ error: () => undefined });

      http
        .expectOne('/api/auth/login')
        .flush({ code: 'validation.required' }, { status: 400, statusText: 'Bad Request' });

      http.expectNone('/api/antiforgery');
    });
  });
});
