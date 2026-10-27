/** A cell of the month grid: a day, or an empty cell before the first / after the last day of the month. */
export type CalendarCell = { date: string; day: number } | null;

const pad = (value: number) => String(value).padStart(2, '0');

export function monthOf(date: string): string {
  return date.slice(0, 7);
}

export function addMonths(month: string, delta: number): string {
  const [year, number] = month.split('-').map(Number);
  const index = year * 12 + (number - 1) + delta;
  return `${Math.floor(index / 12)}-${pad((index % 12) + 1)}`;
}

/** "2026-11" -> weeks of seven cells, Monday first. */
export function monthGrid(month: string): CalendarCell[][] {
  const [year, number] = month.split('-').map(Number);
  const daysInMonth = new Date(Date.UTC(year, number, 0)).getUTCDate();
  // getUTCDay: Sunday = 0; shifting by one makes Monday the first column.
  const offset = (new Date(Date.UTC(year, number - 1, 1)).getUTCDay() + 6) % 7;

  const cells: CalendarCell[] = [
    ...Array.from({ length: offset }, () => null),
    ...Array.from({ length: daysInMonth }, (_, index) => ({
      date: `${month}-${pad(index + 1)}`,
      day: index + 1,
    })),
  ];
  while (cells.length % 7 !== 0) {
    cells.push(null);
  }

  return Array.from({ length: cells.length / 7 }, (_, week) => cells.slice(week * 7, week * 7 + 7));
}

export type DayPart = 'morning' | 'afternoon' | 'evening';

/** Before noon, noon to five, five o'clock on. */
export function dayPartOf(time: string): DayPart {
  const hour = Number(time.slice(0, 2));
  return hour < 12 ? 'morning' : hour < 17 ? 'afternoon' : 'evening';
}

export interface SlotGroup<T> {
  part: DayPart;
  slots: T[];
}

/** Start times grouped into the parts of the day; empty parts are left out. */
export function groupByDayPart<T extends { time: string }>(slots: readonly T[]): SlotGroup<T>[] {
  return (['morning', 'afternoon', 'evening'] as const)
    .map((part) => ({ part, slots: slots.filter((slot) => dayPartOf(slot.time) === part) }))
    .filter((group) => group.slots.length > 0);
}
