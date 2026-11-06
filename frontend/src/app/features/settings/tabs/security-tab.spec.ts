import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { OWNER } from '../../../../testing/auth-fixtures';
import { provideTestI18n } from '../../../../testing/test-i18n';
import { ApiError } from '../../../core/http/api-error';
import { LanguageService } from '../../../core/i18n/language.service';
import { Toast } from '../../../core/ui/toast';
import { Me } from '../../auth/auth.models';
import { AuthService } from '../../auth/auth.service';
import { SecurityService } from '../security.service';
import { SecurityTab } from './security-tab';

const SETUP = {
  sharedKey: 'JBSWY3DPEHPK3PXP',
  authenticatorUri: 'otpauth://totp/Kalora:x',
  qrPngBase64: 'iVBORw0KGgo=',
};
const CODES = { recoveryCodes: ['aaaaa-bbbbb', 'ccccc-ddddd'] };

async function mount(enabled = false) {
  localStorage.clear();
  const user = signal<Me>({ ...OWNER, twoFactorEnabled: enabled });
  const security = {
    setup: vi.fn().mockResolvedValue(SETUP),
    enable: vi.fn().mockImplementation(async () => {
      user.update((me) => ({ ...me, twoFactorEnabled: true }));
      return CODES;
    }),
    disable: vi.fn().mockImplementation(async () => {
      user.update((me) => ({ ...me, twoFactorEnabled: false }));
    }),
    regenerateRecoveryCodes: vi.fn().mockResolvedValue(CODES),
  };
  const toast = { show: vi.fn() };
  TestBed.configureTestingModule({
    providers: [
      provideTestI18n(),
      { provide: SecurityService, useValue: security },
      { provide: AuthService, useValue: { currentUser: user.asReadonly() } },
      { provide: Toast, useValue: toast },
    ],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(SecurityTab);
  await settle(fixture);
  return { fixture, security, toast };
}

async function settle(fixture: ComponentFixture<unknown>) {
  fixture.detectChanges();
  await fixture.whenStable();
  await new Promise((resolve) => setTimeout(resolve));
  fixture.detectChanges();
}

const q = <E extends HTMLElement>(fixture: ComponentFixture<unknown>, selector: string) =>
  fixture.nativeElement.querySelector(selector) as E | null;
const type = (input: HTMLInputElement, value: string) => {
  input.value = value;
  input.dispatchEvent(new Event('input'));
};
const click = async (fixture: ComponentFixture<unknown>, selector: string) => {
  q(fixture, selector)!.click();
  await settle(fixture);
};

describe('SecurityTab', () => {
  it('offers to set it up while it is off, and shows nothing secret yet', async () => {
    const { fixture } = await mount(false);

    expect(q(fixture, '.status')?.textContent).toContain('settings.security.off');
    expect(q(fixture, 'button.start')).not.toBeNull();
    expect(q(fixture, 'img.qr')).toBeNull();
    expect(q(fixture, 'ul.codes')).toBeNull();
  });

  it('shows the QR code and the key by hand, and asks for the first code', async () => {
    const { fixture, security } = await mount(false);

    await click(fixture, 'button.start');

    expect(security.setup).toHaveBeenCalledOnce();
    const image = q<HTMLImageElement>(fixture, 'img.qr')!;
    expect(image.getAttribute('src')).toBe('data:image/png;base64,iVBORw0KGgo=');
    expect(image.getAttribute('alt')).toBe('settings.security.qrAlt');
    expect(q(fixture, '.key')?.textContent).toBe('JBSWY3DPEHPK3PXP');
    expect(q<HTMLInputElement>(fixture, 'input[name=code]')?.getAttribute('autocomplete')).toBe(
      'one-time-code',
    );
  });

  it('turns it on with the code and shows the recovery codes once', async () => {
    const { fixture, security } = await mount(false);
    await click(fixture, 'button.start');

    type(q<HTMLInputElement>(fixture, 'input[name=code]')!, ' 123 456 ');
    await click(fixture, 'button.enable');

    expect(security.enable).toHaveBeenCalledWith('123 456');
    expect(
      Array.from(fixture.nativeElement.querySelectorAll('ul.codes li')).map((li) =>
        (li as HTMLElement).textContent?.trim(),
      ),
    ).toEqual(CODES.recoveryCodes);
    expect(q(fixture, 'img.qr')).toBeNull();

    await click(fixture, 'button.done');

    expect(q(fixture, 'ul.codes')).toBeNull();
    expect(q(fixture, '.status')?.textContent).toContain('settings.security.on');
  });

  it('shows the reason when the code is wrong and stays on the step', async () => {
    const { fixture, security } = await mount(false);
    security.enable.mockRejectedValue(new ApiError(400, 'auth.invalid_two_factor_code', 'code'));
    await click(fixture, 'button.start');

    type(q<HTMLInputElement>(fixture, 'input[name=code]')!, '000000');
    await click(fixture, 'button.enable');

    expect(q(fixture, '[role=alert]')).not.toBeNull();
    expect(q(fixture, 'img.qr')).not.toBeNull();
    expect(q(fixture, 'ul.codes')).toBeNull();
  });

  it('can be cancelled before it is on', async () => {
    const { fixture, security } = await mount(false);
    await click(fixture, 'button.start');

    await click(fixture, 'button.cancel');

    expect(q(fixture, 'img.qr')).toBeNull();
    expect(q(fixture, 'button.start')).not.toBeNull();
    expect(security.enable).not.toHaveBeenCalled();
  });

  it('says it is on, and needs the password for everything that follows', async () => {
    const { fixture } = await mount(true);

    expect(q(fixture, '.status')?.textContent).toContain('settings.security.on');
    expect(q<HTMLButtonElement>(fixture, 'button.disable')!.disabled).toBe(true);
    expect(q<HTMLButtonElement>(fixture, 'button.regenerate')!.disabled).toBe(true);

    type(q<HTMLInputElement>(fixture, 'input[name=password]')!, 'secret-password');
    await settle(fixture);

    expect(q<HTMLButtonElement>(fixture, 'button.disable')!.disabled).toBe(false);
    expect(q<HTMLButtonElement>(fixture, 'button.regenerate')!.disabled).toBe(false);
  });

  it('turns it off with the password, says so and forgets the password', async () => {
    const { fixture, security, toast } = await mount(true);
    type(q<HTMLInputElement>(fixture, 'input[name=password]')!, 'secret-password');
    await settle(fixture);

    await click(fixture, 'button.disable');

    expect(security.disable).toHaveBeenCalledWith('secret-password');
    expect(toast.show).toHaveBeenCalledWith('settings.security.disabled');
    expect(q(fixture, 'button.start')).not.toBeNull();
    expect(q(fixture, 'input[name=password]')).toBeNull();
  });

  it('shows new recovery codes after the password, and then not again', async () => {
    const { fixture, security } = await mount(true);
    type(q<HTMLInputElement>(fixture, 'input[name=password]')!, 'secret-password');
    await settle(fixture);

    await click(fixture, 'button.regenerate');

    expect(security.regenerateRecoveryCodes).toHaveBeenCalledWith('secret-password');
    expect(fixture.nativeElement.querySelectorAll('ul.codes li')).toHaveLength(2);

    await click(fixture, 'button.done');

    expect(q(fixture, 'ul.codes')).toBeNull();
    expect(q<HTMLInputElement>(fixture, 'input[name=password]')!.value).toBe('');
  });

  it('says why when the password is wrong or the account is locked', async () => {
    const { fixture, security, toast } = await mount(true);
    security.disable.mockRejectedValue(new ApiError(401, 'auth.locked_out'));
    type(q<HTMLInputElement>(fixture, 'input[name=password]')!, 'wrong');
    await settle(fixture);

    await click(fixture, 'button.disable');

    expect(q(fixture, '[role=alert]')).not.toBeNull();
    expect(toast.show).not.toHaveBeenCalled();
    expect(q(fixture, '.status')?.textContent).toContain('settings.security.on');
  });

  it('copies the recovery codes, and says if copying is not possible', async () => {
    const { fixture, toast } = await mount(true);
    const writeText = vi
      .fn()
      .mockResolvedValueOnce(undefined)
      .mockRejectedValueOnce(new Error('denied'));
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    type(q<HTMLInputElement>(fixture, 'input[name=password]')!, 'secret-password');
    await settle(fixture);
    await click(fixture, 'button.regenerate');

    await click(fixture, 'button.copy');
    await click(fixture, 'button.copy');

    expect(writeText).toHaveBeenCalledWith('aaaaa-bbbbb\nccccc-ddddd');
    expect(toast.show).toHaveBeenNthCalledWith(1, 'settings.security.copied');
    expect(toast.show).toHaveBeenNthCalledWith(2, 'settings.security.copyFailed');
  });
});
