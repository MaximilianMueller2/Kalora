import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { Router, provideRouter } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom, of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { OWNER, fakeAuth } from '../../../testing/auth-fixtures';
import { provideTestI18n } from '../../../testing/test-i18n';
import { ApiError } from '../../core/http/api-error';
import { LanguageService } from '../../core/i18n/language.service';
import { Toast } from '../../core/ui/toast';
import { AuthService } from '../auth/auth.service';
import { CustomerDetailPage } from './customer-detail-page';
import { CustomersService } from './customers.service';
import { customerAppointment, customerDetail, fakeCustomers } from './customers-fixtures';

async function mount(
  options: { detail?: ReturnType<typeof customerDetail>; confirmed?: boolean } = {},
) {
  localStorage.clear();
  const customers = fakeCustomers(undefined, options.detail);
  const toast = { show: vi.fn() };
  const dialog = {
    open: vi.fn().mockImplementation(() => ({ afterClosed: () => of(options.confirmed) })),
  };
  TestBed.configureTestingModule({
    providers: [
      provideTestI18n(),
      provideRouter([]),
      { provide: CustomersService, useValue: customers },
      { provide: AuthService, useValue: fakeAuth(OWNER) },
      { provide: Toast, useValue: toast },
      { provide: MatDialog, useValue: dialog },
    ],
  });
  const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(CustomerDetailPage);
  fixture.componentRef.setInput('id', 'c1');
  await settle(fixture);
  return { fixture, customers, toast, dialog, navigate };
}

async function settle(fixture: ComponentFixture<unknown>) {
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  await new Promise((resolve) => setTimeout(resolve));
  fixture.detectChanges();
}

const root = (fixture: ComponentFixture<unknown>) => fixture.nativeElement as HTMLElement;
const q = (fixture: ComponentFixture<unknown>, selector: string) =>
  root(fixture).querySelector(selector) as HTMLElement | null;

describe('CustomerDetailPage', () => {
  it('shows the contact data and the history with both notes', async () => {
    const { fixture, customers } = await mount({
      detail: customerDetail({
        marketingConsentAtUtc: '2026-02-01T10:00:00Z',
        appointments: [
          customerAppointment({ customerNote: 'Bitte ruhig', internalNote: 'Allergie' }),
        ],
      }),
    });

    expect(customers.get).toHaveBeenCalledWith('c1');
    expect(q(fixture, 'h1')?.textContent).toContain('Kim Klein');
    const facts = q(fixture, 'dl.facts')?.textContent ?? '';
    expect(facts).toContain('kim@example.com');
    expect(facts).toContain('0170 1234567');
    expect(facts).toContain('customers.consent');
    const history = q(fixture, 'ul.history')?.textContent ?? '';
    expect(history).toContain('Damenschnitt');
    expect(history).toContain('Tom');
    expect(history).toContain('Bitte ruhig');
    expect(history).toContain('Allergie');
  });

  it('links the two downloads to the export endpoints', async () => {
    const { fixture } = await mount();

    expect(q(fixture, 'a.export-json')?.getAttribute('href')).toBe('/api/customers/c1/export.json');
    expect(q(fixture, 'a.export-csv')?.getAttribute('href')).toBe('/api/customers/c1/export.csv');
  });

  it('does not show the consent line without consent', async () => {
    const { fixture } = await mount();

    expect(q(fixture, 'dl.facts')?.textContent).not.toContain('customers.consent');
  });

  it('shows an anonymized customer without contact data, export and delete, but with the history', async () => {
    const { fixture } = await mount({
      detail: customerDetail({
        name: 'Anonymisiert',
        email: null,
        phone: null,
        anonymized: true,
      }),
    });

    expect(q(fixture, 'h1')?.textContent).toContain('customers.anonymized');
    expect(q(fixture, '.note')?.textContent).toContain('customers.anonymizedNote');
    expect(q(fixture, 'dl.facts')).toBeNull();
    expect(q(fixture, 'a.export-json')).toBeNull();
    expect(q(fixture, 'button.delete')).toBeNull();
    expect(q(fixture, 'ul.history')).not.toBeNull();
  });

  it('says so when there are no appointments', async () => {
    const { fixture } = await mount({ detail: customerDetail({ appointments: [] }) });

    expect(q(fixture, '.empty')?.textContent).toContain('customers.noAppointments');
  });

  it('does nothing when the confirmation is cancelled', async () => {
    const { fixture, customers, dialog } = await mount({ confirmed: undefined });

    q(fixture, 'button.delete')?.click();
    await settle(fixture);

    expect(dialog.open).toHaveBeenCalled();
    expect(customers.anonymize).not.toHaveBeenCalled();
  });

  it('anonymizes after confirmation, tells so and goes back to the list', async () => {
    const { fixture, customers, toast, navigate } = await mount({ confirmed: true });

    q(fixture, 'button.delete')?.click();
    await settle(fixture);

    expect(customers.anonymize).toHaveBeenCalledWith('c1');
    expect(toast.show).toHaveBeenCalledWith('customers.deleted');
    expect(navigate).toHaveBeenCalledWith(['/app/customers']);
  });

  it('stays on the page and shows the reason if the customer still has open appointments', async () => {
    const { fixture, customers, toast, navigate } = await mount({ confirmed: true });
    customers.anonymize.mockRejectedValue(new ApiError(409, 'customer.has_open_appointments'));

    q(fixture, 'button.delete')?.click();
    await settle(fixture);

    expect(q(fixture, '[role="alert"]')).not.toBeNull();
    expect(toast.show).not.toHaveBeenCalled();
    expect(navigate).not.toHaveBeenCalled();
    expect((q(fixture, 'button.delete') as HTMLButtonElement).disabled).toBe(false);
  });

  it('says so when the customer cannot be loaded', async () => {
    localStorage.clear();
    const customers = fakeCustomers();
    customers.get.mockRejectedValue(new Error('x'));
    TestBed.configureTestingModule({
      providers: [
        provideTestI18n(),
        provideRouter([]),
        { provide: CustomersService, useValue: customers },
        { provide: AuthService, useValue: fakeAuth(OWNER) },
      ],
    });
    await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
    await TestBed.inject(LanguageService).use('de');
    const fixture = TestBed.createComponent(CustomerDetailPage);
    fixture.componentRef.setInput('id', 'c1');
    await settle(fixture);

    expect(q(fixture, '[role="alert"]')?.textContent).toContain('customers.loadFailed');
  });
});
