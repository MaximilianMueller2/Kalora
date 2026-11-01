import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiClient } from '../../core/http/api-client';
import { ManagedAppointment } from './manage.models';

/** A customer's own appointment, reached by the token in their link. The page shows errors itself, so these calls are `silent`. */
@Injectable({ providedIn: 'root' })
export class ManageService {
  private readonly api = inject(ApiClient);

  get(token: string): Promise<ManagedAppointment> {
    return firstValueFrom(
      this.api.get<ManagedAppointment>(`/public/appointments/${encodeURIComponent(token)}`, {
        silent: true,
      }),
    );
  }

  cancel(token: string): Promise<ManagedAppointment> {
    return firstValueFrom(
      this.api.post<ManagedAppointment>(
        `/public/appointments/${encodeURIComponent(token)}/cancel`,
        undefined,
        { silent: true },
      ),
    );
  }
}
