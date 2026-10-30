import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom, timer } from 'rxjs';
import { injectIsDesktop } from '../../core/layout/viewport';
import { ApiError } from '../../core/http/api-error';
import { ErrorNotifier } from '../../core/http/error-notifier';
import { LocaleFormatService } from '../../core/i18n/locale-format.service';
import { Toast } from '../../core/ui/toast';
import { Appointment } from '../requests/requests.models';
import { AppointmentPanel } from './appointment-panel';
import { BlockPanel } from './block-panel';
import { CalendarGrid, MoveDrop, MoveRejected, SlotTap } from './calendar-grid';
import { instantAtMinutes, minutesOfDay, snap } from './calendar-geometry';
import { CalendarBlock, CalendarView } from './calendar.models';
import { CalendarService } from './calendar.service';
import { CalendarStore } from './calendar.store';
import { QuickCreateData, QuickCreateDialog } from './quick-create-dialog';

type Selection = { kind: 'appointment'; id: string } | { kind: 'block'; block: CalendarBlock };

const CLOCK_MS = 60_000;
const REFRESH_MS = 120_000;
const SWIPE_DISTANCE = 60;

/** The team calendar: day view with a column per person, week view for one person, a phone-friendly day view per person. */
@Component({
  selector: 'app-calendar-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    AppointmentPanel,
    BlockPanel,
    CalendarGrid,
    MatButtonModule,
    MatIconModule,
    TranslocoPipe,
  ],
  providers: [CalendarStore],
  templateUrl: './calendar-page.html',
  styleUrl: './calendar-page.scss',
})
export class CalendarPage {
  protected readonly store = inject(CalendarStore);
  private readonly service = inject(CalendarService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(Toast);
  private readonly errors = inject(ErrorNotifier);
  private readonly format = inject(LocaleFormatService);
  protected readonly isDesktop = injectIsDesktop();

  protected readonly selection = signal<Selection | null>(null);
  private swipeStart: { x: number; y: number } | null = null;

  /** The phone shows one person at a time in the day view. */
  private readonly singlePerson = computed(() => !this.isDesktop() && this.store.view() === 'day');
  protected readonly lanes = computed(() => this.store.lanes(this.singlePerson()));
  protected readonly window = computed(() => this.store.window(this.singlePerson()));
  protected readonly byDay = computed(() => this.store.view() === 'week');
  protected readonly people = computed(() =>
    this.store.people().map((p) => ({ staffId: p.staffId, displayName: p.displayName })),
  );
  protected readonly showPeopleFilter = computed(
    () => this.isDesktop() && this.store.view() === 'day' && this.store.people().length > 1,
  );
  protected readonly showPeopleTabs = computed(
    () => !this.isDesktop() && this.store.people().length > 1,
  );
  protected readonly selectedAppointment = computed(() => {
    const selected = this.selection();
    if (selected?.kind !== 'appointment') {
      return null;
    }
    return (
      this.store
        .people()
        .flatMap((p) => p.appointments)
        .find((a) => a.id === selected.id) ?? null
    );
  });
  protected readonly selectedBlock = computed(() => {
    const selected = this.selection();
    return selected?.kind === 'block' ? selected.block : null;
  });
  protected readonly title = computed(() => {
    const days = this.store.days();
    const zone = 'UTC';
    const first = this.format.formatDate(
      `${days[0]}T12:00:00Z`,
      this.store.view() === 'week' ? 'date' : 'fullDate',
      zone,
    );
    return this.store.view() === 'week'
      ? `${first} – ${this.format.formatDate(`${days[6]}T12:00:00Z`, 'date', zone)}`
      : first;
  });

  constructor() {
    timer(CLOCK_MS, CLOCK_MS)
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.store.tick());
    timer(REFRESH_MS, REFRESH_MS)
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.store.reload());
  }

  protected setView(view: CalendarView): void {
    this.store.setView(view);
    this.selection.set(null);
  }

  protected step(direction: -1 | 1): void {
    this.store.step(direction);
    this.selection.set(null);
  }

  protected today(): void {
    this.store.today();
    this.selection.set(null);
  }

  protected select(appointment: Appointment): void {
    this.selection.set({ kind: 'appointment', id: appointment.id });
  }

  protected selectBlock(block: CalendarBlock): void {
    this.selection.set({ kind: 'block', block });
  }

  protected closePanel(): void {
    this.selection.set(null);
  }

  protected changed(): void {
    this.selection.set(null);
    this.store.reload();
  }

  protected openFree(tap: SlotTap): Promise<void> {
    return this.openCreate(tap.startUtc, tap.lane.column.staffId);
  }

  /** The "New" button, for those who do not tap into the grid: the next free quarter hour of the shown day, for the first person. */
  protected openNew(): Promise<void> {
    const zone = this.store.zone();
    const date = this.store.days()[0];
    const slot = this.store.slotMinutes();
    const nowMinute = minutesOfDay(this.store.now(), date, zone);
    const minute = Math.max(snap(nowMinute + slot - 1, slot), this.window().startMinute);
    const staffId = (this.store.focused() ?? this.store.people().at(0))?.staffId;
    return staffId
      ? this.openCreate(instantAtMinutes(date, minute, zone), staffId)
      : Promise.resolve();
  }

  protected async moved(drop: MoveDrop): Promise<void> {
    try {
      await this.service.move(drop.appointment.id, {
        startUtc: drop.startUtc,
        staffId: drop.staffId,
        reason: null,
      });
      this.toast.show('calendar.moved');
    } catch (error) {
      if (error instanceof ApiError) {
        this.errors.show(error);
      }
    }
    this.store.reload();
  }

  protected rejected(rejection: MoveRejected): void {
    this.toast.show(`calendar.conflict.${rejection.reason}`);
  }

  protected touchStart(event: TouchEvent): void {
    const touch = event.touches[0];
    this.swipeStart = touch ? { x: touch.clientX, y: touch.clientY } : null;
  }

  /** On a phone, a horizontal swipe in the day view moves to the next or previous person. */
  protected touchEnd(event: TouchEvent): void {
    const start = this.swipeStart;
    const touch = event.changedTouches[0];
    this.swipeStart = null;
    if (!start || !touch || !this.singlePerson()) {
      return;
    }
    const dx = touch.clientX - start.x;
    if (Math.abs(dx) >= SWIPE_DISTANCE && Math.abs(dx) > 2 * Math.abs(touch.clientY - start.y)) {
      this.store.stepPerson(dx < 0 ? 1 : -1);
    }
  }

  private async openCreate(startUtc: string, staffId: string): Promise<void> {
    const data: QuickCreateData = {
      startUtc,
      staffId,
      people: this.people(),
      zone: this.store.zone(),
      slotMinutes: this.store.slotMinutes(),
    };
    const saved = await firstValueFrom(
      this.dialog
        .open<QuickCreateDialog, QuickCreateData, boolean>(QuickCreateDialog, {
          data,
          width: '520px',
          maxWidth: '95vw',
        })
        .afterClosed(),
    );
    if (saved) {
      this.store.reload();
    }
  }
}
