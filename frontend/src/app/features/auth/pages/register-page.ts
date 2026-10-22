import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import {
  FormField,
  FormRoot,
  email,
  form,
  minLength,
  required,
  validate,
} from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiError } from '../../../core/http/api-error';
import { LanguageService } from '../../../core/i18n/language.service';
import { fieldErrorText } from '../../../shared/forms/field-error';
import { injectErrorMessage } from '../../../shared/forms/server-error';
import { SALON_INDUSTRIES, SalonIndustry } from '../auth.models';
import { AuthService } from '../auth.service';

export const MIN_PASSWORD_LENGTH = 10;

@Component({
  selector: 'app-register-page',
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
    <h1>{{ 'auth.register.title' | transloco }}</h1>
    <p class="lead">{{ 'auth.register.lead' | transloco }}</p>

    @if (errorMessage(); as message) {
      <p class="alert error" role="alert">{{ message }}</p>
    }

    <form [formRoot]="form">
      <mat-form-field appearance="outline">
        <mat-label>{{ 'auth.register.salonName' | transloco }}</mat-label>
        <input matInput type="text" autocomplete="organization" [formField]="form.salonName" />
        @if (errorText(form.salonName().errors()); as error) {
          <mat-error>{{ error.key | transloco: error.params }}</mat-error>
        }
      </mat-form-field>

      <mat-form-field appearance="outline">
        <mat-label>{{ 'auth.register.industry' | transloco }}</mat-label>
        <select matNativeControl [formField]="form.industry">
          @for (industry of industries; track industry) {
            <option [value]="industry">
              {{ 'auth.register.industries.' + industry | transloco }}
            </option>
          }
        </select>
      </mat-form-field>

      <mat-form-field appearance="outline">
        <mat-label>{{ 'auth.register.name' | transloco }}</mat-label>
        <input matInput type="text" autocomplete="name" [formField]="form.name" />
        @if (errorText(form.name().errors()); as error) {
          <mat-error>{{ error.key | transloco: error.params }}</mat-error>
        }
      </mat-form-field>

      <mat-form-field appearance="outline">
        <mat-label>{{ 'auth.register.email' | transloco }}</mat-label>
        <input
          matInput
          type="email"
          autocomplete="email"
          inputmode="email"
          [formField]="form.email"
        />
        @if (errorText(form.email().errors()); as error) {
          <mat-error>{{ error.key | transloco: error.params }}</mat-error>
        }
      </mat-form-field>

      <mat-form-field appearance="outline">
        <mat-label>{{ 'auth.register.password' | transloco }}</mat-label>
        <input matInput type="password" autocomplete="new-password" [formField]="form.password" />
        <mat-hint>{{
          'auth.register.passwordHint' | transloco: { count: minPasswordLength }
        }}</mat-hint>
        @if (errorText(form.password().errors()); as error) {
          <mat-error>{{ error.key | transloco: error.params }}</mat-error>
        }
      </mat-form-field>

      <mat-checkbox class="terms" [formField]="form.acceptTerms">
        {{ 'auth.register.termsPrefix' | transloco }}
        <a routerLink="/legal/terms" target="_blank">{{ 'auth.register.termsLink' | transloco }}</a>
        {{ 'auth.register.termsAnd' | transloco }}
        <a routerLink="/legal/privacy" target="_blank">{{
          'auth.register.privacyLink' | transloco
        }}</a>
      </mat-checkbox>
      @if (form.acceptTerms().touched() && errorText(form.acceptTerms().errors()); as error) {
        <p class="field-error" role="alert">{{ error.key | transloco }}</p>
      }

      <button mat-flat-button class="submit" type="submit" [disabled]="form().submitting()">
        {{ 'auth.register.submit' | transloco }}
      </button>
      <p class="hint">{{ 'auth.register.trialNote' | transloco }}</p>
    </form>

    <div class="links">
      <span>{{ 'auth.register.haveAccount' | transloco }}</span>
      <a routerLink="/auth/login">{{ 'auth.register.login' | transloco }}</a>
    </div>
  `,
})
export class RegisterPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly language = inject(LanguageService);

  protected readonly industries = SALON_INDUSTRIES;
  protected readonly minPasswordLength = MIN_PASSWORD_LENGTH;
  protected readonly errorCode = signal<string | null>(null);
  protected readonly errorMessage = injectErrorMessage(this.errorCode);
  protected readonly errorText = fieldErrorText;

  private readonly model = signal({
    salonName: '',
    industry: 'Hair' as SalonIndustry,
    name: '',
    email: '',
    password: '',
    acceptTerms: false,
  });

  protected readonly form = form(
    this.model,
    (path) => {
      required(path.salonName);
      required(path.name);
      required(path.email);
      email(path.email);
      required(path.password);
      minLength(path.password, MIN_PASSWORD_LENGTH);
      validate(path.acceptTerms, ({ value }) => (value() ? undefined : { kind: 'termsRequired' }));
    },
    { submission: { action: () => this.submit() } },
  );

  private async submit(): Promise<undefined> {
    this.errorCode.set(null);
    const value = this.model();

    try {
      await this.auth.register({
        salonName: value.salonName.trim(),
        industry: value.industry,
        name: value.name.trim(),
        email: value.email.trim(),
        password: value.password,
        language: this.language.current(),
        acceptTerms: value.acceptTerms,
      });
      // The onboarding assistant (a later step) takes over from here.
      await this.router.navigate(['/app/onboarding']);
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    }
    return undefined;
  }
}
