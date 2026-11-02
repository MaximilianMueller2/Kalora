import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  resource,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { ApiError } from '../../core/http/api-error';
import { LocaleFormatService } from '../../core/i18n/locale-format.service';
import { Toast } from '../../core/ui/toast';
import { injectErrorMessage } from '../../shared/forms/server-error';
import { AuthService } from '../auth/auth.service';
import { CustomersService } from './customers.service';
import { DeleteCustomerDialog } from './delete-customer-dialog';

/** One customer: contact data, the history of appointments with the notes, export as JSON or CSV, and "delete customer" (anonymize). */
@Component({
  selector: 'app-customer-detail-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, RouterLink, TranslocoPipe],
  templateUrl: './customer-detail-page.html',
  styleUrl: './customer-detail-page.scss',
})
export class CustomerDetailPage {
  private readonly service = inject(CustomersService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly toast = inject(Toast);
  private readonly format = inject(LocaleFormatService);
  private readonly auth = inject(AuthService);

  /** From the route: `/app/customers/:id`. */
  readonly id = input.required<string>();

  protected readonly customer = resource({
    params: () => this.id(),
    loader: ({ params }) => this.service.get(params),
  });

  protected readonly busy = signal(false);
  protected readonly errorCode = signal<string | null>(null);
  protected readonly error = injectErrorMessage(this.errorCode);

  protected readonly loading = computed(
    () => this.customer.isLoading() && !this.customer.hasValue(),
  );
  protected readonly failed = computed(
    () => this.customer.status() === 'error' && !this.customer.hasValue(),
  );
  protected readonly detail = computed(() =>
    this.customer.hasValue() ? this.customer.value() : null,
  );
  protected readonly json = computed(() => this.service.exportUrl(this.id(), 'json'));
  protected readonly csv = computed(() => this.service.exportUrl(this.id(), 'csv'));
  private readonly zone = computed(() => this.auth.currentUser()?.salon?.timeZone ?? 'UTC');

  protected when(iso: string): string {
    return `${this.format.formatDate(iso, 'fullDate', this.zone())}, ${this.format.formatDate(iso, 'time', this.zone())}`;
  }

  protected date(iso: string): string {
    return this.format.formatDate(iso, 'date', this.zone());
  }

  protected async remove(): Promise<void> {
    const confirmed = await firstValueFrom(
      this.dialog
        .open<DeleteCustomerDialog, undefined, boolean>(DeleteCustomerDialog, {
          width: '480px',
          maxWidth: '95vw',
        })
        .afterClosed(),
    );
    if (!confirmed) {
      return;
    }

    this.busy.set(true);
    this.errorCode.set(null);
    try {
      await this.service.anonymize(this.id());
      this.toast.show('customers.deleted');
      await this.router.navigate(['/app/customers']);
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    } finally {
      this.busy.set(false);
    }
  }
}
