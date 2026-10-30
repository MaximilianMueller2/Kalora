import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiClient } from '../../core/http/api-client';
import { Appointment } from '../requests/requests.models';
import {
  CalendarBlock,
  CalendarData,
  CreateBlockRequest,
  CreateManualAppointmentRequest,
  CustomerHit,
  MoveAppointmentRequest,
} from './calendar.models';

/**
 * The team calendar's API. Reading reports problems through the global snackbar; the calls that change something are
 * `silent`, because the dialog or the drop shows the error code itself.
 */
@Injectable({ providedIn: 'root' })
export class CalendarService {
  private readonly api = inject(ApiClient);

  get(from: string, to: string): Promise<CalendarData> {
    return firstValueFrom(this.api.get<CalendarData>('/calendar', { params: { from, to } }));
  }

  searchCustomers(text: string): Promise<CustomerHit[]> {
    return firstValueFrom(
      this.api.get<CustomerHit[]>('/calendar/customers', { params: { q: text }, silent: true }),
    );
  }

  createAppointment(request: CreateManualAppointmentRequest): Promise<Appointment> {
    return firstValueFrom(
      this.api.post<Appointment>('/calendar/appointments', request, { silent: true }),
    );
  }

  move(id: string, request: MoveAppointmentRequest): Promise<Appointment> {
    return firstValueFrom(
      this.api.post<Appointment>(`/calendar/appointments/${id}/move`, request, { silent: true }),
    );
  }

  createBlock(request: CreateBlockRequest): Promise<CalendarBlock> {
    return firstValueFrom(
      this.api.post<CalendarBlock>('/calendar/blocks', request, { silent: true }),
    );
  }

  deleteBlock(id: string): Promise<void> {
    return firstValueFrom(this.api.delete<void>(`/calendar/blocks/${id}`, { silent: true }));
  }
}
