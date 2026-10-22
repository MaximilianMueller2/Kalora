import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageSwitcher } from '../../shared/ui/language-switcher';
import { ThemeToggle } from '../../shared/ui/theme-toggle';

/** Frame for login, registration and the other account pages: a centered card on a calm background. */
@Component({
  selector: 'app-auth-layout',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LanguageSwitcher, RouterLink, RouterOutlet, ThemeToggle, TranslocoPipe],
  template: `
    <a class="skip-link" href="#main">{{ 'common.skipToContent' | transloco }}</a>
    <header>
      <a class="brand" routerLink="/">Kalora</a>
      <div class="tools">
        <app-language-switcher />
        <app-theme-toggle />
      </div>
    </header>
    <main id="main" tabindex="-1">
      <div class="card">
        <router-outlet />
      </div>
    </main>
  `,
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

      &:focus {
        left: 0;
      }
    }

    header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 8px 12px 8px 20px;
    }

    .brand {
      font: var(--mat-sys-title-large);
      font-weight: 600;
      color: var(--mat-sys-primary);
      text-decoration: none;
    }

    .tools {
      display: flex;
      align-items: center;
      gap: 4px;
    }

    main {
      display: flex;
      flex: 1;
      align-items: flex-start;
      justify-content: center;
      padding: 16px;
      outline: none;
    }

    .card {
      width: 100%;
      max-width: 440px;
      padding: 28px 24px;
      border-radius: var(--mat-sys-corner-extra-large);
      background: var(--mat-sys-surface);
      box-shadow: var(--mat-sys-level1);
    }

    @media (min-width: 600px) {
      main {
        align-items: center;
        padding-bottom: 64px;
      }

      .card {
        padding: 36px 40px;
      }
    }
  `,
})
export class AuthLayout {}
