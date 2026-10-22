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
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiError } from '../../../core/http/api-error';
import { Toast } from '../../../core/ui/toast';
import { injectErrorMessage } from '../../../shared/forms/server-error';
import { WeekScheduleEditor } from '../../../shared/week-schedule/week-schedule-editor';
import {
  WeekSchedule,
  emptySchedule,
  scheduleProblems,
} from '../../../shared/week-schedule/week-schedule.model';
import { SettingsService } from '../settings.service';

@Component({
  selector: 'app-hours-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, TranslocoPipe, WeekScheduleEditor],
  styleUrl: '../settings-tabs.scss',
  template: `
    <h2>{{ 'settings.hours.title' | transloco }}</h2>
    <p class="lead">{{ 'settings.hours.lead' | transloco }}</p>

    @if (hours.isLoading()) {
      <p role="status">{{ 'common.loading' | transloco }}</p>
    } @else if (hours.error()) {
      <p class="alert" role="alert">{{ 'errors.unknown' | transloco }}</p>
    } @else {
      @if (errorMessage(); as message) {
        <p class="alert" role="alert">{{ message }}</p>
      }
      <app-week-schedule-editor [(schedule)]="schedule" />
      <div class="actions">
        <button
          mat-flat-button
          type="button"
          [disabled]="saving() || hasProblems()"
          (click)="save()"
        >
          {{ 'settings.save' | transloco }}
        </button>
      </div>
    }
  `,
})
export class HoursTab {
  private readonly settings = inject(SettingsService);
  private readonly toast = inject(Toast);

  protected readonly hours = resource({ loader: () => this.settings.getOpeningHours() });
  protected readonly schedule = signal<WeekSchedule>(emptySchedule());
  protected readonly saving = signal(false);
  protected readonly errorCode = signal<string | null>(null);
  protected readonly errorMessage = injectErrorMessage(this.errorCode);
  protected readonly hasProblems = computed(
    () => Object.keys(scheduleProblems(this.schedule())).length > 0,
  );

  constructor() {
    effect(() => {
      const loaded = this.hours.value();
      if (loaded) {
        untracked(() => this.schedule.set(loaded));
      }
    });
  }

  protected async save(): Promise<void> {
    this.errorCode.set(null);
    this.saving.set(true);
    try {
      this.schedule.set(await this.settings.updateOpeningHours(this.schedule()));
      this.toast.show('settings.saved');
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    } finally {
      this.saving.set(false);
    }
  }
}
