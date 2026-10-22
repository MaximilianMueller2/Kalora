import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { SaveServiceRequest } from './catalog.models';
import { CatalogService } from './catalog.service';

const REQUEST: SaveServiceRequest = {
  categoryId: 'c1',
  name: 'Haarschnitt',
  description: null,
  priceCents: 3000,
  priceIsFrom: false,
  durationMinutes: 30,
  bufferBeforeMinutes: 0,
  bufferAfterMinutes: 5,
  isBookableOnline: true,
  isActive: true,
};

describe('CatalogService', () => {
  let service: CatalogService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(CatalogService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function expectCall(method: string, url: string, body?: unknown) {
    const request = http.expectOne(url);
    expect(request.request.method).toBe(method);
    if (body !== undefined) {
      expect(request.request.body).toEqual(body);
    }
    request.flush({ categories: [] });
  }

  it('loads the catalog', async () => {
    const result = service.get();
    expectCall('GET', '/api/catalog');
    expect(await result).toEqual({ categories: [] });
  });

  it.each([
    [
      'createCategory',
      () => service.createCategory('Damen'),
      'POST',
      '/api/catalog/categories',
      { name: 'Damen' },
    ],
    [
      'renameCategory',
      () => service.renameCategory('c1', 'Frauen'),
      'PUT',
      '/api/catalog/categories/c1',
      { name: 'Frauen' },
    ],
    [
      'deleteCategory',
      () => service.deleteCategory('c1'),
      'DELETE',
      '/api/catalog/categories/c1',
      undefined,
    ],
    [
      'reorderCategories',
      () => service.reorderCategories(['b', 'a']),
      'PUT',
      '/api/catalog/categories/order',
      { ids: ['b', 'a'] },
    ],
    [
      'createService',
      () => service.createService(REQUEST),
      'POST',
      '/api/catalog/services',
      REQUEST,
    ],
    [
      'updateService',
      () => service.updateService('s1', REQUEST),
      'PUT',
      '/api/catalog/services/s1',
      REQUEST,
    ],
    [
      'deleteService',
      () => service.deleteService('s1'),
      'DELETE',
      '/api/catalog/services/s1',
      undefined,
    ],
    [
      'reorderServices',
      () => service.reorderServices('c1', ['y', 'x']),
      'PUT',
      '/api/catalog/categories/c1/services/order',
      { ids: ['y', 'x'] },
    ],
    [
      'applyTemplate',
      () => service.applyTemplate('Barber'),
      'POST',
      '/api/catalog/templates/apply',
      { industry: 'Barber' },
    ],
    [
      'applyTemplate (own industry)',
      () => service.applyTemplate(null),
      'POST',
      '/api/catalog/templates/apply',
      { industry: null },
    ],
  ])('%s calls the API', async (_name, call, method, url, body) => {
    const result = call();
    expectCall(method, url, body);
    expect(await result).toEqual({ categories: [] });
  });
});
