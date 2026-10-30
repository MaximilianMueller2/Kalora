import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';
import { OWNER, fakeAuth } from '../../../testing/auth-fixtures';
import { AuthService } from '../auth/auth.service';
import { ANNA_COLUMN, BEN_COLUMN, DAY, calendarData, fakeCalendar } from './calendar-fixtures';
import { CalendarService } from './calendar.service';
import { CalendarStore } from './calendar.store';

async function setup(data = calendarData()) {
  const service = fakeCalendar(data);
  TestBed.configureTestingModule({
    providers: [
      CalendarStore,
      { provide: CalendarService, useValue: service },
      { provide: AuthService, useValue: fakeAuth(OWNER) },
    ],
  });
  const store = TestBed.inject(CalendarStore);
  store.goTo(DAY);
  await settle();
  return { store, service };
}

async function settle() {
  TestBed.tick();
  await Promise.resolve();
  await new Promise((resolve) => setTimeout(resolve));
  TestBed.tick();
}

describe('CalendarStore', () => {
  it('loads the shown day and offers a lane for every person', async () => {
    const { store, service } = await setup();

    expect(service.get).toHaveBeenCalledWith(DAY, DAY);
    expect(store.people().map((p) => p.displayName)).toEqual(['Anna', 'Ben']);
    expect(store.lanes(false).map((lane) => [lane.column.displayName, lane.date])).toEqual([
      ['Anna', DAY],
      ['Ben', DAY],
    ]);
    expect(store.slotMinutes()).toBe(15);
    expect(store.zone()).toBe('Europe/Berlin');
  });

  it('asks for the whole week, Monday to Sunday, in the week view', async () => {
    const { store, service } = await setup();

    store.setView('week');
    await settle();

    expect(store.days()).toEqual([
      '2026-11-02',
      '2026-11-03',
      '2026-11-04',
      '2026-11-05',
      '2026-11-06',
      '2026-11-07',
      '2026-11-08',
    ]);
    expect(service.get).toHaveBeenLastCalledWith('2026-11-02', '2026-11-08');
    // One person, seven days.
    expect(store.lanes(false).map((lane) => [lane.column.displayName, lane.date])).toEqual(
      store.days().map((date) => ['Anna', date]),
    );
  });

  it('shows one person on a phone in the day view', async () => {
    const { store } = await setup();

    expect(store.lanes(true).map((lane) => lane.column.displayName)).toEqual(['Anna']);
    store.stepPerson(1);
    expect(store.lanes(true).map((lane) => lane.column.displayName)).toEqual(['Ben']);
    store.stepPerson(1);
    expect(store.focused()?.displayName).toBe('Ben'); // stays at the end
    store.stepPerson(-1);
    store.stepPerson(-1);
    expect(store.focused()?.displayName).toBe('Anna');
  });

  it('moves by a day in the day view and by a week in the week view', async () => {
    const { store, service } = await setup();

    store.step(1);
    expect(store.date()).toBe('2026-11-05');
    store.step(-1);
    store.step(-1);
    expect(store.date()).toBe('2026-11-03');
    store.setView('week');
    store.step(1);
    expect(store.date()).toBe('2026-11-10');
    await settle();
    expect(service.get).toHaveBeenLastCalledWith('2026-11-09', '2026-11-15');
  });

  it('hides and shows people with the filter without asking the server again', async () => {
    const { store, service } = await setup();
    const calls = service.get.mock.calls.length;

    store.toggleStaff('s1');
    expect(store.lanes(false).map((lane) => lane.column.displayName)).toEqual(['Ben']);
    store.toggleStaff('s1');
    expect(store.lanes(false)).toHaveLength(2);
    expect(service.get.mock.calls.length).toBe(calls);
  });

  it('chooses the person of the week view', async () => {
    const { store } = await setup();

    store.focusOn('s2');
    store.setView('week');

    expect(store.lanes(false).every((lane) => lane.column.staffId === 's2')).toBe(true);
  });

  it('stretches the visible hours to what happens in the columns', async () => {
    const early = {
      ...BEN_COLUMN,
      working: [{ startUtc: '2026-11-04T05:00:00Z', endUtc: '2026-11-04T19:30:00Z' }],
    };
    const { store } = await setup(calendarData([ANNA_COLUMN, early]));

    expect(store.window(false)).toEqual({ startMinute: 6 * 60, endMinute: 21 * 60 });
    store.toggleStaff('s2');
    expect(store.window(false)).toEqual({ startMinute: 8 * 60, endMinute: 18 * 60 });
  });

  it('marks today and follows the clock', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-11-04T09:00:00Z'));
    try {
      const { store } = await setup();
      expect(store.lanes(false).every((lane) => lane.isToday)).toBe(true);

      store.goTo('2026-11-05');
      expect(store.lanes(false).some((lane) => lane.isToday)).toBe(false);

      store.today();
      expect(store.date()).toBe(DAY);
    } finally {
      vi.useRealTimers();
    }
  });

  it('reports a failed load', async () => {
    const service = fakeCalendar();
    service.get.mockRejectedValue(new Error('offline'));
    TestBed.configureTestingModule({
      providers: [
        CalendarStore,
        { provide: CalendarService, useValue: service },
        { provide: AuthService, useValue: fakeAuth(OWNER) },
      ],
    });
    const store = TestBed.inject(CalendarStore);
    await settle();

    expect(store.failed()).toBe(true);
    expect(store.lanes(false)).toEqual([]);
  });

  it('loads again on demand', async () => {
    const { store, service } = await setup();
    const calls = service.get.mock.calls.length;

    store.reload();
    await settle();

    expect(service.get.mock.calls.length).toBe(calls + 1);
  });
});
