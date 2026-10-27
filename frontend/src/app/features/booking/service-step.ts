import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { LocaleFormatService } from '../../core/i18n/locale-format.service';
import { MatButtonModule } from '@angular/material/button';
import { BookingService } from './booking.service';
import { PublicService } from './booking.models';
import { StepHeader } from './step-header';

@Component({
  selector: 'app-service-step',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, StepHeader, TranslocoPipe],
  styleUrl: './skeleton.scss',
  styles: `
    section {
      margin: 0 0 24px;
    }

    h3 {
      margin: 0 0 8px;
      font: var(--mat-sys-title-medium);
      color: var(--mat-sys-on-surface-variant);
    }

    ul {
      display: grid;
      gap: 8px;
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .card {
      display: grid;
      gap: 2px;
      width: 100%;
      min-height: 64px;
      padding: 12px 16px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--mat-sys-corner-large);
      background: var(--mat-sys-surface);
      color: inherit;
      font: inherit;
      text-align: start;
      cursor: pointer;

      &:hover {
        background: var(--mat-sys-surface-container-low);
      }

      &:focus-visible {
        outline: 2px solid var(--mat-sys-primary);
        outline-offset: 2px;
      }

      &[aria-current='true'] {
        border-color: var(--mat-sys-primary);
        border-width: 2px;
        padding: 11px 15px;
      }
    }

    .name {
      font: var(--mat-sys-title-medium);
    }

    .meta {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-medium);
    }

    .description {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }

    .skeleton {
      height: 72px;
      margin-bottom: 8px;
    }
  `,
  template: `
    <app-step-header [step]="1" titleKey="booking.steps.service.title" />

    @if (booking.categories.hasValue()) {
      @for (category of booking.categories.value(); track category.name) {
        <section>
          <h3>{{ category.name }}</h3>
          <ul>
            @for (service of category.services; track service.id) {
              <li>
                <button
                  type="button"
                  class="card"
                  [attr.aria-current]="booking.selectedService()?.id === service.id"
                  (click)="choose(service)"
                >
                  <span class="name">{{ service.name }}</span>
                  <span class="meta">
                    {{ format.formatDuration(service.durationMinutes) }} ·
                    @if (service.priceIsFrom) {
                      {{
                        'booking.priceFrom'
                          | transloco
                            : { price: format.formatMoney(service.priceCents, service.currency) }
                      }}
                    } @else {
                      {{ format.formatMoney(service.priceCents, service.currency) }}
                    }
                  </span>
                  @if (service.description) {
                    <span class="description">{{ service.description }}</span>
                  }
                </button>
              </li>
            }
          </ul>
        </section>
      } @empty {
        <p>{{ 'booking.steps.service.empty' | transloco }}</p>
      }
    } @else if (booking.categories.error()) {
      <p class="alert" role="alert">{{ 'errors.unknown' | transloco }}</p>
      <button mat-stroked-button type="button" (click)="booking.categories.reload()">
        {{ 'booking.retry' | transloco }}
      </button>
    } @else {
      <div aria-busy="true" role="status">
        <span class="visually-hidden">{{ 'common.loading' | transloco }}</span>
        @for (n of [1, 2, 3, 4]; track n) {
          <span class="skeleton"></span>
        }
      </div>
    }
  `,
})
export class ServiceStep {
  protected readonly booking = inject(BookingService);
  protected readonly format = inject(LocaleFormatService);
  private readonly router = inject(Router);

  protected choose(service: PublicService): void {
    this.booking.selectService(service);
    void this.router.navigate(['/', this.booking.slug(), 'person']);
  }
}
