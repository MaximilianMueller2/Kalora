import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { TeamService } from './team.service';

describe('TeamService', () => {
  let service: TeamService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(TeamService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const staff = {
    displayName: 'Anna',
    color: '#7C5CFC',
    isBookableOnline: true,
    autoConfirm: false,
  };
  const plan = { validFrom: '2026-11-03', days: [{ weekday: 'Monday' as const, blocks: [] }] };

  it.each([
    ['list', () => service.list(), 'GET', '/api/staff', undefined],
    ['get', () => service.get('s1'), 'GET', '/api/staff/s1', undefined],
    [
      'create',
      () => service.create({ ...staff, linkToCurrentUser: true }),
      'POST',
      '/api/staff',
      { ...staff, linkToCurrentUser: true },
    ],
    [
      'update',
      () => service.update('s1', { ...staff, isActive: false }),
      'PUT',
      '/api/staff/s1',
      { ...staff, isActive: false },
    ],
    [
      'invite',
      () => service.invite({ staffId: 's1', email: 'a@b.de', role: 'Staff' }),
      'POST',
      '/api/invitations',
      { staffId: 's1', email: 'a@b.de', role: 'Staff' },
    ],
    ['getSkills', () => service.getSkills('s1'), 'GET', '/api/staff/s1/services', undefined],
    [
      'saveSkills',
      () => service.saveSkills('s1', { skills: [] }),
      'PUT',
      '/api/staff/s1/services',
      { skills: [] },
    ],
    [
      'getSchedule',
      () => service.getSchedule('s1'),
      'GET',
      '/api/staff/s1/working-hours',
      undefined,
    ],
    [
      'saveSchedule',
      () => service.saveSchedule('s1', plan),
      'PUT',
      '/api/staff/s1/working-hours',
      plan,
    ],
    ['listAbsences', () => service.listAbsences('s1'), 'GET', '/api/staff/s1/absences', undefined],
    [
      'createAbsence',
      () =>
        service.createAbsence('s1', {
          startDate: '2026-11-04',
          endDate: '2026-11-04',
          type: 'Sick',
        }),
      'POST',
      '/api/staff/s1/absences',
      { startDate: '2026-11-04', endDate: '2026-11-04', type: 'Sick' },
    ],
    [
      'deleteAbsence',
      () => service.deleteAbsence('s1', 'a1'),
      'DELETE',
      '/api/staff/s1/absences/a1',
      undefined,
    ],
  ])('%s calls the API', async (_name, call, method, url, body) => {
    const result = call();

    const request = http.expectOne(url);
    expect(request.request.method).toBe(method);
    if (body !== undefined) {
      expect(request.request.body).toEqual(body);
    }
    request.flush(method === 'DELETE' ? null : { ok: true });
    await result;
  });
});
