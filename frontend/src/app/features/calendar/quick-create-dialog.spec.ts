import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { provideTestI18n } from '../../../testing/test-i18n';
import { ApiError } from '../../core/http/api-error';
import { LanguageService } from '../../core/i18n/language.service';
import { Toast } from '../../core/ui/toast';
import { CatalogService } from '../services/catalog.service';
import { TeamService } from '../team/team.service';
import { fakeCalendar } from './calendar-fixtures';
import { CalendarService } from './calendar.service';
import { QuickCreateData, QuickCreateDialog } from './quick-create-dialog';

const DATA: QuickCreateData = {
  startUtc: '2026-11-04T09:00:00Z', // 10:00 in Berlin
  staffId: 's1',
  people: [
    { staffId: 's1', displayName: 'Anna' },
    { staffId: 's2', displayName: 'Ben' },
  ],
  zone: 'Europe/Berlin',
  slotMinutes: 15,
};

const service = (id: string, name: string, minutes: number, isActive = true) => ({
  id,
  name,
  durationMinutes: minutes,
  isActive,
});

async function mount() {
  localStorage.clear();
  const calendar = fakeCalendar();
  const ref = { close: vi.fn() };
  const toast = { show: vi.fn() };
  const catalog = {
    get: vi.fn().mockResolvedValue({
      categories: [
        {
          services: [
            service('cut', 'Haarschnitt', 60),
            service('color', 'Färben', 90),
            service('old', 'Alt', 30, false),
          ],
        },
      ],
    }),
  };
  const team = {
    getSkills: vi.fn().mockImplementation((id: string) =>
      Promise.resolve({
        skills:
          id === 's1' ? [{ serviceId: 'cut' }, { serviceId: 'old' }] : [{ serviceId: 'color' }],
      }),
    ),
  };
  TestBed.configureTestingModule({
    providers: [
      provideTestI18n(),
      { provide: MAT_DIALOG_DATA, useValue: DATA },
      { provide: MatDialogRef, useValue: ref },
      { provide: CalendarService, useValue: calendar },
      { provide: CatalogService, useValue: catalog },
      { provide: TeamService, useValue: team },
      { provide: Toast, useValue: toast },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(QuickCreateDialog);
  await settle(fixture);
  return { fixture, calendar, ref, toast, team };
}

async function settle(fixture: ComponentFixture<unknown>) {
  fixture.detectChanges();
  await fixture.whenStable();
  await new Promise((resolve) => setTimeout(resolve));
  fixture.detectChanges();
}

const root = (fixture: ComponentFixture<unknown>) => fixture.nativeElement as HTMLElement;
const field = (fixture: ComponentFixture<unknown>, name: string) =>
  root(fixture).querySelector<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>(
    `[name="${name}"]`,
  );

async function fill(fixture: ComponentFixture<unknown>, name: string, value: string) {
  const element = field(fixture, name);
  if (element) {
    element.value = value;
    element.dispatchEvent(new Event(element instanceof HTMLSelectElement ? 'change' : 'input'));
  }
  await settle(fixture);
}

const submit = async (fixture: ComponentFixture<unknown>) => {
  root(fixture).querySelector<HTMLFormElement>('form')?.dispatchEvent(new Event('submit'));
  await settle(fixture);
};

const press = async (fixture: ComponentFixture<unknown>, selector: string) => {
  root(fixture).querySelector<HTMLButtonElement>(selector)?.click();
  await settle(fixture);
};

const saveButton = (fixture: ComponentFixture<unknown>) =>
  root(fixture).querySelector<HTMLButtonElement>('button[type="submit"]');

describe('QuickCreateDialog', () => {
  const ready = mount;
  /** Longer than the pause before the search starts. */
  const pause = () => new Promise((resolve) => setTimeout(resolve, 300));

  it('starts at the tapped place, for the tapped person', async () => {
    const { fixture } = await ready();

    expect(field(fixture, 'date')?.value).toBe('2026-11-04');
    expect(field(fixture, 'start')?.value).toBe('10:00');
    expect(field(fixture, 'start')?.getAttribute('step')).toBe('900');
    expect((field(fixture, 'staff') as HTMLSelectElement).value).toBe('s1');
  });

  it('offers the active services of the chosen person only', async () => {
    const { fixture, team } = await ready();

    const options = () =>
      Array.from(field(fixture, 'service')?.querySelectorAll('option') ?? []).map((o) =>
        o.textContent?.replace(/\s+/g, ' ').trim(),
      );
    expect(options()).toEqual(['calendar.create.chooseService', 'Haarschnitt (60 min)']);

    await fill(fixture, 'staff', 's2');
    expect(team.getSkills).toHaveBeenCalledWith('s2');
    expect(options()).toEqual(['calendar.create.chooseService', 'Färben (90 min)']);
  });

  it('cannot be saved before a service and a customer are given', async () => {
    const { fixture } = await ready();
    expect(saveButton(fixture)?.disabled).toBe(true);

    await fill(fixture, 'service', 'cut');
    expect(saveButton(fixture)?.disabled).toBe(true);

    await fill(fixture, 'name', 'Willi');
    expect(saveButton(fixture)?.disabled).toBe(false);
  });

  it('saves an appointment for a new customer at the local time', async () => {
    const { fixture, calendar, ref, toast } = await ready();

    await fill(fixture, 'service', 'cut');
    await fill(fixture, 'name', ' Walk-in Willi ');
    await fill(fixture, 'phone', '+49 170 555');
    await fill(fixture, 'email', 'willi@example.com');
    await fill(fixture, 'note', 'Stammkunde');
    await fill(fixture, 'start', '14:15');
    await submit(fixture);

    expect(calendar.createAppointment).toHaveBeenCalledWith({
      serviceId: 'cut',
      staffId: 's1',
      startUtc: '2026-11-04T13:15:00.000Z',
      customerId: null,
      customerName: 'Walk-in Willi',
      customerEmail: 'willi@example.com',
      customerPhone: '+49 170 555',
      note: 'Stammkunde',
    });
    expect(toast.show).toHaveBeenCalledWith('calendar.create.appointmentSaved');
    expect(ref.close).toHaveBeenCalledWith(true);
  });

  it('finds known customers while typing, after a short pause and from two letters on', async () => {
    const { fixture, calendar } = await ready();
    calendar.searchCustomers.mockResolvedValue([
      { id: 'c1', name: 'Anna Schmidt', email: 'anna@example.com', phone: '+49 170 111' },
    ]);

    await fill(fixture, 'name', 'A');
    await pause();
    expect(calendar.searchCustomers).not.toHaveBeenCalled();

    await fill(fixture, 'name', 'Anna');
    expect(calendar.searchCustomers).not.toHaveBeenCalled(); // still waiting for a pause
    await pause();
    await settle(fixture);

    expect(calendar.searchCustomers).toHaveBeenCalledOnce();
    expect(calendar.searchCustomers).toHaveBeenCalledWith('Anna');
    expect(root(fixture).querySelector('.hits')?.textContent).toContain('Anna Schmidt');
  });

  it('books for a known customer by id and ignores what was typed in the other fields', async () => {
    const { fixture, calendar } = await ready();
    calendar.searchCustomers.mockResolvedValue([
      { id: 'c1', name: 'Anna Schmidt', email: 'anna@example.com', phone: null },
    ]);
    await fill(fixture, 'service', 'cut');
    await fill(fixture, 'name', 'Anna');
    await pause();
    await settle(fixture);

    await press(fixture, '.hits button');

    expect(root(fixture).querySelector('.chosen')?.textContent).toContain('Anna Schmidt');
    await submit(fixture);
    expect(calendar.createAppointment).toHaveBeenCalledWith(
      expect.objectContaining({
        customerId: 'c1',
        customerName: null,
        customerEmail: null,
        customerPhone: null,
      }),
    );
  });

  it('lets the customer choice be undone', async () => {
    const { fixture, calendar } = await ready();
    calendar.searchCustomers.mockResolvedValue([
      { id: 'c1', name: 'Anna Schmidt', email: null, phone: null },
    ]);
    await fill(fixture, 'name', 'Anna');
    await pause();
    await settle(fixture);
    await press(fixture, '.hits button');

    await press(fixture, '.chosen button');

    expect(root(fixture).querySelector('.chosen')).toBeNull();
    expect(field(fixture, 'name')).not.toBeNull();
  });

  it('shows why saving failed and stays open', async () => {
    const { fixture, calendar, ref } = await ready();
    calendar.createAppointment.mockRejectedValue(new ApiError(409, 'booking.slot_unavailable'));
    await fill(fixture, 'service', 'cut');
    await fill(fixture, 'name', 'Willi');

    await submit(fixture);

    expect(root(fixture).querySelector('[role="alert"]')).not.toBeNull();
    expect(ref.close).not.toHaveBeenCalled();
    expect(saveButton(fixture)?.disabled).toBe(false); // can be tried again
  });

  describe('as a blocker', () => {
    it('asks for the end and the label instead of service and customer', async () => {
      const { fixture } = await ready();

      await press(fixture, '.modes button:nth-child(2)');

      expect(field(fixture, 'service')).toBeNull();
      expect(field(fixture, 'end')?.value).toBe('10:30'); // half an hour by default
      expect(field(fixture, 'label')).not.toBeNull();
      expect(saveButton(fixture)?.disabled).toBe(false);
    });

    it('saves the blocker with start and end as instants', async () => {
      const { fixture, calendar, ref, toast } = await ready();
      await press(fixture, '.modes button:nth-child(2)');

      await fill(fixture, 'end', '11:00');
      await fill(fixture, 'label', ' Mittagspause ');
      await submit(fixture);

      expect(calendar.createBlock).toHaveBeenCalledWith({
        staffId: 's1',
        startUtc: '2026-11-04T09:00:00.000Z',
        endUtc: '2026-11-04T10:00:00.000Z',
        label: 'Mittagspause',
      });
      expect(toast.show).toHaveBeenCalledWith('calendar.create.blockSaved');
      expect(ref.close).toHaveBeenCalledWith(true);
    });

    it('sends no label when it was left empty', async () => {
      const { fixture, calendar } = await ready();
      await press(fixture, '.modes button:nth-child(2)');

      await submit(fixture);

      expect(calendar.createBlock).toHaveBeenCalledWith(expect.objectContaining({ label: null }));
    });
  });
});
