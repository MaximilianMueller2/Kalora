import { Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { OWNER } from '../../../../testing/auth-fixtures';
import { click, submit, texts, type } from '../../../../testing/forms';
import { provideTestI18n } from '../../../../testing/test-i18n';
import { ApiError } from '../../../core/http/api-error';
import { AuthService } from '../auth.service';
import { ConfirmEmailPage } from './confirm-email-page';
import { ForgotPasswordPage } from './forgot-password-page';
import { InvitationPage } from './invitation-page';
import { LoginPage } from './login-page';
import { RegisterPage } from './register-page';
import { ResetPasswordPage } from './reset-password-page';

interface FakeAuth {
  login: ReturnType<typeof vi.fn>;
  register: ReturnType<typeof vi.fn>;
  confirmEmail: ReturnType<typeof vi.fn>;
  forgotPassword: ReturnType<typeof vi.fn>;
  resetPassword: ReturnType<typeof vi.fn>;
  getInvitation: ReturnType<typeof vi.fn>;
  acceptInvitation: ReturnType<typeof vi.fn>;
}

async function mount<T>(
  page: Type<T>,
  inputs: Record<string, unknown> = {},
  setup?: (auth: FakeAuth) => void,
) {
  const auth: FakeAuth = {
    login: vi.fn().mockResolvedValue(OWNER),
    register: vi.fn().mockResolvedValue(OWNER),
    confirmEmail: vi.fn().mockResolvedValue(undefined),
    forgotPassword: vi.fn().mockResolvedValue(undefined),
    resetPassword: vi.fn().mockResolvedValue(undefined),
    getInvitation: vi.fn(),
    acceptInvitation: vi.fn().mockResolvedValue(OWNER),
  };
  setup?.(auth);
  TestBed.configureTestingModule({
    providers: [provideRouter([]), provideTestI18n(), { provide: AuthService, useValue: auth }],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  const router = TestBed.inject(Router);
  const navigateByUrl = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
  const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
  const fixture = TestBed.createComponent(page);
  for (const [name, value] of Object.entries(inputs)) {
    fixture.componentRef.setInput(name, value);
  }
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return { fixture, auth, navigate, navigateByUrl };
}

const errors = (fixture: ComponentFixture<unknown>) => texts(fixture, 'mat-error');
const alert = (fixture: ComponentFixture<unknown>) => texts(fixture, '[role=alert]');

describe('LoginPage', () => {
  beforeEach(() => localStorage.clear());

  it('does not submit an empty or invalid form and shows what is missing', async () => {
    const { fixture, auth } = await mount(LoginPage);

    await submit(fixture);
    expect(auth.login).not.toHaveBeenCalled();
    expect(errors(fixture)).toEqual([
      'Dieses Feld ist erforderlich.',
      'Dieses Feld ist erforderlich.',
    ]);

    type(fixture, 'input[type=email]', 'not-an-email');
    type(fixture, 'input[type=password]', 'secret');
    await submit(fixture);
    expect(auth.login).not.toHaveBeenCalled();
    expect(errors(fixture)).toEqual(['Bitte gib eine gültige E-Mail-Adresse ein.']);
  });

  it('signs in with trimmed credentials and goes to the return URL', async () => {
    const { fixture, auth, navigateByUrl } = await mount(LoginPage, { returnUrl: '/app/team' });

    type(fixture, 'input[type=email]', '  maria@example.com ');
    type(fixture, 'input[type=password]', 'correct-horse-battery');
    click(fixture, 'mat-checkbox input');
    await submit(fixture);

    expect(auth.login).toHaveBeenCalledWith({
      email: 'maria@example.com',
      password: 'correct-horse-battery',
      rememberMe: true,
      twoFactorCode: undefined,
      recoveryCode: undefined,
    });
    expect(navigateByUrl).toHaveBeenCalledWith('/app/team');
  });

  it('ignores a return URL that leads outside the app', async () => {
    const { fixture, navigateByUrl } = await mount(LoginPage, {
      returnUrl: 'https://evil.example',
    });

    type(fixture, 'input[type=email]', 'maria@example.com');
    type(fixture, 'input[type=password]', 'correct-horse-battery');
    await submit(fixture);

    expect(navigateByUrl).toHaveBeenCalledWith('/app');
  });

  it('shows a translated message for a server error and stays on the page', async () => {
    const { fixture, auth, navigateByUrl } = await mount(LoginPage);
    auth.login.mockRejectedValue(new ApiError(401, 'auth.invalid_credentials'));

    type(fixture, 'input[type=email]', 'maria@example.com');
    type(fixture, 'input[type=password]', 'wrong');
    await submit(fixture);

    expect(alert(fixture)).toEqual(['E-Mail oder Passwort stimmt nicht.']);
    expect(navigateByUrl).not.toHaveBeenCalled();
  });

  it('asks for the second factor when required and then sends the code, or a recovery code', async () => {
    const { fixture, auth } = await mount(LoginPage);
    auth.login.mockRejectedValueOnce(new ApiError(401, 'auth.two_factor_required'));

    type(fixture, 'input[type=email]', 'maria@example.com');
    type(fixture, 'input[type=password]', 'correct-horse-battery');
    await submit(fixture);

    expect(fixture.nativeElement.querySelector('input[type=email]')).toBeNull();
    expect(alert(fixture)).toEqual([]);

    type(fixture, 'input[autocomplete=one-time-code]', '123456');
    await submit(fixture);
    expect(auth.login).toHaveBeenLastCalledWith(
      expect.objectContaining({ twoFactorCode: '123456', recoveryCode: undefined }),
    );

    auth.login.mockRejectedValueOnce(new ApiError(401, 'auth.invalid_two_factor_code'));
    click(fixture, 'button[type=button]'); // switch to recovery code
    type(fixture, 'input[autocomplete=one-time-code]', 'ABCDE-12345');
    await submit(fixture);
    expect(auth.login).toHaveBeenLastCalledWith(
      expect.objectContaining({ twoFactorCode: undefined, recoveryCode: 'ABCDE-12345' }),
    );
  });

  it('explains why the user is back at the login page', async () => {
    const expired = await mount(LoginPage, { reason: 'expired' });
    expect(texts(expired.fixture, '.alert.info')).toHaveLength(1);
    TestBed.resetTestingModule();

    const reset = await mount(LoginPage, { notice: 'password-reset' });
    expect(texts(reset.fixture, '.alert.info')).toHaveLength(1);
    TestBed.resetTestingModule();

    const plain = await mount(LoginPage);
    expect(texts(plain.fixture, '.alert.info')).toHaveLength(0);
  });
});

describe('RegisterPage', () => {
  beforeEach(() => localStorage.clear());

  const fillValid = (fixture: ComponentFixture<unknown>) => {
    type(fixture, 'input[autocomplete=organization]', '  Haarwerk Müller ');
    type(fixture, 'input[autocomplete=name]', 'Maria Müller');
    type(fixture, 'input[type=email]', 'maria@example.com');
    type(fixture, 'input[autocomplete=new-password]', 'correct-horse-battery');
    click(fixture, 'mat-checkbox input');
  };

  it('requires all fields, a password of at least ten characters and the terms', async () => {
    const { fixture, auth } = await mount(RegisterPage);

    await submit(fixture);
    expect(auth.register).not.toHaveBeenCalled();
    expect(errors(fixture)).toHaveLength(4);
    expect(texts(fixture, '.field-error')).toEqual(['Bitte stimme zu.']);

    type(fixture, 'input[autocomplete=new-password]', 'too-short');
    await submit(fixture);
    expect(errors(fixture)).toContain('Mindestens 10 Zeichen.');
  });

  it('registers with the current UI language and continues to onboarding', async () => {
    const { fixture, auth, navigate } = await mount(RegisterPage);

    fillValid(fixture);
    await submit(fixture);

    expect(auth.register).toHaveBeenCalledWith({
      salonName: 'Haarwerk Müller',
      industry: 'Hair',
      name: 'Maria Müller',
      email: 'maria@example.com',
      password: 'correct-horse-battery',
      language: 'de',
      acceptTerms: true,
    });
    expect(navigate).toHaveBeenCalledWith(['/app/onboarding']);
  });

  it('reports an address that is already registered', async () => {
    const { fixture, auth, navigate } = await mount(RegisterPage);
    auth.register.mockRejectedValue(new ApiError(409, 'auth.email_taken', 'email'));

    fillValid(fixture);
    await submit(fixture);

    expect(alert(fixture)).toEqual(['Konto existiert schon.']);
    expect(navigate).not.toHaveBeenCalled();
  });
});

describe('ForgotPasswordPage', () => {
  it('asks for a valid address and then shows the neutral confirmation', async () => {
    const { fixture, auth } = await mount(ForgotPasswordPage);

    type(fixture, 'input[type=email]', 'nope');
    await submit(fixture);
    expect(auth.forgotPassword).not.toHaveBeenCalled();

    type(fixture, 'input[type=email]', ' maria@example.com ');
    await submit(fixture);

    expect(auth.forgotPassword).toHaveBeenCalledWith('maria@example.com');
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
    expect(texts(fixture, '.alert.info')).toHaveLength(1);
  });
});

describe('ResetPasswordPage', () => {
  it('rejects a link without e-mail or token', async () => {
    const { fixture } = await mount(ResetPasswordPage, { email: 'maria@example.com' });

    expect(fixture.nativeElement.querySelector('form')).toBeNull();
    expect(alert(fixture)).toHaveLength(1);
  });

  it('checks length and that both passwords match before sending', async () => {
    const { fixture, auth } = await mount(ResetPasswordPage, {
      email: 'maria@example.com',
      token: 't0ken',
    });
    const [password, confirm] = ['input[autocomplete=new-password]:nth-of-type(1)', 'x'];
    void password;
    void confirm;
    const inputs = () => fixture.nativeElement.querySelectorAll('input[autocomplete=new-password]');

    type(fixture, 'mat-form-field:nth-of-type(1) input', 'short');
    await submit(fixture);
    expect(errors(fixture)).toContain('Mindestens 10 Zeichen.');

    type(fixture, 'mat-form-field:nth-of-type(1) input', 'a-brand-new-password');
    type(fixture, 'mat-form-field:nth-of-type(2) input', 'something-different');
    await submit(fixture);
    expect(inputs()).toHaveLength(2);
    expect(errors(fixture)).toEqual(['Die Passwörter stimmen nicht überein.']);
    expect(auth.resetPassword).not.toHaveBeenCalled();
  });

  it('sends the new password with the link data and goes to the login page with a notice', async () => {
    const { fixture, auth, navigate } = await mount(ResetPasswordPage, {
      email: 'maria@example.com',
      token: 't0ken',
    });

    type(fixture, 'mat-form-field:nth-of-type(1) input', 'a-brand-new-password');
    type(fixture, 'mat-form-field:nth-of-type(2) input', 'a-brand-new-password');
    await submit(fixture);

    expect(auth.resetPassword).toHaveBeenCalledWith(
      'maria@example.com',
      't0ken',
      'a-brand-new-password',
    );
    expect(navigate).toHaveBeenCalledWith(['/auth/login'], {
      queryParams: { notice: 'password-reset' },
    });
  });
});

describe('ConfirmEmailPage', () => {
  it('confirms with the data from the link', async () => {
    const { fixture, auth } = await mount(ConfirmEmailPage, { userId: 'u1', token: 't0ken' });

    expect(auth.confirmEmail).toHaveBeenCalledWith('u1', 't0ken');
    expect(texts(fixture, '.alert.info')).toHaveLength(1);
  });

  it('reports a link that the server rejects', async () => {
    const { fixture } = await mount(ConfirmEmailPage, { userId: 'u1', token: 'bad' }, (auth) =>
      auth.confirmEmail.mockRejectedValue(new ApiError(400, 'auth.invalid_token')),
    );

    expect(alert(fixture)).toHaveLength(1);
    expect(texts(fixture, '.alert.info')).toHaveLength(0);
  });

  it('reports an incomplete link without asking the server', async () => {
    const { fixture, auth } = await mount(ConfirmEmailPage, {});

    expect(auth.confirmEmail).not.toHaveBeenCalled();
    expect(alert(fixture)).toHaveLength(1);
  });
});

describe('InvitationPage', () => {
  const invite = {
    salonName: 'Haarwerk',
    staffDisplayName: 'Tom',
    email: 'tom@example.com',
    role: 'Staff',
    expiresAtUtc: '2026-11-10T09:00:00Z',
  };

  it('shows the invitation and creates the account with the chosen password', async () => {
    const { fixture, auth, navigate } = await mount(InvitationPage, { token: 'tok' }, (fake) =>
      fake.getInvitation.mockResolvedValue(invite),
    );

    expect(auth.getInvitation).toHaveBeenCalledWith('tok');
    type(fixture, 'input[type=password]', 'too-short');
    await submit(fixture);
    expect(errors(fixture)).toContain('Mindestens 10 Zeichen.');
    expect(auth.acceptInvitation).not.toHaveBeenCalled();

    type(fixture, 'input[type=password]', 'staff-password-123');
    await submit(fixture);

    expect(auth.acceptInvitation).toHaveBeenCalledWith('tok', 'staff-password-123');
    expect(navigate).toHaveBeenCalledWith(['/app']);
  });

  it('shows a translated message instead of the form when the invitation cannot be used', async () => {
    const { fixture } = await mount(InvitationPage, { token: 'bad' }, (fake) =>
      fake.getInvitation.mockRejectedValue(new ApiError(404, 'invitation.invalid')),
    );

    expect(alert(fixture)).toEqual(['Einladung ungültig.']);
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
  });

  it('keeps the form and shows the reason when accepting fails', async () => {
    const { fixture, navigate } = await mount(InvitationPage, { token: 'tok' }, (fake) => {
      fake.getInvitation.mockResolvedValue(invite);
      fake.acceptInvitation.mockRejectedValue(new ApiError(409, 'auth.email_taken'));
    });

    type(fixture, 'input[type=password]', 'staff-password-123');
    await submit(fixture);

    expect(alert(fixture)).toEqual(['Konto existiert schon.']);
    expect(navigate).not.toHaveBeenCalled();
  });
});
