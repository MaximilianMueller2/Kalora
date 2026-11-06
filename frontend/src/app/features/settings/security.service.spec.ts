import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { ApiClient } from '../../core/http/api-client';
import { AuthService } from '../auth/auth.service';
import { SecurityService } from './security.service';

function setup() {
  const api = {
    post: vi
      .fn()
      .mockImplementation((path: string) =>
        of(
          path.endsWith('setup')
            ? { sharedKey: 'ABC', authenticatorUri: 'otpauth://x', qrPngBase64: 'AAAA' }
            : { recoveryCodes: ['one', 'two'] },
        ),
      ),
  };
  const auth = { reload: vi.fn().mockResolvedValue(undefined) };
  TestBed.configureTestingModule({
    providers: [
      { provide: ApiClient, useValue: api },
      { provide: AuthService, useValue: auth },
    ],
  });
  return { api, auth, service: TestBed.inject(SecurityService) };
}

describe('SecurityService', () => {
  it('asks for the secret quietly, because the page shows the error', async () => {
    const { api, service } = setup();

    expect((await service.setup()).sharedKey).toBe('ABC');
    expect(api.post).toHaveBeenCalledWith('/auth/two-factor/setup', undefined, { silent: true });
  });

  it('turns it on with the first code, returns the recovery codes and reloads the profile', async () => {
    const { api, auth, service } = setup();

    expect((await service.enable('123456')).recoveryCodes).toEqual(['one', 'two']);
    expect(api.post).toHaveBeenCalledWith(
      '/auth/two-factor/enable',
      { code: '123456' },
      { silent: true },
    );
    expect(auth.reload).toHaveBeenCalledOnce();
  });

  it('turns it off with the password and reloads the profile', async () => {
    const { api, auth, service } = setup();

    await service.disable('secret-password');

    expect(api.post).toHaveBeenCalledWith(
      '/auth/two-factor/disable',
      { password: 'secret-password' },
      { silent: true },
    );
    expect(auth.reload).toHaveBeenCalledOnce();
  });

  it('gets new recovery codes with the password, without any change to the profile', async () => {
    const { api, auth, service } = setup();

    expect((await service.regenerateRecoveryCodes('secret-password')).recoveryCodes).toHaveLength(
      2,
    );
    expect(api.post).toHaveBeenCalledWith(
      '/auth/two-factor/recovery-codes',
      { password: 'secret-password' },
      { silent: true },
    );
    expect(auth.reload).not.toHaveBeenCalled();
  });

  it('does not reload the profile when turning it on fails', async () => {
    const { api, auth, service } = setup();
    api.post.mockReturnValue(
      new (await import('rxjs')).Observable((subscriber) =>
        subscriber.error(new Error('bad code')),
      ),
    );

    await expect(service.enable('000000')).rejects.toThrow('bad code');
    expect(auth.reload).not.toHaveBeenCalled();
  });
});
