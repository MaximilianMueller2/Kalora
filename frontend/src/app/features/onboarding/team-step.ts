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
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiError } from '../../core/http/api-error';
import { todayInZone } from '../../shared/dates/today';
import { injectErrorMessage } from '../../shared/forms/server-error';
import { WEEKDAYS } from '../../shared/week-schedule/week-schedule.model';
import { AuthService } from '../auth/auth.service';
import { CatalogService } from '../services/catalog.service';
import { SettingsService } from '../settings/settings.service';
import { STAFF_COLORS } from '../team/team.models';
import { TeamService } from '../team/team.service';

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** A person who is still to be created. */
interface Draft {
  key: number;
  name: string;
  /** Invite them by e-mail after creating them; optional. */
  email: string;
  /** The signed-in user's own calendar column. */
  isMe: boolean;
  skills: ReadonlySet<string>;
}

/**
 * Step 4: who works in the salon. By default only the owner ("just me"); more people can be added, each with the services they
 * do and, if wanted, an invitation by e-mail. Everybody gets the opening hours of the salon as their working hours, which can
 * be changed under "Team". People that were created stay created: a retry only creates the rest.
 */
@Component({
  selector: 'app-onboarding-team-step',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatCheckboxModule, MatIconModule, RouterLink, TranslocoPipe],
  styleUrl: './step.scss',
  styles: `
    .person {
      margin: 0 0 16px;
      padding: 16px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--mat-sys-corner-large);
      background: var(--mat-sys-surface);
    }

    .person legend {
      padding: 0 4px;
      font: var(--mat-sys-title-small);
    }

    .head {
      display: flex;
      align-items: flex-end;
      gap: 8px;
    }

    .head .field {
      flex: 1;
    }

    .skills {
      margin: 12px 0 0;
      padding: 0;
      border: 0;
    }

    .skills legend {
      padding: 0;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-label-large);
    }

    .skills mat-checkbox {
      display: block;
    }

    .existing {
      margin: 0 0 16px;
      padding: 0;
      list-style: none;
    }

    .existing li {
      padding: 4px 0;
    }
  `,
  template: `
    <h2>{{ 'onboarding.team.title' | transloco }}</h2>
    <p class="lead">{{ 'onboarding.team.lead' | transloco }}</p>

    @if (ready()) {
      @if (message(); as text) {
        <p class="alert" role="alert">{{ text }}</p>
      }
      @if (inviteFailed().length > 0) {
        <p class="alert" role="status">
          {{ 'onboarding.team.inviteFailed' | transloco: { names: inviteFailed().join(', ') } }}
        </p>
      }

      @if (existing().length > 0) {
        <h3>{{ 'onboarding.team.existing' | transloco }}</h3>
        <ul class="existing">
          @for (person of existing(); track person.id) {
            <li>{{ person.displayName }}</li>
          }
        </ul>
        <p class="hint">
          {{ 'onboarding.team.existingHint' | transloco }}
          <a routerLink="/app/team">{{ 'nav.team' | transloco }}</a>
        </p>
      }

      @for (draft of drafts(); track draft.key) {
        <fieldset class="person">
          <legend>
            {{
              draft.isMe
                ? ('onboarding.team.me' | transloco)
                : ('onboarding.team.person' | transloco)
            }}
          </legend>
          <div class="head">
            <label class="field">
              <span>{{ 'onboarding.team.name' | transloco }}</span>
              <input
                type="text"
                autocomplete="off"
                [value]="draft.name"
                (input)="update(draft, { name: $any($event.target).value })"
              />
            </label>
            @if (!draft.isMe) {
              <button
                mat-icon-button
                type="button"
                class="remove"
                [attr.aria-label]="'onboarding.team.remove' | transloco"
                (click)="remove(draft)"
              >
                <mat-icon aria-hidden="true">close</mat-icon>
              </button>
            }
          </div>
          @if (!draft.isMe) {
            <label class="field">
              <span>{{ 'onboarding.team.email' | transloco }}</span>
              <input
                type="email"
                autocomplete="off"
                [value]="draft.email"
                (input)="update(draft, { email: $any($event.target).value })"
              />
            </label>
          }
          <fieldset class="skills">
            <legend>{{ 'onboarding.team.skills' | transloco }}</legend>
            @for (service of services(); track service.id) {
              <mat-checkbox
                [checked]="draft.skills.has(service.id)"
                (change)="toggle(draft, service.id, $event.checked)"
              >
                {{ service.name }}
              </mat-checkbox>
            }
          </fieldset>
        </fieldset>
      }

      <div class="actions">
        <button mat-stroked-button type="button" class="add" (click)="add()">
          <mat-icon aria-hidden="true">person_add</mat-icon>
          {{ 'onboarding.team.add' | transloco }}
        </button>
      </div>
      <div class="actions">
        <button mat-flat-button type="button" class="next" [disabled]="saving()" (click)="save()">
          {{ 'onboarding.next' | transloco }}
        </button>
      </div>
    } @else if (loadFailed()) {
      <p class="alert" role="alert">{{ 'errors.unknown' | transloco }}</p>
    } @else {
      <div class="skeleton" aria-busy="true" aria-hidden="true"></div>
    }
  `,
})
export class TeamStep {
  private readonly team = inject(TeamService);
  private readonly catalogService = inject(CatalogService);
  private readonly settings = inject(SettingsService);
  private readonly auth = inject(AuthService);

