import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom, of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { provideTestI18n } from '../../../testing/test-i18n';
import { ApiError } from '../../core/http/api-error';
import { LanguageService } from '../../core/i18n/language.service';
import { Toast } from '../../core/ui/toast';
import { fakeManage, managed } from './manage-fixtures';
import { ManagePage } from './manage-page';
import { ManageService } from './manage.service';
import { ManagedAppointment } from './manage.models';

async function mount(
  options: { appointment?: ManagedAppointment; confirm?: boolean; loadError?: Error } = {},
) {
  localStorage.clear();
  const manage = fakeManage(options.appointment);
  if (options.loadError) {
    manage.get.mockRejectedValue(options.loadError);
  }
  const toast = { show: vi.fn() };
  const dialog = {
    open: vi.fn().mockImplementation(() => ({ afterClosed: () => of(options.confirm ?? true) })),
  };
  TestBed.configureTestingModule({
    providers: [
      provideTestI18n(),
      provideRouter([]),
      { provide: ManageService, useValue: manage },
      { provide: Toast, useValue: toast },
      { provide: MatDialog, useValue: dialog },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(ManagePage);
  fixture.componentRef.setInput('token', 'secret-token');
  await settle(fixture);
  return { fixture, manage, toast, dialog };
}

async function settle(fixture: ComponentFixture<unknown>) {
  fixture.detectChanges();
  await fixture.whenStable();
  await new Promise((resolve) => setTimeout(resolve));
  fixture.detectChanges();
}

const root = (fixture: ComponentFixture<unknown>) => fixture.nativeElement as HTMLElement;
const text = (fixture: ComponentFixture<unknown>, selector: string) =>
  (root(fixture).querySelector(selector)?.textContent ?? '').replace(/\s+/g, ' ').trim();
const q = (fixture: ComponentFixture<unknown>, selector: string) =>
  root(fixture).querySelector<HTMLElement>(selector);

describe('ManagePage', () => {
  it('asks for the appointment of the token in the link and shows salon, service, time and place', async () => {
    const { fixture, manage } = await mount();

    expect(manage.get).toHaveBeenCalledWith('secret-token');
    expect(text(fixture, '.salon')).toBe('Haarwerk Müller');
    expect(text(fixture, 'h1')).toBe('Haarschnitt');
    expect(text(fixture, '.status')).toBe('manage.status.Confirmed');
    expect(
      Array.from(root(fixture).querySelectorAll('dd')).map((dd) =>
        dd.textContent?.replace(/\s+/g, ' ').trim(),
      ),
    ).toEqual(['Do., 5. November 2026, 10:00 – 11:00', 'Hauptstraße 1, 10115 Berlin']);
  });

  it('offers the calendar links for a confirmed appointment, with the token for the .ics file', async () => {
    const { fixture } = await mount();

    const links = Array.from(
      root(fixture).querySelectorAll<HTMLAnchorElement>('app-calendar-actions a'),
    );
    expect(links).toHaveLength(3);
    expect(links[0].getAttribute('href')).toBe(
      '/api/public/appointments/secret-token/calendar.ics',
    );
    expect(links[1].href).toContain('https://calendar.google.com/calendar/render');
    expect(new URL(links[1].href).searchParams.get('text')).toBe('Haarschnitt – Haarwerk Müller');
    expect(new URL(links[2].href).searchParams.get('location')).toBe('Hauptstraße 1, 10115 Berlin');
  });

  it('has no calendar for an open request', async () => {
    const { fixture } = await mount({
      appointment: managed({ status: 'Requested', canAddToCalendar: false }),
    });

    expect(q(fixture, 'app-calendar-actions')).toBeNull();
    expect(text(fixture, '.status-text')).toBe('manage.statusText.Requested');
  });

  describe('cancelling', () => {
    it('says until when the customer can cancel online', async () => {
      const { fixture } = await mount();

      expect(text(fixture, '.cancel .hint')).toBe('manage.cancel.until');
      expect(q(fixture, '.cancel button')).not.toBeNull();
      expect(q(fixture, '.notice')).toBeNull();
    });

    it('asks first, then cancels and shows the new state with the invitation to ask again', async () => {
      const { fixture, manage, dialog, toast } = await mount();

      q(fixture, '.cancel button')?.click();
      await settle(fixture);

      expect(dialog.open).toHaveBeenCalledOnce();
      expect(manage.cancel).toHaveBeenCalledWith('secret-token');
      expect(toast.show).toHaveBeenCalledWith('manage.cancelled');
      expect(text(fixture, '.status')).toBe('manage.status.Cancelled');
      expect(q(fixture, '.cancel')).toBeNull();
      expect(q(fixture, 'app-calendar-actions')).toBeNull();
      expect(q(fixture, '.rebook')?.getAttribute('href')).toBe('/haarwerk');
    });

    it('does nothing when the customer keeps the appointment', async () => {
      const { fixture, manage } = await mount({ confirm: false });

      q(fixture, '.cancel button')?.click();
      await settle(fixture);

      expect(manage.cancel).not.toHaveBeenCalled();
      expect(text(fixture, '.status')).toBe('manage.status.Confirmed');
    });

    it('shows why cancelling failed and loads the true state again', async () => {
      const { fixture, manage } = await mount();
      manage.cancel.mockRejectedValue(
        new ApiError(409, 'appointment.cancellation_deadline_passed'),
      );

      q(fixture, '.cancel button')?.click();
      await settle(fixture);

      expect(q(fixture, '[role="alert"]')).not.toBeNull();
      expect(manage.get).toHaveBeenCalledTimes(2);
      expect(text(fixture, '.status')).toBe('manage.status.Confirmed');
    });

    it('refers to the salon by phone once the period is over', async () => {
      const { fixture } = await mount({ appointment: managed({ canCancel: false }) });

      expect(q(fixture, '.cancel')).toBeNull();
      expect(text(fixture, '.notice p')).toBe('manage.cancel.tooLate');
      expect(q(fixture, '.notice a')?.getAttribute('href')).toBe('tel:+49 30 123456');
    });

    it('does not leave a dangling "call:" when the salon has no phone number', async () => {
      const { fixture } = await mount({
        appointment: managed({ canCancel: false, salonPhone: '' }),
      });

      expect(text(fixture, '.notice p')).toBe('manage.cancel.tooLateNoPhone');
      expect(q(fixture, '.notice a')).toBeNull();
    });

    it('does not offer cancelling for an appointment that is already answered or over', async () => {
      const { fixture } = await mount({
        appointment: managed({ status: 'Completed', canCancel: false, canAddToCalendar: false }),
      });

      expect(q(fixture, '.cancel')).toBeNull();
      expect(q(fixture, '.notice')).toBeNull();
    });
  });

  it.each(['Declined', 'Expired', 'Cancelled'] as const)(
    'invites the customer to ask for a new appointment after "%s"',
    async (status) => {
      const { fixture } = await mount({
        appointment: managed({
          status,
          canCancel: false,
          canAddToCalendar: false,
          canRebook: true,
          cancelUntilUtc: null,
        }),
      });

      expect(text(fixture, '.status')).toBe(`manage.status.${status}`);
      expect(q(fixture, '.rebook')?.getAttribute('href')).toBe('/haarwerk');
      expect(q(fixture, '.cancel')).toBeNull();
    },
  );

  it('says so when the link is not valid (any more)', async () => {
    const { fixture } = await mount({ loadError: new ApiError(404, 'appointment.not_found') });

    expect(text(fixture, 'h1')).toBe('manage.notFound.title');
    expect(q(fixture, '.card .salon')).toBeNull();
    expect(q(fixture, '[role="alert"]')).toBeNull();
  });

  it('says so when loading failed for another reason', async () => {
    const { fixture } = await mount({ loadError: new ApiError(0, 'network.unreachable') });

    expect(text(fixture, '[role="alert"]')).toBe('manage.loadFailed');
    expect(q(fixture, 'h1')).toBeNull();
  });
});
