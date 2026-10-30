import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { ApiError } from '../../core/http/api-error';
import { LocaleFormatService } from '../../core/i18n/locale-format.service';
import { Toast } from '../../core/ui/toast';
import { injectErrorMessage } from '../../shared/forms/server-error';
import { targetValue } from '../../shared/forms/target-value';
import { DeclineDialog, ReasonDialogData } from '../requests/decline-dialog';
import { Appointment } from '../requests/requests.models';
import { RequestsService } from '../requests/requests.service';
import { instantAtMinutes, localDate, minutesOfDay } from './calendar-geometry';
import { CalendarService } from './calendar.service';

const CANCEL_TEXTS: ReasonDialogData = {
  titleKey: 'calendar.panel.cancelTitle',
  leadKey: 'calendar.panel.cancelLead',
  confirmKey: 'calendar.panel.cancelConfirm',
};

const pad = (value: number) => String(value).padStart(2, '0');
export const toTimeInput = (minutes: number) =>
  `${pad(Math.floor(minutes / 60))}:${pad(minutes % 60)}`;
export const fromTimeInput = (text: string) => {
  const [hours, minutes] = text.split(':').map(Number);
  return hours * 60 + minutes;
};

/** Details of one appointment and what can be done with it: answer, cancel, mark, move (also without dragging). */
@Component({
  selector: 'app-appointment-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, MatButtonModule, MatIconModule, TranslocoPipe],
  templateUrl: './appointment-panel.html',
  styleUrl: './panel.scss',
})
export class AppointmentPanel {
  private readonly requests = inject(RequestsService);
  private readonly calendar = inject(CalendarService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(Toast);
  private readonly format = inject(LocaleFormatService);

  readonly appointment = input.required<Appointment>();
  readonly zone = input.required<string>();
  readonly people = input.required<{ staffId: string; displayName: string }[]>();
  readonly slotMinutes = input(15);
  readonly now = input.required<number>();

  readonly closed = output<void>();
  /** Something about the appointment changed; the calendar loads again. */
  readonly changed = output<void>();

  protected readonly busy = signal(false);
  protected readonly errorCode = signal<string | null>(null);
  protected readonly error = injectErrorMessage(this.errorCode);
  protected readonly moving = signal(false);
  protected readonly moveDate = signal('');
  protected readonly moveTime = signal('');
  protected readonly moveStaff = signal('');

  protected readonly when = computed(() => {
    const a = this.appointment();
    const zone = this.zone();
    return `${this.format.formatDate(a.startUtc, 'fullDate', zone)}, ${this.format.formatDate(a.startUtc, 'time', zone)} – ${this.format.formatDate(a.endUtc, 'time', zone)}`;
  });
  protected readonly can = computed(() => {
    const a = this.appointment();
    const started = Date.parse(a.startUtc) <= this.now();
    return {
      answer: a.nextStates.includes('Confirmed'),
      cancel: a.status === 'Confirmed' && a.nextStates.includes('Cancelled'),
      move: a.status === 'Confirmed' && Date.parse(a.endUtc) > this.now(),
      mark: started && a.nextStates.includes('Completed'),
    };
  });
  protected readonly step = computed(() => this.slotMinutes() * 60);

  protected readonly valueOf = targetValue;

  protected openMove(): void {
    const a = this.appointment();
    const date = localDate(a.startUtc, this.zone());
    this.moveDate.set(date);
    this.moveTime.set(toTimeInput(minutesOfDay(a.startUtc, date, this.zone())));
    this.moveStaff.set(a.staff.id);
    this.errorCode.set(null);
    this.moving.set(true);
  }

  protected async submitMove(): Promise<void> {
    const a = this.appointment();
    const startUtc = instantAtMinutes(this.moveDate(), fromTimeInput(this.moveTime()), this.zone());
    await this.run(async () => {
      await this.calendar.move(a.id, { startUtc, staffId: this.moveStaff(), reason: null });
      this.toast.show('calendar.panel.moved');
      this.moving.set(false);
    });
  }

  protected confirm(): Promise<void> {
    return this.run(async () => {
      await this.requests.confirm(this.appointment().id);
      this.toast.show('calendar.panel.confirmed');
    });
  }

  protected async decline(): Promise<void> {
    const reason = await this.askReason();
    if (reason !== undefined) {
      await this.run(async () => {
        await this.requests.decline(this.appointment().id, reason || null);
        this.toast.show('calendar.panel.declined');
      });
    }
  }

  protected async cancel(): Promise<void> {
    const reason = await this.askReason(CANCEL_TEXTS);
    if (reason !== undefined) {
      await this.run(async () => {
        await this.requests.cancel(this.appointment().id, reason || null);
        this.toast.show('calendar.panel.cancelled');
      });
    }
  }

  protected attended(): Promise<void> {
    return this.run(() => this.requests.markAttended(this.appointment().id));
  }

  protected noShow(): Promise<void> {
    return this.run(() => this.requests.markNoShow(this.appointment().id));
  }

  private askReason(data?: ReasonDialogData): Promise<string | undefined> {
    return firstValueFrom(
      this.dialog
        .open<DeclineDialog, ReasonDialogData | undefined, string>(DeclineDialog, {
          data,
          width: '480px',
          maxWidth: '95vw',
        })
        .afterClosed(),
    );
  }

  private async run(action: () => Promise<unknown>): Promise<void> {
    this.busy.set(true);
    this.errorCode.set(null);
    try {
      await action();
      this.changed.emit();
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    } finally {
      this.busy.set(false);
    }
  }
}
