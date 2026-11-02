import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { provideTestI18n } from '../../../testing/test-i18n';
import { LanguageService } from '../../core/i18n/language.service';
import { AdminService } from './admin.service';
import { fakeAdmin, salonItem } from './admin-fixtures';
import { SalonsPage } from './salons-page';

async function mount(admin = fakeAdmin()) {
  localStorage.clear();
  TestBed.configureTestingModule({
    providers: [provideTestI18n(), provideRouter([]), { provide: AdminService, useValue: admin }],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(SalonsPage);
  await settle(fixture);
  return { fixture, admin };
}

async function settle(fixture: ComponentFixture<unknown>) {
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  await new Promise((resolve) => setTimeout(resolve));
  fixture.detectChanges();
}

const root = (fixture: ComponentFixture<unknown>) => fixture.nativeElement as HTMLElement;
const text = (element: Element | null) => (element?.textContent ?? '').replace(/\s+/g, ' ').trim();

describe('SalonsPage', () => {
  afterEach(() => vi.useRealTimers());

  it('lists each salon with status, plan, team, appointments and the end of the trial, leading to its page', async () => {
    const { fixture, admin } = await mount();

    const link = root(fixture).querySelector('ul.list a') as HTMLAnchorElement;
    expect(admin.list).toHaveBeenCalledWith('', '');
    expect(link.getAttribute('href')).toBe('/admin/salons/s1');
    const shown = text(link);
    for (const part of [
      'Haarwerk Müller',
      'maria@example.com',
      'admin.status.Trial',
      'admin.plan.Solo',
      'admin.team',
      'admin.appointments',
      'admin.trialEnds',
    ]) {
      expect(shown).toContain(part);
    }
    expect(link.querySelector('.limited')).toBeNull();
  });

  it('marks a salon that can only read, and shows no trial end for one that is not in the trial', async () => {
    const { fixture } = await mount(
      fakeAdmin({
        items: [salonItem({ status: 'Suspended', access: 'Suspended', hasNote: true })],
        hasMore: false,
      }),
    );

    const link = root(fixture).querySelector('ul.list a') as HTMLElement;
    expect(link.querySelector('.limited')?.textContent).toContain('admin.access.Suspended');
    expect(text(link)).not.toContain('admin.trialEnds');
    expect(link.querySelector('mat-icon.note')).not.toBeNull();
  });

  it('says so when there are no salons, when there are more than shown, and when loading fails', async () => {
    const none = await mount(fakeAdmin({ items: [], hasMore: false }));
    expect(text(root(none.fixture).querySelector('.empty'))).toContain('admin.none');
    TestBed.resetTestingModule();

    const more = await mount(fakeAdmin({ items: [salonItem()], hasMore: true }));
    expect(text(root(more.fixture).querySelector('.more'))).toContain('admin.more');
    TestBed.resetTestingModule();

    const admin = fakeAdmin();
    admin.list.mockRejectedValue(new Error('x'));
    const failed = await mount(admin);
    expect(text(root(failed.fixture).querySelector('[role="alert"]'))).toContain(
      'admin.loadFailed',
    );
  });

  it('searches after a short pause, not on every key', async () => {
    const { fixture, admin } = await mount();
    vi.useFakeTimers();
    const input = root(fixture).querySelector('input[type="search"]') as HTMLInputElement;

    for (const value of ['h', 'ha', ' haar ']) {
      input.value = value;
      input.dispatchEvent(new Event('input'));
    }
    expect(admin.list).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(400);
    vi.useRealTimers();
    await settle(fixture);

    expect(admin.list).toHaveBeenLastCalledWith('haar', '');
  });

  it('filters by status at once', async () => {
    const { fixture, admin } = await mount();
    const select = root(fixture).querySelector('select') as HTMLSelectElement;

    select.value = 'Suspended';
    select.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(admin.list).toHaveBeenLastCalledWith('', 'Suspended');
    expect(select.querySelectorAll('option')).toHaveLength(6);
  });
});
