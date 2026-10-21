import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageSwitcher } from '../../../shared/ui/language-switcher';
import { ThemeToggle } from '../../../shared/ui/theme-toggle';
import { NAV_ITEMS } from '../nav-items';
import { injectIsDesktop } from '../viewport';

/** Frame of the salon area: side navigation on desktop, bottom navigation on phones. */
@Component({
  selector: 'app-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    LanguageSwitcher,
    MatIconModule,
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    ThemeToggle,
    TranslocoPipe,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  protected readonly items = NAV_ITEMS;
  protected readonly isDesktop = injectIsDesktop();
}
