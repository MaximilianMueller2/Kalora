import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { ApiClient } from '../../core/http/api-client';
import { managed } from './manage-fixtures';
import { ManageService } from './manage.service';

function setup() {
  const api = {
    get: vi.fn().mockReturnValue(of(managed())),
    post: vi.fn().mockReturnValue(of(managed({ status: 'Cancelled' }))),
  };
  TestBed.configureTestingModule({ providers: [{ provide: ApiClient, useValue: api }] });
  return { api, service: TestBed.inject(ManageService) };
}

describe('ManageService', () => {
  it('reads the appointment of a token quietly, because the page shows the error', async () => {
    const { api, service } = setup();

    expect((await service.get('abc')).serviceName).toBe('Haarschnitt');
    expect(api.get).toHaveBeenCalledWith('/public/appointments/abc', { silent: true });
  });

  it('cancels by posting to the token, and escapes it', async () => {
    const { api, service } = setup();

    expect((await service.cancel('a/b c')).status).toBe('Cancelled');
    expect(api.post).toHaveBeenCalledWith('/public/appointments/a%2Fb%20c/cancel', undefined, {
      silent: true,
    });
  });
});
