import { BreakpointObserver } from '@angular/cdk/layout';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { TranslocoService } from '@jsverse/transloco';
import { BehaviorSubject, firstValueFrom, of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { OWNER, fakeAuth } from '../../../testing/auth-fixtures';
import { provideTestI18n } from '../../../testing/test-i18n';
import { ApiError } from '../../core/http/api-error';
import { ErrorNotifier } from '../../core/http/error-notifier';
import { LanguageService } from '../../core/i18n/language.service';
import { Toast } from '../../core/ui/toast';
import { AuthService } from '../auth/auth.service';
import { RequestsService } from '../requests/requests.service';
import { fakeRequests } from '../requests/requests-fixtures';
import { ANNA_COLUMN, BEN_COLUMN, DAY, calendarData, fakeCalendar } from './calendar-fixtures';
import { CalendarGrid, MoveDrop } from './calendar-grid';
import { CalendarPage } from './calendar-page';
import { CalendarService } from './calendar.service';
import { CalendarStore } from './calendar.store';

async function mount(options: { desktop?: boolean; data?: ReturnType<typeof calendarData> } = {}) {
  localStorage.clear();
  const desktop = options.desktop ?? true;
  const calendar = fakeCalendar(options.data);
  const toast = { show: vi.fn() };
  const errors = { show: vi.fn() };
  const dialog = { open: vi.fn().mockImplementation(() => ({ afterClosed: () => of(true) })) };
  const state = new BehaviorSubject({ matches: desktop, breakpoints: {} });
  TestBed.configureTestingModule({
    providers: [
      provideTestI18n(),
      { provide: CalendarService, useValue: calendar },
      { provide: RequestsService, useValue: fakeRequests() },
      { provide: AuthService, useValue: fakeAuth(OWNER) },
      { provide: Toast, useValue: toast },
      { provide: ErrorNotifier, useValue: errors },
      { provide: MatDialog, useValue: dialog },
      { provide: BreakpointObserver, useValue: { observe: () => state, isMatched: () => desktop } },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(CalendarPage);
  await settle(fixture);
  // Show the known test day instead of "today".
  fixture.debugElement.injector.get(CalendarStore).goTo(DAY);
  await settle(fixture);
  return { fixture, calendar, toast, errors, dialog };
}

async function settle(fixture: ComponentFixture<unknown>) {
  fixture.detectChanges();
  await fixture.whenStable();
  await new Promise((resolve) => setTimeout(resolve));
  fixture.detectChanges();
}

const root = (fixture: ComponentFixture<unknown>) => fixture.nativeElement as HTMLElement;
const qa = (fixture: ComponentFixture<unknown>, selector: string) =>
  Array.from(root(fixture).querySelectorAll<HTMLElement>(selector));
const plain = (text: string | null | undefined) => (text ?? '').replace(/\s+/g, ' ').trim();
const press = async (fixture: ComponentFixture<unknown>, element: HTMLElement | undefined) => {
  element?.click();
  await settle(fixture);
};
const grid = (fixture: ComponentFixture<unknown>) =>
  fixture.debugElement.query((e) => e.componentInstance instanceof CalendarGrid)
    .componentInstance as CalendarGrid;

describe('CalendarPage', () => {
  it('loads the day and shows a column for everybody with the date as title', async () => {
    const { fixture, calendar } = await mount();

    expect(calendar.get).toHaveBeenCalledWith(DAY, DAY);
    expect(qa(fixture, '.head').map((h) => plain(h.textContent))).toEqual(['Anna', 'Ben']);
    expect(plain(root(fixture).querySelector('h1')?.textContent)).toBe('Mi., 4. November 2026');
  });

  it('moves through the days and back to today', async () => {
    const { fixture, calendar } = await mount();

    await press(
      fixture,
      root(fixture).querySelector<HTMLElement>('.nav button:last-child') ?? undefined,
    );
    expect(calendar.get).toHaveBeenLastCalledWith('2026-11-05', '2026-11-05');
    await press(
      fixture,
      root(fixture).querySelector<HTMLElement>('.nav button:first-child') ?? undefined,
    );
    await press(
      fixture,
      root(fixture).querySelector<HTMLElement>('.nav button:first-child') ?? undefined,
    );
    expect(calendar.get).toHaveBeenLastCalledWith('2026-11-03', '2026-11-03');
  });

  it('switches to the week of one person', async () => {
    const { fixture, calendar } = await mount();

    await press(fixture, qa(fixture, '.views button')[1]);

    expect(calendar.get).toHaveBeenLastCalledWith('2026-11-02', '2026-11-08');
    expect(qa(fixture, '.head')).toHaveLength(7);
    expect(plain(root(fixture).querySelector('h1')?.textContent)).toBe('02.11.2026 – 08.11.2026');
    // The person of the week can be chosen.
    await press(fixture, qa(fixture, '.people button')[1]);
    expect(
      qa(fixture, '.lane').every(
        (lane) => lane.style.getPropertyValue('--staff') === BEN_COLUMN.color,
      ),
    ).toBe(true);
  });

  it('lets people be hidden in the day view', async () => {
    const { fixture } = await mount();

    await press(fixture, qa(fixture, '.people button')[0]);

    expect(qa(fixture, '.head').map((h) => plain(h.textContent))).toEqual(['Ben']);
  });

  it('opens the details of a tapped appointment and closes them again', async () => {
    const { fixture } = await mount();

    await press(fixture, qa(fixture, '.event')[0]);

    expect(root(fixture).querySelector('app-appointment-panel')).not.toBeNull();
    expect(plain(root(fixture).querySelector('.panel h2')?.textContent)).toBe('Haarschnitt');
    expect(qa(fixture, '.event')[0].getAttribute('aria-pressed')).toBe('true');

    await press(
      fixture,
      root(fixture).querySelector<HTMLElement>('.panel .top button') ?? undefined,
    );
    expect(root(fixture).querySelector('.panel')).toBeNull();
  });

  it('opens a blocker with the option to remove it', async () => {
    const { fixture, calendar } = await mount();

    await press(fixture, qa(fixture, '.block')[0]);
    expect(root(fixture).querySelector('app-block-panel')).not.toBeNull();
    await press(
      fixture,
      root(fixture).querySelector<HTMLElement>('.panel .actions button') ?? undefined,
    );

    expect(calendar.deleteBlock).toHaveBeenCalledWith('b1');
    expect(calendar.get.mock.calls.length).toBeGreaterThan(1); // the calendar loaded again
    expect(root(fixture).querySelector('.panel')).toBeNull();
  });

  it('opens the create dialog for a tap on free space, with the tapped person and time', async () => {
    const { fixture, dialog } = await mount();
    const startUtc = '2026-11-04T09:30:00.000Z';

    grid(fixture).slotTap.emit({ lane: grid(fixture).lanes()[1], startUtc });
    await settle(fixture);

    expect(dialog.open).toHaveBeenCalledOnce();
    expect(dialog.open.mock.calls[0][1].data).toEqual({
      startUtc,
      staffId: 's2',
      people: [
        { staffId: 's1', displayName: 'Anna' },
        { staffId: 's2', displayName: 'Ben' },
      ],
      zone: 'Europe/Berlin',
      slotMinutes: 15,
    });
  });

  it('has a "New" button for those who cannot tap into the grid, and reloads after saving', async () => {
    const { fixture, dialog, calendar } = await mount();
    const calls = calendar.get.mock.calls.length;

    await press(fixture, root(fixture).querySelector<HTMLElement>('.new') ?? undefined);

    expect(dialog.open).toHaveBeenCalledOnce();
    expect(dialog.open.mock.calls[0][1].data.staffId).toBe('s1');
    expect(calendar.get.mock.calls.length).toBe(calls + 1);
  });

  describe('moving by drag and drop', () => {
    const drop = (staffId = 's2'): MoveDrop => ({
      appointment: ANNA_COLUMN.appointments[0],
      staffId,
      startUtc: '2026-11-04T13:00:00.000Z',
    });

    it('sends the new place, tells the salon and loads again', async () => {
      const { fixture, calendar, toast } = await mount();
      const calls = calendar.get.mock.calls.length;

      grid(fixture).moved.emit(drop());
      await settle(fixture);

      expect(calendar.move).toHaveBeenCalledWith('a1', {
        startUtc: '2026-11-04T13:00:00.000Z',
        staffId: 's2',
        reason: null,
      });
      expect(toast.show).toHaveBeenCalledWith('calendar.moved');
      expect(calendar.get.mock.calls.length).toBe(calls + 1);
    });

    it("shows the server's refusal and puts the card back by loading again", async () => {
      const { fixture, calendar, errors, toast } = await mount();
      const error = new ApiError(409, 'booking.slot_unavailable');
      calendar.move.mockRejectedValue(error);
      const calls = calendar.get.mock.calls.length;

      grid(fixture).moved.emit(drop());
      await settle(fixture);

      expect(errors.show).toHaveBeenCalledWith(error);
      expect(toast.show).not.toHaveBeenCalledWith('calendar.moved');
      expect(calendar.get.mock.calls.length).toBe(calls + 1);
    });

    it('says why a drop was refused before asking the server', async () => {
      const { fixture, calendar, toast } = await mount();

      grid(fixture).rejected.emit({ appointment: ANNA_COLUMN.appointments[0], reason: 'block' });
      await settle(fixture);

      expect(toast.show).toHaveBeenCalledWith('calendar.conflict.block');
      expect(calendar.move).not.toHaveBeenCalled();
    });
  });

  describe('on a phone', () => {
    const swipe = async (fixture: ComponentFixture<unknown>, dx: number, dy = 0) => {
      const main = root(fixture).querySelector<HTMLElement>('.main');
      const touch = (x: number, y: number) => ({ clientX: x, clientY: y });
      const start = new Event('touchstart') as Event & { touches: unknown[] };
      start.touches = [touch(200, 300)];
      main?.dispatchEvent(start);
      const end = new Event('touchend') as Event & { changedTouches: unknown[] };
      end.changedTouches = [touch(200 + dx, 300 + dy)];
      main?.dispatchEvent(end);
      await settle(fixture);
    };

    it('shows one person at a time and tabs to choose', async () => {
      const { fixture } = await mount({ desktop: false });

      expect(qa(fixture, '.head').map((h) => plain(h.textContent))).toEqual(['Anna']);
      expect(qa(fixture, '.people button').map((b) => plain(b.textContent))).toEqual([
        'Anna',
        'Ben',
      ]);

      await press(fixture, qa(fixture, '.people button')[1]);
      expect(qa(fixture, '.head').map((h) => plain(h.textContent))).toEqual(['Ben']);
    });

    it('swipes to the next and the previous person', async () => {
      const { fixture } = await mount({ desktop: false });

      await swipe(fixture, -120); // to the left: next
      expect(qa(fixture, '.head').map((h) => plain(h.textContent))).toEqual(['Ben']);

      await swipe(fixture, 120); // to the right: back
      expect(qa(fixture, '.head').map((h) => plain(h.textContent))).toEqual(['Anna']);
    });

    it('ignores short and mostly vertical swipes, which are scrolling', async () => {
      const { fixture } = await mount({ desktop: false });

      await swipe(fixture, -30);
      await swipe(fixture, -100, 90);

      expect(qa(fixture, '.head').map((h) => plain(h.textContent))).toEqual(['Anna']);
    });
  });

  it('says so when there is nobody in the calendar', async () => {
    const { fixture } = await mount({ data: calendarData([]) });

    expect(root(fixture).querySelector('app-calendar-grid')).toBeNull();
    expect(plain(root(fixture).querySelector('.notice')?.textContent)).toBe('calendar.empty');
  });

  it('says so when loading failed', async () => {
    localStorage.clear();
    const calendar = fakeCalendar();
    calendar.get.mockRejectedValue(new Error('offline'));
    TestBed.configureTestingModule({
      providers: [
        provideTestI18n(),
        { provide: CalendarService, useValue: calendar },
        { provide: RequestsService, useValue: fakeRequests() },
        { provide: AuthService, useValue: fakeAuth(OWNER) },
        { provide: Toast, useValue: { show: vi.fn() } },
        { provide: ErrorNotifier, useValue: { show: vi.fn() } },
        { provide: MatDialog, useValue: { open: vi.fn() } },
        {
          provide: BreakpointObserver,
          useValue: {
            observe: () => new BehaviorSubject({ matches: true, breakpoints: {} }),
            isMatched: () => true,
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(CalendarPage);

    await settle(fixture);

    expect(plain(root(fixture).querySelector('[role="alert"]')?.textContent)).toBe(
      'calendar.loadFailed',
    );
  });
});
