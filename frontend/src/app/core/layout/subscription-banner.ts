import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../features/auth/auth.service';
import { LocaleFormatService } from '../i18n/locale-format.service';

const DAY_MS = 24 * 60 * 60 * 1000;
/** The trial is pointed out this long before it ends. */
const TRIAL_NOTICE_DAYS = 7;

/**
 * Tells the salon that it can only look at its data, or that its trial is about to end. The server refuses every change
 * of a limited salon on its own; this says why.
 */
@Component({
  selector: 'app-subscription-banner',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, TranslocoPipe],
  styles: `
    :host {
      display: block;
    }

    .banner {
      display: flex;
      align-items: flex-start;
      gap: 12px;
      margin: 0 0 16px;
      padding: 12px 16px;
      border-radius: var(--mat-sys-corner-medium);
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
    }

    .banner.limited {
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }

    .banner span {
      padding-top: 2px;
    }
  `,
  template: `
    @if (notice(); as notice) {
      <p class="banner" role="note" [class.limited]="notice.limited">
        <mat-icon aria-hidden="true">{{ notice.limited ? 'lock' : 'schedule' }}</mat-icon>
        <span>{{ notice.key | transloco: notice.params }}</span>
      </p>
    }
  `,
})
export class SubscriptionBanner {
  private readonly auth = inject(AuthService);
  private readonly format = inject(LocaleFormatService);

  protected readonly notice = computed(() => {
    const salon = this.auth.currentUser()?.salon;
    if (!salon) {
      return null;
    }

    if (salon.access !== 'Full') {
      return { limited: true, key: `banner.limited.${salon.access}`, params: {} };
    }

    const ends = salon.trialEndsAtUtc ? Date.parse(salon.trialEndsAtUtc) : Number.NaN;
    const managing = this.auth.hasAnyRole(['Owner', 'Manager']);
    if (salon.status === 'Trial' && managing && ends - Date.now() <= TRIAL_NOTICE_DAYS * DAY_MS) {
      return {
        limited: false,
        key: 'banner.trialEnds',
        params: { date: this.format.formatDate(ends, 'date', salon.timeZone) },
      };
    }

    return null;
  });
}
