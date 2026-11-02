import { TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { provideTestI18n } from '../../../testing/test-i18n';
import { ApiError } from '../../core/http/api-error';
import { LanguageService } from '../../core/i18n/language.service';
import { CatalogService } from '../services/catalog.service';
import { SettingsService } from '../settings/settings.service';
import { EMPTY_CATALOG, PROFILE, fakeCatalog, fakeSettings } from './onboarding-fixtures';
import { q, qa, settle, typeInto } from './step-helpers';
import { ServicesStep } from './services-step';

async function mount(catalog = fakeCatalog(), industry: 'Hair' | 'Barber' = 'Hair') {
  localStorage.clear();
  TestBed.configureTestingModule({
    providers: [
      provideTestI18n(),
      { provide: CatalogService, useValue: catalog },
      { provide: SettingsService, useValue: fakeSettings({ ...PROFILE, industry }) },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(ServicesStep);
  const saved = vi.fn();
  fixture.componentInstance.saved.subscribe(saved);
  await settle(fixture);
  return { fixture, catalog, saved };
}

const priceInputs = (fixture: Awaited<ReturnType<typeof mount>>['fixture']) =>
  qa<HTMLInputElement>(fixture, '.price input');

describe('ServicesStep', () => {
  it('offers the template of the salon’s industry when there are no services yet, and then shows them', async () => {
    const { fixture, catalog } = await mount(fakeCatalog(EMPTY_CATALOG));
    expect(q(fixture, '.hint')?.textContent).toContain('onboarding.services.empty');
    expect(priceInputs(fixture)).toHaveLength(0);

    q<HTMLButtonElement>(fixture, 'button.template')!.click();
    await settle(fixture);

    expect(catalog.applyTemplate).toHaveBeenCalledWith(null);
    expect(qa(fixture, '.service .name div:first-child').map((e) => e.textContent?.trim())).toEqual(
      ['Haarschnitt', 'Färben'],
    );
    expect(q(fixture, 'button.template')).toBeNull();
  });

  it('shows the services by category with their prices in the field', async () => {
    const { fixture } = await mount();

    expect(qa(fixture, 'legend').map((e) => e.textContent?.trim())).toEqual(['Damen']);
    expect(priceInputs(fixture).map((input) => input.value)).toEqual(['45', '80']);
  });

  it('saves only the prices that were changed, and goes on', async () => {
    const { fixture, catalog, saved } = await mount();

    typeInto(priceInputs(fixture)[0], '49,50');
    q<HTMLButtonElement>(fixture, 'button.next')!.click();
    await settle(fixture);

    expect(catalog.updateService).toHaveBeenCalledOnce();
    expect(catalog.updateService).toHaveBeenCalledWith('s1', {
      categoryId: 'c1',
      name: 'Haarschnitt',
      description: null,
      priceCents: 4950,
      priceIsFrom: false,
      durationMinutes: 45,
      bufferBeforeMinutes: 0,
      bufferAfterMinutes: 5,
      isBookableOnline: true,
      isActive: true,
    });
    expect(saved).toHaveBeenCalledOnce();
  });

  it('goes on without saving anything when nothing was changed', async () => {
    const { fixture, catalog, saved } = await mount();

    q<HTMLButtonElement>(fixture, 'button.next')!.click();
    await settle(fixture);

    expect(catalog.updateService).not.toHaveBeenCalled();
    expect(saved).toHaveBeenCalledOnce();
  });

  it('does not accept something that is not a price', async () => {
    const { fixture, catalog } = await mount();

    typeInto(priceInputs(fixture)[1], 'viel');
    await settle(fixture);

    expect(priceInputs(fixture)[1].getAttribute('aria-invalid')).toBe('true');
    expect(q<HTMLButtonElement>(fixture, 'button.next')!.disabled).toBe(true);
    expect(catalog.updateService).not.toHaveBeenCalled();
  });

  it('stays on the step and shows the reason when saving a price fails', async () => {
    const catalog = fakeCatalog();
    catalog.updateService.mockRejectedValue(new ApiError(400, 'service.price_invalid'));
    const { fixture, saved } = await mount(catalog);

    typeInto(priceInputs(fixture)[0], '50');
    q<HTMLButtonElement>(fixture, 'button.next')!.click();
    await settle(fixture);

    expect(q(fixture, '[role=alert]')).not.toBeNull();
    expect(saved).not.toHaveBeenCalled();
  });

  it('shows the reason when the template cannot be applied', async () => {
    const catalog = fakeCatalog(EMPTY_CATALOG);
    catalog.applyTemplate.mockRejectedValue(new ApiError(400, 'catalog.template_unknown'));
    const { fixture } = await mount(catalog);

    q<HTMLButtonElement>(fixture, 'button.template')!.click();
    await settle(fixture);

    expect(q(fixture, '[role=alert]')).not.toBeNull();
    expect(q(fixture, 'button.template')).not.toBeNull();
  });

  it('says so when the services cannot be loaded', async () => {
    const catalog = fakeCatalog();
    catalog.get.mockRejectedValue(new Error('offline'));
    const { fixture } = await mount(catalog);

    expect(q(fixture, '[role=alert]')?.textContent).toContain('Unbekannter Fehler');
  });
});
