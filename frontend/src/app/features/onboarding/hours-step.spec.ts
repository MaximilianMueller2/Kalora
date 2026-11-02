import { TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { provideTestI18n } from '../../../testing/test-i18n';
import { ApiError } from '../../core/http/api-error';
import { LanguageService } from '../../core/i18n/language.service';
import {
  WEEKDAYS,
  WeekSchedule,
  emptySchedule,
} from '../../shared/week-schedule/week-schedule.model';
import { SettingsService } from '../settings/settings.service';
import { HoursStep, suggestedHours } from './hours-step';
import { fakeSettings } from './onboarding-fixtures';
import { q, settle } from './step-helpers';

async function mount(settings = fakeSettings()) {
  localStorage.clear();
  TestBed.configureTestingModule({
    providers: [provideTestI18n(), { provide: SettingsService, useValue: settings }],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(HoursStep);
  const saved = vi.fn();
  fixture.componentInstance.saved.subscribe(saved);
  await settle(fixture);
  return { fixture, settings, saved };
}

describe('suggestedHours', () => {
  it('is Tuesday to Saturday from 9 to 18 and closed on Sunday and Monday', () => {
    const week = suggestedHours();

    expect(WEEKDAYS.filter((day) => week[day].length > 0)).toEqual([
      'Tuesday',
      'Wednesday',
      'Thursday',
      'Friday',
      'Saturday',
    ]);
    expect(week.Tuesday).toEqual([{ start: '09:00', end: '18:00' }]);
  });

  it('gives a new copy every time, so editing one week never changes the next suggestion', () => {
    suggestedHours().Tuesday[0].start = '07:00';

    expect(suggestedHours().Tuesday[0].start).toBe('09:00');
  });
});

describe('HoursStep', () => {
  it('suggests Tuesday to Saturday for a salon without opening hours, and says it is a suggestion', async () => {
    const { fixture } = await mount();

    expect(q(fixture, '.lead')?.textContent).toContain('onboarding.hours.suggested');
    expect(q(fixture, 'app-week-schedule-editor')).not.toBeNull();
  });

  it('saves the suggestion as it is with one click', async () => {
    const { fixture, settings, saved } = await mount();

    q<HTMLButtonElement>(fixture, '.actions button')!.click();
    await settle(fixture);

    expect(settings.updateOpeningHours).toHaveBeenCalledWith(suggestedHours());
    expect(saved).toHaveBeenCalledOnce();
  });

  it('shows the hours the salon already has instead of the suggestion', async () => {
    const own: WeekSchedule = { ...emptySchedule(), Monday: [{ start: '08:00', end: '12:00' }] };
    const settings = fakeSettings();
    settings.getOpeningHours.mockResolvedValue(own);
    const { fixture, settings: used } = await mount(settings);

    expect(q(fixture, '.lead')?.textContent).toContain('onboarding.hours.lead');
    q<HTMLButtonElement>(fixture, '.actions button')!.click();
    await settle(fixture);

    expect(used.updateOpeningHours).toHaveBeenCalledWith(own);
  });

  it('stays on the step and shows the reason when saving fails', async () => {
    const settings = fakeSettings();
    settings.updateOpeningHours.mockRejectedValue(new ApiError(400, 'opening_hours.overlap'));
    const { fixture, saved } = await mount(settings);

    q<HTMLButtonElement>(fixture, '.actions button')!.click();
    await settle(fixture);

    expect(q(fixture, '[role=alert]')).not.toBeNull();
    expect(saved).not.toHaveBeenCalled();
  });

  it('says so when the hours cannot be loaded', async () => {
    const settings = fakeSettings();
    settings.getOpeningHours.mockRejectedValue(new Error('offline'));
    const { fixture } = await mount(settings);

    expect(q(fixture, '[role=alert]')?.textContent).toContain('Unbekannter Fehler');
  });
});
