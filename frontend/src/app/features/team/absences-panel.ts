import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  resource,
  signal,
} from '@angular/core';
import { FormField, FormRoot, form, required, validate } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiError } from '../../core/http/api-error';
import { LocaleFormatService } from '../../core/i18n/locale-format.service';
import { Toast } from '../../core/ui/toast';
import { todayInZone } from '../../shared/dates/today';
import { fieldErrorText } from '../../shared/forms/field-error';
import { injectErrorMessage } from '../../shared/forms/server-error';
import { AuthService } from '../auth/auth.service';
import { ABSENCE_TYPES, AbsenceType, AffectedAppointment, MAX_ABSENCE_DAYS } from './team.models';
import { TeamService } from './team.service';

const FALLBACK_ZONE = 'UTC';

/** Days from `start` to `end` including both ends; dates are "yyyy-MM-dd". */
export function daysBetween(start: string, end: string): number {
  return Math.round((Date.parse(end) - Date.parse(start)) / 86_400_000) + 1;
}

/**
 * Absences (vacation, sick, private) of one person: upcoming ones, a form to add one, and the warning with the
 * appointments that fall into a newly entered absence. Used for the team detail page and for the staff's own page.
 */
@Component({
  selector: 'app-absences-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormField,
    FormRoot,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    TranslocoPipe,
  ],
  styleUrl: '../../shared/forms/panel-form.scss',
  styles: `
    h3 {
      margin: 32px 0 8px;
      font: var(--mat-sys-title-medium);
    }

    .warning {
      margin: 0 0 16px;
      padding: 12px 16px;
      border-radius: var(--mat-sys-corner-medium);
      background: var(--mat-sys-tertiary-container);
      color: var(--mat-sys-on-tertiary-container);
    }

    .warning h3 {
      margin: 0 0 4px;
      font: var(--mat-sys-title-small);
    }

    .warning ul {
      margin: 8px 0;
      padding-left: 20px;
    }

    .list {
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .list li {
      display: flex;
      align-items: center;
      gap: 12px;
      min-height: 56px;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }

    .what {
      flex: 1;
      min-width: 0;
      display: grid;
    }

    .range {
      font-weight: 500;
    }

    .sub {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }

    .sub.conflict {
      color: var(--mat-sys-error);
    }

    .remove {
      width: 44px;
      height: 44px;
    }
  `,
  template: `
    <h2>{{ 'team.absences.title' | transloco }}</h2>
    <p class="lead">{{ 'team.absences.lead' | transloco }}</p>

    @if (affected(); as appointments) {
      <div class="warning" role="status">
        <h3>{{ 'team.absences.conflictTitle' | transloco: { count: appointments.length } }}</h3>
        <p>{{ 'team.absences.conflictText' | transloco }}</p>
        <ul>
          @for (appointment of appointments; track appointment.id) {
            <li>
              {{ time(appointment) }} · {{ appointment.serviceName }} ·
              {{ appointment.customerName }}
              @if (appointment.status === 'Requested') {
                ({{ 'team.absences.requested' | transloco }})
              }
            </li>
          }
        </ul>
        <button mat-button type="button" (click)="affected.set(null)">
          {{ 'team.absences.understood' | transloco }}
        </button>
      </div>
    }

    @if (errorMessage(); as message) {
      <p class="alert" role="alert">{{ message }}</p>
    }

    <form [formRoot]="form">
      <mat-form-field appearance="outline">
        <mat-label>{{ 'team.absences.type' | transloco }}</mat-label>
        <select matNativeControl [formField]="form.type">
          @for (type of types; track type) {
            <option [value]="type">{{ 'team.absences.types.' + type | transloco }}</option>
          }
        </select>
      </mat-form-field>
      <div class="row">
        <mat-form-field appearance="outline">
          <mat-label>{{ 'team.absences.from' | transloco }}</mat-label>
          <input matInput type="date" [formField]="form.startDate" />
          @if (errorText(form.startDate().errors()); as error) {
            <mat-error>{{ error.key | transloco: error.params }}</mat-error>
          }
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>{{ 'team.absences.to' | transloco }}</mat-label>
          <input matInput type="date" [formField]="form.endDate" />
          <mat-hint>{{ 'team.absences.toHint' | transloco }}</mat-hint>
          @if (errorText(form.endDate().errors()); as error) {
            <mat-error>{{ error.key | transloco: error.params }}</mat-error>
          }
        </mat-form-field>
      </div>
      <div class="actions">
        <button mat-flat-button type="submit" [disabled]="form().submitting()">
          {{ 'team.absences.add' | transloco }}
        </button>
      </div>
    </form>

    <h3>{{ 'team.absences.upcoming' | transloco }}</h3>
    @if (absences.hasValue()) {
      @if (absences.value().length === 0) {
        <p class="hint">{{ 'team.absences.empty' | transloco }}</p>
      } @else {
        <ul class="list">
          @for (absence of absences.value(); track absence.id) {
            <li>
              <span class="what">
                <span class="range">{{ range(absence.startDate, absence.endDate) }}</span>
                <span class="sub">
                  {{ 'team.absences.types.' + absence.type | transloco }}
                </span>
                @if (absence.conflictCount > 0) {
                  <span class="sub conflict">
                    {{
                      'team.absences.conflictCount' | transloco: { count: absence.conflictCount }
                    }}
                  </span>
                }
              </span>
              <button
                mat-icon-button
                type="button"
                class="remove"
                [attr.aria-label]="
                  'team.absences.remove'
                    | transloco: { range: range(absence.startDate, absence.endDate) }
                "
                (click)="remove(absence.id)"
              >
                <mat-icon aria-hidden="true">delete</mat-icon>
              </button>
            </li>
          }
        </ul>
      }
    } @else if (absences.error()) {
      <p class="alert" role="alert">{{ 'errors.unknown' | transloco }}</p>
    } @else {
      <p role="status">{{ 'common.loading' | transloco }}</p>
    }
  `,
})
export class AbsencesPanel {
  readonly staffId = input.required<string>();

