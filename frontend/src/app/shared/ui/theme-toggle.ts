import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { ThemeMode, ThemeService } from '../../core/theme/theme.service';

const ICONS: Record<ThemeMode, string> = {
  system: 'brightness_auto',
  light: 'light_mode',
  dark: 'dark_mode',
};

@Component({
  selector: 'app-theme-toggle',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, TranslocoPipe],
  template: `
    <button
      mat-icon-button
      class="toggle"
      (click)="theme.cycle()"
      [attr.aria-label]="'theme.' + theme.mode() | transloco"
      [title]="'theme.' + theme.mode() | transloco"
    >
      <mat-icon aria-hidden="true">{{ icon() }}</mat-icon>
    </button>
  `,
  styles: `
    .toggle {
      width: 44px;
      height: 44px;
    }
  `,
})
export class ThemeToggle {
  protected readonly theme = inject(ThemeService);
  protected readonly icon = computed(() => ICONS[this.theme.mode()]);
}
