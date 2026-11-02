import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  output,
  resource,
  signal,
  untracked,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiError } from '../../core/http/api-error';
import { LocaleFormatService } from '../../core/i18n/locale-format.service';
import { injectErrorMessage } from '../../shared/forms/server-error';
import { SalonIndustry } from '../auth/auth.models';
import { CatalogService } from '../services/catalog.service';
import { Catalog, ServiceItem } from '../services/catalog.models';
import { centsToInputText, parsePriceToCents } from '../services/price';
import { SettingsService } from '../settings/settings.service';

/** Step 3: services from the template of the industry, with their prices to adjust. More can be added later in "Services". */
@Component({
  selector: 'app-onboarding-services-step',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, TranslocoPipe],
  styleUrl: './step.scss',
  styles: `
    .category {
      margin: 0 0 16px;
      padding: 0;
      border: 0;
    }

    .category legend {
      padding: 0;
      font: var(--mat-sys-title-small);
    }

    .service {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 4px 12px;
      padding: 8px 0;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }

    .service .name {
      flex: 1 1 180px;
      overflow-wrap: anywhere;
    }

    .service .meta {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }

    .price {
      display: flex;
      align-items: center;
      gap: 6px;
    }

    .price input {
      width: 96px;
      min-height: 44px;
      padding: 0 10px;
      border: 1px solid var(--mat-sys-outline);
      border-radius: var(--mat-sys-corner-small);
      background: var(--mat-sys-surface);
      color: var(--mat-sys-on-surface);
      font: var(--mat-sys-body-large);
      text-align: end;
      box-sizing: border-box;
    }

    .price input.invalid {
      border-color: var(--mat-sys-error);
    }

    .price input:focus-visible {
      outline: 3px solid var(--mat-sys-primary);
      outline-offset: 1px;
    }
  `,
  template: `
    <h2>{{ 'onboarding.services.title' | transloco }}</h2>
    <p class="lead">{{ 'onboarding.services.lead' | transloco }}</p>

    @if (catalog.hasValue()) {
      @if (message(); as text) {
        <p class="alert" role="alert">{{ text }}</p>
      }
      @if (services().length === 0) {
        <p class="hint">{{ 'onboarding.services.empty' | transloco }}</p>
        <div class="actions">
          <button
            mat-flat-button
            type="button"
            class="template"
            [disabled]="saving()"
            (click)="applyTemplate()"
          >
            {{
              'onboarding.services.applyTemplate'
                | transloco: { industry: ('auth.register.industries.' + industry() | transloco) }
            }}
          </button>
        </div>
      } @else {
        @for (category of categories(); track category.id) {
          @if (category.services.length > 0) {
            <fieldset class="category">
              <legend>{{ category.name }}</legend>
              @for (service of category.services; track service.id) {
                <div class="service">
                  <div class="name">
                    <div>{{ service.name }}</div>
                    <div class="meta">{{ format.formatDuration(service.durationMinutes) }}</div>
                  </div>
                  <label class="price">
                    <span class="visually-hidden">
                      {{ 'onboarding.services.price' | transloco }}: {{ service.name }}
                    </span>
                    <input
                      type="text"
                      inputmode="decimal"
                      autocomplete="off"
                      [class.invalid]="isInvalid(service)"
                      [attr.aria-invalid]="isInvalid(service)"
                      [value]="priceText(service)"
                      (input)="setPrice(service, $event)"
                    />
                    <span aria-hidden="true">{{
                      service.currency === 'EUR' ? '€' : service.currency
                    }}</span>
                  </label>
                </div>
              }
            </fieldset>
          }
        }
        <p class="hint">{{ 'onboarding.services.more' | transloco }}</p>
        <div class="actions">
          <button
            mat-flat-button
            type="button"
            class="next"
            [disabled]="saving() || hasInvalid()"
            (click)="save()"
          >
            {{ 'onboarding.next' | transloco }}
          </button>
        </div>
      }
    } @else if (catalog.error()) {
      <p class="alert" role="alert">{{ 'errors.unknown' | transloco }}</p>
    } @else {
      <div class="skeleton" aria-busy="true" aria-hidden="true"></div>
    }
  `,
})
export class ServicesStep {
  private readonly catalogService = inject(CatalogService);
  private readonly settings = inject(SettingsService);
  protected readonly format = inject(LocaleFormatService);

  readonly saved = output<void>();

  protected readonly catalog = resource({ loader: () => this.catalogService.get() });
  protected readonly industry = signal<SalonIndustry>('Hair');
  /** What was typed per service; a service without an entry still has its saved price. */
  private readonly typed = signal<Record<string, string>>({});
  protected readonly saving = signal(false);
  private readonly errorCode = signal<string | null>(null);
  protected readonly message = injectErrorMessage(this.errorCode);
  /** Set once a template was applied or the catalog was changed here, so the loaded catalog is replaced by the newer one. */
  private readonly current = signal<Catalog | null>(null);

  protected readonly categories = computed(() => this.shown()?.categories ?? []);
  protected readonly services = computed(() =>
    this.categories().flatMap((category) => category.services),
  );
  protected readonly hasInvalid = computed(() => this.services().some((s) => this.isInvalid(s)));

  private readonly separator = this.format.formatNumber(1.5).charAt(1);

  constructor() {
    // The template is chosen by the salon's industry, so it is read from the profile.
    void this.settings.getProfile().then((profile) => this.industry.set(profile.industry));
    effect(() => {
      if (this.catalog.hasValue()) {
        const loaded = this.catalog.value();
        untracked(() => this.current.set(loaded));
      }
    });
  }

  private shown(): Catalog | null {
    return this.current() ?? (this.catalog.hasValue() ? this.catalog.value() : null);
  }

  protected priceText(service: ServiceItem): string {
    return this.typed()[service.id] ?? centsToInputText(service.priceCents, this.separator);
  }

  protected isInvalid(service: ServiceItem): boolean {
    const text = this.typed()[service.id];
    return text !== undefined && parsePriceToCents(text) === null;
  }

  protected setPrice(service: ServiceItem, event: Event): void {
    const text = (event.target as HTMLInputElement).value;
    this.typed.update((typed) => ({ ...typed, [service.id]: text }));
  }

  protected async applyTemplate(): Promise<void> {
    this.saving.set(true);
    this.errorCode.set(null);
    try {
      this.current.set(await this.catalogService.applyTemplate(null));
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    } finally {
      this.saving.set(false);
    }
  }

  /** Saves the prices that were changed, then goes on. */
  protected async save(): Promise<void> {
    this.saving.set(true);
    this.errorCode.set(null);
    try {
      for (const service of this.services()) {
        const text = this.typed()[service.id];
        const cents = text === undefined ? null : parsePriceToCents(text);
        if (cents !== null && cents !== service.priceCents) {
          this.current.set(
            await this.catalogService.updateService(service.id, {
              categoryId: service.categoryId,
              name: service.name,
              description: service.description,
              priceCents: cents,
              priceIsFrom: service.priceIsFrom,
              durationMinutes: service.durationMinutes,
              bufferBeforeMinutes: service.bufferBeforeMinutes,
              bufferAfterMinutes: service.bufferAfterMinutes,
              isBookableOnline: service.isBookableOnline,
              isActive: service.isActive,
            }),
          );
          this.typed.update((typed) => {
            const rest = { ...typed };
            delete rest[service.id];
            return rest;
          });
        }
      }
      this.saved.emit();
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    } finally {
      this.saving.set(false);
    }
  }
}
