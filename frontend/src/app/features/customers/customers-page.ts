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
import { AuthService } from '../auth/auth.service';
import { CustomersService } from './customers.service';

const SEARCH_DELAY_MS = 300;

/** The customer file: a search box and the customers, each leading to their page. */
@Component({
  selector: 'app-customers-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, RouterLink, TranslocoPipe],
  templateUrl: './customers-page.html',
  styleUrl: './customers-page.scss',
})
export class CustomersPage {
  private readonly service = inject(CustomersService);
  private readonly format = inject(LocaleFormatService);
  private readonly auth = inject(AuthService);
  private timer: ReturnType<typeof setTimeout> | undefined;

  /** What is typed; the list follows after a short pause. */
  protected readonly typed = signal('');
  private readonly search = signal('');

  private readonly list = resource({
    params: () => this.search(),
    loader: ({ params }) => this.service.list(params),
  });

  protected readonly loading = computed(() => this.list.isLoading() && !this.list.hasValue());
  protected readonly failed = computed(
    () => this.list.status() === 'error' && !this.list.hasValue(),
  );
  protected readonly items = computed(() => (this.list.hasValue() ? this.list.value().items : []));
  protected readonly hasMore = computed(() => this.list.hasValue() && this.list.value().hasMore);
  protected readonly searching = computed(() => this.search().trim().length > 0);
  private readonly zone = computed(() => this.auth.currentUser()?.salon?.timeZone ?? 'UTC');

  protected onInput(event: Event): void {
    this.type((event.target as HTMLInputElement).value);
  }

  protected type(value: string): void {
    this.typed.set(value);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.search.set(value.trim()), SEARCH_DELAY_MS);
  }

  protected lastVisit(iso: string | null): string {
    return iso ? this.format.formatDate(iso, 'date', this.zone()) : '';
  }
}
