import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { MarkdownView } from '../../shared/markdown/markdown-view';
import { BookingService } from './booking.service';

/** The salon's own privacy notice, as the salon wrote it (Markdown). */
@Component({
  selector: 'app-privacy-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MarkdownView, MatIconModule, RouterLink, TranslocoPipe],
  styles: `
    a.back {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      min-height: 44px;
      color: var(--mat-sys-primary);
      text-decoration: none;
    }
  `,
  template: `
    <a class="back" [routerLink]="'/' + booking.slug()">
      <mat-icon aria-hidden="true">arrow_back</mat-icon>
      {{ 'booking.back' | transloco }}
    </a>
    @if (booking.salon.hasValue() && booking.salon.value().privacyText; as text) {
      <app-markdown-view [source]="text" />
    }
  `,
})
export class PrivacyPage {
  protected readonly booking = inject(BookingService);
}
