import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  resource,
  signal,
  untracked,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiError } from '../../core/http/api-error';
import { LocaleFormatService } from '../../core/i18n/locale-format.service';
import { Toast } from '../../core/ui/toast';
import { injectErrorMessage } from '../../shared/forms/server-error';
import { CATALOG_LIMITS } from '../services/catalog.models';
import { CatalogService } from '../services/catalog.service';
import { minuteOptions } from '../services/price';
import { injectStaffId } from './staff-id';
import { StaffSkill } from './team.models';
import { TeamService } from './team.service';

/** Selected services of the person: service ID -> own duration in minutes as text ("" = the service's default). */
type Selection = Record<string, string>;

@Component({
  selector: 'app-skills-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatCheckboxModule, RouterLink, TranslocoPipe],
  styleUrl: '../../shared/forms/panel-form.scss',
  styles: `
    .category {
      margin: 0 0 16px;
      padding: 0;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--mat-sys-corner-large);
    }

    legend {
      padding: 0 8px;
      margin-left: 8px;
      font: var(--mat-sys-title-small);
    }

    .service {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 4px 16px;
      padding: 0 12px;
      border-top: 1px solid var(--mat-sys-outline-variant);
    }

    legend + .service {
      border-top: 0;
    }

    .service mat-checkbox {
      flex: 1;
      min-width: 200px;
    }

    .duration {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: 0 0 8px 40px;
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);

      select {
        min-height: 40px;
        padding: 0 8px;
        border: 1px solid var(--mat-sys-outline);
        border-radius: var(--mat-sys-corner-small);
        background: var(--mat-sys-surface);
        color: var(--mat-sys-on-surface);
        font: var(--mat-sys-body-medium);
      }
    }

    .bulk {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      margin: 0 0 16px;
    }
  `,
  template: `
    <h2>{{ 'team.skills.title' | transloco }}</h2>
    <p class="lead">{{ 'team.skills.lead' | transloco }}</p>

    @if (catalog.hasValue() && skills.hasValue()) {
      @if (services().length === 0) {
        <p class="hint">
          {{ 'team.skills.noServices' | transloco }}
          <a routerLink="/app/services">{{ 'nav.services' | transloco }}</a>
        </p>
      } @else {
        @if (errorMessage(); as message) {
          <p class="alert" role="alert">{{ message }}</p>
        }
        <div class="bulk">
          <button mat-button type="button" (click)="selectAll()">
            {{ 'team.skills.all' | transloco }}
          </button>
          <button mat-button type="button" (click)="selectNone()">
            {{ 'team.skills.none' | transloco }}
          </button>
        </div>

        @for (category of catalog.value().categories; track category.id) {
          @if (category.services.length > 0) {
            <fieldset class="category">
              <legend>{{ category.name }}</legend>
              @for (service of category.services; track service.id) {
                <div class="service">
                  <mat-checkbox
                    [checked]="isSelected(service.id)"
                    (change)="toggle(service.id, $event.checked)"
                  >
                    {{ service.name }}
                    @if (!service.isActive) {
                      <span class="hint">({{ 'services.service.inactiveBadge' | transloco }})</span>
                    }
                  </mat-checkbox>
                </div>
                @if (isSelected(service.id)) {
                  <label class="duration">
                    {{ 'team.skills.duration' | transloco }}
                    <select (change)="setDuration(service.id, $any($event.target).value)">
                      <option value="" [selected]="selection()[service.id] === ''">
                        {{
                          'team.skills.sameAsService'
                            | transloco: { duration: duration(service.durationMinutes) }
                        }}
                      </option>
                      @for (minutes of durations; track minutes) {
                        <option
                          [value]="minutes"
                          [selected]="selection()[service.id] === minutes.toString()"
                        >
                          {{ duration(minutes) }}
                        </option>
                      }
                    </select>
                  </label>
                }
              }
            </fieldset>
          }
        }

        <div class="actions">
          <button mat-flat-button type="button" [disabled]="saving()" (click)="save()">
            {{ 'team.save' | transloco }}
          </button>
        </div>
      }
    } @else if (catalog.error() || skills.error()) {
      <p class="alert" role="alert">{{ 'errors.unknown' | transloco }}</p>
    } @else {
      <p role="status">{{ 'common.loading' | transloco }}</p>
    }
  `,
})
export class SkillsTab {
  private readonly team = inject(TeamService);
  private readonly catalogApi = inject(CatalogService);
  private readonly toast = inject(Toast);
  private readonly format = inject(LocaleFormatService);
  private readonly staffId = injectStaffId();

  protected readonly durations = minuteOptions(
    CATALOG_LIMITS.stepMinutes,
    CATALOG_LIMITS.maxDurationMinutes,
  );
  protected readonly catalog = resource({ loader: () => this.catalogApi.get() });
  protected readonly skills = resource({
    params: () => this.staffId(),
    loader: ({ params }) => this.team.getSkills(params),
  });
  protected readonly selection = signal<Selection>({});
  protected readonly saving = signal(false);
  protected readonly errorCode = signal<string | null>(null);
  protected readonly errorMessage = injectErrorMessage(this.errorCode);
  protected readonly services = computed(() =>
    (this.catalog.hasValue() ? this.catalog.value().categories : []).flatMap((c) => c.services),
  );

  constructor() {
    effect(() => {
      if (this.skills.hasValue()) {
        const loaded = this.skills.value();
        untracked(() =>
          this.selection.set(
            Object.fromEntries(
              loaded.skills.map((s) => [s.serviceId, s.durationOverrideMinutes?.toString() ?? '']),
            ),
          ),
        );
      }
    });
  }

  protected isSelected(serviceId: string): boolean {
    return serviceId in this.selection();
  }

  protected duration(minutes: number): string {
    return this.format.formatDuration(minutes);
  }

  protected toggle(serviceId: string, checked: boolean): void {
    this.selection.update((current) => {
      const next = { ...current };
      if (checked) {
        next[serviceId] = '';
      } else {
        delete next[serviceId];
      }
      return next;
    });
  }

  protected setDuration(serviceId: string, value: string): void {
    this.selection.update((current) => ({ ...current, [serviceId]: value }));
  }

  protected selectAll(): void {
    this.selection.update((current) =>
      Object.fromEntries(this.services().map((s) => [s.id, current[s.id] ?? ''])),
    );
  }

  protected selectNone(): void {
    this.selection.set({});
  }

  protected async save(): Promise<void> {
    this.errorCode.set(null);
    this.saving.set(true);
    const skills: StaffSkill[] = Object.entries(this.selection()).map(([serviceId, minutes]) => ({
      serviceId,
      durationOverrideMinutes: minutes === '' ? null : Number(minutes),
    }));
    try {
      await this.team.saveSkills(this.staffId(), { skills });
      this.toast.show('team.saved');
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    } finally {
      this.saving.set(false);
    }
  }
}
