import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { ApiClient } from '../../core/http/api-client';
import { request } from './requests-fixtures';
import { RequestsService } from './requests.service';

function setup() {
  const api = {
    get: vi.fn().mockReturnValue(of([request()])),
    post: vi.fn().mockReturnValue(of(request({ status: 'Confirmed' }))),
  };
  TestBed.configureTestingModule({ providers: [{ provide: ApiClient, useValue: api }] });
  return { api, service: TestBed.inject(RequestsService) };
}

describe('RequestsService', () => {
  it('loads the open requests', async () => {
    const { api, service } = setup();

    expect(await service.list()).toHaveLength(1);
    expect(api.get).toHaveBeenCalledWith('/appointments/requests');
  });

  it('counts quietly, because the badge asks every minute', async () => {
    const { api, service } = setup();
    api.get.mockReturnValue(of({ count: 3 }));

    expect(await service.count()).toBe(3);
    expect(api.get).toHaveBeenCalledWith('/appointments/requests/count', { silent: true });
  });

  it('confirms and declines by posting to the appointment', async () => {
    const { api, service } = setup();

    await service.confirm('r1');
    await service.decline('r1', 'Betriebsausflug');
    await service.decline('r2', null);

    expect(api.post).toHaveBeenNthCalledWith(1, '/appointments/r1/confirm');
    expect(api.post).toHaveBeenNthCalledWith(2, '/appointments/r1/decline', {
      reason: 'Betriebsausflug',
    });
    expect(api.post).toHaveBeenNthCalledWith(3, '/appointments/r2/decline', { reason: null });
  });
});
