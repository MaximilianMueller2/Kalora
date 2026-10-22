import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiClient } from '../../core/http/api-client';
import {
  WeekSchedule,
  WEEKDAYS,
  emptySchedule,
} from '../../shared/week-schedule/week-schedule.model';
import {
  BookingRules,
  Closure,
  CreateClosureRequest,
  OpeningHoursPlan,
  SalonProfile,
} from './settings.models';

/**
 * Settings of the signed-in user's salon. Saving calls are `silent`: the pages show the error code next to the form;
 * loading calls report problems through the global snackbar.
 */
@Injectable({ providedIn: 'root' })
export class SettingsService {
  private readonly api = inject(ApiClient);

  getProfile(): Promise<SalonProfile> {
    return firstValueFrom(this.api.get<SalonProfile>('/settings/profile'));
  }

  updateProfile(profile: SalonProfile): Promise<SalonProfile> {
    return firstValueFrom(
      this.api.put<SalonProfile>('/settings/profile', profile, { silent: true }),
    );
  }

  suggestSlug(name: string): Promise<string> {
    return firstValueFrom(
      this.api.get<{ slug: string }>('/settings/slug-suggestion', {
        params: { name },
        silent: true,
      }),
    ).then((suggestion) => suggestion.slug);
  }

  getBookingRules(): Promise<BookingRules> {
    return firstValueFrom(this.api.get<BookingRules>('/settings/booking-rules'));
  }

  updateBookingRules(rules: BookingRules): Promise<BookingRules> {
    return firstValueFrom(
      this.api.put<BookingRules>('/settings/booking-rules', rules, { silent: true }),
    );
  }

  async getOpeningHours(): Promise<WeekSchedule> {
    return toSchedule(
      await firstValueFrom(this.api.get<OpeningHoursPlan>('/settings/opening-hours')),
    );
  }

  async updateOpeningHours(schedule: WeekSchedule): Promise<WeekSchedule> {
    return toSchedule(
      await firstValueFrom(
        this.api.put<OpeningHoursPlan>('/settings/opening-hours', toPlan(schedule), {
          silent: true,
        }),
      ),
    );
  }

  listClosures(): Promise<Closure[]> {
    return firstValueFrom(this.api.get<Closure[]>('/settings/closures'));
  }

  createClosures(request: CreateClosureRequest): Promise<Closure[]> {
    return firstValueFrom(
      this.api.post<Closure[]>('/settings/closures', request, { silent: true }),
    );
  }

  deleteClosure(id: string): Promise<void> {
    return firstValueFrom(this.api.delete<void>(`/settings/closures/${id}`));
  }
}

export function toSchedule(plan: OpeningHoursPlan): WeekSchedule {
  const schedule = emptySchedule();
  for (const day of plan.days) {
    schedule[day.weekday] = day.blocks.map((block) => ({ start: block.start, end: block.end }));
  }
  return schedule;
}

export function toPlan(schedule: WeekSchedule): OpeningHoursPlan {
  return { days: WEEKDAYS.map((weekday) => ({ weekday, blocks: schedule[weekday] })) };
}
