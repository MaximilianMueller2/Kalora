import { CdkDragEnd, CdkDragMove } from '@angular/cdk/drag-drop';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { provideTestI18n } from '../../../testing/test-i18n';
import { LanguageService } from '../../core/i18n/language.service';
import { appointment, column, DAY } from './calendar-fixtures';
import { CalendarGrid, MoveDrop, MoveRejected } from './calendar-grid';
import { PX_PER_MINUTE } from './calendar-geometry';
import { Appointment } from '../requests/requests.models';
import { Lane } from './calendar.store';
import { ANNA_COLUMN, BEN_COLUMN } from './calendar-fixtures';

const WINDOW = { startMinute: 8 * 60, endMinute: 18 * 60 };
const NOW = Date.parse('2026-11-03T09:00:00Z'); // the day before

const lane = (col = ANNA_COLUMN, date = DAY, isToday = false): Lane => ({
  key: `${col.staffId}-${date}`,
  date,
  column: col,
  isToday,
});

async function mount(lanes: Lane[], extra: { now?: number; byDay?: boolean } = {}) {
  localStorage.clear();
  TestBed.configureTestingModule({ providers: [provideTestI18n()] });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(CalendarGrid);
  fixture.componentRef.setInput('lanes', lanes);
  fixture.componentRef.setInput('window', WINDOW);
  fixture.componentRef.setInput('zone', 'Europe/Berlin');
  fixture.componentRef.setInput('now', extra.now ?? NOW);
  fixture.componentRef.setInput('byDay', extra.byDay ?? false);
  fixture.detectChanges();
  await fixture.whenStable();
  // jsdom has no layout: give every lane a 200 px wide box side by side.
  fixture.nativeElement.querySelectorAll('.lane').forEach((element: HTMLElement, index: number) => {
    element.getBoundingClientRect = () => ({
      left: index * 200,
      right: index * 200 + 200,
      top: 0,
      bottom: 720,
      width: 200,
      height: 720,
      x: index * 200,
      y: 0,
      toJSON: () => ({}),
    });
  });
  return fixture;
}

const q = (fixture: ComponentFixture<unknown>, selector: string) =>
  fixture.nativeElement.querySelector(selector) as HTMLElement | null;
const qa = (fixture: ComponentFixture<unknown>, selector: string) =>
  Array.from<HTMLElement>(fixture.nativeElement.querySelectorAll(selector));
const style = (element: HTMLElement | null, property: string) =>
  element?.style.getPropertyValue(property);

/** The private drag handlers, called the way the CDK calls them. */
interface Drag {
  dragMoved(event: CdkDragMove, appointment: Appointment, lane: Lane): void;
  dragEnded(event: CdkDragEnd, appointment: Appointment, lane: Lane): void;
  tapped(event: MouseEvent, lane: Lane): void;
}
const drag = (fixture: ComponentFixture<CalendarGrid>) =>
  fixture.componentInstance as unknown as Drag;
const moveEvent = (x: number, y: number) => ({ distance: { x, y } }) as CdkDragMove;
const endEvent = (x: number, y: number) =>
  ({ distance: { x, y }, source: { reset: vi.fn() } }) as unknown as CdkDragEnd;

