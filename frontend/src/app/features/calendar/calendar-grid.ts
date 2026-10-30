import { CdkDrag, CdkDragEnd, CdkDragMove } from '@angular/cdk/drag-drop';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  input,
  output,
  signal,
  viewChildren,
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { LocaleFormatService } from '../../core/i18n/locale-format.service';
import { Appointment } from '../requests/requests.models';
import { ConflictReason, findConflict } from './calendar-conflicts';
import {
  DayWindow,
  PX_PER_MINUTE,
  Rect,
  instantAtMinutes,
  minutesAtPixel,
  minutesOfDay,
  nowTop,
  offRects,
  rectOf,
  snap,
  windowHeight,
} from './calendar-geometry';
import { CalendarBlock } from './calendar.models';
import { Lane } from './calendar.store';

/** A place in the grid that was tapped. */
export interface SlotTap {
  lane: Lane;
  startUtc: string;
}

export interface MoveDrop {
  appointment: Appointment;
  staffId: string;
  startUtc: string;
}

export interface MoveRejected {
  appointment: Appointment;
  reason: ConflictReason;
}

interface Ghost {
  laneKey: string;
  rect: Rect;
  conflict: ConflictReason | null;
}

const MIN_CARD_HEIGHT = 20;
const TOUCH_DRAG_DELAY_MS = 350;
const CLICK_AFTER_DRAG_MS = 250;

/**
 * The time grid: an axis on the left, one lane per column. Pure presentation: it positions what it is given, reports
 * taps and drops, and shows red where a dragged appointment would not fit. The decisions are made by the page.
 */
@Component({
  selector: 'app-calendar-grid',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CdkDrag, MatIconModule, TranslocoPipe],
  templateUrl: './calendar-grid.html',
  styleUrl: './calendar-grid.scss',
})
export class CalendarGrid {
  private readonly format = inject(LocaleFormatService);

  readonly lanes = input.required<Lane[]>();
  readonly window = input.required<DayWindow>();
  readonly zone = input.required<string>();
  readonly slotMinutes = input(15);
  readonly now = input.required<number>();
  /** Lanes are days of one person (week view) instead of people on one day. */
  readonly byDay = input(false);
  readonly selectedId = input<string | null>(null);

  readonly slotTap = output<SlotTap>();
  readonly appointmentTap = output<Appointment>();
  readonly blockTap = output<CalendarBlock>();
  readonly moved = output<MoveDrop>();
  readonly rejected = output<MoveRejected>();

  protected readonly touchDelay = { touch: TOUCH_DRAG_DELAY_MS, mouse: 0 };
  protected readonly ghost = signal<Ghost | null>(null);
  private readonly laneElements = viewChildren<ElementRef<HTMLElement>>('laneBody');

  protected readonly height = computed(() => windowHeight(this.window()));
  protected readonly hours = computed(() => {
    const { startMinute, endMinute } = this.window();
    const hours: { label: string; top: number }[] = [];
    for (let minute = Math.ceil(startMinute / 60) * 60; minute < endMinute; minute += 60) {
      hours.push({
        label: `${String(Math.floor(minute / 60)).padStart(2, '0')}:00`,
        top: (minute - startMinute) * PX_PER_MINUTE,
      });
    }
    return hours;
  });

  protected readonly columns = computed(() => {
    const window = this.window();
    const zone = this.zone();
    const now = this.now();
    return this.lanes().map((lane) => ({
      lane,
      off: offRects(lane.column.working, lane.date, zone, window),
      absences: lane.column.absences
        .map((absence) => ({ absence, rect: rectOf(absence, lane.date, zone, window) }))
        .filter((item): item is { absence: typeof item.absence; rect: Rect } => item.rect !== null),
      blocks: lane.column.blocks
        .map((block) => ({ block, rect: rectOf(block, lane.date, zone, window) }))
        .filter((item): item is { block: CalendarBlock; rect: Rect } => item.rect !== null),
      appointments: lane.column.appointments
        .map((appointment) => ({
          appointment,
          rect: rectOf(appointment, lane.date, zone, window),
          movable: this.movable(appointment, now),
        }))
        .filter(
          (item): item is { appointment: Appointment; rect: Rect; movable: boolean } =>
            item.rect !== null,
        ),
      nowTop: nowTop(now, lane.date, zone, window),
    }));
  });

