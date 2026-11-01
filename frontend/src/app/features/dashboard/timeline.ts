import { minutesOfDay } from '../calendar/calendar-geometry';
import { DashboardPerson, Span } from './dashboard.models';

/** The hours a timeline covers, in minutes since local midnight. */
export interface TimelineWindow {
  startMinute: number;
  endMinute: number;
}

const DEFAULT_WINDOW: TimelineWindow = { startMinute: 8 * 60, endMinute: 18 * 60 };

/** From the first working hour to the last of all people, in whole hours; 08:00 to 18:00 if nobody works today. */
export function timelineWindow(
  people: DashboardPerson[],
  date: string,
  zone: string,
): TimelineWindow {
  const spans = people.flatMap((person) => person.working);
  if (spans.length === 0) {
    return DEFAULT_WINDOW;
  }
  const start = Math.min(...spans.map((span) => minutesOfDay(span.startUtc, date, zone)));
  const end = Math.max(...spans.map((span) => minutesOfDay(span.endUtc, date, zone)));
  return {
    startMinute: Math.max(0, Math.floor(start / 60) * 60),
    endMinute: Math.min(24 * 60, Math.ceil(end / 60) * 60),
  };
}

/** Where a span lies on the timeline, in percent of its width; null when it lies outside. */
export function placeOnTimeline(
  span: Span,
  date: string,
  zone: string,
  window: TimelineWindow,
): { left: number; width: number } | null {
  const total = window.endMinute - window.startMinute;
  const start = Math.max(minutesOfDay(span.startUtc, date, zone), window.startMinute);
  const end = Math.min(minutesOfDay(span.endUtc, date, zone), window.endMinute);
  return end > start
    ? { left: ((start - window.startMinute) / total) * 100, width: ((end - start) / total) * 100 }
    : null;
}

/** The hour marks under the timeline: "08:00", "10:00", ... roughly every two hours. */
export function timelineMarks(window: TimelineWindow): { label: string; left: number }[] {
  const total = window.endMinute - window.startMinute;
  const marks: { label: string; left: number }[] = [];
  for (let minute = window.startMinute; minute <= window.endMinute; minute += 120) {
    marks.push({
      label: `${String(Math.floor(minute / 60)).padStart(2, '0')}:00`,
      left: ((minute - window.startMinute) / total) * 100,
    });
  }
  return marks;
}
