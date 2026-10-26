import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { OWNER, fakeAuth } from '../../../testing/auth-fixtures';
import { submit, texts } from '../../../testing/forms';
import { provideTestI18n } from '../../../testing/test-i18n';
import { ApiError } from '../../core/http/api-error';
import { LanguageService } from '../../core/i18n/language.service';
import { Toast } from '../../core/ui/toast';
import { AuthService } from '../auth/auth.service';
import { AbsencesPanel, daysBetween } from './absences-panel';
import { fakeTeam } from './team-fixtures';
import { Absence, AbsenceCreated } from './team.models';
import { TeamService } from './team.service';

@Component({ imports: [AbsencesPanel], template: `<app-absences-panel staffId="st1" />` })
class Host {}

const VACATION: Absence = {
  id: 'a1',
  startDate: '2026-11-10',
  endDate: '2026-11-14',
  type: 'Vacation',
  conflictCount: 0,
};
const SICK: Absence = {
  id: 'a2',
  startDate: '2026-11-04',
  endDate: '2026-11-04',
  type: 'Sick',
  conflictCount: 2,
};

const created = (
  absence: Absence,
  affected: AbsenceCreated['affectedAppointments'] = [],
): AbsenceCreated => ({
  absence,
  affectedAppointments: affected,
});

describe('daysBetween', () => {
  it('counts both ends', () => {
    expect(daysBetween('2026-11-03', '2026-11-03')).toBe(1);
    expect(daysBetween('2026-11-03', '2026-11-05')).toBe(3);
    expect(daysBetween('2026-10-24', '2026-10-27')).toBe(4);
    expect(daysBetween('2026-01-01', '2027-01-01')).toBe(366);
  });
});

