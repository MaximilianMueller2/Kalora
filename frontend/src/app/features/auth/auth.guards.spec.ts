import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  CanActivateFn,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { describe, expect, it } from 'vitest';
import { OWNER, STAFF, fakeAuth } from '../../../testing/auth-fixtures';
import { Me } from './auth.models';
import { authGuard, guestGuard, roleGuard } from './auth.guards';
import { AuthService } from './auth.service';

/** Runs a guard as the router would, for the given signed-in user (or nobody). */
async function runGuard(guard: CanActivateFn, user: Me | null, url = '/app/team') {
  const auth = fakeAuth(user);
  TestBed.configureTestingModule({
    providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
  });
  const result = await TestBed.runInInjectionContext(() =>
    guard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot),
  );
  const path = result instanceof UrlTree ? TestBed.inject(Router).serializeUrl(result) : result;
  return { path, auth };
}

describe('authGuard', () => {
  it('lets signed-in users through after the session has loaded', async () => {
    const { path, auth } = await runGuard(authGuard, OWNER);

    expect(path).toBe(true);
    expect(auth.ensureLoaded).toHaveBeenCalledOnce();
  });

  it('sends everybody else to the login page and remembers where they wanted to go', async () => {
    const { path } = await runGuard(authGuard, null, '/app/team?x=1');

    expect(path).toBe('/auth/login?returnUrl=%2Fapp%2Fteam%3Fx%3D1');
  });
});

describe('guestGuard', () => {
  it('allows visitors', async () => {
    expect((await runGuard(guestGuard, null)).path).toBe(true);
  });

  it('redirects signed-in users into the app', async () => {
    expect((await runGuard(guestGuard, OWNER)).path).toBe('/app');
  });
});

describe('roleGuard', () => {
  const managers = roleGuard('Owner', 'Manager');

  it('allows the listed roles', async () => {
    expect((await runGuard(managers, OWNER)).path).toBe(true);
  });

  it('sends other roles to the dashboard', async () => {
    expect((await runGuard(managers, STAFF)).path).toBe('/app/dashboard');
  });

  it('sends visitors to the login page', async () => {
    expect((await runGuard(managers, null, '/app/settings')).path).toBe(
      '/auth/login?returnUrl=%2Fapp%2Fsettings',
    );
  });
});
