import { Injectable, computed, inject, resource, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiClient } from '../../core/http/api-client';
import { LanguageService } from '../../core/i18n/language.service';
import { monthOf } from './booking-calendar';
import {
  AppointmentRequest,
  BookingReceipt,
  PublicCategory,
  PublicDays,
  PublicSalon,
  PublicService,
  PublicSlot,
  PublicSlots,
  PublicStaff,
} from './booking.models';

export interface Contact {
  name: string;
  email: string;
  phone: string;
  note: string;
  website: string;
}

/**
 * State of one booking in the browser and the calls to the public API of the salon in the URL. All calls are
 * `silent`: the booking pages show problems in the page itself. Choosing something earlier in the flow clears what
 * depended on it (a new service invalidates the person, the day and the time).
 */
@Injectable({ providedIn: 'root' })
export class BookingService {
  private readonly api = inject(ApiClient);
  private readonly language = inject(LanguageService);

  private readonly slugState = signal('');
  readonly slug = this.slugState.asReadonly();

  readonly selectedService = signal<PublicService | null>(null);
  /** `null` is "anyone". */
  readonly selectedStaff = signal<PublicStaff | null>(null);
  readonly month = signal('');
  readonly selectedDate = signal<string | null>(null);
  readonly selectedSlot = signal<PublicSlot | null>(null);
  readonly contact = signal<Contact>({ name: '', email: '', phone: '', note: '', website: '' });
  readonly receipt = signal<BookingReceipt | null>(null);

  readonly salon = resource({
    params: () => this.slug() || undefined,
    loader: ({ params }) => this.get<PublicSalon>(params, ''),
  });

  readonly categories = resource({
    params: () => this.slug() || undefined,
    loader: ({ params }) => this.get<PublicCategory[]>(params, '/services'),
  });

  readonly staff = resource({
    params: () => {
      const service = this.selectedService();
      return this.slug() && service ? { slug: this.slug(), serviceId: service.id } : undefined;
    },
    loader: ({ params }) =>
      this.get<PublicStaff[]>(params.slug, '/staff', { serviceId: params.serviceId }),
  });

  /** Days of the shown month that have a free time. */
  readonly days = resource({
    params: () => {
      const service = this.selectedService();
      const month = this.month();
      return this.slug() && service && month
        ? { slug: this.slug(), serviceId: service.id, staffId: this.selectedStaff()?.id, month }
        : undefined;
    },
    loader: ({ params }) =>
      this.get<PublicDays>(params.slug, '/availability/days', {
        serviceId: params.serviceId,
        staffId: params.staffId,
        month: params.month,
      }),
  });

  /** Start times of the selected day. */
  readonly slots = resource({
    params: () => {
      const service = this.selectedService();
      const date = this.selectedDate();
      return this.slug() && service && date
        ? { slug: this.slug(), serviceId: service.id, staffId: this.selectedStaff()?.id, date }
        : undefined;
    },
    loader: ({ params }) =>
      this.get<PublicSlots>(params.slug, '/availability/slots', {
        serviceId: params.serviceId,
        staffId: params.staffId,
        date: params.date,
      }),
  });

  readonly hasService = computed(() => this.selectedService() !== null);
  readonly hasSlot = computed(() => this.selectedSlot() !== null);

  /** A different salon starts from scratch; the same one keeps what the visitor has chosen. */
  setSlug(slug: string): void {
    if (slug !== this.slugState()) {
      this.reset();
      this.slugState.set(slug);
    }
  }

  selectService(service: PublicService): void {
    if (this.selectedService()?.id !== service.id) {
      this.selectedService.set(service);
      this.selectedStaff.set(null);
      this.clearTime();
    }
  }

  selectStaff(staff: PublicStaff | null): void {
    if (this.selectedStaff()?.id !== staff?.id) {
      this.selectedStaff.set(staff);
      this.clearTime();
    }
  }

  selectDate(date: string): void {
    if (this.selectedDate() !== date) {
      this.selectedDate.set(date);
      this.selectedSlot.set(null);
    }
  }

  selectSlot(slot: PublicSlot): void {
    this.selectedSlot.set(slot);
  }

  /** After "that time is gone": keep service and person, pick the day and time again from fresh data. */
  clearTime(): void {
    this.selectedDate.set(null);
    this.selectedSlot.set(null);
    this.month.set('');
    this.days.reload();
  }

  /** Opens the calendar on the month of the selected day, or on `fallbackMonth` (the salon's current month). */
  showMonth(fallbackMonth: string): void {
    if (!this.month()) {
      const date = this.selectedDate();
      this.month.set(date ? monthOf(date) : fallbackMonth);
    }
  }

  async submit(): Promise<BookingReceipt> {
    const service = this.selectedService();
    const slot = this.selectedSlot();
    if (!service || !slot) {
      throw new Error('Nothing selected.');
    }

    const contact = this.contact();
    const request: AppointmentRequest = {
      serviceId: service.id,
      startUtc: slot.startUtc,
      staffId: this.selectedStaff()?.id ?? null,
      name: contact.name.trim(),
      email: contact.email.trim(),
      phone: contact.phone.trim() || null,
      note: contact.note.trim() || null,
      language: this.language.current(),
      website: contact.website,
    };
    const receipt = await firstValueFrom(
      this.api.post<BookingReceipt>(`/public/salons/${this.slug()}/appointment-requests`, request, {
        silent: true,
      }),
    );
    this.receipt.set(receipt);
    return receipt;
  }

  /** Another booking at the same salon: the contact details stay, the rest starts again. */
  startOver(): void {
    this.selectedService.set(null);
    this.selectedStaff.set(null);
    this.clearTime();
    this.receipt.set(null);
  }

  private reset(): void {
    this.selectedService.set(null);
    this.selectedStaff.set(null);
    this.selectedDate.set(null);
    this.selectedSlot.set(null);
    this.month.set('');
    this.contact.set({ name: '', email: '', phone: '', note: '', website: '' });
    this.receipt.set(null);
  }

  private get<T>(
    slug: string,
    path: string,
    params?: Record<string, string | undefined>,
  ): Promise<T> {
    return firstValueFrom(
      this.api.get<T>(`/public/salons/${slug}${path}`, { params, silent: true }),
    );
  }
}
