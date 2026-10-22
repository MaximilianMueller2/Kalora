import { CdkDropList } from '@angular/cdk/drag-drop';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { MatDialog } from '@angular/material/dialog';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom, of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { texts } from '../../../testing/forms';
import { provideTestI18n } from '../../../testing/test-i18n';
import { ApiError } from '../../core/http/api-error';
import { ErrorNotifier } from '../../core/http/error-notifier';
import { LanguageService } from '../../core/i18n/language.service';
import { Toast } from '../../core/ui/toast';
import { Catalog, Category, ServiceItem } from './catalog.models';
import { CatalogService } from './catalog.service';
import { CategoryDialog } from './dialogs/category-dialog';
import { ServiceDialog } from './dialogs/service-dialog';
import { ServicesPage } from './services-page';

const service = (
  id: string,
  categoryId: string,
  sortOrder: number,
  patch: Partial<ServiceItem> = {},
): ServiceItem => ({
  id,
  categoryId,
  name: `Service ${id}`,
  description: null,
  priceCents: 3000,
  priceIsFrom: false,
  currency: 'EUR',
  durationMinutes: 90,
  bufferBeforeMinutes: 0,
  bufferAfterMinutes: 0,
  isBookableOnline: true,
  isActive: true,
  sortOrder,
  hasAppointments: false,
  ...patch,
});

const category = (id: string, sortOrder: number, services: ServiceItem[]): Category => ({
  id,
  name: `Category ${id}`,
  sortOrder,
  services,
});

const CATALOG: Catalog = {
  categories: [
    category('A', 0, [
      service('a1', 'A', 0),
      service('a2', 'A', 1, { priceIsFrom: true, isActive: false }),
      service('a3', 'A', 2, { isBookableOnline: false, hasAppointments: true }),
    ]),
    category('B', 1, []),
  ],
};

const NON_BREAKING_SPACES = new RegExp('[\\u00a0\\u202f]', 'g');

const EMPTY: Catalog = { categories: [] };

function fakeApi(initial: Catalog = CATALOG) {
  return {
    get: vi.fn().mockResolvedValue(initial),
    createCategory: vi.fn(),
    renameCategory: vi.fn(),
    deleteCategory: vi.fn().mockResolvedValue(EMPTY),
    reorderCategories: vi.fn().mockResolvedValue(CATALOG),
    createService: vi.fn(),
    updateService: vi.fn().mockResolvedValue(CATALOG),
    deleteService: vi.fn().mockResolvedValue(CATALOG),
    reorderServices: vi.fn().mockResolvedValue(CATALOG),
    applyTemplate: vi.fn().mockResolvedValue(CATALOG),
  };
}

