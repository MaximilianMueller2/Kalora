import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiClient } from '../../core/http/api-client';
import { RecoveryCodes, TwoFactorSetup } from '../auth/auth.models';
import { AuthService } from '../auth/auth.service';

/**
 * Two-factor sign-in with an authenticator app. All calls are `silent`: the page shows the error code next to the form.
 * What changes the signed-in profile (`twoFactorEnabled`) reloads it.
 */
@Injectable({ providedIn: 'root' })
export class SecurityService {
  private readonly api = inject(ApiClient);
  private readonly auth = inject(AuthService);

  setup(): Promise<TwoFactorSetup> {
    return firstValueFrom(
      this.api.post<TwoFactorSetup>('/auth/two-factor/setup', undefined, { silent: true }),
    );
  }

  /** Switches it on; the answer holds the recovery codes, which are shown once and never again. */
  async enable(code: string): Promise<RecoveryCodes> {
    const codes = await firstValueFrom(
      this.api.post<RecoveryCodes>('/auth/two-factor/enable', { code }, { silent: true }),
    );
    await this.auth.reload();
    return codes;
  }

  async disable(password: string): Promise<void> {
    await firstValueFrom(
      this.api.post<void>('/auth/two-factor/disable', { password }, { silent: true }),
    );
    await this.auth.reload();
  }

  /** New recovery codes; the old ones stop working. */
  regenerateRecoveryCodes(password: string): Promise<RecoveryCodes> {
    return firstValueFrom(
      this.api.post<RecoveryCodes>(
        '/auth/two-factor/recovery-codes',
        { password },
        { silent: true },
      ),
    );
  }
}
