import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiClient } from '../../core/http/api-client';
import { SalonIndustry } from '../auth/auth.models';
import { Catalog, SaveServiceRequest } from './catalog.models';

/**
 * Categories and services of the signed-in user's salon. Every change answers with the whole new catalog.
 * Saving calls are `silent`: dialogs show the error code next to the form; loading reports through the snackbar.
 */
@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly api = inject(ApiClient);

  get(): Promise<Catalog> {
    return firstValueFrom(this.api.get<Catalog>('/catalog'));
  }

  createCategory(name: string): Promise<Catalog> {
    return firstValueFrom(
      this.api.post<Catalog>('/catalog/categories', { name }, { silent: true }),
    );
  }

  renameCategory(id: string, name: string): Promise<Catalog> {
    return firstValueFrom(
      this.api.put<Catalog>(`/catalog/categories/${id}`, { name }, { silent: true }),
    );
  }

  deleteCategory(id: string): Promise<Catalog> {
    return firstValueFrom(this.api.delete<Catalog>(`/catalog/categories/${id}`, { silent: true }));
  }

  reorderCategories(ids: string[]): Promise<Catalog> {
    return firstValueFrom(this.api.put<Catalog>('/catalog/categories/order', { ids }));
  }

  createService(request: SaveServiceRequest): Promise<Catalog> {
    return firstValueFrom(this.api.post<Catalog>('/catalog/services', request, { silent: true }));
  }

  updateService(id: string, request: SaveServiceRequest): Promise<Catalog> {
    return firstValueFrom(
      this.api.put<Catalog>(`/catalog/services/${id}`, request, { silent: true }),
    );
  }

  deleteService(id: string): Promise<Catalog> {
    return firstValueFrom(this.api.delete<Catalog>(`/catalog/services/${id}`, { silent: true }));
  }

  reorderServices(categoryId: string, ids: string[]): Promise<Catalog> {
    return firstValueFrom(
      this.api.put<Catalog>(`/catalog/categories/${categoryId}/services/order`, { ids }),
    );
  }

  applyTemplate(industry: SalonIndustry | null): Promise<Catalog> {
    return firstValueFrom(
      this.api.post<Catalog>('/catalog/templates/apply', { industry }, { silent: true }),
    );
  }
}
