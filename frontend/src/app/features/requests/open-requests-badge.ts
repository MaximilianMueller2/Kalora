import { Injectable, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EMPTY, catchError, exhaustMap, from, timer } from 'rxjs';
import { RequestsService } from './requests.service';

/** How often the number of open requests is asked for again. */
export const BADGE_REFRESH_MS = 60_000;

/** The number of open requests for the badge in the navigation. */
@Injectable({ providedIn: 'root' })
export class OpenRequestsBadge {
  private readonly requests = inject(RequestsService);
  private readonly counted = signal(0);

  readonly count = this.counted.asReadonly();

  /** Starts asking now and then every minute until the calling component is destroyed. Call from a constructor. */
  watch(): void {
    timer(0, BADGE_REFRESH_MS)
      .pipe(
        exhaustMap(() => from(this.requests.count()).pipe(catchError(() => EMPTY))),
        takeUntilDestroyed(),
      )
      .subscribe((count) => this.counted.set(count));
  }

  /** After the salon answered a request, so the badge does not wait for the next round. */
  async refresh(): Promise<void> {
    try {
      this.counted.set(await this.requests.count());
    } catch {
      // The next round tries again.
    }
  }
}
