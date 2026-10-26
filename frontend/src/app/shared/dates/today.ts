/** Today's calendar day ("yyyy-MM-dd") in the given IANA time zone, e.g. the salon's. */
export function todayInZone(timeZone: string, now: Date = new Date()): string {
  // The "en-CA" locale writes dates as yyyy-MM-dd.
  return new Intl.DateTimeFormat('en-CA', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(now);
}
