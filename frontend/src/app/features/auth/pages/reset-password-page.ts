import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { FormField, FormRoot, form, minLength, required, validate } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiError } from '../../../core/http/api-error';
import { fieldErrorText } from '../../../shared/forms/field-error';
import { injectErrorMessage } from '../../../shared/forms/server-error';
import { AuthService } from '../auth.service';
import { MIN_PASSWORD_LENGTH } from './register-page';

@Component({
  selector: 'app-reset-password-page',
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
    <h1>{{ 'auth.reset.title' | transloco }}</h1>

    @if (!hasLink()) {
      <p class="alert error" role="alert">{{ 'auth.reset.incompleteLink' | transloco }}</p>
      <div class="links">
        <a routerLink="/auth/forgot-password">{{ 'auth.reset.requestNew' | transloco }}</a>
      </div>
    } @else {
      <p class="lead">{{ 'auth.reset.lead' | transloco }}</p>
      @if (errorMessage(); as message) {
        <p class="alert error" role="alert">{{ message }}</p>
      }
      <form [formRoot]="form">
        <mat-form-field appearance="outline">
          <mat-label>{{ 'auth.reset.newPassword' | transloco }}</mat-label>
          <input matInput type="password" autocomplete="new-password" [formField]="form.password" />
          <mat-hint>{{
            'auth.register.passwordHint' | transloco: { count: minPasswordLength }
          }}</mat-hint>
          @if (errorText(form.password().errors()); as error) {
            <mat-error>{{ error.key | transloco: error.params }}</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'auth.reset.confirmPassword' | transloco }}</mat-label>
          <input matInput type="password" autocomplete="new-password" [formField]="form.confirm" />
          @if (errorText(form.confirm().errors()); as error) {
            <mat-error>{{ error.key | transloco: error.params }}</mat-error>
          }
        </mat-form-field>

        <button mat-flat-button class="submit" type="submit" [disabled]="form().submitting()">
          {{ 'auth.reset.submit' | transloco }}
        </button>
      </form>
    }
  `,
})
export class ResetPasswordPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** Query parameters of the link in the e-mail. */
  readonly email = input<string>();
  readonly token = input<string>();

  protected readonly hasLink = computed(() => !!this.email() && !!this.token());
  protected readonly minPasswordLength = MIN_PASSWORD_LENGTH;
  protected readonly errorCode = signal<string | null>(null);
  protected readonly errorMessage = injectErrorMessage(this.errorCode);
  protected readonly errorText = fieldErrorText;

  private readonly model = signal({ password: '', confirm: '' });

  protected readonly form = form(
    this.model,
    (path) => {
      required(path.password);
      minLength(path.password, MIN_PASSWORD_LENGTH);
      required(path.confirm);
      validate(path.confirm, ({ value, valueOf }) =>
        value() === valueOf(path.password) ? undefined : { kind: 'passwordMismatch' },
      );
    },
    { submission: { action: () => this.submit() } },
  );

  private async submit(): Promise<undefined> {
    this.errorCode.set(null);
    try {
      await this.auth.resetPassword(this.email() ?? '', this.token() ?? '', this.model().password);
      await this.router.navigate(['/auth/login'], { queryParams: { notice: 'password-reset' } });
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    }
    return undefined;
  }
}
