import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../features/auth/auth.service';
import { ApiClient } from './api-client';
import { ErrorNotifier } from './error-notifier';
import { errorInterceptor } from './error.interceptor';
import { sessionExpiredInterceptor } from './session-expired.interceptor';

describe('sessionExpiredInterceptor', () => {
  const sessionExpired = vi.fn();
  let api: ApiClient;
  let http: HttpTestingController;

  beforeEach(() => {
    sessionExpired.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([sessionExpiredInterceptor, errorInterceptor])),
        provideHttpClientTesting(),
        { provide: ErrorNotifier, useValue: { show: vi.fn() } },
        { provide: AuthService, useValue: { sessionExpired } },
      ],
    });
    api = TestBed.inject(ApiClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reports a 401 on an API call to the auth service', () => {
    api.get('/staff').subscribe({ error: () => undefined });

    http.expectOne('/api/staff').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(sessionExpired).toHaveBeenCalledOnce();
  });

  it.each([403, 404, 500])('ignores a %i', (status) => {
    api.get('/staff').subscribe({ error: () => undefined });

    http.expectOne('/api/staff').flush(null, { status, statusText: 'Error' });

    expect(sessionExpired).not.toHaveBeenCalled();
  });

  it('ignores 401 responses for files outside the API', () => {
    api.getAsset('/i18n/de.json').subscribe({ error: () => undefined });

    http.expectOne('/i18n/de.json').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(sessionExpired).not.toHaveBeenCalled();
  });
});