  private readonly team = inject(TeamService);
  private readonly toast = inject(Toast);
  private readonly format = inject(LocaleFormatService);
  private readonly auth = inject(AuthService);

  protected readonly types = ABSENCE_TYPES;
  protected readonly errorText = fieldErrorText;
  protected readonly absences = resource({
    params: () => this.staffId(),
    loader: ({ params }) => this.team.listAbsences(params),
  });
  protected readonly affected = signal<AffectedAppointment[] | null>(null);
  protected readonly errorCode = signal<string | null>(null);
  protected readonly errorMessage = injectErrorMessage(this.errorCode);

  private readonly zone = computed(() => this.auth.currentUser()?.salon?.timeZone ?? FALLBACK_ZONE);
  private readonly model = signal<{ type: AbsenceType; startDate: string; endDate: string }>({
    type: 'Vacation',
    startDate: todayInZone(this.zone()),
    endDate: '',
  });

  protected readonly form = form(
    this.model,
    (path) => {
      required(path.startDate);
      validate(path.endDate, ({ value, valueOf }) => {
        const start = valueOf(path.startDate);
        if (!value() || !start) {
          return undefined;
        }
        if (value() < start) {
          return { kind: 'endBeforeStart' };
        }
        return daysBetween(start, value()) > MAX_ABSENCE_DAYS
          ? { kind: 'rangeTooLong' }
          : undefined;
      });
    },
    { submission: { action: () => this.add() } },
  );

  /** "Mo., 3. November 2026" or "Mo., 3. November – Fr., 7. November 2026". */
  protected range(start: string, end: string): string {
    // Dates travel as "yyyy-MM-dd" without a time zone, so they are formatted as UTC days.
    const first = this.format.formatDate(start, 'fullDate', 'UTC');
    return start === end ? first : `${first} – ${this.format.formatDate(end, 'fullDate', 'UTC')}`;
  }

  protected time(appointment: AffectedAppointment): string {
    return this.format.formatDate(appointment.startUtc, 'dateTime', this.zone());
  }

  protected async remove(absenceId: string): Promise<void> {
    await this.team.deleteAbsence(this.staffId(), absenceId);
    this.absences.reload();
    this.toast.show('team.removed');
  }

  private async add(): Promise<undefined> {
    this.errorCode.set(null);
    const { type, startDate, endDate } = this.model();
    try {
      const created = await this.team.createAbsence(this.staffId(), {
        type,
        startDate,
        endDate: endDate || startDate,
      });
      this.affected.set(
        created.affectedAppointments.length > 0 ? created.affectedAppointments : null,
      );
      this.model.update((current) => ({ ...current, endDate: '' }));
      this.absences.reload();
      this.toast.show('team.saved');
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    }
    return undefined;
  }
}
