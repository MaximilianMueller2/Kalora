import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { LocaleFormatService } from '../../core/i18n/locale-format.service';
import { DashboardWeek } from './dashboard.models';

/** The figures of the week. The revenue is clearly marked as an estimate from list prices. */
@Component({
  selector: 'app-week-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, TranslocoPipe],
  templateUrl: './week-card.html',
  styleUrl: './week-card.scss',
})
export class WeekCard {
  private readonly format = inject(LocaleFormatService);

  readonly week = input.required<DashboardWeek>();

  protected readonly revenue = computed(() => {
    const week = this.week();
    return this.format.formatNumber(week.estimatedRevenueCents / 100, {
      style: 'currency',
      currency: week.currency,
    });
  });
  protected readonly booked = computed(() => this.format.formatDuration(this.week().bookedMinutes));
  protected readonly available = computed(() =>
    this.format.formatDuration(this.week().availableMinutes),
  );
  protected readonly range = computed(() => {
    const week = this.week();
    return `${this.format.formatDate(`${week.from}T12:00:00Z`, 'date', 'UTC')} – ${this.format.formatDate(`${week.to}T12:00:00Z`, 'date', 'UTC')}`;
  });
}
