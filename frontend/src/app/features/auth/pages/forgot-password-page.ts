import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormField, FormRoot, email, form, required } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiError } from '../../../core/http/api-error';
import { fieldErrorText } from '../../../shared/forms/field-error';
import { injectErrorMessage } from '../../../shared/forms/server-error';
import { AuthService } from '../auth.service';

@Component({
  selector: 'app-forgot-password-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormField,
    FormRoot,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    RouterLink,
    TranslocoPipe,
  ],
  styleUrl: '../auth-pages.scss',
  template: `
    <h1>{{ 'auth.forgot.title' | transloco }}</h1>

    @if (sent()) {
      <p class="alert info" role="status">{{ 'auth.forgot.sent' | transloco }}</p>
    } @else {
      <p class="lead">{{ 'auth.forgot.lead' | transloco }}</p>
      @if (errorMessage(); as message) {
        <p class="alert error" role="alert">{{ message }}</p>
      }
      <form [formRoot]="form">
        <mat-form-field appearance="outline">
          <mat-label>{{ 'auth.forgot.email' | transloco }}</mat-label>
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
        <button mat-flat-button class="submit" type="submit" [disabled]="form().submitting()">
          {{ 'auth.forgot.submit' | transloco }}
        </button>
      </form>
    }

    <div class="links">
      <a routerLink="/auth/login">{{ 'auth.forgot.back' | transloco }}</a>
    </div>
  `,
})
export class ForgotPasswordPage {
  private readonly auth = inject(AuthService);

  protected readonly sent = signal(false);
  protected readonly errorCode = signal<string | null>(null);
  protected readonly errorMessage = injectErrorMessage(this.errorCode);
  protected readonly errorText = fieldErrorText;

  private readonly model = signal({ email: '' });

  protected readonly form = form(
    this.model,
    (path) => {
      required(path.email);
      email(path.email);
    },
    { submission: { action: () => this.submit() } },
  );

  private async submit(): Promise<undefined> {
    this.errorCode.set(null);
    try {
      await this.auth.forgotPassword(this.model().email.trim());
      this.sent.set(true);
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    }
    return undefined;
  }
}
