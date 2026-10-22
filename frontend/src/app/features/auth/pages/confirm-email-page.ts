import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../auth.service';

@Component({
  selector: 'app-confirm-email-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatProgressSpinnerModule, RouterLink, TranslocoPipe],
  styleUrl: '../auth-pages.scss',
  template: `
    <h1>{{ 'auth.confirm.title' | transloco }}</h1>

    @switch (state()) {
      @case ('working') {
        <p class="lead" role="status">
          <mat-spinner diameter="20" aria-hidden="true" />
          {{ 'auth.confirm.working' | transloco }}
        </p>
      }
      @case ('done') {
        <p class="alert info" role="status">{{ 'auth.confirm.success' | transloco }}</p>
        <a mat-flat-button class="submit" routerLink="/app">{{
          'auth.confirm.continue' | transloco
        }}</a>
      }
      @case ('failed') {
        <p class="alert error" role="alert">{{ 'auth.confirm.failure' | transloco }}</p>
        <a mat-stroked-button class="submit" routerLink="/app">{{
          'auth.confirm.continue' | transloco
        }}</a>
      }
    }
  `,
})
export class ConfirmEmailPage implements OnInit {
  private readonly auth = inject(AuthService);

  /** Query parameters of the link in the e-mail. */
  readonly userId = input<string>();
  readonly token = input<string>();

  protected readonly state = signal<'working' | 'done' | 'failed'>('working');

  async ngOnInit(): Promise<void> {
    const userId = this.userId();
    const token = this.token();
    if (!userId || !token) {
      this.state.set('failed');
      return;
    }

    try {
      await this.auth.confirmEmail(userId, token);
      this.state.set('done');
    } catch {
      this.state.set('failed');
    }
  }
}
