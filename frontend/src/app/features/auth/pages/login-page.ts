import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { FormField, FormRoot, email, form, required } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiError } from '../../../core/http/api-error';
import { fieldErrorText } from '../../../shared/forms/field-error';
import { injectErrorMessage } from '../../../shared/forms/server-error';
import { AuthService } from '../auth.service';
import { safeReturnUrl } from '../safe-return-url';

type Step = 'credentials' | 'twoFactor' | 'recovery';

@Component({
  selector: 'app-login-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormField,
    FormRoot,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    RouterLink,
    TranslocoPipe,
  ],
  styleUrl: '../auth-pages.scss',
  template: `
    <h1>{{ 'auth.login.title' | transloco }}</h1>
    <p class="lead">
      {{ (step() === 'credentials' ? 'auth.login.lead' : 'auth.login.twoFactorLead') | transloco }}
    </p>

    @if (info(); as key) {
      <p class="alert info" role="status">{{ key | transloco }}</p>
    }
    @if (errorMessage(); as message) {
      <p class="alert error" role="alert">{{ message }}</p>
    }

    <form [formRoot]="form">
      @if (step() === 'credentials') {
        <mat-form-field appearance="outline">
          <mat-label>{{ 'auth.login.email' | transloco }}</mat-label>
          <input
            matInput
            type="email"
            autocomplete="username"
            inputmode="email"
            [formField]="form.email"
          />
          @if (errorText(form.email().errors()); as error) {
            <mat-error>{{ error.key | transloco: error.params }}</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'auth.login.password' | transloco }}</mat-label>
          <input
            matInput
            type="password"
            autocomplete="current-password"
            [formField]="form.password"
          />
          @if (errorText(form.password().errors()); as error) {
            <mat-error>{{ error.key | transloco: error.params }}</mat-error>
          }
        </mat-form-field>

        <mat-checkbox [formField]="form.rememberMe">{{
          'auth.login.remember' | transloco
        }}</mat-checkbox>
      } @else {
        <mat-form-field appearance="outline">
          <mat-label>
            {{
              (step() === 'twoFactor' ? 'auth.login.twoFactorCode' : 'auth.login.recoveryCode')
                | transloco
            }}
          </mat-label>
          <input
            matInput
            type="text"
            [attr.inputmode]="step() === 'twoFactor' ? 'numeric' : 'text'"
            autocomplete="one-time-code"
            [formField]="form.code"
          />
          @if (errorText(form.code().errors()); as error) {
            <mat-error>{{ error.key | transloco: error.params }}</mat-error>
          }
        </mat-form-field>
        <button mat-button type="button" (click)="toggleRecovery()">
          {{
            (step() === 'twoFactor' ? 'auth.login.useRecovery' : 'auth.login.useCode') | transloco
          }}
        </button>
      }

      <button mat-flat-button class="submit" type="submit" [disabled]="form().submitting()">
        {{ 'auth.login.submit' | transloco }}
      </button>
    </form>

    <div class="links">
      <a routerLink="/auth/forgot-password">{{ 'auth.login.forgot' | transloco }}</a>
      <a routerLink="/auth/register">{{ 'auth.login.register' | transloco }}</a>
    </div>
  `,
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** Query parameters, bound by the router. */
  readonly returnUrl = input<string>();
  readonly reason = input<string>();
  readonly notice = input<string>();

  protected readonly step = signal<Step>('credentials');
  protected readonly errorCode = signal<string | null>(null);
  protected readonly errorMessage = injectErrorMessage(this.errorCode);
  protected readonly errorText = fieldErrorText;

  protected readonly info = computed(() => {
    if (this.reason() === 'expired') {
      return 'auth.login.sessionExpired';
    }
    return this.notice() === 'password-reset' ? 'auth.login.passwordResetDone' : null;
  });

  private readonly model = signal({ email: '', password: '', rememberMe: false, code: '' });

  protected readonly form = form(
    this.model,
    (path) => {
      required(path.email);
      email(path.email);
      required(path.password);
      required(path.code, { when: () => this.step() !== 'credentials' });
    },
    { submission: { action: () => this.submit() } },
  );

  protected toggleRecovery(): void {
    this.step.update((step) => (step === 'twoFactor' ? 'recovery' : 'twoFactor'));
    this.model.update((value) => ({ ...value, code: '' }));
    this.errorCode.set(null);
  }

  private async submit(): Promise<undefined> {
    this.errorCode.set(null);
    const { email, password, rememberMe, code } = this.model();
    const step = this.step();

    try {
      await this.auth.login({
        email: email.trim(),
        password,
        rememberMe,
        twoFactorCode: step === 'twoFactor' ? code.trim() : undefined,
        recoveryCode: step === 'recovery' ? code.trim() : undefined,
      });
      await this.router.navigateByUrl(safeReturnUrl(this.returnUrl()));
    } catch (error) {
      if (error instanceof ApiError && error.code === 'auth.two_factor_required') {
        this.step.set('twoFactor');
        return;
      }
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    }
    return undefined;
  }
}
