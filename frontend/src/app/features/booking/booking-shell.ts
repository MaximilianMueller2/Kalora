import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiError } from '../../core/http/api-error';
import { LocaleFormatService } from '../../core/i18n/locale-format.service';
import { PageBrand } from '../../core/layout/page-brand';
import { BookingService } from './booking.service';
import { PublicSalon } from './booking.models';

/** Header of a salon's public page (name, address, opening hours), the booking steps below it, imprint and privacy at the bottom. */
@Component({
  selector: 'app-booking-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, RouterLink, RouterOutlet, TranslocoPipe],
  styleUrl: './skeleton.scss',
  styles: `
    :host {
      display: block;
    }

    header {
      margin-bottom: 24px;
    }

    h1 {
      margin: 0 0 4px;
      font: var(--mat-sys-headline-medium);
      overflow-wrap: anywhere;
    }

    .contact {
      display: flex;
      flex-wrap: wrap;
      gap: 0 16px;
      margin: 0;
      padding: 0;
      list-style: none;

      a {
        display: inline-flex;
        align-items: center;
        gap: 6px;
        min-height: 44px;
        color: var(--mat-sys-primary);
        text-decoration: none;

        &:hover {
          text-decoration: underline;
        }

        &:focus-visible {
          outline: 2px solid var(--mat-sys-primary);
          outline-offset: 2px;
        }
      }

      mat-icon {
        width: 20px;
        height: 20px;
        font-size: 20px;
      }
    }

    details {
      border-top: 1px solid var(--mat-sys-outline-variant);
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }

    summary {
      display: flex;
      align-items: center;
      justify-content: space-between;
      min-height: 48px;
      cursor: pointer;
      font: var(--mat-sys-title-small);

      &:focus-visible {
        outline: 2px solid var(--mat-sys-primary);
        outline-offset: 2px;
      }
    }

    summary::after {
      content: '';
      width: 8px;
      height: 8px;
      margin-right: 6px;
      border-right: 2px solid currentColor;
      border-bottom: 2px solid currentColor;
      transform: rotate(45deg);
      transition: transform 150ms;
    }

    details[open] summary::after {
      transform: rotate(-135deg);
    }

    @media (prefers-reduced-motion: reduce) {
      summary::after {
        transition: none;
      }
    }

    dl.hours {
      display: grid;
      grid-template-columns: auto 1fr;
      gap: 2px 24px;
      margin: 0 0 12px;

      dd {
        margin: 0;
      }

      .closed {
        color: var(--mat-sys-on-surface-variant);
      }
    }

    footer {
      display: flex;
      flex-wrap: wrap;
      gap: 0 16px;
      margin-top: 32px;
      padding-top: 8px;
      border-top: 1px solid var(--mat-sys-outline-variant);

      a {
        display: inline-flex;
        align-items: center;
        min-height: 44px;
        color: var(--mat-sys-on-surface-variant);
        font: var(--mat-sys-label-large);
      }
    }

    .skeleton.title {
      width: 60%;
      height: 36px;
      margin-bottom: 12px;
    }

    .skeleton.line {
      width: 80%;
      height: 20px;
    }

    .missing {
      padding: 48px 0;
      text-align: center;
    }
  `,
  template: `
    @if (booking.salon.hasValue()) {
      @let salon = booking.salon.value();
      <header>
        <h1>{{ salon.name }}</h1>
        <ul class="contact">
          @if (address(salon); as text) {
            <li>
              <a [href]="mapUrl(text)" target="_blank" rel="noopener noreferrer">
                <mat-icon aria-hidden="true">place</mat-icon>{{ text }}
                <span class="visually-hidden">{{ 'booking.opensInNewTab' | transloco }}</span>
              </a>
            </li>
          }
          @if (salon.phone) {
            <li>
              <a [href]="'tel:' + phoneNumber(salon.phone)">
                <mat-icon aria-hidden="true">call</mat-icon>{{ salon.phone }}
              </a>
            </li>
          }
        </ul>
        <details>
          <summary>{{ 'booking.hours' | transloco }}</summary>
          <dl class="hours">
            @for (day of salon.openingHours; track day.weekday) {
              <dt>{{ format.weekdayName(day.weekday) }}</dt>
              <dd>
                @if (day.blocks.length === 0) {
                  <span class="closed">{{ 'booking.closed' | transloco }}</span>
                } @else {
                  {{ blocks(day) }}
                }
              </dd>
            }
          </dl>
        </details>
      </header>

      <router-outlet />

      <footer>
        <a [routerLink]="'/' + booking.slug() + '/imprint'">{{ 'booking.imprint' | transloco }}</a>
        @if (salon.privacyPolicyUrl; as url) {
          <a [href]="url" target="_blank" rel="noopener noreferrer">{{
            'booking.privacy.link' | transloco
          }}</a>
        }
      </footer>
    } @else if (notFound()) {
      <div class="missing" role="alert">
        <h1>{{ 'booking.notFound.title' | transloco }}</h1>
        <p>{{ 'booking.notFound.text' | transloco }}</p>
      </div>
    } @else if (booking.salon.error()) {
      <p class="alert" role="alert">{{ 'errors.unknown' | transloco }}</p>
    } @else {
      <div aria-busy="true" role="status">
        <span class="visually-hidden">{{ 'common.loading' | transloco }}</span>
        <span class="skeleton title"></span>
        <span class="skeleton line"></span>
      </div>
    }
  `,
})
export class BookingShell {
  /** The salon's slug from the URL. */
  readonly slug = input.required<string>();

  protected readonly booking = inject(BookingService);
  protected readonly format = inject(LocaleFormatService);
  private readonly brand = inject(PageBrand);

  protected readonly notFound = computed(() => {
    const error = this.booking.salon.error();
    return error instanceof ApiError && error.status === 404;
  });

  constructor() {
    effect(() => this.booking.setSlug(this.slug().toLowerCase()));
    effect(() =>
      this.brand.set(this.booking.salon.hasValue() ? this.booking.salon.value().name : null),
    );
    inject(DestroyRef).onDestroy(() => this.brand.set(null));
  }

  protected address(salon: PublicSalon): string {
    const city = [salon.postalCode, salon.city].filter(Boolean).join(' ');
    return [salon.street, city].filter(Boolean).join(', ');
  }

  protected mapUrl(address: string): string {
    return `https://www.openstreetmap.org/search?query=${encodeURIComponent(address)}`;
  }

  protected phoneNumber(phone: string): string {
    return phone.replace(/[^0-9+]/g, '');
  }

  protected blocks(day: { blocks: { start: string; end: string }[] }): string {
    return day.blocks.map((block) => `${block.start}–${block.end}`).join(', ');
  }
}
