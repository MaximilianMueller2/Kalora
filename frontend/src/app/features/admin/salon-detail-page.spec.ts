import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { provideTestI18n } from '../../../testing/test-i18n';
import { ApiError } from '../../core/http/api-error';
import { LanguageService } from '../../core/i18n/language.service';
import { Toast } from '../../core/ui/toast';
import { AdminService } from './admin.service';
import { fakeAdmin, salonDetail } from './admin-fixtures';
import { SalonDetailPage } from './salon-detail-page';

async function mount(detail = salonDetail()) {
  localStorage.clear();
  const admin = fakeAdmin(undefined, detail);
  const toast = { show: vi.fn() };
  TestBed.configureTestingModule({
    providers: [
      provideTestI18n(),
      provideRouter([]),
      { provide: AdminService, useValue: admin },
      { provide: Toast, useValue: toast },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(SalonDetailPage);
  fixture.componentRef.setInput('id', 's1');
  await settle(fixture);
  return { fixture, admin, toast };
}

async function settle(fixture: ComponentFixture<unknown>) {
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  await new Promise((resolve) => setTimeout(resolve));
  fixture.detectChanges();
}

const q = <E extends HTMLElement>(fixture: ComponentFixture<unknown>, selector: string) =>
  fixture.nativeElement.querySelector(selector) as E | null;
const text = (element: Element | null) => (element?.textContent ?? '').replace(/\s+/g, ' ').trim();

describe('SalonDetailPage', () => {
  it('shows master data and the subscription of the salon', async () => {
    const { fixture, admin } = await mount();

    expect(admin.get).toHaveBeenCalledWith('s1');
    expect(text(q(fixture, 'h1'))).toBe('Haarwerk Müller');
    const facts = text(q(fixture, 'dl.facts-list'));
    for (const part of [
      'Maria Müller',
      'maria@example.com',
      '/haarwerk',
      'Berlin',
      '+49 30 123456',
    ]) {
      expect(facts).toContain(part);
    }
    expect((q(fixture, 'select.plan') as HTMLSelectElement).value).toBe('Solo');
    expect((q(fixture, 'select.status') as HTMLSelectElement).value).toBe('Trial');
  });

  it('sets the plan and says so', async () => {
    const { fixture, admin, toast } = await mount();
    const select = q<HTMLSelectElement>(fixture, 'select.plan')!;

    select.value = 'Team';
    select.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(admin.setPlan).toHaveBeenCalledWith('s1', 'Team');
    expect(toast.show).toHaveBeenCalledWith('admin.saved');
    expect(q<HTMLSelectElement>(fixture, 'select.plan')!.value).toBe('Team');
  });

  it('sets the status directly', async () => {
    const { fixture, admin } = await mount();
    const select = q<HTMLSelectElement>(fixture, 'select.status')!;

    select.value = 'PastDue';
    select.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(admin.setStatus).toHaveBeenCalledWith('s1', 'PastDue');
    expect(text(q(fixture, '.title .chip'))).toContain('admin.status.PastDue');
  });

  it('suspends, and then offers to unsuspend', async () => {
    const { fixture, admin } = await mount();
    expect(text(q(fixture, 'button.toggle'))).toContain('admin.suspend');

    q(fixture, 'button.toggle')!.click();
    await settle(fixture);

    expect(admin.suspend).toHaveBeenCalledWith('s1');
    expect(text(q(fixture, 'button.toggle'))).toContain('admin.unsuspend');
    expect(q(fixture, '.title .limited')).not.toBeNull();

    q(fixture, 'button.toggle')!.click();
    await settle(fixture);
    expect(admin.unsuspend).toHaveBeenCalledWith('s1');
  });

  it('extends the trial in steps, only while the salon is in the trial', async () => {
    const { fixture, admin } = await mount();
    const buttons = Array.from(
      fixture.nativeElement.querySelectorAll('button.extend'),
    ) as HTMLElement[];
    expect(buttons).toHaveLength(3);

    buttons[1].click();
    await settle(fixture);

    expect(admin.extendTrial).toHaveBeenCalledWith('s1', 14);
    TestBed.resetTestingModule();

    const active = await mount(salonDetail({ status: 'Active' }));
    expect(q(active.fixture, 'button.extend')).toBeNull();
  });

  it('saves the internal note', async () => {
    const { fixture, admin } = await mount(salonDetail({ adminNote: 'alt' }));
    const textarea = q<HTMLTextAreaElement>(fixture, 'textarea')!;
    expect(textarea.value).toBe('alt');

    textarea.value = 'Zahlt per Überweisung';
    textarea.dispatchEvent(new Event('input'));
    q(fixture, 'button.save-note')!.click();
    await settle(fixture);

    expect(admin.setNote).toHaveBeenCalledWith('s1', 'Zahlt per Überweisung');
  });

  it('shows the reason in the active language and changes nothing on screen when a change is refused', async () => {
    const { fixture, admin, toast } = await mount();
    admin.extendTrial.mockRejectedValue(new ApiError(400, 'admin.trial_days_invalid'));

    q(fixture, 'button.extend')!.click();
    await settle(fixture);

    expect(q(fixture, '[role="alert"]')).not.toBeNull();
    expect(toast.show).not.toHaveBeenCalled();
    expect(text(q(fixture, 'h1'))).toBe('Haarwerk Müller');
  });

  it('says so when the salon cannot be loaded', async () => {
    localStorage.clear();
    const admin = fakeAdmin();
    admin.get.mockRejectedValue(new Error('x'));
    TestBed.configureTestingModule({
      providers: [provideTestI18n(), provideRouter([]), { provide: AdminService, useValue: admin }],
    });
    await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
    await TestBed.inject(LanguageService).use('de');
    const fixture = TestBed.createComponent(SalonDetailPage);
    fixture.componentRef.setInput('id', 's1');
    await settle(fixture);

    expect(text(q(fixture, '[role="alert"]'))).toContain('admin.loadFailed');
  });
});
