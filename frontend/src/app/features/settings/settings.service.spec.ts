import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { emptySchedule } from '../../shared/week-schedule/week-schedule.model';
import { SettingsService, toPlan, toSchedule } from './settings.service';

describe('SettingsService', () => {
  let service: SettingsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(SettingsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('converts between the API plan and the editor schedule', () => {
    const plan = {
      days: [
        { weekday: 'Monday' as const, blocks: [{ start: '09:00', end: '18:00' }] },
        { weekday: 'Sunday' as const, blocks: [] },
      ],
    };

    const schedule = toSchedule(plan);

    expect(schedule.Monday).toEqual([{ start: '09:00', end: '18:00' }]);
    expect(schedule.Tuesday).toEqual([]);
    expect(toPlan(schedule).days.map((day) => day.weekday)).toEqual([
      'Monday',
      'Tuesday',
      'Wednesday',
      'Thursday',
      'Friday',
      'Saturday',
      'Sunday',
    ]);
    expect(toPlan(emptySchedule()).days.every((day) => day.blocks.length === 0)).toBe(true);
  });

  it('reads and writes the opening hours as a whole week', async () => {
    const read = service.getOpeningHours();
    http
      .expectOne('/api/settings/opening-hours')
      .flush({ days: [{ weekday: 'Friday', blocks: [{ start: '10:00', end: '16:00' }] }] });
    expect((await read).Friday).toEqual([{ start: '10:00', end: '16:00' }]);

    const schedule = { ...emptySchedule(), Monday: [{ start: '09:00', end: '12:00' }] };
    const write = service.updateOpeningHours(schedule);
    const request = http.expectOne('/api/settings/opening-hours');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body.days).toHaveLength(7);
    expect(request.request.body.days[0]).toEqual({
      weekday: 'Monday',
      blocks: [{ start: '09:00', end: '12:00' }],
    });
    request.flush({ days: request.request.body.days });
    expect((await write).Monday).toHaveLength(1);
  });

  it('uses the settings endpoints for profile, rules, slug suggestion and closures', async () => {
    void service.getProfile();
    expect(http.expectOne('/api/settings/profile').request.method).toBe('GET');

    void service.updateProfile({ name: 'X' } as never);
    expect(http.expectOne('/api/settings/profile').request.method).toBe('PUT');

    void service.getBookingRules();
    http.expectOne('/api/settings/booking-rules');

    void service.updateBookingRules({} as never);
    expect(http.expectOne('/api/settings/booking-rules').request.method).toBe('PUT');

    const suggestion = service.suggestSlug('Kamm & Schere');
    const request = http.expectOne((r) => r.url === '/api/settings/slug-suggestion');
    expect(request.request.params.get('name')).toBe('Kamm & Schere');
    request.flush({ slug: 'kamm-schere' });
    expect(await suggestion).toBe('kamm-schere');

    void service.listClosures();
    http.expectOne('/api/settings/closures');

    void service.createClosures({ date: '2026-12-24', endDate: null, reason: null });
    expect(http.expectOne('/api/settings/closures').request.body).toEqual({
      date: '2026-12-24',
      endDate: null,
      reason: null,
    });

    void service.deleteClosure('abc');
    const removal = http.expectOne('/api/settings/closures/abc');
    expect(removal.request.method).toBe('DELETE');
  });
});