describe('CalendarGrid', () => {
  it('draws a header and a lane for every column, with the person in the person color', async () => {
    const fixture = await mount([lane(ANNA_COLUMN), lane(BEN_COLUMN)]);

    expect(qa(fixture, '.head').map((h) => h.textContent?.trim())).toEqual(['Anna', 'Ben']);
    expect(qa(fixture, '.lane').map((l) => style(l, '--staff'))).toEqual(['#2563EB', '#DC2626']);
  });

  it('names the days in the week view', async () => {
    const days = ['2026-11-04', '2026-11-05'].map((date) => lane(ANNA_COLUMN, date));
    const fixture = await mount(days, { byDay: true });

    expect(qa(fixture, '.head').map((h) => h.textContent?.replace(/\s+/g, ' ').trim())).toEqual([
      'Mi., 4.',
      'Do., 5.',
    ]);
    expect(q(fixture, '.head .dot')).toBeNull();
  });

  it('labels the hours of the window and sizes the day by it', async () => {
    const fixture = await mount([lane()]);

    const labels = qa(fixture, '.hour').map((h) => h.textContent?.trim());
    expect(labels[0]).toBe('08:00');
    expect(labels.at(-1)).toBe('17:00');
    expect(labels).toHaveLength(10);
    expect(q(fixture, '.body')?.style.height).toBe('720px');
  });

  it('places appointments, blockers and absences by their local time', async () => {
    const fixture = await mount([lane()]);

    const [first, second] = qa(fixture, '.event');
    expect(first.style.top).toBe(`${120 * PX_PER_MINUTE}px`); // 10:00 local = 120 min after 08:00
    expect(first.style.height).toBe(`${60 * PX_PER_MINUTE}px`);
    expect(second.style.top).toBe(`${240 * PX_PER_MINUTE}px`); // 12:00 local
    expect(q(fixture, '.block')?.style.top).toBe(`${300 * PX_PER_MINUTE}px`); // 13:00 local
    expect(q(fixture, '.block')?.textContent).toContain('Pause');
    expect(q(fixture, '.absence')?.style.top).toBe(`${420 * PX_PER_MINUTE}px`); // 15:00 local
    expect(q(fixture, '.absence')?.textContent).toContain('calendar.absence.Private');
  });

  it('greys out the time outside working hours', async () => {
    const fixture = await mount([lane()]);

    const off = qa(fixture, '.off');
    expect(off).toHaveLength(2);
    expect(off[0].style.height).toBe(`${60 * PX_PER_MINUTE}px`); // 08:00-09:00
    expect(off[1].style.top).toBe(`${540 * PX_PER_MINUTE}px`); // from 17:00
  });

  it('draws requests dashed and finished appointments light, and shows who and what', async () => {
    const finished = column('s1', 'Anna', {
      appointments: [appointment('f', 9, 10, { status: 'NoShow' })],
    });
    const fixture = await mount([lane(ANNA_COLUMN), lane(finished)]);

    expect(
      qa(fixture, '.event').map((e) => [
        e.classList.contains('request'),
        e.classList.contains('finished'),
      ]),
    ).toEqual([
      [false, false],
      [true, false],
      [false, true],
    ]);
    expect(q(fixture, '.event .what')?.textContent).toBe('Kim Kundin');
    expect(q(fixture, '.event .service')?.textContent).toBe('Haarschnitt');
    expect(q(fixture, '.event .time')?.textContent?.replace(/\s+/g, ' ')).toBe('10:00 – 11:00');
  });

  it('marks the selected appointment', async () => {
    const fixture = await mount([lane()]);
    fixture.componentRef.setInput('selectedId', 'a1');
    fixture.detectChanges();

    expect(qa(fixture, '.event').map((e) => e.getAttribute('aria-pressed'))).toEqual([
      'true',
      'false',
    ]);
  });

  it('draws the now line on today only', async () => {
    const today = await mount([lane(ANNA_COLUMN, DAY, true)], {
      now: Date.parse('2026-11-04T09:30:00Z'),
    });
    expect(q(today, '.now')?.style.top).toBe(`${150 * PX_PER_MINUTE}px`); // 10:30 local
    TestBed.resetTestingModule();

    const other = await mount([lane()], { now: Date.parse('2026-11-05T09:30:00Z') });
    expect(q(other, '.now')).toBeNull();
  });

  it('reports a tap on free space as a snapped start in the lane', async () => {
    const fixture = await mount([lane()]);
    const taps: unknown[] = [];
    fixture.componentInstance.slotTap.subscribe((tap) => taps.push(tap));

    drag(fixture).tapped({ offsetY: 100 } as MouseEvent, lane()); // 100 px = 83 min after 08:00 -> 09:30 (snapped to 15)

    expect(taps).toEqual([{ lane: lane(), startUtc: '2026-11-04T08:30:00.000Z' }]);
  });

  it('opens an appointment and a blocker by their buttons', async () => {
    const fixture = await mount([lane()]);
    const opened: string[] = [];
    fixture.componentInstance.appointmentTap.subscribe((a) => opened.push(a.id));
    fixture.componentInstance.blockTap.subscribe((b) => opened.push(b.id));

    q(fixture, '.event')?.click();
    q(fixture, '.block')?.click();

    expect(opened).toEqual(['a1', 'b1']);
  });

  it('only lets confirmed appointments that are not over be dragged', async () => {
    const fixture = await mount([lane()]);

    const disabled = qa(fixture, '.event').map((e) => e.classList.contains('cdk-drag-disabled'));
    expect(disabled).toEqual([false, true]); // the second one is a request
  });

  describe('dragging', () => {
    const a1 = ANNA_COLUMN.appointments[0];

    it('shows where the card would land and that it fits', async () => {
      const fixture = await mount([lane(ANNA_COLUMN), lane(BEN_COLUMN)]);
      const source = lane(ANNA_COLUMN);

      drag(fixture).dragMoved(moveEvent(0, 4 * 72), a1, source); // four hours down: 10:00 -> 14:00 local, between the break and the absence
      fixture.detectChanges();

      const ghost = q(fixture, '.ghost');
      expect(ghost).not.toBeNull();
      expect(ghost?.style.top).toBe(`${360 * PX_PER_MINUTE}px`); // 14:00 local = 360 min after 08:00
      expect(ghost?.classList.contains('conflict')).toBe(false);
    });

    it('turns red and names the problem when the place is taken', async () => {
      const fixture = await mount([lane(ANNA_COLUMN)]);

      // 12:00 local is where Anna's request sits.
      drag(fixture).dragMoved(moveEvent(0, 2 * 72), a1, lane(ANNA_COLUMN));
      fixture.detectChanges();

      expect(q(fixture, '.ghost')?.classList.contains('conflict')).toBe(true);
      expect(q(fixture, '.ghost')?.textContent).toContain('calendar.conflict.appointment');
    });

    it('names the other problems too', async () => {
      const fixture = await mount([lane(ANNA_COLUMN)]);
      const reasons: Record<string, number> = {
        'calendar.conflict.block': 3 * 72, // 13:00 local: the break
        'calendar.conflict.absence': 5 * 72, // 15:00 local: the absence
        'calendar.conflict.closed': -2 * 72, // 08:00 local: before work starts
      };

      for (const [text, distance] of Object.entries(reasons)) {
        drag(fixture).dragMoved(moveEvent(0, distance), a1, lane(ANNA_COLUMN));
        fixture.detectChanges();
        expect(q(fixture, '.ghost')?.textContent).toContain(text);
      }
    });

    it('moves the ghost to the lane under the card', async () => {
      const fixture = await mount([lane(ANNA_COLUMN), lane(BEN_COLUMN)]);

      drag(fixture).dragMoved(moveEvent(200, 3 * 72), a1, lane(ANNA_COLUMN));
      fixture.detectChanges();

      expect(qa(fixture, '.lane')[0].querySelector('.ghost')).toBeNull();
      expect(qa(fixture, '.lane')[1].querySelector('.ghost')).not.toBeNull();
    });

    it('reports a drop on a free place with the person and the snapped start', async () => {
      const fixture = await mount([lane(ANNA_COLUMN), lane(BEN_COLUMN)]);
      const drops: MoveDrop[] = [];
      fixture.componentInstance.moved.subscribe((drop) => drops.push(drop));
      const end = endEvent(200, 4 * 72 + 7); // Ben's column, 14:00 local plus a few pixels

      drag(fixture).dragMoved(end as unknown as CdkDragMove, a1, lane(ANNA_COLUMN));
      drag(fixture).dragEnded(end, a1, lane(ANNA_COLUMN));

      expect(drops).toEqual([
        { appointment: a1, staffId: 's2', startUtc: '2026-11-04T13:00:00.000Z' },
      ]);
      expect(end.source.reset).toHaveBeenCalled();
      expect(q(fixture, '.ghost')).toBeNull();
    });

    it('rejects a drop on a conflict and tells why', async () => {
      const fixture = await mount([lane(ANNA_COLUMN)]);
      const drops: MoveDrop[] = [];
      const rejections: MoveRejected[] = [];
      fixture.componentInstance.moved.subscribe((drop) => drops.push(drop));
      fixture.componentInstance.rejected.subscribe((rejection) => rejections.push(rejection));
      const end = endEvent(0, 2 * 72);

      drag(fixture).dragMoved(end as unknown as CdkDragMove, a1, lane(ANNA_COLUMN));
      drag(fixture).dragEnded(end, a1, lane(ANNA_COLUMN));

      expect(drops).toEqual([]);
      expect(rejections).toEqual([{ appointment: a1, reason: 'appointment' }]);
    });

    it('does not count the click that ends a drag as a tap on the card or the lane', async () => {
      const fixture = await mount([lane(ANNA_COLUMN)]);
      const events: unknown[] = [];
      fixture.componentInstance.appointmentTap.subscribe((a) => events.push(a));
      fixture.componentInstance.slotTap.subscribe((tap) => events.push(tap));
      const end = endEvent(0, 0);

      drag(fixture).dragEnded(end, a1, lane(ANNA_COLUMN));
      q(fixture, '.event')?.click();
      drag(fixture).tapped({ offsetY: 10 } as MouseEvent, lane(ANNA_COLUMN));

      expect(events).toEqual([]);
    });

    it('does nothing when the card is dropped where it was', async () => {
      const fixture = await mount([lane(ANNA_COLUMN)]);
      const events: unknown[] = [];
      fixture.componentInstance.moved.subscribe((drop) => events.push(drop));
      fixture.componentInstance.rejected.subscribe((rejection) => events.push(rejection));
      const end = endEvent(0, 0);

      drag(fixture).dragMoved(end as unknown as CdkDragMove, a1, lane(ANNA_COLUMN));
      drag(fixture).dragEnded(end, a1, lane(ANNA_COLUMN));

      expect(events).toEqual([]);
    });

    it('snaps to the 15-minute grid and stays inside the window', async () => {
      const fixture = await mount([lane(ANNA_COLUMN)]);
      const drops: MoveDrop[] = [];
      fixture.componentInstance.moved.subscribe((drop) => drops.push(drop));
      const free = appointment('free', 9, 10);
      const only = column('s1', 'Anna', { appointments: [free] });
      fixture.componentRef.setInput('lanes', [lane(only)]);
      fixture.detectChanges();

      const end = endEvent(0, 4 * 72 + 6 * PX_PER_MINUTE); // +4 h and 6 min: 14:06 local, snaps to 14:00
      drag(fixture).dragMoved(end as unknown as CdkDragMove, free, lane(only));
      drag(fixture).dragEnded(end, free, lane(only));

      expect(drops[0].startUtc).toBe('2026-11-04T13:00:00.000Z');
    });
  });
});
