export const WEEKDAYS = [
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
  'Sunday',
] as const;
export type Weekday = (typeof WEEKDAYS)[number];

/** A span of the day in local time, as "HH:mm" (the value format of an HTML time input and of the API). */
export interface TimeBlock {
  start: string;
  end: string;
}

/** Working or opening hours of a week; a weekday without blocks is a day off. Used for the salon and for staff. */
export type WeekSchedule = Record<Weekday, TimeBlock[]>;

export type DayProblem = 'block_invalid' | 'overlap' | 'too_many_blocks';

export const MAX_BLOCKS_PER_DAY = 4;
export const DEFAULT_BLOCK: TimeBlock = { start: '09:00', end: '18:00' };

export function emptySchedule(): WeekSchedule {
  return Object.fromEntries(WEEKDAYS.map((day) => [day, []])) as unknown as WeekSchedule;
}

export function toMinutes(time: string): number {
  const [hours, minutes] = time.split(':').map(Number);
  return hours * 60 + minutes;
}

function fromMinutes(total: number): string {
  const clamped = Math.min(Math.max(total, 0), 23 * 60 + 59);
  return `${String(Math.floor(clamped / 60)).padStart(2, '0')}:${String(clamped % 60).padStart(2, '0')}`;
}

/** The same rules the API enforces: each block ends after it starts, blocks may touch but not overlap. */
export function dayProblem(blocks: readonly TimeBlock[]): DayProblem | null {
  if (blocks.length > MAX_BLOCKS_PER_DAY) {
    return 'too_many_blocks';
  }
  if (
    blocks.some(
      (block) => !block.start || !block.end || toMinutes(block.end) <= toMinutes(block.start),
    )
  ) {
    return 'block_invalid';
  }
  const ordered = [...blocks].sort((a, b) => toMinutes(a.start) - toMinutes(b.start));
  for (let index = 1; index < ordered.length; index++) {
    if (toMinutes(ordered[index].start) < toMinutes(ordered[index - 1].end)) {
      return 'overlap';
    }
  }
  return null;
}

export function scheduleProblems(schedule: WeekSchedule): Partial<Record<Weekday, DayProblem>> {
  const problems: Partial<Record<Weekday, DayProblem>> = {};
  for (const day of WEEKDAYS) {
    const problem = dayProblem(schedule[day]);
    if (problem) {
      problems[day] = problem;
    }
  }
  return problems;
}

/** What a new block for the day should look like: the usual hours, or the two hours after the last block. */
export function nextBlock(blocks: readonly TimeBlock[]): TimeBlock | null {
  const last = blocks.at(-1);
  if (!last) {
    return { ...DEFAULT_BLOCK };
  }
  const start = toMinutes(last.end);
  return start >= 23 * 60 ? null : { start: last.end, end: fromMinutes(start + 120) };
}

export function previousDay(day: Weekday): Weekday | null {
  const index = WEEKDAYS.indexOf(day);
  return index > 0 ? WEEKDAYS[index - 1] : null;
}

/** "Same as the day before": copies the previous weekday's blocks (empty if that day is off). */
export function copyFromPrevious(schedule: WeekSchedule, day: Weekday): WeekSchedule {
  const previous = previousDay(day);
  return previous
    ? { ...schedule, [day]: schedule[previous].map((block) => ({ ...block })) }
    : schedule;
}

export function setDayOpen(schedule: WeekSchedule, day: Weekday, open: boolean): WeekSchedule {
  return { ...schedule, [day]: open ? [{ ...DEFAULT_BLOCK }] : [] };
}

export function updateBlock(
  schedule: WeekSchedule,
  day: Weekday,
  index: number,
  change: Partial<TimeBlock>,
): WeekSchedule {
  return {
    ...schedule,
    [day]: schedule[day].map((block, i) => (i === index ? { ...block, ...change } : block)),
  };
}

export function addBlock(schedule: WeekSchedule, day: Weekday): WeekSchedule {
  const block = nextBlock(schedule[day]);
  return block && schedule[day].length < MAX_BLOCKS_PER_DAY
    ? { ...schedule, [day]: [...schedule[day], block] }
    : schedule;
}

export function removeBlock(schedule: WeekSchedule, day: Weekday, index: number): WeekSchedule {
  return { ...schedule, [day]: schedule[day].filter((_, i) => i !== index) };
}
