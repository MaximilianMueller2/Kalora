import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService } from '../../core/i18n/language.service';
import { LANGUAGE_NAMES, Language, SUPPORTED_LANGUAGES } from '../../core/i18n/languages';

@Component({
  selector: 'app-language-switcher',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatMenuModule, TranslocoPipe],
  template: `
    <button
      mat-button
      class="trigger"
      [matMenuTriggerFor]="menu"
      [attr.aria-label]="language.current().toUpperCase() + ': ' + ('common.language' | transloco)"
    >
      <mat-icon aria-hidden="true">translate</mat-icon>
      <span>{{ language.current().toUpperCase() }}</span>
    </button>
    <mat-menu #menu="matMenu">
      @for (code of languages; track code) {
        <button
          mat-menu-item
          [lang]="code"
          (click)="select(code)"
          [attr.aria-current]="code === language.current()"
        >
          {{ names[code] }}
        </button>
      }
    </mat-menu>
  `,
  styles: `
    .trigger {
      min-width: 44px;
      min-height: 44px;
    }
  `,
})
export class LanguageSwitcher {
  protected readonly language = inject(LanguageService);
  protected readonly languages = SUPPORTED_LANGUAGES;
  protected readonly names = LANGUAGE_NAMES;

  protected select(code: Language): void {
    void this.language.use(code);
  }
}
