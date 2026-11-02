import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { ApiClient } from '../../core/http/api-client';
import { customerDetail, customerItem } from './customers-fixtures';
import { CustomersService } from './customers.service';

function setup() {
  const api = {
    get: vi.fn().mockReturnValue(of({ items: [customerItem()], hasMore: false })),
    post: vi.fn().mockReturnValue(of(undefined)),
  };
  TestBed.configureTestingModule({ providers: [{ provide: ApiClient, useValue: api }] });
  return { api, service: TestBed.inject(CustomersService) };
}

describe('CustomersService', () => {
  it('lists the customers and searches with q', async () => {
    const { api, service } = setup();

    await service.list('kim');

    expect(api.get).toHaveBeenCalledWith('/customers', { params: { q: 'kim' } });
  });

  it('does not send an empty search', async () => {
    const { api, service } = setup();

    await service.list('');

    expect(api.get).toHaveBeenCalledWith('/customers', { params: { q: undefined } });
  });

  it('loads one customer', async () => {
    const { api, service } = setup();
    api.get.mockReturnValue(of(customerDetail()));

    expect((await service.get('c1')).name).toBe('Kim Klein');
    expect(api.get).toHaveBeenCalledWith('/customers/c1');
  });

  it('points the download links at the export endpoints', () => {
    const { service } = setup();

    expect(service.exportUrl('c1', 'json')).toMatch(/\/customers\/c1\/export\.json$/);
    expect(service.exportUrl('c1', 'csv')).toMatch(/\/customers\/c1\/export\.csv$/);
  });

  it('anonymizes quietly, because the page shows the error itself', async () => {
    const { api, service } = setup();

    await service.anonymize('c1');

    expect(api.post).toHaveBeenCalledWith('/customers/c1/anonymize', undefined, { silent: true });
  });
});