async function mount(initial: Catalog = CATALOG, dialogResult: unknown = undefined) {
  localStorage.clear();
  const api = fakeApi(initial);
  const dialog = {
    open: vi.fn().mockImplementation(() => ({ afterClosed: () => of(dialogResult) })),
  };
  const toast = { show: vi.fn() };
  const notifier = { show: vi.fn() };
  TestBed.configureTestingModule({
    providers: [
      provideTestI18n(),
      { provide: CatalogService, useValue: api },
      { provide: MatDialog, useValue: dialog },
      { provide: Toast, useValue: toast },
      { provide: ErrorNotifier, useValue: notifier },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(ServicesPage);
  await settle(fixture);
  return { fixture, api, dialog, toast, notifier };
}

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
}

const names = (fixture: ComponentFixture<unknown>) => texts(fixture, '.service-name');

/** Opens the "more" menu of the service or category with the given name (menus render in the overlay). */
async function openMenu(
  fixture: ComponentFixture<unknown>,
  kind: 'service' | 'category',
  name: string,
): Promise<HTMLButtonElement[]> {
  const label = Array.from<HTMLElement>(
    fixture.nativeElement.querySelectorAll(
      kind === 'service' ? '.service-name' : '.category-head h2',
    ),
  ).find((element) => element.textContent?.trim() === name);
  const row = label?.closest(kind === 'service' ? 'li' : '.category-head');
  row?.querySelector<HTMLButtonElement>('button[aria-haspopup]')?.click();
  await settle(fixture);
  return Array.from(document.querySelectorAll<HTMLButtonElement>('button[mat-menu-item]'));
}

async function choose(
  fixture: ComponentFixture<unknown>,
  items: HTMLButtonElement[],
  textKey: string,
): Promise<void> {
  items.find((item) => item.textContent?.includes(textKey))?.click();
  await settle(fixture);
}

describe('ServicesPage', () => {
  beforeEach(() => {
    document
      .querySelectorAll('.cdk-overlay-container')
      .forEach((overlay) => (overlay.innerHTML = ''));
  });

  it('lists categories with their services, duration, price and state', async () => {
    const { fixture } = await mount();

    expect(texts(fixture, '.category-head h2')).toEqual(['Category A', 'Category B']);
    expect(names(fixture)).toEqual(['Service a1', 'Service a2', 'Service a3']);
    const meta = texts(fixture, '.service-meta').map((text) =>
      text.replace(NON_BREAKING_SPACES, ' ').replace(/\s+/g, ' '),
    );
    expect(meta[0]).toContain('1 Std. 30 Min.');
    expect(meta[0]).toContain('30,00 €');
    expect(meta[1]).toContain('services.service.priceFrom');
    expect(meta[1]).toContain('services.service.inactiveBadge');
    expect(meta[2]).toContain('services.service.internalBadge');
    expect(texts(fixture, '.no-services')).toEqual(['services.category.empty']);
  });

  it('offers the template when nothing exists yet and shows the result', async () => {
    const { fixture, api, toast } = await mount(EMPTY);
    expect(texts(fixture, '.empty h2')).toEqual(['services.empty.title']);

    (fixture.nativeElement.querySelector('.empty-actions button') as HTMLButtonElement).click();
    await settle(fixture);

    expect(api.applyTemplate).toHaveBeenCalledWith(null);
    expect(toast.show).toHaveBeenCalledWith('services.templateApplied');
    expect(names(fixture)).toEqual(['Service a1', 'Service a2', 'Service a3']);
    expect(fixture.nativeElement.querySelector('.empty')).toBeNull();
  });

  it('shows an error when the catalog cannot be loaded', async () => {
    localStorage.clear();
    const api = fakeApi();
    api.get.mockRejectedValue(new ApiError(500, 'unknown', 'x'));
    TestBed.configureTestingModule({
      providers: [provideTestI18n(), { provide: CatalogService, useValue: api }],
    });
    await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
    const fixture = TestBed.createComponent(ServicesPage);
    await settle(fixture);

    expect(texts(fixture, '[role=alert]')).toEqual(['Unbekannter Fehler']);
  });

  it('opens the service dialog for the category and takes over the saved catalog', async () => {
    const saved: Catalog = { categories: [category('A', 0, [service('new', 'A', 0)])] };
    const { fixture, dialog, toast } = await mount(CATALOG, saved);

    (fixture.nativeElement.querySelector('.add-inline') as HTMLButtonElement).click();
    await settle(fixture);

    const [component, config] = dialog.open.mock.calls[0];
    expect(component).toBe(ServiceDialog);
    expect(config.data.categoryId).toBe('A');
    expect(config.data.categories).toHaveLength(2);
    expect(names(fixture)).toEqual(['Service new']);
    expect(toast.show).toHaveBeenCalledWith('services.saved');
  });

  it('keeps everything when a dialog is cancelled', async () => {
    const { fixture, dialog, toast } = await mount(CATALOG, undefined);

    (fixture.nativeElement.querySelector('.head-actions button') as HTMLButtonElement).click();
    await settle(fixture);

    expect(dialog.open.mock.calls[0][0]).toBe(CategoryDialog);
    expect(toast.show).not.toHaveBeenCalled();
    expect(names(fixture)).toHaveLength(3);
  });

  it('edits a service when its row is clicked', async () => {
    const { fixture, dialog } = await mount();

    (fixture.nativeElement.querySelector('.service-main') as HTMLButtonElement).click();
    await settle(fixture);

    expect(dialog.open.mock.calls[0][1].data.service.id).toBe('a1');
  });

  it('moves a service down through the menu and shows the order at once', async () => {
    const { fixture, api } = await mount();
    const reordered: Catalog = {
      categories: [
        category('A', 0, [service('a2', 'A', 0), service('a1', 'A', 1), service('a3', 'A', 2)]),
        CATALOG.categories[1],
      ],
    };
    api.reorderServices.mockResolvedValue(reordered);

    const items = await openMenu(fixture, 'service', 'Service a1');
    await choose(fixture, items, 'services.moveDown');

    expect(api.reorderServices).toHaveBeenCalledWith('A', ['a2', 'a1', 'a3']);
    expect(names(fixture)).toEqual(['Service a2', 'Service a1', 'Service a3']);
  });

  it('disables moving the first service up', async () => {
    const { fixture } = await mount();

    const items = await openMenu(fixture, 'service', 'Service a1');

    expect(items.find((item) => item.textContent?.includes('services.moveUp'))?.disabled).toBe(
      true,
    );
    expect(items.find((item) => item.textContent?.includes('services.moveDown'))?.disabled).toBe(
      false,
    );
  });

  it('loads the stored order again when saving the new order fails', async () => {
    const { fixture, api } = await mount();
    api.reorderServices.mockRejectedValue(new ApiError(409, 'catalog.order_mismatch', 'x'));

    const items = await openMenu(fixture, 'service', 'Service a1');
    await choose(fixture, items, 'services.moveDown');

    expect(api.get).toHaveBeenCalledTimes(2);
    expect(names(fixture)).toEqual(['Service a1', 'Service a2', 'Service a3']);
  });

  it('moves a category', async () => {
    const { fixture, api } = await mount();

    const items = await openMenu(fixture, 'category', 'Category A');
    await choose(fixture, items, 'services.moveDown');

    expect(api.reorderCategories).toHaveBeenCalledWith(['B', 'A']);
  });

  it('deactivates and reactivates a service with all its other values unchanged', async () => {
    const { fixture, api } = await mount();

    await choose(
      fixture,
      await openMenu(fixture, 'service', 'Service a1'),
      'services.service.deactivate',
    );
    await choose(
      fixture,
      await openMenu(fixture, 'service', 'Service a2'),
      'services.service.activate',
    );

    expect(api.updateService).toHaveBeenNthCalledWith(
      1,
      'a1',
      expect.objectContaining({ isActive: false, priceCents: 3000, categoryId: 'A' }),
    );
    expect(api.updateService).toHaveBeenNthCalledWith(
      2,
      'a2',
      expect.objectContaining({ isActive: true, priceIsFrom: true }),
    );
  });

  it('only offers deletion for services without appointments and asks first', async () => {
    const confirmed = await mount(CATALOG, true);
    const withAppointments = await openMenu(confirmed.fixture, 'service', 'Service a3');
    expect(
      withAppointments.some((item) => item.textContent?.includes('services.service.delete')),
    ).toBe(false);
    document
      .querySelectorAll('.cdk-overlay-container')
      .forEach((overlay) => (overlay.innerHTML = ''));

    await choose(
      confirmed.fixture,
      await openMenu(confirmed.fixture, 'service', 'Service a1'),
      'services.service.delete',
    );

    expect(confirmed.dialog.open.mock.calls[0][1].data.textParams).toEqual({ name: 'Service a1' });
    expect(confirmed.api.deleteService).toHaveBeenCalledWith('a1');
  });

  it('does not delete when the confirmation is declined', async () => {
    const { fixture, api } = await mount(CATALOG, false);

    await choose(
      fixture,
      await openMenu(fixture, 'service', 'Service a1'),
      'services.service.delete',
    );

    expect(api.deleteService).not.toHaveBeenCalled();
  });

  it('only deletes empty categories', async () => {
    const { fixture, api } = await mount(CATALOG, true);

    const full = await openMenu(fixture, 'category', 'Category A');
    expect(
      full.find((item) => item.textContent?.includes('services.category.delete'))?.disabled,
    ).toBe(true);
    document
      .querySelectorAll('.cdk-overlay-container')
      .forEach((overlay) => (overlay.innerHTML = ''));

    await choose(
      fixture,
      await openMenu(fixture, 'category', 'Category B'),
      'services.category.delete',
    );

    expect(api.deleteCategory).toHaveBeenCalledWith('B');
  });

  it('reports a failed change with the translated error and keeps the list', async () => {
    const { fixture, api, notifier } = await mount(CATALOG, true);
    const error = new ApiError(409, 'service.has_appointments', 'x');
    api.deleteService.mockRejectedValue(error);

    await choose(
      fixture,
      await openMenu(fixture, 'service', 'Service a1'),
      'services.service.delete',
    );

    expect(notifier.show).toHaveBeenCalledWith(error);
    expect(names(fixture)).toHaveLength(3);
  });
});

describe('ServicesPage drag and drop', () => {
  it('saves the order after a drop', async () => {
    const { fixture, api } = await mount();
    const lists = fixture.debugElement.queryAll(By.directive(CdkDropList));
    const serviceList = lists.find((list) => list.nativeElement.classList.contains('services'));
    const categoryList = lists.find((list) => list.nativeElement.classList.contains('categories'));

    serviceList?.triggerEventHandler('cdkDropListDropped', { previousIndex: 2, currentIndex: 0 });
    await settle(fixture);
    categoryList?.triggerEventHandler('cdkDropListDropped', {
      previousIndex: 0,
      currentIndex: 1,
      container: { data: CATALOG },
    });
    await settle(fixture);

    expect(api.reorderServices).toHaveBeenCalledWith('A', ['a3', 'a1', 'a2']);
    expect(api.reorderCategories).toHaveBeenCalledWith(['B', 'A']);
  });
});
