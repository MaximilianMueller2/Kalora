import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { ApiClient } from '../../core/http/api-client';
import { AdminService } from './admin.service';
import { salonDetail } from './admin-fixtures';

function setup() {
  const api = {
    get: vi.fn().mockReturnValue(of({ items: [], hasMore: false })),
    post: vi.fn().mockReturnValue(of(salonDetail())),
    put: vi.fn().mockReturnValue(of(salonDetail())),
  };
  TestBed.configureTestingModule({ providers: [{ provide: ApiClient, useValue: api }] });
  return { api, service: TestBed.inject(AdminService) };
}

describe('AdminService', () => {
  it('lists with the search and the status, and sends neither when empty', async () => {
    const { api, service } = setup();

    await service.list('haar', 'Trial');
    await service.list('', '');

    expect(api.get).toHaveBeenNthCalledWith(1, '/admin/salons', {
      params: { q: 'haar', status: 'Trial' },
    });
    expect(api.get).toHaveBeenNthCalledWith(2, '/admin/salons', {
      params: { q: undefined, status: undefined },
    });
  });

  it('loads one salon', async () => {
    const { api, service } = setup();
    api.get.mockReturnValue(of(salonDetail()));

    expect((await service.get('s1')).name).toBe('Haarwerk Müller');
    expect(api.get).toHaveBeenCalledWith('/admin/salons/s1');
  });

  it('changes plan, status and note with PUT and quietly, because the page shows the error', async () => {
    const { api, service } = setup();

    await service.setPlan('s1', 'Team');
    await service.setStatus('s1', 'PastDue');
    await service.setNote('s1', 'Zahlt per Überweisung');

    expect(api.put).toHaveBeenNthCalledWith(
      1,
      '/admin/salons/s1/plan',
      { planCode: 'Team' },
      { silent: true },
    );
    expect(api.put).toHaveBeenNthCalledWith(
      2,
      '/admin/salons/s1/status',
      { status: 'PastDue' },
      { silent: true },
    );
    expect(api.put).toHaveBeenNthCalledWith(
      3,
      '/admin/salons/s1/note',
      { note: 'Zahlt per Überweisung' },
      { silent: true },
    );
  });

  it('extends the trial, suspends and unsuspends with POST', async () => {
    const { api, service } = setup();

    await service.extendTrial('s1', 14);
    await service.suspend('s1');
    await service.unsuspend('s1');

    expect(api.post).toHaveBeenNthCalledWith(
      1,
      '/admin/salons/s1/trial/extend',
      { days: 14 },
      { silent: true },
    );
    expect(api.post).toHaveBeenNthCalledWith(2, '/admin/salons/s1/suspend', undefined, {
      silent: true,
    });
    expect(api.post).toHaveBeenNthCalledWith(3, '/admin/salons/s1/unsuspend', undefined, {
      silent: true,
    });
  });
});
