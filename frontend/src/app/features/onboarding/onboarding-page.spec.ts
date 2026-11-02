import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { describe, expect, it } from 'vitest';
import { OWNER, fakeAuth } from '../../../testing/auth-fixtures';
import { provideTestI18n } from '../../../testing/test-i18n';
import { LanguageService } from '../../core/i18n/language.service';
import { AuthService } from '../auth/auth.service';
import { DashboardService } from '../dashboard/dashboard.service';
import { fakeDashboard } from '../dashboard/dashboard-fixtures';
import { CatalogService } from '../services/catalog.service';
import { SettingsService } from '../settings/settings.service';
import { TeamService } from '../team/team.service';
import { fakeTeam } from '../team/team-fixtures';
import { OnboardingPage } from './onboarding-page';
import { fakeCatalog, fakeOnboarding, fakeSettings, status } from './onboarding-fixtures';
import { OnboardingStatus } from './onboarding.models';
import { OnboardingService } from './onboarding.service';
import { q, qa, settle } from './step-helpers';

async function mount(current: OnboardingStatus, query: Record<string, string> = {}) {
  localStorage.clear();
  const onboarding = fakeOnboarding(current);
  TestBed.configureTestingModule({
    providers: [
      provideTestI18n(),
      provideRouter([]),
      { provide: OnboardingService, useValue: onboarding },
      { provide: SettingsService, useValue: fakeSettings() },
      { provide: CatalogService, useValue: fakeCatalog() },
      { provide: TeamService, useValue: fakeTeam() },
      { provide: DashboardService, useValue: fakeDashboard() },
      { provide: AuthService, useValue: fakeAuth(OWNER) },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { queryParamMap: convertToParamMap(query) } },
      },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(OnboardingPage);
  await settle(fixture);
  return { fixture, onboarding };
}

const shown = (fixture: Awaited<ReturnType<typeof mount>>['fixture']) =>
  ['profile', 'hours', 'services', 'team', 'done'].find((key) =>
    q(fixture, `app-onboarding-${key}-step`),
  );

describe('OnboardingPage', () => {
  it('starts at the first step that is still open', async () => {
    expect(shown((await mount(status())).fixture)).toBe('profile');
    TestBed.resetTestingModule();
    expect(shown((await mount(status('profile', 'hours'))).fixture)).toBe('services');
    TestBed.resetTestingModule();
    expect(shown((await mount(status('profile', 'hours', 'services'))).fixture)).toBe('team');
  });

  it('starts at the last screen when everything is done', async () => {
    const { fixture } = await mount(status('profile', 'hours', 'services', 'team'));

    expect(shown(fixture)).toBe('done');
  });

  it('opens the step named in the address, which is how the checklist links to it', async () => {
    const { fixture } = await mount(status('profile'), { step: '4' });

    expect(shown(fixture)).toBe('team');
  });

  it('ignores a step number that does not exist', async () => {
    const { fixture } = await mount(status(), { step: '9' });

    expect(shown(fixture)).toBe('profile');
  });

  it('shows five steps, marks the one that is open, and ticks the ones that are done', async () => {
    const { fixture } = await mount(status('profile'));

    const steps = qa(fixture, 'button.step');
    expect(steps).toHaveLength(5);
    expect(steps.map((b) => b.classList.contains('done'))).toEqual([
      true,
      false,
      false,
      false,
      false,
    ]);
    expect(steps.map((b) => b.getAttribute('aria-current'))).toEqual([
      null,
      'step',
      null,
      null,
      null,
    ]);
  });

  it('lets people jump to any step by its name', async () => {
    const { fixture } = await mount(status());

    qa<HTMLButtonElement>(fixture, 'button.step')[2].click();
    await settle(fixture);

    expect(shown(fixture)).toBe('services');
  });

  it('goes back and skips ahead without saving anything, but has no “back” on the first step', async () => {
    const { fixture, onboarding } = await mount(status('profile'));
    expect(q(fixture, 'button.back')).not.toBeNull(); // the first open step is "hours"

    q<HTMLButtonElement>(fixture, 'button.skip')!.click();
    await settle(fixture);
    expect(shown(fixture)).toBe('services');
    expect(onboarding.status).toHaveBeenCalledTimes(2);

    q<HTMLButtonElement>(fixture, 'button.back')!.click();
    q<HTMLButtonElement>(fixture, 'button.back')!.click();
    await settle(fixture);
    expect(shown(fixture)).toBe('profile');
    expect(q(fixture, 'button.back')).toBeNull();
  });

  it('can be left at any time for the dashboard', async () => {
    const { fixture } = await mount(status());

    expect(q<HTMLAnchorElement>(fixture, 'a.later')?.getAttribute('href')).toBe('/app/dashboard');
  });

  it('reads the progress again after a step, so the screen at the end is right', async () => {
    const { fixture, onboarding } = await mount(status('profile', 'hours', 'services'));
    onboarding.status.mockResolvedValue(status('profile', 'hours', 'services', 'team'));

    q<HTMLButtonElement>(fixture, 'button.skip')!.click();
    await settle(fixture);

    expect(shown(fixture)).toBe('done');
    expect(q(fixture, 'a.test-booking')).not.toBeNull();
  });

  it('says so when the progress cannot be read', async () => {
    localStorage.clear();
    const onboarding = fakeOnboarding();
    onboarding.status.mockRejectedValue(new Error('offline'));
    TestBed.configureTestingModule({
      providers: [
        provideTestI18n(),
        provideRouter([]),
        { provide: OnboardingService, useValue: onboarding },
        { provide: AuthService, useValue: fakeAuth(OWNER) },
      ],
    });
    await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
    const fixture = TestBed.createComponent(OnboardingPage);
    await settle(fixture);

    expect(q(fixture, '[role=alert]')?.textContent).toContain('Unbekannter Fehler');
  });
});
