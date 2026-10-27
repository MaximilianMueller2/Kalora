import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { BookingService } from './booking.service';

/** The salon's imprint text, as the salon wrote it. */
@Component({
  selector: 'app-imprint-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, RouterLink, TranslocoPipe],
  styles: `
    a {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      min-height: 44px;
      color: var(--mat-sys-primary);
      text-decoration: none;
    }

    h2 {
      margin: 8px 0 16px;
      font: var(--mat-sys-headline-small);
    }

    p {
      margin: 0;
      white-space: pre-line;
      overflow-wrap: anywhere;
    }
  `,
  template: `
    <a [routerLink]="'/' + booking.slug()">
      <mat-icon aria-hidden="true">arrow_back</mat-icon>
      {{ 'booking.back' | transloco }}
    </a>
    <h2>{{ 'booking.imprint' | transloco }}</h2>
    @if (booking.salon.hasValue()) {
      <p>{{ booking.salon.value().imprintText }}</p>
    }
  `,
})
export class ImprintPage {
  protected readonly booking = inject(BookingService);
}
