import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { apiUrl } from '../../core/http/api-client';
import { CalendarEvent, googleCalendarUrl, outlookCalendarUrl } from './calendar-links';

/** "Add to calendar" for a confirmed appointment: Google and Outlook as prefilled links, everything else (iPhone, Apple, Thunderbird) as the .ics file. */
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
  /** The management token: the .ics file of the appointment is served for it. */
  readonly token = input.required<string>();
  readonly event = input.required<CalendarEvent>();

  protected readonly icsUrl = computed(() =>
    apiUrl(`/public/appointments/${encodeURIComponent(this.token())}/calendar.ics`),
  );
  protected readonly google = computed(() => googleCalendarUrl(this.event()));
  protected readonly outlook = computed(() => outlookCalendarUrl(this.event()));
}
