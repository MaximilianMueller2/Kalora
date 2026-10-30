import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom, of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { provideTestI18n } from '../../../testing/test-i18n';
import { ApiError } from '../../core/http/api-error';
import { LanguageService } from '../../core/i18n/language.service';
import { Toast } from '../../core/ui/toast';
import { fakeRequests } from '../requests/requests-fixtures';
import { RequestsService } from '../requests/requests.service';
import { Appointment } from '../requests/requests.models';
import { AppointmentPanel } from './appointment-panel';
import { appointment, fakeCalendar } from './calendar-fixtures';
import { CalendarService } from './calendar.service';

const NOW = Date.parse('2026-11-03T09:00:00Z');
const PEOPLE = [
  { staffId: 's1', displayName: 'Anna' },
  { staffId: 's2', displayName: 'Ben' },
];

async function mount(subject: Appointment, options: { now?: number; dialogResult?: unknown } = {}) {
  localStorage.clear();
  const requests = fakeRequests();
  const calendar = fakeCalendar();
  const toast = { show: vi.fn() };
  const dialog = {
    open: vi.fn().mockImplementation(() => ({ afterClosed: () => of(options.dialogResult) })),
  };
  TestBed.configureTestingModule({
    providers: [
      provideTestI18n(),
      { provide: RequestsService, useValue: requests },
      { provide: CalendarService, useValue: calendar },
      { provide: Toast, useValue: toast },
      { provide: MatDialog, useValue: dialog },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(AppointmentPanel);
  fixture.componentRef.setInput('appointment', subject);
  fixture.componentRef.setInput('zone', 'Europe/Berlin');
  fixture.componentRef.setInput('people', PEOPLE);
  fixture.componentRef.setInput('now', options.now ?? NOW);
  const changed = vi.fn();
  const closed = vi.fn();
  fixture.componentInstance.changed.subscribe(changed);
  fixture.componentInstance.closed.subscribe(closed);
  await settle(fixture);
  return { fixture, requests, calendar, toast, dialog, changed, closed };
}

async function settle(fixture: ComponentFixture<unknown>) {
  fixture.detectChanges();
  await fixture.whenStable();
  await new Promise((resolve) => setTimeout(resolve));
  fixture.detectChanges();
}

const root = (fixture: ComponentFixture<unknown>) => fixture.nativeElement as HTMLElement;
const plain = (text: string | null | undefined) => (text ?? '').replace(/\s+/g, ' ').trim();
const buttons = (fixture: ComponentFixture<unknown>) =>
  Array.from(root(fixture).querySelectorAll<HTMLButtonElement>('.actions button')).map((b) =>
    plain(b.textContent),
  );
const press = async (fixture: ComponentFixture<unknown>, label: string) => {
  const button = Array.from(
    root(fixture).querySelectorAll<HTMLButtonElement>('.actions button'),
  ).find((b) => plain(b.textContent) === label);
  button?.click();
  await settle(fixture);
};

const open = appointment('r', 9, 10, {
  status: 'Requested',
  nextStates: ['Confirmed', 'Declined', 'Expired', 'Cancelled'],
});

describe('AppointmentPanel', () => {
  it('shows what the appointment is about', async () => {
    const { fixture } = await mount(
      appointment('a', 9, 10, { source: 'Manual', internalNote: 'Stammkunde' }),
    );

    const facts = Array.from(root(fixture).querySelectorAll('dd')).map((dd) =>
      plain(dd.textContent),
    );
    expect(plain(root(fixture).querySelector('h2')?.textContent)).toBe('Haarschnitt');
    expect(plain(root(fixture).querySelector('.status')?.textContent)).toBe(
      'calendar.status.Confirmed',
    );
    expect(facts[0]).toBe('Mi., 4. November 2026, 10:00 – 11:00');
    expect(facts[1]).toBe('Anna');
    expect(facts[2]).toContain('Kim Kundin');
    expect(facts).toContain('Bitte ohne Föhn');
    expect(facts).toContain('Stammkunde');
    expect(facts).toContain('calendar.panel.manual');
    expect(
      Array.from(root(fixture).querySelectorAll<HTMLAnchorElement>('a.contact')).map((a) =>
        a.getAttribute('href'),
      ),
    ).toEqual(['tel:+49 170 1234567', 'mailto:kim@example.com']);
  });

  it('offers only what the status allows', async () => {
    expect(buttons((await mount(open)).fixture)).toEqual([
      'requests.confirm',
      'requests.declineAction',
    ]);
    TestBed.resetTestingModule();
    expect(buttons((await mount(appointment('a', 9, 10))).fixture)).toEqual([
      'calendar.panel.move',
      'calendar.panel.cancel',
    ]);
    TestBed.resetTestingModule();
    const started = await mount(appointment('a', 9, 10), {
      now: Date.parse('2026-11-04T09:30:00Z'),
    });
    expect(buttons(started.fixture)).toEqual([
      'calendar.panel.attended',
      'calendar.panel.noShow',
      'calendar.panel.move',
      'calendar.panel.cancel',
    ]);
    TestBed.resetTestingModule();
    const over = await mount(appointment('a', 9, 10), { now: Date.parse('2026-11-04T12:00:00Z') });
    expect(buttons(over.fixture)).toEqual([
      'calendar.panel.attended',
      'calendar.panel.noShow',
      'calendar.panel.cancel',
    ]);
    TestBed.resetTestingModule();
    const done = await mount(appointment('a', 9, 10, { status: 'Completed', nextStates: [] }));
    expect(buttons(done.fixture)).toEqual([]);
  });

  it('accepts a request', async () => {
    const { fixture, requests, toast, changed } = await mount(open);

    await press(fixture, 'requests.confirm');

    expect(requests.confirm).toHaveBeenCalledWith('r');
    expect(toast.show).toHaveBeenCalledWith('calendar.panel.confirmed');
    expect(changed).toHaveBeenCalledOnce();
  });

  it('declines with the reason from the dialog, or not at all when the dialog is cancelled', async () => {
    const withReason = await mount(open, { dialogResult: 'Betriebsausflug' });
    await press(withReason.fixture, 'requests.declineAction');
    expect(withReason.requests.decline).toHaveBeenCalledWith('r', 'Betriebsausflug');
    TestBed.resetTestingModule();

    const cancelled = await mount(open, { dialogResult: undefined });
    await press(cancelled.fixture, 'requests.declineAction');
    expect(cancelled.requests.decline).not.toHaveBeenCalled();
    expect(cancelled.changed).not.toHaveBeenCalled();
  });

  it('cancels with its own dialog texts', async () => {
    const { fixture, requests, dialog, changed } = await mount(appointment('a', 9, 10), {
      dialogResult: '',
    });

    await press(fixture, 'calendar.panel.cancel');

    expect(dialog.open.mock.calls[0][1].data).toEqual({
      titleKey: 'calendar.panel.cancelTitle',
      leadKey: 'calendar.panel.cancelLead',
      confirmKey: 'calendar.panel.cancelConfirm',
    });
    expect(requests.cancel).toHaveBeenCalledWith('a', null);
    expect(changed).toHaveBeenCalledOnce();
  });

  it('marks an appointment as attended or missed', async () => {
    const started = Date.parse('2026-11-04T12:00:00Z');
    const attended = await mount(appointment('a', 9, 10), { now: started });
    await press(attended.fixture, 'calendar.panel.attended');
    expect(attended.requests.markAttended).toHaveBeenCalledWith('a');
    TestBed.resetTestingModule();

    const missed = await mount(appointment('a', 9, 10), { now: started });
    await press(missed.fixture, 'calendar.panel.noShow');
    expect(missed.requests.markNoShow).toHaveBeenCalledWith('a');
    expect(missed.changed).toHaveBeenCalledOnce();
  });

  it('shows the error of the server and does not claim a change', async () => {
    const { fixture, requests, changed } = await mount(open);
    requests.confirm.mockRejectedValue(new ApiError(409, 'appointment.request_expired'));

    await press(fixture, 'requests.confirm');

    expect(root(fixture).querySelector('[role="alert"]')).not.toBeNull();
    expect(changed).not.toHaveBeenCalled();
    expect(buttons(fixture)).toContain('requests.confirm'); // can be tried again or declined
  });

  describe('moving', () => {
    const open = async (options = {}) => {
      const context = await mount(appointment('a', 9, 10), options);
      await press(context.fixture, 'calendar.panel.move');
      return context;
    };
    const field = (fixture: ComponentFixture<unknown>, name: string) =>
      root(fixture).querySelector<HTMLInputElement | HTMLSelectElement>(`[name="${name}"]`);
    const set = async (fixture: ComponentFixture<unknown>, name: string, value: string) => {
      const input = field(fixture, name);
      if (input) {
        input.value = value;
        input.dispatchEvent(new Event(input instanceof HTMLSelectElement ? 'change' : 'input'));
      }
      await settle(fixture);
    };

    it('starts with the current time and person', async () => {
      const { fixture } = await open();

      expect(field(fixture, 'date')?.value).toBe('2026-11-04');
      expect(field(fixture, 'time')?.value).toBe('10:00');
      expect(field(fixture, 'staff')?.value).toBe('s1');
      expect(field(fixture, 'time')?.getAttribute('step')).toBe('900');
      expect(buttons(fixture)).toEqual(['calendar.panel.moveConfirm', 'Abbrechen']); // the form replaces the other buttons
    });

    it('moves to the chosen local time and person', async () => {
      const { fixture, calendar, changed, toast } = await open();

      await set(fixture, 'date', '2026-11-05');
      await set(fixture, 'time', '14:30');
      await set(fixture, 'staff', 's2');
      root(fixture).querySelector<HTMLFormElement>('form')?.dispatchEvent(new Event('submit'));
      await settle(fixture);

      expect(calendar.move).toHaveBeenCalledWith('a', {
        startUtc: '2026-11-05T13:30:00.000Z',
        staffId: 's2',
        reason: null,
      });
      expect(toast.show).toHaveBeenCalledWith('calendar.panel.moved');
      expect(changed).toHaveBeenCalledOnce();
    });

    it('shows why a move failed and stays open', async () => {
      const { fixture, calendar, changed } = await open();
      calendar.move.mockRejectedValue(new ApiError(409, 'booking.slot_unavailable'));

      root(fixture).querySelector<HTMLFormElement>('form')?.dispatchEvent(new Event('submit'));
      await settle(fixture);

      expect(root(fixture).querySelector('[role="alert"]')).not.toBeNull();
      expect(root(fixture).querySelector('form')).not.toBeNull();
      expect(changed).not.toHaveBeenCalled();
    });
  });

  it('can be closed', async () => {
    const { fixture, closed } = await mount(open);

    root(fixture).querySelector<HTMLButtonElement>('.top button')?.click();

    expect(closed).toHaveBeenCalledOnce();
  });
});
