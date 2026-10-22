import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../../features/auth/auth.service';
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
    MatButtonModule,
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
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly user = this.auth.currentUser;
  protected readonly items = computed(() =>
    NAV_ITEMS.filter((item) => !item.roles || this.auth.hasAnyRole(item.roles)),
  );
  protected readonly isDesktop = injectIsDesktop();

  protected async logout(): Promise<void> {
    await this.auth.logout();
    await this.router.navigate(['/auth/login']);
  }
}
