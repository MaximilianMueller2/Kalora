import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  resource,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { ApiError } from '../../core/http/api-error';
import { LocaleFormatService } from '../../core/i18n/locale-format.service';
import { Toast } from '../../core/ui/toast';
import { injectErrorMessage } from '../../shared/forms/server-error';
import { CalendarActions } from '../booking/calendar-actions';
import { CalendarEvent } from '../booking/calendar-links';
import { CancelDialog } from './cancel-dialog';
import { ManagedAppointment } from './manage.models';
import { ManageService } from './manage.service';

/**
 * A customer's own page (`/m/:token`): the appointment, adding it to a calendar, cancelling it (up to the salon's cancellation
 * period, after that the phone number of the salon), or asking for a new one. The token in the link is the only credential.
 */
@Component({
  selector: 'app-manage-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CalendarActions, MatButtonModule, MatIconModule, RouterLink, TranslocoPipe],
  templateUrl: './manage-page.html',
  styleUrl: './manage-page.scss',
})
export class ManagePage {
  private readonly service = inject(ManageService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(Toast);
  private readonly format = inject(LocaleFormatService);

  /** From the link: `/m/:token`. */
  readonly token = input.required<string>();

  private readonly loaded = resource({
    params: () => this.token(),
    loader: ({ params }) => this.service.get(params),
  });
  /** The appointment after the customer cancelled; it replaces what was loaded. */
  private readonly changed = signal<ManagedAppointment | null>(null);

  protected readonly busy = signal(false);
  protected readonly errorCode = signal<string | null>(null);
  protected readonly error = injectErrorMessage(this.errorCode);

  protected readonly appointment = computed<ManagedAppointment | null>(
    () => this.changed() ?? (this.loaded.hasValue() ? this.loaded.value() : null),
  );
  protected readonly loading = computed(
    () => this.loaded.isLoading() && this.appointment() === null,
  );
  protected readonly notFound = computed(
    () =>
      this.loaded.status() === 'error' &&
      this.loaded.error() instanceof ApiError &&
      (this.loaded.error() as ApiError).status === 404,
  );
  protected readonly failed = computed(() => this.loaded.status() === 'error' && !this.notFound());

  protected readonly when = computed(() => {
    const a = this.appointment();
    return a
      ? `${this.format.formatDate(a.startUtc, 'fullDate', a.timeZone)}, ${this.format.formatDate(a.startUtc, 'time', a.timeZone)} – ${this.format.formatDate(a.endUtc, 'time', a.timeZone)}`
      : '';
  });
  protected readonly cancelUntil = computed(() => {
    const a = this.appointment();
    return a?.cancelUntilUtc
      ? this.format.formatDate(a.cancelUntilUtc, 'dateTime', a.timeZone)
      : '';
  });
  protected readonly isOpen = computed(() => {
    const status = this.appointment()?.status;
    return status === 'Requested' || status === 'Confirmed';
  });
  protected readonly calendarEvent = computed<CalendarEvent | null>(() => {
    const a = this.appointment();
    return a
      ? {
          title: `${a.serviceName} – ${a.salonName}`,
          startUtc: a.startUtc,
          endUtc: a.endUtc,
          location: a.salonAddress,
          details: a.serviceName,
        }
      : null;
  });

  protected async cancel(): Promise<void> {
    const confirmed = await firstValueFrom(
      this.dialog
        .open<CancelDialog, undefined, boolean>(CancelDialog, { width: '420px', maxWidth: '95vw' })
        .afterClosed(),
    );
    if (!confirmed) {
      return;
    }

    this.busy.set(true);
    this.errorCode.set(null);
    try {
      this.changed.set(await this.service.cancel(this.token()));
      this.toast.show('manage.cancelled');
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
      // The state may have changed meanwhile (the salon answered); show what is true now.
      this.loaded.reload();
    } finally {
      this.busy.set(false);
    }
  }
}
