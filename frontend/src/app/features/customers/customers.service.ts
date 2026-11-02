import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiClient, apiUrl } from '../../core/http/api-client';
import { CustomerDetail, CustomerList } from './customers.models';

/** The customer file of the salon: look up, export and delete (anonymize). */
@Injectable({ providedIn: 'root' })
export class CustomersService {
  private readonly api = inject(ApiClient);

  list(search: string): Promise<CustomerList> {
    return firstValueFrom(
      this.api.get<CustomerList>('/customers', { params: { q: search || undefined } }),
    );
  }

  get(id: string): Promise<CustomerDetail> {
    return firstValueFrom(this.api.get<CustomerDetail>(`/customers/${id}`));
  }

  /** Download links; the browser sends the session cookie along. */
  exportUrl(id: string, format: 'json' | 'csv'): string {
    return apiUrl(`/customers/${id}/export.${format}`);
  }

  /** The caller shows the error (e.g. open appointments), so it is not reported by the global snackbar. */
  anonymize(id: string): Promise<void> {
    return firstValueFrom(
      this.api.post<void>(`/customers/${id}/anonymize`, undefined, { silent: true }),
    );
  }
}
