import { Appointment } from '../requests/requests.models';
import { CalendarColumn, CalendarSpan } from './calendar.models';

/** Why a place in the calendar cannot take an appointment; the server decides in the end, this shows it before dropping. */
export type ConflictReason = 'past' | 'closed' | 'absence' | 'block' | 'appointment';

const overlaps = (a: { start: number; end: number }, b: { start: number; end: number }) =>
  a.start < b.end && a.end > b.start;

const range = (span: CalendarSpan) => ({
  start: Date.parse(span.startUtc),
  end: Date.parse(span.endUtc),
});

/**
 * Checks whether `appointment` fits in `column` if it started at `startUtc`: in the past, outside the working time,
 * in an absence, on a blocker or on another appointment (buffers included, the appointment's own place excluded).
 * The first problem is returned in that order, null if it fits.
 */
export function findConflict(
  column: CalendarColumn,
  appointment: Appointment,
  startUtc: string,
  now: number,
): ConflictReason | null {
  const start = Date.parse(startUtc);
  const duration = Date.parse(appointment.endUtc) - Date.parse(appointment.startUtc);
  const before = Date.parse(appointment.startUtc) - Date.parse(appointment.blockedStartUtc);
  const after = Date.parse(appointment.blockedEndUtc) - Date.parse(appointment.endUtc);
  const taken = { start: start - before, end: start + duration + after };

  if (start < now) {
    return 'past';
  }
  if (
    !column.working.some((span) => range(span).start <= taken.start && range(span).end >= taken.end)
  ) {
    return 'closed';
  }
  if (column.absences.some((absence) => overlaps(taken, range(absence)))) {
    return 'absence';
  }
  if (column.blocks.some((block) => overlaps(taken, range(block)))) {
    return 'block';
  }
  const blocking = column.appointments.filter(
    (other) =>
      other.id !== appointment.id && (other.status === 'Requested' || other.status === 'Confirmed'),
  );
  if (
    blocking.some((other) =>
      overlaps(taken, range({ startUtc: other.blockedStartUtc, endUtc: other.blockedEndUtc })),
    )
  ) {
    return 'appointment';
  }
  return null;
}
