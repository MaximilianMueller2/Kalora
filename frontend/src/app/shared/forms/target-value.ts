/** The value of the form control an event came from (input, select, textarea). */
export function targetValue(event: Event): string {
  return (event.target as HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement).value;
}
