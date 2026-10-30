import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiClient } from '../../core/http/api-client';
import { Appointment, OpenRequestCount } from './requests.models';

/** Open booking requests and the salon's answers to them. Failed answers are reported by the global error snackbar. */
@Injectable({ providedIn: 'root' })
export class RequestsService {
  private readonly api = inject(ApiClient);

  list(): Promise<Appointment[]> {
    return firstValueFrom(this.api.get<Appointment[]>('/appointments/requests'));
  }

  /** Polled in the background, so a failure must not pop up a message every minute. */
  async count(): Promise<number> {
    const result = await firstValueFrom(
      this.api.get<OpenRequestCount>('/appointments/requests/count', { silent: true }),
    );
    return result.count;
  }

  confirm(id: string): Promise<Appointment> {
    return firstValueFrom(this.api.post<Appointment>(`/appointments/${id}/confirm`));
  }

  decline(id: string, reason: string | null): Promise<Appointment> {
    return firstValueFrom(this.api.post<Appointment>(`/appointments/${id}/decline`, { reason }));
  }
}
