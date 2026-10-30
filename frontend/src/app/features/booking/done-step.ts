import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterNextRender,
  computed,
  inject,
  viewChild,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { LocaleFormatService } from '../../core/i18n/locale-format.service';
import { BookingService } from './booking.service';
import { CalendarActions } from './calendar-actions';

/** The receipt after booking: "request sent" while the salon still has to confirm, "confirmed" with automatic confirmation. */
@Component({
  selector: 'app-done-step',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CalendarActions, MatButtonModule, MatIconModule, TranslocoPipe],
  styleUrl: './skeleton.scss',
  styles: `
    :host {
      display: block;
      text-align: center;
    }

    mat-icon {
      width: 56px;
      height: 56px;
      font-size: 56px;
      color: var(--mat-sys-primary);
    }

    h2 {
      margin: 8px 0;
      font: var(--mat-sys-headline-small);
      outline: none;
    }

    .lead {
      margin: 0 auto 24px;
      max-width: 40ch;
      color: var(--mat-sys-on-surface-variant);
    }

    dl {
      display: grid;
      grid-template-columns: auto 1fr;
      gap: 4px 16px;
      margin: 0 0 24px;
      padding: 16px;
      border-radius: var(--mat-sys-corner-large);
      background: var(--mat-sys-surface-container-low);
      text-align: start;

      dt {
        color: var(--mat-sys-on-surface-variant);
      }

      dd {
        margin: 0;
        font-weight: 500;
      }
    }
  `,
  template: `
    @if (booking.receipt(); as receipt) {
      <mat-icon aria-hidden="true">{{
        receipt.status === 'Confirmed' ? 'event_available' : 'mark_email_read'
      }}</mat-icon>
      <h2 #title tabindex="-1">
        {{
          (receipt.status === 'Confirmed'
            ? 'booking.done.confirmedTitle'
            : 'booking.done.requestedTitle'
          ) | transloco
        }}
      </h2>
      <p class="lead">
        {{
          (receipt.status === 'Confirmed'
            ? 'booking.done.confirmedText'
            : 'booking.done.requestedText'
          ) | transloco
        }}
      </p>
      <dl>
        <dt>{{ 'booking.summary.service' | transloco }}</dt>
        <dd>{{ receipt.serviceName }}</dd>
        <dt>{{ 'booking.summary.person' | transloco }}</dt>
        <dd>{{ receipt.staffName }}</dd>
        <dt>{{ 'booking.summary.when' | transloco }}</dt>
        <dd>{{ when() }}</dd>
      </dl>
      @if (receipt.status === 'Confirmed' && booking.salon.hasValue()) {
        <app-calendar-actions [receipt]="receipt" [salon]="booking.salon.value()" />
      }
      <button mat-stroked-button type="button" (click)="another()">
        {{ 'booking.done.another' | transloco }}
      </button>
    }
  `,
})
export class DoneStep {
  protected readonly booking = inject(BookingService);
  private readonly format = inject(LocaleFormatService);
  private readonly router = inject(Router);
  private readonly title = viewChild<ElementRef<HTMLElement>>('title');

  protected readonly when = computed(() => {
    const receipt = this.booking.receipt();
    const zone = this.booking.salon.hasValue() ? this.booking.salon.value().timeZone : 'UTC';
    return receipt
      ? `${this.format.formatDate(receipt.startUtc, 'fullDate', zone)}, ${this.format.formatDate(receipt.startUtc, 'time', zone)}`
      : '';
  });

  constructor() {
    afterNextRender(() => this.title()?.nativeElement.focus({ preventScroll: true }));
  }

  protected another(): void {
    this.booking.startOver();
    void this.router.navigate(['/', this.booking.slug()]);
  }
}
