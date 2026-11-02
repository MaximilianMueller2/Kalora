import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiClient } from '../../core/http/api-client';
import { AdminSalonDetail, AdminSalonList, PlanCode, SalonStatus } from './admin.models';

/** What the platform admin does with the salons. Changes report their errors on the page, not in the global message. */
@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly api = inject(ApiClient);

  list(search: string, status: SalonStatus | ''): Promise<AdminSalonList> {
    return firstValueFrom(
      this.api.get<AdminSalonList>('/admin/salons', {
        params: { q: search || undefined, status: status || undefined },
      }),
    );
  }

  get(id: string): Promise<AdminSalonDetail> {
    return firstValueFrom(this.api.get<AdminSalonDetail>(`/admin/salons/${id}`));
  }

  setPlan(id: string, planCode: PlanCode): Promise<AdminSalonDetail> {
    return firstValueFrom(
      this.api.put<AdminSalonDetail>(`/admin/salons/${id}/plan`, { planCode }, { silent: true }),
    );
  }

  extendTrial(id: string, days: number): Promise<AdminSalonDetail> {
    return firstValueFrom(
      this.api.post<AdminSalonDetail>(
        `/admin/salons/${id}/trial/extend`,
        { days },
        { silent: true },
      ),
    );
  }

  setStatus(id: string, status: SalonStatus): Promise<AdminSalonDetail> {
    return firstValueFrom(
      this.api.put<AdminSalonDetail>(`/admin/salons/${id}/status`, { status }, { silent: true }),
    );
  }

  suspend(id: string): Promise<AdminSalonDetail> {
    return firstValueFrom(
      this.api.post<AdminSalonDetail>(`/admin/salons/${id}/suspend`, undefined, { silent: true }),
    );
  }

  unsuspend(id: string): Promise<AdminSalonDetail> {
    return firstValueFrom(
      this.api.post<AdminSalonDetail>(`/admin/salons/${id}/unsuspend`, undefined, { silent: true }),
    );
  }

  setNote(id: string, note: string): Promise<AdminSalonDetail> {
    return firstValueFrom(
      this.api.put<AdminSalonDetail>(`/admin/salons/${id}/note`, { note }, { silent: true }),
    );
  }
}
