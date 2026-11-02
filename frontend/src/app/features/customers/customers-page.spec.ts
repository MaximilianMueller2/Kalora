import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { OWNER, fakeAuth } from '../../../testing/auth-fixtures';
import { provideTestI18n } from '../../../testing/test-i18n';
import { LanguageService } from '../../core/i18n/language.service';
import { AuthService } from '../auth/auth.service';
import { CustomersPage } from './customers-page';
import { CustomersService } from './customers.service';
import { customerItem, fakeCustomers } from './customers-fixtures';

async function mount(customers = fakeCustomers()) {
  localStorage.clear();
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
  const fixture = TestBed.createComponent(CustomersPage);
  await settle(fixture);
  return { fixture, customers };
}

async function settle(fixture: ComponentFixture<unknown>) {
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  await new Promise((resolve) => setTimeout(resolve));
  fixture.detectChanges();
}

const root = (fixture: ComponentFixture<unknown>) => fixture.nativeElement as HTMLElement;

describe('CustomersPage', () => {
  afterEach(() => vi.useRealTimers());

  it('lists the customers, each leading to their page', async () => {
    const { fixture } = await mount();

    const link = root(fixture).querySelector('ul.list a') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe('/app/customers/c1');
    expect(link.textContent).toContain('Kim Klein');
    expect(link.textContent).toContain('kim@example.com');
    expect(link.querySelector('.chip')).toBeNull();
  });

  it('shows the phone number if there is no e-mail address, and the no-shows as a chip', async () => {
    const { fixture } = await mount(
      fakeCustomers({
        items: [customerItem({ email: null, phone: '0170 1', noShows: 2 })],
        hasMore: false,
      }),
    );

    const link = root(fixture).querySelector('ul.list a') as HTMLElement;
    expect(link.textContent).toContain('0170 1');
    expect(link.querySelector('.chip')).not.toBeNull();
  });

  it('says so when there are no customers yet', async () => {
    const { fixture } = await mount(fakeCustomers({ items: [], hasMore: false }));

    expect(root(fixture).querySelector('.empty')?.textContent).toContain('customers.none');
    expect(root(fixture).querySelector('.empty')?.textContent).not.toContain('noneFound');
  });

  it('asks to narrow the search if there are more customers than shown', async () => {
    const { fixture } = await mount(fakeCustomers({ items: [customerItem()], hasMore: true }));

    expect(root(fixture).querySelector('.more')?.textContent).toContain('customers.more');
  });

  it('says so when the list cannot be loaded', async () => {
    const customers = fakeCustomers();
    customers.list.mockRejectedValue(new Error('x'));

    const { fixture } = await mount(customers);

    expect(root(fixture).querySelector('[role="alert"]')?.textContent).toContain(
      'customers.loadFailed',
    );
  });

  it('searches after a short pause, not on every key', async () => {
    const { fixture, customers } = await mount();
    vi.useFakeTimers();
    const input = root(fixture).querySelector('input[type="search"]') as HTMLInputElement;

    for (const text of ['k', 'ki', ' kim ']) {
      input.value = text;
      input.dispatchEvent(new Event('input'));
    }
    expect(customers.list).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(400);
    vi.useRealTimers();
    await settle(fixture);

    expect(customers.list).toHaveBeenCalledTimes(2);
    expect(customers.list).toHaveBeenLastCalledWith('kim');
  });

  it('has a different empty text when a search finds nothing', async () => {
    const customers = fakeCustomers();
    const { fixture } = await mount(customers);
    customers.list.mockResolvedValue({ items: [], hasMore: false });
    vi.useFakeTimers();
    const input = root(fixture).querySelector('input[type="search"]') as HTMLInputElement;
    input.value = 'zzz';
    input.dispatchEvent(new Event('input'));
    await vi.advanceTimersByTimeAsync(400);
    vi.useRealTimers();
    await settle(fixture);

    expect(root(fixture).querySelector('.empty')?.textContent).toContain('customers.noneFound');
  });
});
