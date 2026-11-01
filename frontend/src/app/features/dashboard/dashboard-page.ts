import { ChangeDetectionStrategy, Component, computed, inject, resource } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { map, timer } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { BookingLinkCard } from './booking-link-card';
import { DashboardService } from './dashboard.service';
import { RequestsCard } from './requests-card';
import { TodayCard } from './today-card';
import { WeekCard } from './week-card';

const REFRESH_MS = 60_000;

/** The start page of the salon area: today, open requests, the week's figures, and the booking link to hand out. */
@Component({
  selector: 'app-dashboard-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BookingLinkCard, MatIconModule, RequestsCard, TodayCard, TranslocoPipe, WeekCard],
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.scss',
})
export class DashboardPage {
  private readonly service = inject(DashboardService);
  private readonly auth = inject(AuthService);

  private readonly data = resource({ loader: () => this.service.get() });

  /** Moves forward every minute, so "now" on the timeline and the next appointments stay current. */
  protected readonly now = toSignal(timer(0, REFRESH_MS).pipe(map(() => Date.now())), {
    initialValue: Date.now(),
  });

  protected readonly dashboard = computed(() => (this.data.hasValue() ? this.data.value() : null));
  protected readonly loading = computed(() => this.data.isLoading() && this.dashboard() === null);
  protected readonly failed = computed(
    () => this.data.status() === 'error' && this.dashboard() === null,
  );
  protected readonly salonName = computed(() => this.auth.currentUser()?.salon?.name ?? '');
  protected readonly greeting = computed(() => this.auth.currentUser()?.displayName ?? '');

  constructor() {
    timer(REFRESH_MS, REFRESH_MS)
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.reload());
  }

  protected reload(): void {
    this.data.reload();
  }
}
