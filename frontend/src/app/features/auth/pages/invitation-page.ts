import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  resource,
  signal,
} from '@angular/core';
import { FormField, FormRoot, form, minLength, required } from '@angular/forms/signals';
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
  selector: 'app-invitation-page',
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
    <h1>{{ 'auth.invitation.title' | transloco }}</h1>

    @if (invitation.isLoading()) {
      <p class="lead" role="status">{{ 'auth.invitation.loading' | transloco }}</p>
    } @else if (invitation.error()) {
      <p class="alert error" role="alert">{{ loadErrorMessage() }}</p>
      <div class="links">
        <a routerLink="/auth/login">{{ 'auth.forgot.back' | transloco }}</a>
      </div>
    } @else if (invitation.value(); as invite) {
      <p class="lead">
        {{
          'auth.invitation.lead'
            | transloco
              : {
                  salon: invite.salonName,
                  role: ('auth.invitation.roles.' + invite.role | transloco),
                }
        }}
      </p>
      <p class="hint">{{ 'auth.invitation.account' | transloco: { email: invite.email } }}</p>

      @if (errorMessage(); as message) {
        <p class="alert error" role="alert">{{ message }}</p>
      }
      <form [formRoot]="form">
        <mat-form-field appearance="outline">
          <mat-label>{{ 'auth.invitation.password' | transloco }}</mat-label>
          <input matInput type="password" autocomplete="new-password" [formField]="form.password" />
          <mat-hint>{{
            'auth.register.passwordHint' | transloco: { count: minPasswordLength }
          }}</mat-hint>
          @if (errorText(form.password().errors()); as error) {
            <mat-error>{{ error.key | transloco: error.params }}</mat-error>
          }
        </mat-form-field>
        <button mat-flat-button class="submit" type="submit" [disabled]="form().submitting()">
          {{ 'auth.invitation.submit' | transloco }}
        </button>
      </form>
    }
  `,
})
export class InvitationPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** Path parameter of the link in the e-mail. */
  readonly token = input.required<string>();

  protected readonly invitation = resource({
    params: () => this.token(),
    loader: ({ params }) => this.auth.getInvitation(params),
  });

  protected readonly minPasswordLength = MIN_PASSWORD_LENGTH;
  protected readonly errorCode = signal<string | null>(null);
  protected readonly errorMessage = injectErrorMessage(this.errorCode);
  protected readonly errorText = fieldErrorText;

  private readonly loadErrorCode = computed(() => {
    const error = this.invitation.error();
    return error ? (error instanceof ApiError ? error.code : 'unknown') : null;
  });

  protected readonly loadErrorMessage = injectErrorMessage(this.loadErrorCode);

  private readonly model = signal({ password: '' });

  protected readonly form = form(
    this.model,
    (path) => {
      required(path.password);
      minLength(path.password, MIN_PASSWORD_LENGTH);
    },
    { submission: { action: () => this.submit() } },
  );

  private async submit(): Promise<undefined> {
    this.errorCode.set(null);
    try {
      await this.auth.acceptInvitation(this.token(), this.model().password);
      await this.router.navigate(['/app']);
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    }
    return undefined;
  }
}
