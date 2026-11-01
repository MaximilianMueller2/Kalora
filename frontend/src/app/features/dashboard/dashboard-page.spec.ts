import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { OWNER, STAFF, fakeAuth } from '../../../testing/auth-fixtures';
import { provideTestI18n } from '../../../testing/test-i18n';
import { LanguageService } from '../../core/i18n/language.service';
import { Toast } from '../../core/ui/toast';
import { AuthService } from '../auth/auth.service';
import { Me } from '../auth/auth.models';
import { OpenRequestsBadge } from '../requests/open-requests-badge';
import { fakeBadge, fakeRequests } from '../requests/requests-fixtures';
import { RequestsService } from '../requests/requests.service';
import { DashboardPage } from './dashboard-page';
import { dashboard, fakeDashboard } from './dashboard-fixtures';
import { Dashboard } from './dashboard.models';
import { DashboardService } from './dashboard.service';

async function mount(options: { data?: Dashboard; user?: Me; fail?: boolean } = {}) {
  localStorage.clear();
  const service = fakeDashboard(options.data);
  if (options.fail) {
    service.get.mockRejectedValue(new Error('offline'));
  }
  const requests = fakeRequests();
  TestBed.configureTestingModule({
    providers: [
      provideTestI18n(),
      provideRouter([]),
      { provide: DashboardService, useValue: service },
      { provide: RequestsService, useValue: requests },
      { provide: OpenRequestsBadge, useValue: fakeBadge() },
      { provide: AuthService, useValue: fakeAuth(options.user ?? OWNER) },
      { provide: Toast, useValue: { show: vi.fn() } },
      { provide: MatDialog, useValue: { open: vi.fn() } },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(DashboardPage);
  await settle(fixture);
  return { fixture, service, requests };
}

async function settle(fixture: ComponentFixture<unknown>) {
  fixture.detectChanges();
  await fixture.whenStable();
  await new Promise((resolve) => setTimeout(resolve));
  fixture.detectChanges();
}

const root = (fixture: ComponentFixture<unknown>) => fixture.nativeElement as HTMLElement;
const plain = (text: string | null | undefined) => (text ?? '').replace(/\s+/g, ' ').trim();

describe('DashboardPage', () => {
  it('loads once and shows today, the requests, the week and the booking link', async () => {
    const { fixture, service } = await mount();

    expect(service.get).toHaveBeenCalledOnce();
    expect(plain(root(fixture).querySelector('h1')?.textContent)).toBe('dashboard.title');
    for (const card of [
      'app-today-card',
      'app-requests-card',
      'app-week-card',
      'app-booking-link-card',
    ]) {
      expect(root(fixture).querySelector(card), card).not.toBeNull();
    }
  });

  it('greets the person by name', async () => {
    const { fixture } = await mount();

    expect(plain(root(fixture).querySelector('.head p')?.textContent)).toBe('dashboard.greeting');
  });

  it('shows no booking link to staff', async () => {
    const { fixture } = await mount({ data: dashboard({ bookingLink: null }), user: STAFF });

    expect(root(fixture).querySelector('app-booking-link-card')).toBeNull();
    expect(root(fixture).querySelector('app-today-card')).not.toBeNull();
  });

  it('loads again after a request was answered on the dashboard', async () => {
    const { fixture, service } = await mount();

    root(fixture).querySelector<HTMLButtonElement>('app-requests-card li button')?.click();
    await settle(fixture);

    expect(service.get).toHaveBeenCalledTimes(2);
  });

  it('says so when the dashboard could not be loaded', async () => {
    const { fixture } = await mount({ fail: true });

    expect(plain(root(fixture).querySelector('[role="alert"]')?.textContent)).toBe(
      'dashboard.loadFailed',
    );
    expect(root(fixture).querySelector('app-today-card')).toBeNull();
  });
});
