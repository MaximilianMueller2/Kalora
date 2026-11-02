import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { describe, expect, it } from 'vitest';
import { OWNER, STAFF, fakeAuth } from '../../../testing/auth-fixtures';
import { provideTestI18n } from '../../../testing/test-i18n';
import { LanguageService } from '../../core/i18n/language.service';
import { Me } from '../auth/auth.models';
import { AuthService } from '../auth/auth.service';
import { OnboardingChecklist } from './onboarding-checklist';
import { fakeOnboarding, status } from './onboarding-fixtures';
import { OnboardingStatus } from './onboarding.models';
import { OnboardingService } from './onboarding.service';

async function mount(current: OnboardingStatus, user: Me = OWNER) {
  localStorage.clear();
  const onboarding = fakeOnboarding(current);
  TestBed.configureTestingModule({
    providers: [
      provideTestI18n(),
      provideRouter([]),
      { provide: OnboardingService, useValue: onboarding },
      { provide: AuthService, useValue: fakeAuth(user) },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(OnboardingChecklist);
  await settle(fixture);
  return { fixture, onboarding };
}

async function settle(fixture: ComponentFixture<unknown>) {
  fixture.detectChanges();
  await fixture.whenStable();
  await new Promise((resolve) => setTimeout(resolve));
  fixture.detectChanges();
}

const root = (fixture: ComponentFixture<unknown>) => fixture.nativeElement as HTMLElement;

describe('OnboardingChecklist', () => {
  it('lists what is done and what is open, each open one leading to its step of the assistant', async () => {
    const { fixture } = await mount(status('profile', 'hours'));

    const items = Array.from(root(fixture).querySelectorAll('li'));
    expect(items.map((li) => li.classList.contains('done'))).toEqual([true, true, false, false]);
    const open = Array.from(root(fixture).querySelectorAll<HTMLAnchorElement>('li a'));
    expect(open.map((a) => a.getAttribute('href'))).toEqual([
      '/app/onboarding?step=3',
      '/app/onboarding?step=4',
    ]);
    expect(root(fixture).querySelector('p')?.textContent).toContain('onboarding.checklist.lead');
    expect(root(fixture).querySelector('a.continue')?.getAttribute('href')).toBe('/app/onboarding');
  });

  it('is gone once the salon can be booked', async () => {
    const { fixture } = await mount(status('profile', 'hours', 'services', 'team'));

    expect(root(fixture).querySelector('.card')).toBeNull();
  });

  it('does not ask for staff members, who do not set the salon up', async () => {
    const { fixture, onboarding } = await mount(status(), STAFF);

    expect(onboarding.status).not.toHaveBeenCalled();
    expect(root(fixture).querySelector('.card')).toBeNull();
  });

  it('stays quiet if the progress cannot be read', async () => {
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
    const fixture = TestBed.createComponent(OnboardingChecklist);
    await settle(fixture);

    expect(root(fixture).querySelector('.card')).toBeNull();
  });
});
