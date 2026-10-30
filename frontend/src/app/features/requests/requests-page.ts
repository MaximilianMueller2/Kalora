import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  resource,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom, map, timer } from 'rxjs';
import { LocaleFormatService } from '../../core/i18n/locale-format.service';
import { Toast, UndoOffer } from '../../core/ui/toast';
import { AuthService } from '../auth/auth.service';
import { DeclineDialog } from './decline-dialog';
import { OpenRequestsBadge } from './open-requests-badge';
import { Appointment } from './requests.models';
import { RequestsService } from './requests.service';

const FALLBACK_ZONE = 'UTC';
const TICK_MS = 30_000;
const REFRESH_MS = 60_000;
const URGENT_MINUTES = 120;

/**
 * Open booking requests as cards. "Accept" and "Decline" take effect when the undo message has gone away, so a
 * mistaken tap costs nothing; leaving the page makes any pending answer final at once.
 */
@Component({
  selector: 'app-requests-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, TranslocoPipe],
  templateUrl: './requests-page.html',
  styleUrl: './requests-page.scss',
})
export class RequestsPage {
  private readonly requests = inject(RequestsService);
  private readonly badge = inject(OpenRequestsBadge);
  private readonly toast = inject(Toast);
  private readonly dialog = inject(MatDialog);
  private readonly format = inject(LocaleFormatService);
  private readonly auth = inject(AuthService);
  private readonly pending = new Set<UndoOffer>();

  /** From the link in the "new request" mail (/app/requests/:id): the card to bring into view. */
  readonly id = input<string>();

  private readonly list = resource({ loader: () => this.requests.list() });
  /** Ids already answered (or being answered); their cards are gone from the screen even before the server list catches up. */
  private readonly answered = signal<ReadonlySet<string>>(new Set());
  private readonly now = toSignal(timer(TICK_MS, TICK_MS).pipe(map(() => Date.now())), {
    initialValue: Date.now(),
  });
  private readonly zone = computed(() => this.auth.currentUser()?.salon?.timeZone ?? FALLBACK_ZONE);

  protected readonly loading = computed(() => this.list.isLoading() && !this.list.hasValue());
  protected readonly failed = computed(() => this.list.status() === 'error');
  protected readonly cards = computed(() => {
    const answered = this.answered();
    const nowMs = this.now();
    return (this.list.hasValue() ? this.list.value() : [])
      .filter((request) => !answered.has(request.id))
      .map((request) => ({
        request,
        when: this.when(request),
        minutesLeft: this.minutesLeft(request, nowMs),
      }));
  });
  protected readonly missingTarget = computed(() => {
    const target = this.id();
    return !!target && this.list.hasValue() && !this.list.value().some((r) => r.id === target);
  });
  protected readonly urgentMinutes = URGENT_MINUTES;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.pending.forEach((offer) => offer.dismiss()));
    // Pick up requests that arrived in the meantime, like the badge does.
    timer(REFRESH_MS, REFRESH_MS)
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.list.reload());
    effect(() => {
      const target = this.id();
      if (target && this.list.hasValue()) {
        queueMicrotask(() => this.bringIntoView(target));
      }
    });
  }

  protected remaining(minutes: number): string {
    return this.format.formatDuration(Math.max(minutes, 0));
  }

  protected phone(request: Appointment): string | null {
    return request.customer.phone?.trim() || null;
  }

  protected confirm(request: Appointment): Promise<void> {
    return this.answer(request, 'confirmed', () => this.requests.confirm(request.id));
  }

  protected async decline(request: Appointment): Promise<void> {
    const reason = await firstValueFrom(
      this.dialog
        .open<DeclineDialog, undefined, string>(DeclineDialog, { width: '480px', maxWidth: '95vw' })
        .afterClosed(),
    );
    if (reason === undefined) {
      return;
    }

    await this.answer(request, 'declined', () => this.requests.decline(request.id, reason || null));
  }

  private async answer(
    request: Appointment,
    outcome: 'confirmed' | 'declined',
    send: () => Promise<unknown>,
  ): Promise<void> {
    this.answered.update((ids) => new Set(ids).add(request.id));
    const offer = this.toast.offerUndo(`requests.${outcome}`, 'requests.undo');
    this.pending.add(offer);
    const undone = await offer.undone;
    this.pending.delete(offer);

    if (undone) {
      this.answered.update((ids) => {
        const rest = new Set(ids);
        rest.delete(request.id);
        return rest;
      });
      return;
    }

    try {
      await send();
    } catch {
      // The error snackbar has said why; show the card again with the real state.
      this.answered.update((ids) => {
        const rest = new Set(ids);
        rest.delete(request.id);
        return rest;
      });
    }

    this.list.reload();
    await this.badge.refresh();
  }

  private when(request: Appointment): string {
    const zone = this.zone();
    const day = this.format.formatDate(request.startUtc, 'fullDate', zone);
    const from = this.format.formatDate(request.startUtc, 'time', zone);
    const to = this.format.formatDate(request.endUtc, 'time', zone);
    return `${day}, ${from} – ${to}`;
  }

  private minutesLeft(request: Appointment, nowMs: number): number | null {
    return request.expiresAtUtc === null
      ? null
      : Math.ceil((Date.parse(request.expiresAtUtc) - nowMs) / 60_000);
  }

  private bringIntoView(id: string): void {
    const card = document.getElementById(`request-${id}`);
    card?.scrollIntoView({ block: 'center', behavior: 'smooth' });
    card?.focus({ preventScroll: true });
  }
}
