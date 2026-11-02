import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  resource,
  signal,
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { LocaleFormatService } from '../../core/i18n/locale-format.service';
import { AdminService } from './admin.service';
import { SALON_STATUSES, SalonStatus } from './admin.models';

const SEARCH_DELAY_MS = 300;

/** All salons with status, plan, team size and the appointments of the month; a search and a filter by status. */
@Component({
  selector: 'app-admin-salons-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, RouterLink, TranslocoPipe],
  templateUrl: './salons-page.html',
  styleUrl: './admin.scss',
})
export class SalonsPage {
  private readonly service = inject(AdminService);
  private readonly format = inject(LocaleFormatService);
  private timer: ReturnType<typeof setTimeout> | undefined;

  protected readonly statuses = SALON_STATUSES;
  protected readonly typed = signal('');
  private readonly search = signal('');
  protected readonly status = signal<SalonStatus | ''>('');

  private readonly list = resource({
    params: () => ({ search: this.search(), status: this.status() }),
    loader: ({ params }) => this.service.list(params.search, params.status),
  });

  protected readonly loading = computed(() => this.list.isLoading() && !this.list.hasValue());
  protected readonly failed = computed(
    () => this.list.status() === 'error' && !this.list.hasValue(),
  );
  protected readonly items = computed(() => (this.list.hasValue() ? this.list.value().items : []));
  protected readonly hasMore = computed(() => this.list.hasValue() && this.list.value().hasMore);

  protected onInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.typed.set(value);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.search.set(value.trim()), SEARCH_DELAY_MS);
  }

  protected onStatus(event: Event): void {
    this.status.set((event.target as HTMLSelectElement).value as SalonStatus | '');
  }

  protected date(iso: string): string {
    return this.format.formatDate(iso, 'date');
  }
}
