import { Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { click, submit, texts, type } from '../../../../testing/forms';
import { provideTestI18n } from '../../../../testing/test-i18n';
import { ApiError } from '../../../core/http/api-error';
import { LanguageService } from '../../../core/i18n/language.service';
import { Catalog, Category, ServiceItem } from '../catalog.models';
import { CatalogService } from '../catalog.service';
import { CategoryDialog } from './category-dialog';
import { ConfirmDialog } from './confirm-dialog';
import { ServiceDialog } from './service-dialog';

const RESULT: Catalog = { categories: [] };

const EXISTING: ServiceItem = {
  id: 's1',
  categoryId: 'c2',
  name: 'Haarschnitt',
  description: 'Mit Waschen',
  priceCents: 2550,
  priceIsFrom: true,
  currency: 'EUR',
  durationMinutes: 45,
  bufferBeforeMinutes: 5,
  bufferAfterMinutes: 10,
  isBookableOnline: false,
  isActive: false,
  sortOrder: 0,
  hasAppointments: false,
};

const CATEGORIES: Category[] = [
  { id: 'c1', name: 'Damen', sortOrder: 0, services: [] },
  { id: 'c2', name: 'Herren', sortOrder: 1, services: [EXISTING] },
];

function fakeApi() {
  return {
    createCategory: vi.fn().mockResolvedValue(RESULT),
    renameCategory: vi.fn().mockResolvedValue(RESULT),
    createService: vi.fn().mockResolvedValue(RESULT),
    updateService: vi.fn().mockResolvedValue(RESULT),
  };
}

async function mount<T>(component: Type<T>, data: unknown, language: 'de' | 'en' | 'tr' = 'de') {
  localStorage.clear();
  const api = fakeApi();
  const dialogRef = { close: vi.fn() };
  TestBed.configureTestingModule({
    providers: [
      provideTestI18n(),
      { provide: CatalogService, useValue: api },
      { provide: MAT_DIALOG_DATA, useValue: data },
      { provide: MatDialogRef, useValue: dialogRef },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use(language);
  const fixture = TestBed.createComponent(component);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return { fixture, api, dialogRef };
}

const select = (fixture: ComponentFixture<unknown>, index: number): HTMLSelectElement =>
  fixture.nativeElement.querySelectorAll('select')[index];

function choose(fixture: ComponentFixture<unknown>, index: number, value: string): void {
  const element = select(fixture, index);
  element.value = value;
  element.dispatchEvent(new Event('input'));
  element.dispatchEvent(new Event('change'));
  fixture.detectChanges();
}

describe('CategoryDialog', () => {
  it('creates a category with the trimmed name and closes with the new catalog', async () => {
    const { fixture, api, dialogRef } = await mount(CategoryDialog, {});

    type(fixture, 'input', '  Damen ');
    await submit(fixture);

    expect(api.createCategory).toHaveBeenCalledWith('Damen');
    expect(dialogRef.close).toHaveBeenCalledWith(RESULT);
  });

  it('renames an existing category', async () => {
    const { fixture, api, dialogRef } = await mount(CategoryDialog, { category: CATEGORIES[1] });
    expect((fixture.nativeElement.querySelector('input') as HTMLInputElement).value).toBe('Herren');

    type(fixture, 'input', 'Männer');
    await submit(fixture);

    expect(api.renameCategory).toHaveBeenCalledWith('c2', 'Männer');
    expect(dialogRef.close).toHaveBeenCalledWith(RESULT);
  });

  it('does not save an empty name', async () => {
    const { fixture, api, dialogRef } = await mount(CategoryDialog, {});

    await submit(fixture);

    expect(texts(fixture, 'mat-error')).toEqual(['Dieses Feld ist erforderlich.']);
    expect(api.createCategory).not.toHaveBeenCalled();
    expect(dialogRef.close).not.toHaveBeenCalled();
  });
});

describe('ServiceDialog', () => {
  it('sends the price in cents and the chosen minutes, with the preselected category', async () => {
    const { fixture, api, dialogRef } = await mount(ServiceDialog, {
      categories: CATEGORIES,
      categoryId: 'c2',
    });

    type(fixture, 'input[type=text]', 'Färben');
    type(fixture, 'input[inputmode=decimal]', '49,5');
    choose(fixture, 1, '90');
    await submit(fixture);

    expect(api.createService).toHaveBeenCalledWith({
      categoryId: 'c2',
      name: 'Färben',
      description: null,
      priceCents: 4950,
      priceIsFrom: false,
      durationMinutes: 90,
      bufferBeforeMinutes: 0,
      bufferAfterMinutes: 0,
      isBookableOnline: true,
      isActive: true,
    });
    expect(dialogRef.close).toHaveBeenCalledWith(RESULT);
  });

  it('offers durations and buffers in 5-minute steps and keeps the buffers under "Advanced"', async () => {
    const { fixture } = await mount(ServiceDialog, { categories: CATEGORIES });

    expect(select(fixture, 1).options).toHaveLength(144);
    expect(select(fixture, 1).options[0].value).toBe('5');
    expect(select(fixture, 1).value).toBe('30');
    expect(select(fixture, 2).options).toHaveLength(49);
    const details = fixture.nativeElement.querySelector('details') as HTMLDetailsElement;
    expect(details.open).toBe(false);
    expect(details.contains(select(fixture, 2))).toBe(true);
  });

  it('fills the form when editing, opens the buffers and saves changes to the same service', async () => {
    const { fixture, api } = await mount(ServiceDialog, {
      categories: CATEGORIES,
      service: EXISTING,
    });

    expect(
      (fixture.nativeElement.querySelector('input[type=text]') as HTMLInputElement).value,
    ).toBe('Haarschnitt');
    expect(
      (fixture.nativeElement.querySelector('input[inputmode=decimal]') as HTMLInputElement).value,
    ).toBe('25,50');
    expect(select(fixture, 0).value).toBe('c2');
    expect(select(fixture, 1).value).toBe('45');
    expect(select(fixture, 2).value).toBe('5');
    expect(select(fixture, 3).value).toBe('10');
    expect((fixture.nativeElement.querySelector('details') as HTMLDetailsElement).open).toBe(true);

    type(fixture, 'input[inputmode=decimal]', '27');
    await submit(fixture);

    expect(api.updateService).toHaveBeenCalledWith('s1', {
      categoryId: 'c2',
      name: 'Haarschnitt',
      description: 'Mit Waschen',
      priceCents: 2700,
      priceIsFrom: true,
      durationMinutes: 45,
      bufferBeforeMinutes: 5,
      bufferAfterMinutes: 10,
      isBookableOnline: false,
      isActive: false,
    });
    expect(api.createService).not.toHaveBeenCalled();
  });

  it('writes the price with the separator of the active language', async () => {
    const { fixture } = await mount(
      ServiceDialog,
      { categories: CATEGORIES, service: EXISTING },
      'en',
    );

    expect(
      (fixture.nativeElement.querySelector('input[inputmode=decimal]') as HTMLInputElement).value,
    ).toBe('25.50');
  });

  it('refuses an empty name and an unreadable price without calling the API', async () => {
    const { fixture, api } = await mount(ServiceDialog, { categories: CATEGORIES });

    type(fixture, 'input[inputmode=decimal]', 'viel');
    await submit(fixture);

    expect(texts(fixture, 'mat-error')).toEqual([
      'Dieses Feld ist erforderlich.',
      'Ungültiger Preis.',
    ]);
    expect(api.createService).not.toHaveBeenCalled();
  });

  it('accepts a free service', async () => {
    const { fixture, api } = await mount(ServiceDialog, { categories: CATEGORIES });

    type(fixture, 'input[type=text]', 'Beratung');
    type(fixture, 'input[inputmode=decimal]', '0');
    await submit(fixture);

    expect(api.createService).toHaveBeenCalledWith(expect.objectContaining({ priceCents: 0 }));
  });

  it('keeps the dialog open and shows the translated error of the server', async () => {
    const { fixture, api, dialogRef } = await mount(ServiceDialog, {
      categories: CATEGORIES,
      service: EXISTING,
    });
    api.updateService.mockRejectedValue(new ApiError(409, 'service.has_appointments', 'x'));

    await submit(fixture);

    expect(texts(fixture, '[role=alert]')).toEqual(['Es gibt Termine.']);
    expect(dialogRef.close).not.toHaveBeenCalled();
  });
});

describe('ConfirmDialog', () => {
  it('shows the given texts and closes with true or false', async () => {
    const { fixture } = await mount(ConfirmDialog, {
      titleKey: 'services.service.deleteTitle',
      textKey: 'services.service.deleteText',
      textParams: { name: 'X' },
      confirmKey: 'services.service.delete',
    });

    expect(texts(fixture, 'h2')).toEqual(['services.service.deleteTitle']);
    expect(texts(fixture, 'mat-dialog-actions button')).toEqual([
      'services.cancel',
      'services.service.delete',
    ]);
    click(fixture, 'mat-dialog-actions button:last-child');
  });
});
