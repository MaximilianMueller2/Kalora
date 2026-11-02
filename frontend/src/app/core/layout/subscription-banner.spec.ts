import { TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { OWNER, STAFF, fakeAuth } from '../../../testing/auth-fixtures';
import { provideTestI18n } from '../../../testing/test-i18n';
import { Me, MeSalon } from '../../features/auth/auth.models';
import { AuthService } from '../../features/auth/auth.service';
import { LanguageService } from '../i18n/language.service';
import { SubscriptionBanner } from './subscription-banner';

const NOW = new Date('2026-11-03T09:00:00Z');

async function mount(user: Me, salon: Partial<MeSalon> = {}) {
  localStorage.clear();
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(NOW);
  TestBed.configureTestingModule({
    providers: [
      provideTestI18n(),
      {
        provide: AuthService,
        useValue: fakeAuth({ ...user, salon: { ...user.salon!, ...salon } }),
      },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(SubscriptionBanner);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('SubscriptionBanner', () => {
  afterEach(() => vi.useRealTimers());

  it('says nothing to a salon that works fully', async () => {
    expect((await mount(OWNER)).querySelector('.banner')).toBeNull();
  });

  it.each(['TrialExpired', 'Suspended', 'Cancelled'] as const)(
    'tells a salon that can only look why, for %s',
    async (access) => {
      const root = await mount(OWNER, { access });

      const banner = root.querySelector('.banner');
      expect(banner?.classList.contains('limited')).toBe(true);
      expect(banner?.textContent).toContain(`banner.limited.${access}`);
      expect(banner?.getAttribute('role')).toBe('note');
    },
  );

  it('also tells staff members, who cannot change anything either', async () => {
    expect(
      (await mount(STAFF, { access: 'Suspended' })).querySelector('.banner.limited'),
    ).not.toBeNull();
  });

  it('points owners to the end of the trial in its last week', async () => {
    const root = await mount(OWNER, { status: 'Trial', trialEndsAtUtc: '2026-11-09T09:00:00Z' });

    const banner = root.querySelector('.banner');
    expect(banner?.classList.contains('limited')).toBe(false);
    expect(banner?.textContent).toContain('banner.trialEnds');
  });

  it('is quiet about a trial that still has time, and to staff members', async () => {
    expect(
      (
        await mount(OWNER, { status: 'Trial', trialEndsAtUtc: '2026-12-01T09:00:00Z' })
      ).querySelector('.banner'),
    ).toBeNull();
    TestBed.resetTestingModule();

    expect(
      (
        await mount(STAFF, { status: 'Trial', trialEndsAtUtc: '2026-11-04T09:00:00Z' })
      ).querySelector('.banner'),
    ).toBeNull();
  });

  it('does not talk about the trial of an active salon', async () => {
    expect(
      (
        await mount(OWNER, { status: 'Active', trialEndsAtUtc: '2026-11-04T09:00:00Z' })
      ).querySelector('.banner'),
    ).toBeNull();
  });
});
