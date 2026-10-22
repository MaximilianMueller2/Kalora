import { SalonIndustry } from '../auth/auth.models';

export interface ServiceItem {
  id: string;
  categoryId: string;
  name: string;
  description: string | null;
  priceCents: number;
  priceIsFrom: boolean;
  currency: string;
  durationMinutes: number;
  bufferBeforeMinutes: number;
  bufferAfterMinutes: number;
  isBookableOnline: boolean;
  isActive: boolean;
  sortOrder: number;
  hasAppointments: boolean;
}

export interface Category {
  id: string;
  name: string;
  sortOrder: number;
  services: ServiceItem[];
}

export interface Catalog {
  categories: Category[];
}

/** Body of POST/PUT /api/catalog/services; the same limits are enforced by the API (CatalogRules). */
export interface SaveServiceRequest {
  categoryId: string;
  name: string;
  description: string | null;
  priceCents: number;
  priceIsFrom: boolean;
  durationMinutes: number;
  bufferBeforeMinutes: number;
  bufferAfterMinutes: number;
  isBookableOnline: boolean;
  isActive: boolean;
}

export interface ApplyTemplateRequest {
  industry: SalonIndustry | null;
}

export const CATALOG_LIMITS = {
  maxPriceCents: 1_000_000,
  stepMinutes: 5,
  maxDurationMinutes: 720,
  maxBufferMinutes: 240,
} as const;
