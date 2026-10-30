import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { apiUrl } from '../../core/http/api-client';
import { BookingReceipt, PublicSalon } from './booking.models';
import { CalendarEvent, googleCalendarUrl, outlookCalendarUrl } from './calendar-links';

/** "Add to calendar" for a confirmed booking: Google and Outlook as prefilled links, everything else (iPhone, Apple, Thunderbird) as the .ics file. */
@Component({
  selector: 'app-calendar-actions',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, TranslocoPipe],
  styles: `
    :host {
      display: block;
      margin-bottom: 24px;
    }

    h3 {
      margin: 0 0 12px;
      font: var(--mat-sys-title-small);
      color: var(--mat-sys-on-surface-variant);
    }

    .actions {
      display: flex;
      flex-wrap: wrap;
      justify-content: center;
      gap: 8px;
    }
  `,
  template: `
    <h3>{{ 'booking.done.addToCalendar' | transloco }}</h3>
    <div class="actions">
      <a mat-stroked-button [href]="icsUrl()" download="appointment.ics">
        <mat-icon aria-hidden="true">download</mat-icon>
        {{ 'booking.done.downloadIcs' | transloco }}
      </a>
      <a mat-stroked-button [href]="google()" target="_blank" rel="noopener noreferrer">
        {{ 'booking.done.googleCalendar' | transloco }}
      </a>
      <a mat-stroked-button [href]="outlook()" target="_blank" rel="noopener noreferrer">
        {{ 'booking.done.outlookCalendar' | transloco }}
      </a>
    </div>
  `,
})
export class CalendarActions {
  readonly receipt = input.required<BookingReceipt>();
  readonly salon = input.required<PublicSalon>();

  /** The same file the confirmation mail carries; the token in the URL is the customer's credential. */
  protected readonly icsUrl = computed(() =>
    apiUrl(`/public/appointments/${encodeURIComponent(this.receipt().manageToken)}/calendar.ics`),
  );

  private readonly event = computed<CalendarEvent>(() => {
    const receipt = this.receipt();
    const salon = this.salon();
    return {
      title: `${receipt.serviceName} – ${salon.name}`,
      startUtc: receipt.startUtc,
      endUtc: receipt.endUtc,
      location: [salon.street, `${salon.postalCode} ${salon.city}`.trim()]
        .filter((part) => part.length > 0)
        .join(', '),
      details: [receipt.serviceName, receipt.staffName]
        .filter((part) => part.length > 0)
        .join('\n'),
    };
  });

  protected readonly google = computed(() => googleCalendarUrl(this.event()));
  protected readonly outlook = computed(() => outlookCalendarUrl(this.event()));
}
