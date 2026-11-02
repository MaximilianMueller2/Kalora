import { TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { provideTestI18n } from '../../../testing/test-i18n';
import { ApiError } from '../../core/http/api-error';
import { LanguageService } from '../../core/i18n/language.service';
import { SettingsService } from '../settings/settings.service';
import { PROFILE, fakeSettings } from './onboarding-fixtures';
import { ProfileStep } from './profile-step';
import { q, settle, typeInto } from './step-helpers';

async function mount(settings = fakeSettings()) {
  localStorage.clear();
  TestBed.configureTestingModule({
    providers: [provideTestI18n(), { provide: SettingsService, useValue: settings }],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(ProfileStep);
  const saved = vi.fn();
  fixture.componentInstance.saved.subscribe(saved);
  await settle(fixture);
  return { fixture, settings, saved };
}

const fill = (
  fixture: Awaited<ReturnType<typeof mount>>['fixture'],
  values: Record<string, string>,
) => {
  for (const [name, value] of Object.entries(values)) {
    typeInto(q<HTMLInputElement>(fixture, `[name=${name}]`)!, value);
  }
};

describe('ProfileStep', () => {
  it('shows what the salon already has', async () => {
    const { fixture } = await mount(
      fakeSettings({ ...PROFILE, street: 'Hauptstraße 1', industry: 'Barber' }),
    );

    expect(q<HTMLInputElement>(fixture, '[name=street]')!.value).toBe('Hauptstraße 1');
    expect(q<HTMLSelectElement>(fixture, '[name=industry]')!.value).toBe('Barber');
  });

  it('saves the industry, address and phone together with the rest of the profile and goes on', async () => {
    const { fixture, settings, saved } = await mount();

    typeInto(q<HTMLSelectElement>(fixture, '[name=industry]')!, 'Nails');
    fill(fixture, {
      street: ' Hauptstraße 1 ',
      postalCode: '10115',
      city: 'Berlin',
      phone: '+49 30 123456',
    });
    q<HTMLButtonElement>(fixture, 'button[type=submit]')!.click();
    await settle(fixture);

    expect(settings.updateProfile).toHaveBeenCalledWith({
      ...PROFILE,
      industry: 'Nails',
      street: 'Hauptstraße 1',
      postalCode: '10115',
      city: 'Berlin',
      phone: '+49 30 123456',
    });
    expect(saved).toHaveBeenCalledOnce();
  });

  it('wants all four fields and a plausible phone number before saving anything', async () => {
    const { fixture, settings, saved } = await mount();

    fill(fixture, { street: 'Hauptstraße 1', postalCode: '10115', city: 'Berlin' });
    q<HTMLButtonElement>(fixture, 'button[type=submit]')!.click();
    await settle(fixture);
    expect(q(fixture, '[role=alert]')).not.toBeNull();

    fill(fixture, { phone: 'ruf mich an' });
    q<HTMLButtonElement>(fixture, 'button[type=submit]')!.click();
    await settle(fixture);

    expect(q(fixture, '[role=alert]')).not.toBeNull();
    expect(settings.updateProfile).not.toHaveBeenCalled();
    expect(saved).not.toHaveBeenCalled();
  });

  it('stays on the step and shows the reason when the server refuses', async () => {
    const settings = fakeSettings();
    settings.updateProfile.mockRejectedValue(new ApiError(400, 'validation.invalid'));
    const { fixture, saved } = await mount(settings);

    fill(fixture, {
      street: 'Hauptstraße 1',
      postalCode: '10115',
      city: 'Berlin',
      phone: '030 123456',
    });
    q<HTMLButtonElement>(fixture, 'button[type=submit]')!.click();
    await settle(fixture);

    expect(q(fixture, '[role=alert]')).not.toBeNull();
    expect(saved).not.toHaveBeenCalled();
  });

  it('says so when the profile cannot be loaded', async () => {
    const settings = fakeSettings();
    settings.getProfile.mockRejectedValue(new Error('offline'));
    const { fixture } = await mount(settings);

    expect(q(fixture, '[role=alert]')?.textContent).toContain('Unbekannter Fehler');
    expect(q(fixture, 'form')).toBeNull();
  });
});
