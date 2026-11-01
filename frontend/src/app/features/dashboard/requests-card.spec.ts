import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom, of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { provideTestI18n } from '../../../testing/test-i18n';
import { LanguageService } from '../../core/i18n/language.service';
import { Toast } from '../../core/ui/toast';
import { OpenRequestsBadge } from '../requests/open-requests-badge';
import { fakeBadge, fakeRequests, request } from '../requests/requests-fixtures';
import { RequestsService } from '../requests/requests.service';
import { RequestsCard } from './requests-card';

async function mount(
  options: { count?: number; items?: ReturnType<typeof request>[]; dialogResult?: unknown } = {},
) {
  localStorage.clear();
  const requests = fakeRequests();
  const badge = fakeBadge();
  const toast = { show: vi.fn() };
  const dialog = {
    open: vi.fn().mockImplementation(() => ({ afterClosed: () => of(options.dialogResult) })),
  };
  TestBed.configureTestingModule({
    providers: [
      provideTestI18n(),
      provideRouter([]),
      { provide: RequestsService, useValue: requests },
      { provide: OpenRequestsBadge, useValue: badge },
      { provide: Toast, useValue: toast },
      { provide: MatDialog, useValue: dialog },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const items = options.items ?? [
    request({ id: 'r1' }),
    request({ id: 'r2', customer: { name: 'Tom Test', email: null, phone: null } }),
  ];
  const fixture = TestBed.createComponent(RequestsCard);
  fixture.componentRef.setInput('count', options.count ?? items.length);
  fixture.componentRef.setInput('items', items);
  fixture.componentRef.setInput('zone', 'Europe/Berlin');
  const answered = vi.fn();
  fixture.componentInstance.answered.subscribe(answered);
  await settle(fixture);
  return { fixture, requests, badge, toast, dialog, answered };
}

async function settle(fixture: ComponentFixture<unknown>) {
  fixture.detectChanges();
  await fixture.whenStable();
  await new Promise((resolve) => setTimeout(resolve));
  fixture.detectChanges();
}

const root = (fixture: ComponentFixture<unknown>) => fixture.nativeElement as HTMLElement;
const plain = (text: string | null | undefined) => (text ?? '').replace(/\s+/g, ' ').trim();
const buttons = (fixture: ComponentFixture<unknown>, row: number) => {
  const item = root(fixture).querySelectorAll<HTMLElement>('li').item(row);
  return item.querySelectorAll<HTMLButtonElement>('button');
};

describe('RequestsCard', () => {
  it('lists the open requests with customer, service, person and time', async () => {
    const { fixture } = await mount();

    const rows = root(fixture).querySelectorAll('li');
    expect(rows).toHaveLength(2);
    expect(plain(rows[0].querySelector('strong')?.textContent)).toBe('Kim Kundin');
    expect(plain(rows[0].querySelector('.what span')?.textContent)).toBe('Haarschnitt · Anna');
    expect(plain(rows[0].querySelector('.when')?.textContent)).toBe('Mittwoch, 4. November, 09:00');
    expect(plain(root(fixture).querySelector('.count')?.textContent)).toBe('2');
  });

  it('accepts a request right away and tells the dashboard to load again', async () => {
    const { fixture, requests, toast, answered, badge } = await mount();

    buttons(fixture, 0)[0].click();
    await settle(fixture);

    expect(requests.confirm).toHaveBeenCalledWith('r1');
    expect(toast.show).toHaveBeenCalledWith('dashboard.requests.confirmed');
    expect(answered).toHaveBeenCalledOnce();
    expect(badge.refresh).toHaveBeenCalled();
  });

  it('asks for an optional reason before declining', async () => {
    const { fixture, requests, toast } = await mount({ dialogResult: 'Betriebsausflug' });

    buttons(fixture, 1)[1].click();
    await settle(fixture);

    expect(requests.decline).toHaveBeenCalledWith('r2', 'Betriebsausflug');
    expect(toast.show).toHaveBeenCalledWith('dashboard.requests.declined');
  });

  it('sends no reason when the field stays empty and nothing when the dialog is cancelled', async () => {
    const empty = await mount({ dialogResult: '' });
    buttons(empty.fixture, 0)[1].click();
    await settle(empty.fixture);
    expect(empty.requests.decline).toHaveBeenCalledWith('r1', null);
    TestBed.resetTestingModule();

    const cancelled = await mount({ dialogResult: undefined });
    buttons(cancelled.fixture, 0)[1].click();
    await settle(cancelled.fixture);
    expect(cancelled.requests.decline).not.toHaveBeenCalled();
    expect(cancelled.answered).not.toHaveBeenCalled();
  });

  it('loads the dashboard again even when the server refused, so it shows the true state', async () => {
    const { fixture, requests, toast, answered } = await mount();
    requests.confirm.mockRejectedValue(new Error('conflict'));

    buttons(fixture, 0)[0].click();
    await settle(fixture);

    expect(toast.show).not.toHaveBeenCalled();
    expect(answered).toHaveBeenCalledOnce();
  });

  it('links to the inbox, saying how many more there are', async () => {
    const { fixture } = await mount({ count: 7 });

    const link = root(fixture).querySelector<HTMLAnchorElement>('a.all');
    expect(link?.getAttribute('href')).toBe('/app/requests');
    expect(plain(link?.textContent)).toBe('dashboard.requests.more');
  });

  it('says so when nothing is open', async () => {
    const { fixture } = await mount({ count: 0, items: [] });

    expect(plain(root(fixture).querySelector('.empty')?.textContent)).toBe(
      'dashboard.requests.none',
    );
    expect(root(fixture).querySelector('.count')).toBeNull();
    expect(root(fixture).querySelector('a.all')).toBeNull();
  });
});
