import { BreakpointObserver } from '@angular/cdk/layout';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { BehaviorSubject, firstValueFrom } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { OWNER, STAFF, fakeAuth } from '../../../../testing/auth-fixtures';
import { provideTestI18n } from '../../../../testing/test-i18n';
import { Me } from '../../../features/auth/auth.models';
import { AuthService } from '../../../features/auth/auth.service';
import { Shell } from './shell';

async function setup(options: { desktop: boolean; user?: Me | null }) {
  const auth = fakeAuth(options.user === undefined ? OWNER : options.user);
  const state = new BehaviorSubject({ matches: options.desktop, breakpoints: {} });
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideTestI18n(),
      { provide: AuthService, useValue: auth },
      {
        provide: BreakpointObserver,
        useValue: { observe: () => state, isMatched: () => options.desktop },
      },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  const fixture = TestBed.createComponent(Shell);
  fixture.detectChanges();
  return { fixture, auth };
}

const links = (fixture: ComponentFixture<Shell>, selector: string) =>
  Array.from<HTMLAnchorElement>(fixture.nativeElement.querySelectorAll(selector));

describe('Shell', () => {
  beforeEach(() => localStorage.clear());

  it('shows the side navigation with all six areas to an owner on desktop', async () => {
    const { fixture } = await setup({ desktop: true });

    const nav = links(fixture, '.rail .nav-link');
    expect(nav.map((a) => a.querySelector('span')?.textContent?.trim())).toEqual([
      'Dashboard',
      'Kalender',
      'Anfragen',
      'Leistungen',
      'Team',
      'Einstellungen',
    ]);
    expect(nav.map((a) => a.getAttribute('href'))).toEqual([
      '/app/dashboard',
      '/app/calendar',
      '/app/requests',
      '/app/services',
      '/app/team',
      '/app/settings',
    ]);
    expect(fixture.nativeElement.querySelector('.bottom-nav')).toBeNull();
  });

  it('shows staff only the areas they may use', async () => {
    const { fixture } = await setup({ desktop: true, user: STAFF });

    expect(links(fixture, '.rail .nav-link').map((a) => a.getAttribute('href'))).toEqual([
      '/app/dashboard',
      '/app/calendar',
      '/app/requests',
    ]);
  });

  it('shows the bottom navigation on phones and no side navigation', async () => {
    const { fixture } = await setup({ desktop: false });

    expect(links(fixture, '.bottom-nav .bottom-link')).toHaveLength(6);
    expect(fixture.nativeElement.querySelector('.rail')).toBeNull();
    expect(fixture.nativeElement.querySelector('.topbar')).not.toBeNull();
  });

  it('shows who is signed in', async () => {
    const { fixture } = await setup({ desktop: true });

    expect(fixture.nativeElement.querySelector('.who')?.textContent).toContain('Maria Müller');
    expect(fixture.nativeElement.querySelector('.who')?.textContent).toContain('Haarwerk');
  });

  it('signs out and returns to the login page', async () => {
    const { fixture, auth } = await setup({ desktop: true });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    fixture.nativeElement.querySelector('button.logout').click();
    await fixture.whenStable();

    expect(auth.logout).toHaveBeenCalledOnce();
    expect(navigate).toHaveBeenCalledWith(['/auth/login']);
  });

  it('is operable with a keyboard and announced to screen readers', async () => {
    const { fixture } = await setup({ desktop: false });
    const root: HTMLElement = fixture.nativeElement;

    expect(root.querySelector('nav')?.getAttribute('aria-label')).toBe('Hauptnavigation');
    expect(root.querySelector('a.skip-link')?.getAttribute('href')).toBe('#main');
    expect(root.querySelector('main#main')?.getAttribute('tabindex')).toBe('-1');
    expect(root.querySelector('button[aria-label="Abmelden"]')).not.toBeNull();
    // Decorative icons are hidden from assistive technology; the text label carries the meaning.
    expect(
      Array.from(root.querySelectorAll('.bottom-link mat-icon')).every(
        (i) => i.getAttribute('aria-hidden') === 'true',
      ),
    ).toBe(true);
  });
});
