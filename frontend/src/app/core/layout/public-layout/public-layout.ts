import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageSwitcher } from '../../../shared/ui/language-switcher';

/** Plain frame for pages customers see: no navigation, language switcher on the top right. */
@Component({
  selector: 'app-public-layout',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LanguageSwitcher, RouterLink, RouterOutlet, TranslocoPipe],
  template: `
    <a class="skip-link" href="#main">{{ 'common.skipToContent' | transloco }}</a>
    <header>
      <app-language-switcher />
    </header>
    <main id="main" tabindex="-1">
      <router-outlet />
    </main>
    <footer>
      <span>{{ 'common.poweredBy' | transloco }}</span>
      <nav [attr.aria-label]="'common.legalLabel' | transloco">
        <a routerLink="/legal/imprint">{{ 'common.imprint' | transloco }}</a>
        <a routerLink="/legal/privacy">{{ 'common.privacy' | transloco }}</a>
        <a routerLink="/legal/terms">{{ 'common.terms' | transloco }}</a>
      </nav>
    </footer>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      min-height: 100dvh;
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
      justify-content: flex-end;
      padding: 8px 12px;
    }

    main {
      flex: 1;
      width: 100%;
      max-width: 640px;
      margin: 0 auto;
      padding: 8px 16px 32px;
      outline: none;
    }

    footer {
      display: grid;
      gap: 4px;
      padding: 16px;
      text-align: center;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-label-medium);

      nav {
        display: flex;
        flex-wrap: wrap;
        justify-content: center;
        gap: 4px 16px;
      }

      a {
        display: inline-flex;
        align-items: center;
        min-height: 32px;
        color: inherit;
      }
    }
  `,
})
export class PublicLayout {}
