import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { provideTestI18n } from '../../../testing/test-i18n';
import { LanguageService } from '../../core/i18n/language.service';
import { DashboardService } from '../dashboard/dashboard.service';
import { fakeDashboard } from '../dashboard/dashboard-fixtures';
import { DoneStep } from './done-step';
import { status } from './onboarding-fixtures';
import { OnboardingStatus } from './onboarding.models';
import { q, qa, settle } from './step-helpers';

async function mount(current: OnboardingStatus) {
  localStorage.clear();
  TestBed.configureTestingModule({
    providers: [
      provideTestI18n(),
      provideRouter([]),
      { provide: DashboardService, useValue: fakeDashboard() },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(DoneStep);
  fixture.componentRef.setInput('status', current);
  fixture.componentRef.setInput('salonName', 'Haarwerk Müller');
  const goTo = vi.fn();
  fixture.componentInstance.goTo.subscribe(goTo);
  await settle(fixture);
  return { fixture, goTo };
}

describe('DoneStep', () => {
  it('congratulates, offers a test booking on the public page in a new tab, and shows the link and QR code', async () => {
    const { fixture } = await mount(status('profile', 'hours', 'services', 'team'));

    expect(q(fixture, 'h2')?.textContent).toContain('onboarding.done.title');
    const test = q<HTMLAnchorElement>(fixture, 'a.test-booking')!;
    expect(test.getAttribute('href')).toBe('https://kalora.app/haarwerk');
    expect(test.target).toBe('_blank');
    expect(test.rel).toContain('noopener');
    expect(q<HTMLAnchorElement>(fixture, 'a.dashboard')?.getAttribute('href')).toBe(
      '/app/dashboard',
    );
    expect(q(fixture, 'app-booking-link-card')).not.toBeNull();
  });

  it('says what is still missing, instead of congratulating, and offers to open each missing step', async () => {
    const { fixture, goTo } = await mount(status('profile', 'services'));

    expect(q(fixture, 'h2')?.textContent).toContain('onboarding.done.missingTitle');
    expect(q(fixture, 'a.test-booking')).toBeNull();
    const buttons = qa<HTMLButtonElement>(fixture, '.missing button');
    expect(buttons.map((b) => b.textContent?.trim())).toEqual([
      'onboarding.steps.hours',
      'onboarding.steps.team',
    ]);

    buttons[1].click();

    expect(goTo).toHaveBeenCalledWith('team');
  });
});
