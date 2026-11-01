import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { LocaleFormatService } from '../../core/i18n/locale-format.service';
import { Toast } from '../../core/ui/toast';
import { DeclineDialog } from '../requests/decline-dialog';
import { OpenRequestsBadge } from '../requests/open-requests-badge';
import { Appointment } from '../requests/requests.models';
import { RequestsService } from '../requests/requests.service';

/** The open requests with "Accept" and "Decline" right here; the whole inbox is one tap away. */
@Component({
  selector: 'app-requests-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, RouterLink, TranslocoPipe],
  templateUrl: './requests-card.html',
  styleUrl: './requests-card.scss',
})
export class RequestsCard {
  private readonly requests = inject(RequestsService);
  private readonly badge = inject(OpenRequestsBadge);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(Toast);
  private readonly format = inject(LocaleFormatService);

  readonly count = input.required<number>();
  readonly items = input.required<Appointment[]>();
  readonly zone = input.required<string>();
  /** The dashboard loads again after an answer. */
  readonly answered = output<void>();

  protected readonly busy = signal<string | null>(null);
  protected readonly more = computed(() => Math.max(this.count() - this.items().length, 0));
  protected readonly rows = computed(() =>
    this.items().map((request) => ({
      request,
      when: `${this.format.formatDate(request.startUtc, 'weekdayDate', this.zone())}, ${this.format.formatDate(request.startUtc, 'time', this.zone())}`,
    })),
  );

  protected confirm(request: Appointment): Promise<void> {
    return this.answer(request, 'dashboard.requests.confirmed', () =>
      this.requests.confirm(request.id),
    );
  }

  protected async decline(request: Appointment): Promise<void> {
    const reason = await firstValueFrom(
      this.dialog
        .open<DeclineDialog, undefined, string>(DeclineDialog, { width: '480px', maxWidth: '95vw' })
        .afterClosed(),
    );
    if (reason !== undefined) {
      await this.answer(request, 'dashboard.requests.declined', () =>
        this.requests.decline(request.id, reason || null),
      );
    }
  }

  private async answer(
    request: Appointment,
    toastKey: string,
    send: () => Promise<unknown>,
  ): Promise<void> {
    this.busy.set(request.id);
    try {
      await send();
      this.toast.show(toastKey);
    } catch {
      // The error snackbar has said why; the list is loaded again so it shows the real state.
    } finally {
      this.busy.set(null);
    }
    this.answered.emit();
    await this.badge.refresh();
  }
}
