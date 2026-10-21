import { Pipe, PipeTransform, inject } from '@angular/core';
import { DateStyle, LocaleFormatService } from '../../core/i18n/locale-format.service';

/** `{{ appointment.startUtc | localDate: 'dateTime' : salon.timeZone }}`. Impure because the language can change at runtime. */
@Pipe({ name: 'localDate', pure: false })
export class LocalDatePipe implements PipeTransform {
  private readonly format = inject(LocaleFormatService);

  transform(
    value: Date | string | number | null | undefined,
    style: DateStyle = 'date',
    timeZone?: string,
  ): string {
    return value === null || value === undefined
      ? ''
      : this.format.formatDate(value, style, timeZone);
  }
}
