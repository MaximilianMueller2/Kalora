import { Pipe, PipeTransform, inject } from '@angular/core';
import { LocaleFormatService } from '../../core/i18n/locale-format.service';

/** `{{ service.priceCents | money: service.currency }}` */
@Pipe({ name: 'money', pure: false })
export class MoneyPipe implements PipeTransform {
  private readonly format = inject(LocaleFormatService);

  transform(cents: number | null | undefined, currency = 'EUR'): string {
    return cents === null || cents === undefined ? '' : this.format.formatMoney(cents, currency);
  }
}
