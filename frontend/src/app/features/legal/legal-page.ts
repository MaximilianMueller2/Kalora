import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  resource,
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { ApiClient } from '../../core/http/api-client';
import { LanguageService } from '../../core/i18n/language.service';
import { MarkdownView } from '../../shared/markdown/markdown-view';
import { LegalPage, isLegalPage, legalTextPath } from './legal';

/** Imprint, privacy notice, terms and data processing agreement of Kalora. The texts are German; they are the binding ones. */
@Component({
  selector: 'app-legal-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MarkdownView, MatIconModule, TranslocoPipe],
  styles: `
    h1 {
      margin: 16px 0 8px;
      font: var(--mat-sys-headline-medium);
    }

    .note {
      display: flex;
      gap: 8px;
      margin: 0 0 16px;
      padding: 12px 16px;
      border-radius: var(--mat-sys-corner-medium);
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
    }

    .skeleton {
      min-height: 240px;
      border-radius: var(--mat-sys-corner-large);
      background: var(--mat-sys-surface-container);
    }

    .alert {
      padding: 16px;
      border-radius: var(--mat-sys-corner-medium);
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
  `,
  template: `
    @if (known(); as page) {
      <h1>{{ 'legal.' + page | transloco }}</h1>
      @if (language.current() !== 'de') {
        <p class="note" role="note">
          <mat-icon aria-hidden="true">translate</mat-icon>
          {{ 'legal.germanOnly' | transloco }}
        </p>
      }
      @if (text.hasValue()) {
        <app-markdown-view [source]="text.value()" />
      } @else if (text.status() === 'error') {
        <p class="alert" role="alert">{{ 'legal.loadFailed' | transloco }}</p>
      } @else {
        <div class="skeleton" aria-busy="true" aria-hidden="true"></div>
      }
    } @else {
      <p class="alert" role="alert">{{ 'legal.unknown' | transloco }}</p>
    }
  `,
})
export class LegalPageComponent {
  private readonly api = inject(ApiClient);
  protected readonly language = inject(LanguageService);

  /** From the route: `/legal/:page`. */
  readonly page = input.required<string>();

  protected readonly known = computed<LegalPage | null>(() =>
    isLegalPage(this.page()) ? (this.page() as LegalPage) : null,
  );

  protected readonly text = resource({
    params: () => this.known(),
    loader: ({ params }) =>
      params ? firstValueFrom(this.api.getAssetText(legalTextPath(params))) : Promise.resolve(''),
  });
}
