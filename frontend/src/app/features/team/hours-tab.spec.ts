import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { OWNER, fakeAuth } from '../../../testing/auth-fixtures';
import { texts } from '../../../testing/forms';
import { provideTestI18n } from '../../../testing/test-i18n';
import { ApiError } from '../../core/http/api-error';
import { LanguageService } from '../../core/i18n/language.service';
import { Toast } from '../../core/ui/toast';
import { WEEKDAYS } from '../../shared/week-schedule/week-schedule.model';
import { AuthService } from '../auth/auth.service';
import { HoursTab, toDays, toWeekSchedule, versionInEffect } from './hours-tab';
import { fakeTeam, routeWithStaffId } from './team-fixtures';
import { PlanDay, ScheduleVersion } from './team.models';
import { TeamService } from './team.service';

const days = (monday: [string, string][] = [], tuesday: [string, string][] = []): PlanDay[] =>
  WEEKDAYS.map((weekday) => ({
    weekday,
    blocks: (weekday === 'Monday' ? monday : weekday === 'Tuesday' ? tuesday : []).map(
      ([start, end]) => ({ start, end }),
    ),
  }));

const CURRENT: ScheduleVersion = {
  validFrom: '2026-10-01',
  validTo: '2026-11-09',
  days: days([['09:00', '18:00']]),
};
const UPCOMING: ScheduleVersion = {
  validFrom: '2026-11-10',
  validTo: null,
  days: days([], [['09:00', '12:00']]),
};

describe('versionInEffect', () => {
  it('is the latest plan that has already started', () => {
    expect(versionInEffect([CURRENT, UPCOMING], '2026-11-03')).toBe(CURRENT);
    expect(versionInEffect([CURRENT, UPCOMING], '2026-11-10')).toBe(UPCOMING);
    expect(versionInEffect([CURRENT, UPCOMING], '2026-12-01')).toBe(UPCOMING);
  });

  it('is nothing before the first plan starts or without plans', () => {
    expect(versionInEffect([UPCOMING], '2026-11-03')).toBeUndefined();
    expect(versionInEffect([], '2026-11-03')).toBeUndefined();
  });
});

describe('plan conversion', () => {
  it('turns a plan into the editor model and back', () => {
    const schedule = toWeekSchedule(CURRENT);

    expect(schedule.Monday).toEqual([{ start: '09:00', end: '18:00' }]);
    expect(schedule.Sunday).toEqual([]);
    expect(toDays(schedule)).toEqual(CURRENT.days);
  });
});