describe('AbsencesPanel', () => {
  beforeEach(() => vi.useFakeTimers({ toFake: ['Date'], now: new Date('2026-11-03T09:00:00Z') }));
  afterEach(() => vi.useRealTimers());

  async function mount(absences: Absence[] = []) {
    localStorage.clear();
    const team = fakeTeam();
    team.listAbsences.mockResolvedValue(absences);
    const toast = { show: vi.fn() };
    TestBed.configureTestingModule({
      providers: [
        provideTestI18n(),
        { provide: TeamService, useValue: team },
        { provide: AuthService, useValue: fakeAuth(OWNER) },
        { provide: Toast, useValue: toast },
      ],
    });
    await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
    await TestBed.inject(LanguageService).use('de');
    const fixture = TestBed.createComponent(Host);
    await settle(fixture);
    return { fixture, team, toast };
  }

  async function settle(fixture: ComponentFixture<unknown>) {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  const dates = (fixture: ComponentFixture<unknown>) =>
    fixture.nativeElement.querySelectorAll('input[type=date]') as NodeListOf<HTMLInputElement>;

  it('lists upcoming absences with type, range and a warning for open appointments', async () => {
    const { fixture } = await mount([SICK, VACATION]);

    const items = texts(fixture, '.list li').map((t) => t.replace(/\s+/g, ' '));
    expect(items).toHaveLength(2);
    expect(items[0]).toContain('4. November 2026');
    expect(items[0]).toContain('team.absences.types.Sick');
    expect(items[0]).toContain('team.absences.conflictCount');
    expect(items[1]).toContain('Di., 10. November 2026 – Sa., 14. November 2026');
    expect(items[1]).not.toContain('conflictCount');
  });

  it('says so when there are no absences', async () => {
    const { fixture } = await mount([]);

    expect(texts(fixture, '.hint')).toEqual(['team.absences.empty']);
  });

  it('starts the form today and enters a single day when no end is given', async () => {
    const { fixture, team, toast } = await mount();
    team.createAbsence.mockResolvedValue(created(SICK));
    expect(dates(fixture)[0].value).toBe('2026-11-03');

    await submit(fixture);

    expect(team.createAbsence).toHaveBeenCalledWith('st1', {
      type: 'Vacation',
      startDate: '2026-11-03',
      endDate: '2026-11-03',
    });
    expect(toast.show).toHaveBeenCalledWith('team.saved');
    expect(fixture.nativeElement.querySelector('.warning')).toBeNull();
    expect(team.listAbsences).toHaveBeenCalledTimes(2);
  });

  it('sends the chosen type and range', async () => {
    const { fixture, team } = await mount();
    team.createAbsence.mockResolvedValue(created(VACATION));

    const select = fixture.nativeElement.querySelector('select') as HTMLSelectElement;
    select.value = 'Private';
    select.dispatchEvent(new Event('input'));
    select.dispatchEvent(new Event('change'));
    dates(fixture)[0].value = '2026-11-10';
    dates(fixture)[0].dispatchEvent(new Event('input'));
    dates(fixture)[1].value = '2026-11-14';
    dates(fixture)[1].dispatchEvent(new Event('input'));
    await submit(fixture);

    expect(team.createAbsence).toHaveBeenCalledWith('st1', {
      type: 'Private',
      startDate: '2026-11-10',
      endDate: '2026-11-14',
    });
  });

  it('warns about appointments in the new absence and lets the salon dismiss the warning', async () => {
    const { fixture, team } = await mount();
    team.createAbsence.mockResolvedValue(
      created({ ...SICK, conflictCount: 2 }, [
        {
          id: 'p1',
          startUtc: '2026-11-04T09:30:00Z',
          endUtc: '2026-11-04T10:00:00Z',
          serviceName: 'Färben',
          customerName: 'Kim Beispiel',
          status: 'Confirmed',
        },
        {
          id: 'p2',
          startUtc: '2026-11-04T13:00:00Z',
          endUtc: '2026-11-04T13:30:00Z',
          serviceName: 'Schneiden',
          customerName: 'Lee Muster',
          status: 'Requested',
        },
      ]),
    );

    await submit(fixture);

    const warning = fixture.nativeElement.querySelector('.warning') as HTMLElement;
    expect(warning.getAttribute('role')).toBe('status');
    expect(warning.querySelector('h3')?.textContent).toContain('team.absences.conflictTitle');
    const entries = texts(fixture, '.warning li').map((t) => t.replace(/\s+/g, ' '));
    // Times are shown in the salon's time zone (Berlin, UTC+1 in November).
    expect(entries[0]).toBe('04.11.2026, 10:30 · Färben · Kim Beispiel');
    expect(entries[1]).toBe('04.11.2026, 14:00 · Schneiden · Lee Muster (team.absences.requested)');

    (warning.querySelector('button') as HTMLButtonElement).click();
    await settle(fixture);
    expect(fixture.nativeElement.querySelector('.warning')).toBeNull();
  });

  it('refuses an end before the start and a range of more than a year', async () => {
    const { fixture, team } = await mount();

    dates(fixture)[1].value = '2026-11-02';
    dates(fixture)[1].dispatchEvent(new Event('input'));
    await submit(fixture);
    expect(texts(fixture, 'mat-error')).toEqual(['Ende vor Beginn.']);

    dates(fixture)[1].value = '2028-01-01';
    dates(fixture)[1].dispatchEvent(new Event('input'));
    await submit(fixture);
    expect(texts(fixture, 'mat-error')).toEqual(['Zeitraum zu lang.']);
    expect(team.createAbsence).not.toHaveBeenCalled();
  });

  it('shows the translated error of the server', async () => {
    const { fixture, team } = await mount();
    team.createAbsence.mockRejectedValue(new ApiError(400, 'absence.date_in_past', 'x'));

    await submit(fixture);

    expect(texts(fixture, '[role=alert]')).toEqual(['Abwesenheit in der Vergangenheit.']);
  });

  it('removes an absence and reloads', async () => {
    const { fixture, team, toast } = await mount([VACATION]);

    (fixture.nativeElement.querySelector('.remove') as HTMLButtonElement).click();
    await settle(fixture);

    expect(team.deleteAbsence).toHaveBeenCalledWith('st1', 'a1');
    await vi.waitFor(() => expect(team.listAbsences).toHaveBeenCalledTimes(2));
    expect(toast.show).toHaveBeenCalledWith('team.removed');
  });
});
