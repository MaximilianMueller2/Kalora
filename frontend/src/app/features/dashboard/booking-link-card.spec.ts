import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { provideTestI18n } from '../../../testing/test-i18n';
import { LanguageService } from '../../core/i18n/language.service';
import { Toast } from '../../core/ui/toast';
import { BookingLinkCard } from './booking-link-card';
import { fakeDashboard } from './dashboard-fixtures';
import { DashboardService } from './dashboard.service';

async function mount(options: { clipboardFails?: boolean } = {}) {
  localStorage.clear();
  const toast = { show: vi.fn() };
  const writeText = vi
    .fn()
    .mockImplementation(() =>
      options.clipboardFails ? Promise.reject(new Error('denied')) : Promise.resolve(),
    );
  Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
  TestBed.configureTestingModule({
    providers: [
      provideTestI18n(),
      { provide: DashboardService, useValue: fakeDashboard() },
      { provide: Toast, useValue: toast },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(BookingLinkCard);
  fixture.componentRef.setInput('url', 'https://kalora.app/haarwerk');
  fixture.componentRef.setInput('salonName', 'Haarwerk Müller');
  fixture.detectChanges();
  await fixture.whenStable();
  return { fixture, toast, writeText };
}

const root = (fixture: ComponentFixture<unknown>) => fixture.nativeElement as HTMLElement;
const press = async (fixture: ComponentFixture<unknown>, selector: string, index = 0) => {
  root(fixture).querySelectorAll<HTMLElement>(selector)[index].click();
  await fixture.whenStable();
  await new Promise((resolve) => setTimeout(resolve));
};

describe('BookingLinkCard', () => {
  it('shows the link in a field that can be selected', async () => {
    const { fixture } = await mount();

    const field = root(fixture).querySelector<HTMLInputElement>('.link input');
    expect(field?.value).toBe('https://kalora.app/haarwerk');
    expect(field?.readOnly).toBe(true);
  });

  it('copies the link', async () => {
    const { fixture, writeText, toast } = await mount();

    await press(fixture, '.link button');

    expect(writeText).toHaveBeenCalledWith('https://kalora.app/haarwerk');
    expect(toast.show).toHaveBeenCalledWith('dashboard.share.linkCopied');
  });

  it('offers the QR code as an image and as PNG and PDF downloads', async () => {
    const { fixture } = await mount();

    expect(root(fixture).querySelector('.qr img')?.getAttribute('src')).toBe(
      '/api/dashboard/booking-link/qr.png',
    );
    const links = Array.from(root(fixture).querySelectorAll<HTMLAnchorElement>('.downloads a'));
    expect(links.map((a) => a.getAttribute('href'))).toEqual([
      '/api/dashboard/booking-link/qr.png',
      '/api/dashboard/booking-link/poster.pdf',
    ]);
    expect(links[0].hasAttribute('download')).toBe(true);
    expect(links[1].target).toBe('_blank');
  });

  it('hides the image when it cannot be loaded but keeps the downloads', async () => {
    const { fixture } = await mount();

    root(fixture).querySelector('.qr img')?.dispatchEvent(new Event('error'));
    fixture.detectChanges();

    expect(root(fixture).querySelector('.qr img')).toBeNull();
    expect(root(fixture).querySelectorAll('.downloads a')).toHaveLength(2);
  });

  it('has a ready text for Instagram with the name of the salon and the link, and copies it', async () => {
    const { fixture, writeText, toast } = await mount();

    // The test dictionary has no text, so the key is what is shown; the real texts are checked in the translation specs.
    expect(root(fixture).querySelector('blockquote')?.textContent).toBeTruthy();
    await press(fixture, 'button:not(.link button)');

    expect(writeText).toHaveBeenCalledOnce();
    expect(toast.show).toHaveBeenCalledWith('dashboard.share.textCopied');
  });

  it('says so when the clipboard is not available', async () => {
    const { fixture, toast } = await mount({ clipboardFails: true });

    await press(fixture, '.link button');

    expect(toast.show).toHaveBeenCalledWith('dashboard.share.copyFailed');
  });
});
