import { BreakpointObserver } from '@angular/cdk/layout';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { BehaviorSubject, firstValueFrom } from 'rxjs';
import { beforeEach, describe, expect, it } from 'vitest';
import { provideTestI18n } from '../../../../testing/test-i18n';
import { Shell } from './shell';

function setup(desktop: boolean): Promise<ComponentFixture<Shell>> {
  const state = new BehaviorSubject({ matches: desktop, breakpoints: {} });
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideTestI18n(),
      { provide: BreakpointObserver, useValue: { observe: () => state, isMatched: () => desktop } },
    ],
  });
  return firstValueFrom(TestBed.inject(TranslocoService).load('de')).then(() => {
    const fixture = TestBed.createComponent(Shell);
    fixture.detectChanges();
    return fixture;
  });
}

const links = (fixture: ComponentFixture<Shell>, selector: string) =>
  Array.from<HTMLAnchorElement>(fixture.nativeElement.querySelectorAll(selector));

describe('Shell', () => {
  beforeEach(() => localStorage.clear());

  it('shows the side navigation with all six areas on desktop', async () => {
    const fixture = await setup(true);

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

  it('shows the bottom navigation on phones and no side navigation', async () => {
    const fixture = await setup(false);

    expect(links(fixture, '.bottom-nav .bottom-link')).toHaveLength(6);
    expect(fixture.nativeElement.querySelector('.rail')).toBeNull();
    expect(fixture.nativeElement.querySelector('.topbar')).not.toBeNull();
  });

  it('is operable with a keyboard and announced to screen readers', async () => {
    const fixture = await setup(false);
    const root: HTMLElement = fixture.nativeElement;

    expect(root.querySelector('nav')?.getAttribute('aria-label')).toBe('Hauptnavigation');
    expect(root.querySelector('a.skip-link')?.getAttribute('href')).toBe('#main');
    expect(root.querySelector('main#main')?.getAttribute('tabindex')).toBe('-1');
    // Decorative icons are hidden from assistive technology; the text label carries the meaning.
    expect(
      Array.from(root.querySelectorAll('.bottom-link mat-icon')).every(
        (i) => i.getAttribute('aria-hidden') === 'true',
      ),
    ).toBe(true);
  });
});