  protected laneTitle(lane: Lane): string {
    return this.byDay()
      ? this.format.formatDate(`${lane.date}T12:00:00Z`, 'weekdayDay', 'UTC')
      : lane.column.displayName;
  }

  protected timeRange(appointment: Appointment): string {
    const zone = this.zone();
    return `${this.format.formatDate(appointment.startUtc, 'time', zone)} – ${this.format.formatDate(appointment.endUtc, 'time', zone)}`;
  }

  protected cardHeight(rect: Rect): number {
    return Math.max(rect.height, MIN_CARD_HEIGHT);
  }

  /** Letting go of a dragged card can end in a click; it is not a tap. */
  private lastDragEnd = 0;

  protected cardTapped(appointment: Appointment): void {
    if (!this.justDragged()) {
      this.appointmentTap.emit(appointment);
    }
  }

  protected blockTapped(block: CalendarBlock): void {
    if (!this.justDragged()) {
      this.blockTap.emit(block);
    }
  }

  private justDragged(): boolean {
    return Date.now() - this.lastDragEnd < CLICK_AFTER_DRAG_MS;
  }

  protected tapped(event: MouseEvent, lane: Lane): void {
    if (this.justDragged()) {
      return;
    }
    const minute = snap(minutesAtPixel(event.offsetY, this.window()), this.slotMinutes());
    this.slotTap.emit({ lane, startUtc: instantAtMinutes(lane.date, minute, this.zone()) });
  }

  protected dragMoved(event: CdkDragMove, appointment: Appointment, source: Lane): void {
    const target = this.target(event.distance, appointment, source);
    this.ghost.set(
      target
        ? {
            laneKey: target.lane.key,
            rect: {
              top: (target.minute - this.window().startMinute) * PX_PER_MINUTE,
              height: Math.max(
                ((Date.parse(appointment.blockedEndUtc) - Date.parse(appointment.blockedStartUtc)) /
                  60_000) *
                  PX_PER_MINUTE,
                MIN_CARD_HEIGHT,
              ),
            },
            conflict: findConflict(target.lane.column, appointment, target.startUtc, this.now()),
          }
        : null,
    );
  }

  protected dragEnded(event: CdkDragEnd, appointment: Appointment, source: Lane): void {
    this.lastDragEnd = Date.now();
    const target = this.target(event.distance, appointment, source);
    const ghost = this.ghost();
    this.ghost.set(null);
    event.source.reset();
    if (!target) {
      return;
    }
    if (
      target.lane.column.staffId === source.column.staffId &&
      target.startUtc === new Date(appointment.startUtc).toISOString()
    ) {
      return;
    }
    if (ghost?.conflict) {
      this.rejected.emit({ appointment, reason: ghost.conflict });
      return;
    }
    this.moved.emit({
      appointment,
      staffId: target.lane.column.staffId,
      startUtc: target.startUtc,
    });
  }

  /** Where the dragged card would land: the lane under it and the snapped start. */
  private target(distance: { x: number; y: number }, appointment: Appointment, source: Lane) {
    const elements = this.laneElements().map((element) => element.nativeElement);
    const lanes = this.lanes();
    const sourceIndex = lanes.findIndex((lane) => lane.key === source.key);
    const sourceRect = elements[sourceIndex]?.getBoundingClientRect();
    if (!sourceRect) {
      return null;
    }

    const pointerX = sourceRect.left + sourceRect.width / 2 + distance.x;
    let index = elements.findIndex((element) => {
      const rect = element.getBoundingClientRect();
      return pointerX >= rect.left && pointerX < rect.right;
    });
    if (index < 0) {
      index = pointerX < sourceRect.left ? 0 : elements.length - 1;
    }

    const lane = lanes[index];
    const window = this.window();
    const startMinute = minutesOfDay(appointment.startUtc, source.date, this.zone());
    const latest = window.endMinute - 15;
    const minute = Math.min(
      Math.max(
        snap(startMinute + distance.y / PX_PER_MINUTE, this.slotMinutes()),
        window.startMinute,
      ),
      latest,
    );
    return { lane, minute, startUtc: instantAtMinutes(lane.date, minute, this.zone()) };
  }

  private movable(appointment: Appointment, now: number): boolean {
    return appointment.status === 'Confirmed' && Date.parse(appointment.endUtc) > now;
  }
}
