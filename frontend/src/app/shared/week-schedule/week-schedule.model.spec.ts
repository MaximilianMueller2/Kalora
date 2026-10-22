import { describe, expect, it } from 'vitest';
import {
  WEEKDAYS,
  WeekSchedule,
  addBlock,
  copyFromPrevious,
  dayProblem,
  emptySchedule,
  nextBlock,
  previousDay,
  removeBlock,
  scheduleProblems,
  setDayOpen,
  updateBlock,
} from './week-schedule.model';

const block = (start: string, end: string) => ({ start, end });

describe('dayProblem', () => {
  it('accepts no blocks, one block and blocks that touch', () => {
    expect(dayProblem([])).toBeNull();
    expect(dayProblem([block('09:00', '18:00')])).toBeNull();
    expect(dayProblem([block('13:00', '18:00'), block('09:00', '13:00')])).toBeNull();
  });

  it.each([
    [[block('18:00', '09:00')], 'block_invalid'],
    [[block('09:00', '09:00')], 'block_invalid'],
    [[block('', '09:00')], 'block_invalid'],
    [[block('09:00', '13:01'), block('13:00', '18:00')], 'overlap'],
    [[block('09:00', '18:00'), block('10:00', '11:00')], 'overlap'],
  ])('finds the problem in %j', (blocks, expected) => {
    expect(dayProblem(blocks)).toBe(expected);
  });

  it('allows at most four blocks', () => {
    const four = ['06', '08', '10', '12'].map((h) => block(`${h}:00`, `${h}:30`));

    expect(dayProblem(four)).toBeNull();
    expect(dayProblem([...four, block('14:00', '15:00')])).toBe('too_many_blocks');
  });
});

describe('schedule operations', () => {
  const monday = (schedule: WeekSchedule) => schedule.Monday;

  it('starts with seven closed days in Monday-first order', () => {
    const schedule = emptySchedule();

    expect(Object.keys(schedule)).toEqual([...WEEKDAYS]);
    expect(Object.values(schedule).every((blocks) => blocks.length === 0)).toBe(true);
  });

  it('opens a day with the usual hours and closes it again', () => {
    const open = setDayOpen(emptySchedule(), 'Monday', true);
    expect(monday(open)).toEqual([block('09:00', '18:00')]);
    expect(monday(setDayOpen(open, 'Monday', false))).toEqual([]);
  });

  it('adds a block after the last one and refuses when there is no room left', () => {
    let schedule = setDayOpen(emptySchedule(), 'Monday', true);
    schedule = addBlock(schedule, 'Monday');
    expect(monday(schedule)).toEqual([block('09:00', '18:00'), block('18:00', '20:00')]);

    const late = updateBlock(setDayOpen(emptySchedule(), 'Monday', true), 'Monday', 0, {
      end: '23:30',
    });
    expect(addBlock(late, 'Monday')).toBe(late);
    expect(nextBlock(late.Monday)).toBeNull();
  });

  it('never exceeds four blocks', () => {
    let schedule = setDayOpen(emptySchedule(), 'Monday', true);
    schedule = updateBlock(schedule, 'Monday', 0, { end: '10:00' });
    for (let index = 0; index < 6; index++) {
      schedule = addBlock(schedule, 'Monday');
    }

    expect(monday(schedule)).toHaveLength(4);
  });

  it('changes and removes single blocks without touching the other days', () => {
    let schedule = setDayOpen(setDayOpen(emptySchedule(), 'Monday', true), 'Tuesday', true);
    schedule = addBlock(schedule, 'Monday');
    schedule = updateBlock(schedule, 'Monday', 1, { start: '19:00' });
    expect(schedule.Monday[1]).toEqual(block('19:00', '20:00'));
    expect(schedule.Tuesday).toEqual([block('09:00', '18:00')]);

    schedule = removeBlock(schedule, 'Monday', 0);
    expect(schedule.Monday).toEqual([block('19:00', '20:00')]);
  });

  it('copies the previous day, including making a day off, and does nothing for Monday', () => {
    let schedule = addBlock(setDayOpen(emptySchedule(), 'Monday', true), 'Monday');
    schedule = copyFromPrevious(schedule, 'Tuesday');
    expect(schedule.Tuesday).toEqual(schedule.Monday);
    expect(schedule.Tuesday[0]).not.toBe(schedule.Monday[0]);

    expect(copyFromPrevious(schedule, 'Wednesday').Wednesday).toEqual(schedule.Tuesday);
    expect(copyFromPrevious(emptySchedule(), 'Tuesday').Tuesday).toEqual([]);
    expect(copyFromPrevious(schedule, 'Monday')).toBe(schedule);
    expect(previousDay('Monday')).toBeNull();
    expect(previousDay('Sunday')).toBe('Saturday');
  });

  it('reports problems per weekday', () => {
    const schedule = updateBlock(setDayOpen(emptySchedule(), 'Friday', true), 'Friday', 0, {
      end: '08:00',
    });

    expect(scheduleProblems(schedule)).toEqual({ Friday: 'block_invalid' });
    expect(scheduleProblems(emptySchedule())).toEqual({});
  });
});
