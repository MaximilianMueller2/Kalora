import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { OWNER, STAFF } from '../../../testing/auth-fixtures';
import { provideTestI18n } from '../../../testing/test-i18n';
import { ApiError } from '../../core/http/api-error';
import { ErrorNotifier } from '../../core/http/error-notifier';
import { errorInterceptor } from '../../core/http/error.interceptor';
import { LanguageService } from '../../core/i18n/language.service';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let auth: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideTestI18n(),
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        { provide: ErrorNotifier, useValue: { show: vi.fn() } },
      ],
    });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const answerAntiforgery = () =>
    http.expectOne('/api/antiforgery').flush(null, { status: 204, statusText: 'No Content' });
  const flushAsync = () => new Promise((resolve) => setTimeout(resolve));

  describe('session', () => {
    it('loads the signed-in user once and exposes it as signals', async () => {
      const first = auth.ensureLoaded();
      const second = auth.ensureLoaded();
      http.expectOne('/api/me').flush(OWNER);
      await Promise.all([first, second]);

      expect(auth.isLoggedIn()).toBe(true);
      expect(auth.currentUser()?.email).toBe('maria@example.com');
      expect(auth.roles()).toEqual(['Owner']);
    });

    it('treats a 401 as "nobody is signed in"', async () => {
      const loading = auth.ensureLoaded();
      http
        .expectOne('/api/me')
        .flush({ code: 'auth.unauthorized' }, { status: 401, statusText: 'Unauthorized' });
      await loading;

      expect(auth.isLoggedIn()).toBe(false);
      expect(auth.roles()).toEqual([]);
    });

    it('derives roles, including the platform admin flag, and checks them', async () => {
      const loading = auth.ensureLoaded();
      http.expectOne('/api/me').flush({ ...STAFF, isPlatformAdmin: true });
      await loading;

      expect(auth.roles()).toEqual(['Staff', 'PlatformAdmin']);
      expect(auth.hasAnyRole(['Owner', 'Manager'])).toBe(false);
      expect(auth.hasAnyRole(['Owner', 'PlatformAdmin'])).toBe(true);
    });

    it('uses the profile language unless this device has an explicit choice', async () => {
      const loading = auth.ensureLoaded();
      http.expectOne('/api/me').flush({ ...OWNER, preferredLanguage: 'tr' });
      await loading;

      expect(TestBed.inject(LanguageService).current()).toBe('tr');
    });
  });

  describe('login and logout', () => {
    it('signs in, stores the user and fetches a fresh antiforgery token', async () => {
      const login = auth.login({
        email: 'maria@example.com',
        password: 'secret-password',
        rememberMe: true,
      });
      const request = http.expectOne('/api/auth/login');
      expect(request.request.body).toEqual({
        email: 'maria@example.com',
        password: 'secret-password',
        rememberMe: true,
      });
      request.flush(OWNER);
      await flushAsync();
      answerAntiforgery();

      await expect(login).resolves.toEqual(OWNER);
      expect(auth.isLoggedIn()).toBe(true);
      // Already known after login: guards must not ask the server again.
      await auth.ensureLoaded();
    });

    it('hands the error code to the caller and stays signed out when login fails', async () => {
      const login = auth.login({ email: 'a@b.de', password: 'wrong', rememberMe: false });
      http
        .expectOne('/api/auth/login')
        .flush({ code: 'auth.invalid_credentials' }, { status: 401, statusText: 'Unauthorized' });

      const error = await login.catch((e: unknown) => e);
      expect(error).toBeInstanceOf(ApiError);
      expect((error as ApiError).code).toBe('auth.invalid_credentials');
      expect(auth.isLoggedIn()).toBe(false);
    });

    it('logs out on the server first, then clears the user', async () => {
      const signedIn = auth.login({ email: 'a@b.de', password: 'x', rememberMe: false });
      http.expectOne('/api/auth/login').flush(OWNER);
      await flushAsync();
      answerAntiforgery();
      await signedIn;

      const logout = auth.logout();
      http.expectOne('/api/auth/logout').flush(null, { status: 204, statusText: 'No Content' });
      await flushAsync();
      answerAntiforgery();
      await logout;

      expect(auth.isLoggedIn()).toBe(false);
    });
  });

  describe('session expiry', () => {
    it('sends a signed-in user to the login page with the current URL as return target', async () => {
      const signedIn = auth.login({ email: 'a@b.de', password: 'x', rememberMe: false });
      http.expectOne('/api/auth/login').flush(OWNER);
      await flushAsync();
      answerAntiforgery();
      await signedIn;
      const router = TestBed.inject(Router);
      vi.spyOn(router, 'url', 'get').mockReturnValue('/app/requests');
      const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);

      await auth.sessionExpired();

      expect(auth.isLoggedIn()).toBe(false);
      expect(navigate).toHaveBeenCalledWith(['/auth/login'], {
        queryParams: { returnUrl: '/app/requests', reason: 'expired' },
      });
    });

    it('does nothing when nobody was signed in', async () => {
      const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

      await auth.sessionExpired();

      expect(navigate).not.toHaveBeenCalled();
    });
  });

  describe('account requests', () => {
    it('posts the confirmation, reset and invitation requests to the right endpoints', async () => {
      void auth.confirmEmail('u1', 't0ken');
      expect(http.expectOne('/api/auth/confirm-email').request.body).toEqual({
        userId: 'u1',
        token: 't0ken',
      });

      void auth.forgotPassword('a@b.de');
      expect(http.expectOne('/api/auth/forgot-password').request.body).toEqual({ email: 'a@b.de' });

      void auth.resetPassword('a@b.de', 't0ken', 'new-password-123');
      expect(http.expectOne('/api/auth/reset-password').request.body).toEqual({
        email: 'a@b.de',
        token: 't0ken',
        newPassword: 'new-password-123',
      });

      void auth.getInvitation('a/b+c');
      expect(http.expectOne('/api/public/invitations/a%2Fb%2Bc').request.method).toBe('GET');
    });

    it('accepts an invitation with the current UI language and signs the new user in', async () => {
      const accept = auth.acceptInvitation('tok', 'staff-password-1');
      const request = http.expectOne('/api/public/invitations/tok/accept');
      expect(request.request.body).toEqual({ password: 'staff-password-1', language: 'de' });
      request.flush(STAFF);
      await flushAsync();
      answerAntiforgery();

      await expect(accept).resolves.toEqual(STAFF);
      expect(auth.roles()).toEqual(['Staff']);
    });
  });
});