  readonly saved = output<void>();

  private readonly staff = resource({ loader: () => this.team.list() });
  private readonly catalog = resource({ loader: () => this.catalogService.get() });

  protected readonly drafts = signal<Draft[]>([]);
  protected readonly saving = signal(false);
  protected readonly inviteFailed = signal<string[]>([]);
  private readonly errorCode = signal<string | null>(null);
  protected readonly message = injectErrorMessage(this.errorCode);
  private nextKey = 1;
  /** People created so far in this step, for picking the next color. */
  private created = 0;

  protected readonly existing = computed(() =>
    this.staff.hasValue() ? this.staff.value().filter((person) => person.isActive) : [],
  );
  protected readonly services = computed(() =>
    this.catalog.hasValue()
      ? this.catalog
          .value()
          .categories.flatMap((category) => category.services)
          .filter((service) => service.isActive && service.isBookableOnline)
      : [],
  );
  /** The form is shown once the people and services are loaded and the first draft is in place. */
  protected readonly ready = signal(false);
  protected readonly loadFailed = computed(
    () => this.staff.status() === 'error' || this.catalog.status() === 'error',
  );

  constructor() {
    effect(() => {
      if (!this.ready() && this.staff.hasValue() && this.catalog.hasValue()) {
        untracked(() => {
          this.startDrafts();
          this.ready.set(true);
        });
      }
    });
  }

  /** The owner is the first person, unless they already have their calendar column. */
  private startDrafts(): void {
    const people = this.staff.value() ?? [];
    if (people.length === 0) {
      this.drafts.set([
        {
          key: this.nextKey++,
          name: this.auth.currentUser()?.displayName ?? '',
          email: '',
          isMe: true,
          skills: this.allSkills(),
        },
      ]);
    }
  }

  private allSkills(): ReadonlySet<string> {
    return new Set(this.services().map((service) => service.id));
  }

  protected add(): void {
    this.drafts.update((drafts) => [
      ...drafts,
      { key: this.nextKey++, name: '', email: '', isMe: false, skills: this.allSkills() },
    ]);
  }

  protected remove(draft: Draft): void {
    this.drafts.update((drafts) => drafts.filter((d) => d.key !== draft.key));
  }

  protected update(draft: Draft, change: Partial<Pick<Draft, 'name' | 'email'>>): void {
    this.drafts.update((drafts) =>
      drafts.map((d) => (d.key === draft.key ? { ...d, ...change } : d)),
    );
  }

  protected toggle(draft: Draft, serviceId: string, checked: boolean): void {
    const skills = new Set(draft.skills);
    if (checked) {
      skills.add(serviceId);
    } else {
      skills.delete(serviceId);
    }
    this.drafts.update((drafts) => drafts.map((d) => (d.key === draft.key ? { ...d, skills } : d)));
  }

  protected async save(): Promise<void> {
    this.errorCode.set(null);
    this.inviteFailed.set([]);
    const drafts = this.drafts();
    if (
      drafts.some((d) => d.name.trim().length === 0) ||
      drafts.some((d) => d.email.trim() !== '' && !EMAIL_PATTERN.test(d.email.trim()))
    ) {
      this.errorCode.set('validation.invalid');
      return;
    }

    this.saving.set(true);
    try {
      if (drafts.length > 0) {
        const hours = await this.openingDays();
        for (const draft of drafts) {
          await this.create(draft, hours);
          this.drafts.update((rest) => rest.filter((d) => d.key !== draft.key));
        }
      }
      this.saved.emit();
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    } finally {
      this.saving.set(false);
    }
  }

  private async openingDays() {
    const schedule = await this.settings.getOpeningHours();
    return WEEKDAYS.map((weekday) => ({ weekday, blocks: schedule[weekday] }));
  }

  private async create(
    draft: Draft,
    days: { weekday: (typeof WEEKDAYS)[number]; blocks: { start: string; end: string }[] }[],
  ): Promise<void> {
    const count = (this.staff.hasValue() ? this.staff.value().length : 0) + this.created++;
    const person = await this.team.create({
      displayName: draft.name.trim(),
      color: STAFF_COLORS[count % STAFF_COLORS.length],
      isBookableOnline: true,
      autoConfirm: false,
      linkToCurrentUser: draft.isMe,
    });
    await this.team.saveSkills(person.id, {
      skills: [...draft.skills].map((serviceId) => ({ serviceId, durationOverrideMinutes: null })),
    });
    await this.team.saveSchedule(person.id, {
      validFrom: todayInZone(this.auth.currentUser()?.salon?.timeZone ?? 'Europe/Berlin'),
      days,
    });
    const email = draft.email.trim();
    if (email) {
      try {
        await this.team.invite({ staffId: person.id, email, role: 'Staff' });
      } catch {
        // The person exists and can be invited again under "Team"; the others still get done.
        this.inviteFailed.update((names) => [...names, person.displayName]);
      }
    }
  }
}