describe('HoursTab', () => {
  beforeEach(() => vi.useFakeTimers({ toFake: ['Date'], now: new Date('2026-11-03T09:00:00Z') }));
  afterEach(() => vi.useRealTimers());

  async function mount(versions: ScheduleVersion[]) {
    localStorage.clear();
    const team = fakeTeam();
    team.getSchedule.mockResolvedValue({ versions });
    team.saveSchedule.mockImplementation((_id, request) =>
      Promise.resolve({
        versions: [{ validFrom: request.validFrom, validTo: null, days: request.days }],
      }),
    );
    const toast = { show: vi.fn() };
    TestBed.configureTestingModule({
      providers: [
        provideTestI18n(),
        { provide: ActivatedRoute, useValue: routeWithStaffId('st1') },
        { provide: TeamService, useValue: team },
        { provide: AuthService, useValue: fakeAuth(OWNER) },
        { provide: Toast, useValue: toast },
      ],
    });
    await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
    await TestBed.inject(LanguageService).use('de');
    const fixture = TestBed.createComponent(HoursTab);
    await settle(fixture);
    return { fixture, team, toast };
  }

  async function settle(fixture: ComponentFixture<unknown>) {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  const dateInput = (fixture: ComponentFixture<unknown>) =>
    fixture.nativeElement.querySelector('input[type=date]') as HTMLInputElement;
  const timeInputs = (fixture: ComponentFixture<unknown>) =>
    Array.from<HTMLInputElement>(fixture.nativeElement.querySelectorAll('input[type=time]')).map(
      (i) => i.value,
    );
  const saveButton = (fixture: ComponentFixture<unknown>) =>
    fixture.nativeElement.querySelector('.actions button') as HTMLButtonElement;

  it('shows the plan in effect in the editor, valid from today', async () => {
    const { fixture } = await mount([CURRENT, UPCOMING]);

    expect(timeInputs(fixture)).toEqual(['09:00', '18:00']);
    expect(dateInput(fixture).value).toBe('2026-11-03');
    expect(dateInput(fixture).min).toBe('2026-11-03');
  });

  it('lists the current plan and the planned change with a summary', async () => {
    const { fixture } = await mount([CURRENT, UPCOMING]);

    const items = texts(fixture, '.versions li').map((t) => t.replace(/\s+/g, ' '));
    expect(items[0]).toContain('team.hours.currentPlan');
    expect(items[0]).toContain('team.hours.until');
    expect(items[0]).toContain('Mo 09:00–18:00');
    expect(items[1]).toContain('team.hours.fromDate');
    expect(items[1]).toContain('Di 09:00–12:00');
  });

  it('saves the edited plan from the chosen date', async () => {
    const { fixture, team, toast } = await mount([CURRENT]);

    const end = fixture.nativeElement.querySelectorAll('input[type=time]')[1] as HTMLInputElement;
    end.value = '17:00';
    end.dispatchEvent(new Event('input'));
    end.dispatchEvent(new Event('change'));
    const date = dateInput(fixture);
    date.value = '2026-11-17';
    date.dispatchEvent(new Event('input'));
    await settle(fixture);
    saveButton(fixture).click();
    await settle(fixture);

    expect(team.saveSchedule).toHaveBeenCalledWith('st1', {
      validFrom: '2026-11-17',
      days: days([['09:00', '17:00']]),
    });
    expect(toast.show).toHaveBeenCalledWith('team.saved');
  });

  it('loads a planned change into the editor with its own start date', async () => {
    const { fixture } = await mount([CURRENT, UPCOMING]);

    (fixture.nativeElement.querySelectorAll('.versions button')[1] as HTMLButtonElement).click();
    await settle(fixture);

    expect(dateInput(fixture).value).toBe('2026-11-10');
    expect(timeInputs(fixture)).toEqual(['09:00', '12:00']);
  });

  it('can only replace the running plan from today on', async () => {
    const { fixture } = await mount([CURRENT, UPCOMING]);
    (fixture.nativeElement.querySelectorAll('.versions button')[1] as HTMLButtonElement).click();
    await settle(fixture);

    (fixture.nativeElement.querySelectorAll('.versions button')[0] as HTMLButtonElement).click();
    await settle(fixture);

    expect(dateInput(fixture).value).toBe('2026-11-03');
    expect(timeInputs(fixture)).toEqual(['09:00', '18:00']);
  });

  it('cannot save an empty plan and says why', async () => {
    const { fixture, team } = await mount([]);

    expect(texts(fixture, '.hint')).toContain('team.hours.none');
    expect(texts(fixture, '.hint')).toContain('team.hours.emptyHint');
    expect(saveButton(fixture).disabled).toBe(true);
    expect(team.saveSchedule).not.toHaveBeenCalled();
  });

  it('cannot save without a start date', async () => {
    const { fixture } = await mount([CURRENT]);

    const date = dateInput(fixture);
    date.value = '';
    date.dispatchEvent(new Event('input'));
    await settle(fixture);

    expect(saveButton(fixture).disabled).toBe(true);
  });

  it('shows the translated error of the server', async () => {
    const { fixture, team } = await mount([CURRENT]);
    team.saveSchedule.mockRejectedValue(new ApiError(400, 'staff.schedule_empty', 'x'));

    saveButton(fixture).click();
    await settle(fixture);

    expect(texts(fixture, '[role=alert]')).toEqual(['Plan leer.']);
  });
});
