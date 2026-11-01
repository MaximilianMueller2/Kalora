import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiClient, apiUrl } from '../../core/http/api-client';
import { Dashboard } from './dashboard.models';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly api = inject(ApiClient);

  readonly qrPngUrl = apiUrl('/dashboard/booking-link/qr.png');
  readonly posterPdfUrl = apiUrl('/dashboard/booking-link/poster.pdf');

  get(): Promise<Dashboard> {
    return firstValueFrom(this.api.get<Dashboard>('/dashboard'));
  }
}
