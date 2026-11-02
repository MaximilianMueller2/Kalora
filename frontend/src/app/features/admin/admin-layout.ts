import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageSwitcher } from '../../shared/ui/language-switcher';
import { ThemeToggle } from '../../shared/ui/theme-toggle';
import { AuthService } from '../auth/auth.service';

/** Frame of the platform area: a bar on top, the page below. Separate from the salon area, which has its own navigation. */
@Component({
  selector: 'app-admin-layout',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    LanguageSwitcher,
    MatButtonModule,
    MatIconModule,
    RouterLink,
    RouterOutlet,
    ThemeToggle,
    TranslocoPipe,
  ],
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      min-height: 100dvh;
      background: var(--mat-sys-surface-container-low);
    }

    .skip-link {
      position: absolute;
      top: 0;
      left: -9999px;
      z-index: 100;
      padding: 12px 16px;
      background: var(--mat-sys-primary);
      color: var(--mat-sys-on-primary);
    }

    .skip-link:focus {
      left: 0;
    }

    header {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px 16px;
      padding: 8px 16px;
      background: var(--mat-sys-surface);
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }

    .brand {
      color: var(--mat-sys-primary);
      font: var(--mat-sys-title-large);
      text-decoration: none;
    }

    .area {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-label-large);
    }

    .tools {
      display: flex;
      align-items: center;
      gap: 4px;
      margin-inline-start: auto;
    }

    main {
      flex: 1;
      width: 100%;
      max-width: 960px;
      margin: 0 auto;
      padding: 16px;
      box-sizing: border-box;
    }

    main:focus {
      outline: none;
    }
  `,
  template: `
    <a class="skip-link" href="#main">{{ 'common.skipToContent' | transloco }}</a>
    <header>
      <a class="brand" routerLink="/admin">Kalora</a>
      <span class="area">{{ 'admin.area' | transloco }}</span>
      <div class="tools">
        @if (user()?.salon) {
          <a mat-button routerLink="/app">
            <mat-icon aria-hidden="true">storefront</mat-icon>
            {{ 'admin.toSalon' | transloco }}
          </a>
        }
        <app-language-switcher />
        <app-theme-toggle />
        <button
          mat-icon-button
          type="button"
          [attr.aria-label]="'nav.logout' | transloco"
          (click)="logout()"
        >
          <mat-icon aria-hidden="true">logout</mat-icon>
        </button>
      </div>
    </header>
    <main id="main" tabindex="-1">
      <router-outlet />
    </main>
  `,
})
export class AdminLayout {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly user = this.auth.currentUser;

  protected async logout(): Promise<void> {
    await this.auth.logout();
    await this.router.navigate(['/auth/login']);
  }
}
