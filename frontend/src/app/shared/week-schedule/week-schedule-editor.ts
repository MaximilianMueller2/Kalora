import { ChangeDetectionStrategy, Component, computed, inject, model } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { LocaleFormatService } from '../../core/i18n/locale-format.service';
import {
  MAX_BLOCKS_PER_DAY,
  WEEKDAYS,
  WeekSchedule,
  Weekday,
  addBlock,
  copyFromPrevious,
  nextBlock,
  previousDay,
  removeBlock,
  scheduleProblems,
  setDayOpen,
  updateBlock,
} from './week-schedule.model';

/**
 * Edits a week of time blocks (several per day, e.g. 9-13 and 14-18). Used for the salon's opening hours and,
 * later, for staff working hours. Two-way bound: `[(schedule)]="..."`.
 */
@Component({
  selector: 'app-week-schedule-editor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatCheckboxModule, MatIconModule, TranslocoPipe],
  template: `
    @for (day of days; track day) {
      <fieldset class="day">
        <legend>{{ dayName(day) }}</legend>

        <div class="head">
          <mat-checkbox
            [checked]="schedule()[day].length > 0"
            (change)="schedule.set(setOpen(schedule(), day, $event.checked))"
          >
            {{ (schedule()[day].length > 0 ? 'schedule.open' : 'schedule.closed') | transloco }}
          </mat-checkbox>
          @if (previous(day); as before) {
            <button
              mat-button
              type="button"
              class="copy"
              (click)="schedule.set(copyPrevious(schedule(), day))"
            >
              {{ 'schedule.copyPrevious' | transloco: { day: dayName(before) } }}
            </button>
          }
        </div>

        @for (block of schedule()[day]; track $index) {
          <div class="block">
            <label>
              <span class="visually-hidden"
                >{{ 'schedule.start' | transloco }}, {{ dayName(day) }}</span
              >
              <input
                type="time"
                step="300"
                [value]="block.start"
                (change)="onTime(day, $index, 'start', $event)"
              />
            </label>
            <span aria-hidden="true">–</span>
            <label>
              <span class="visually-hidden"
                >{{ 'schedule.end' | transloco }}, {{ dayName(day) }}</span
              >
              <input
                type="time"
                step="300"
                [value]="block.end"
                (change)="onTime(day, $index, 'end', $event)"
              />
            </label>
            <button
              mat-icon-button
              type="button"
              class="icon"
              [attr.aria-label]="'schedule.removeBlock' | transloco"
              (click)="schedule.set(remove(schedule(), day, $index))"
            >
              <mat-icon aria-hidden="true">close</mat-icon>
            </button>
          </div>
        }

        @if (problems()[day]; as problem) {
          <p class="problem" role="alert">{{ 'schedule.problems.' + problem | transloco }}</p>
        }

        @if (schedule()[day].length > 0 && canAdd(day)) {
          <button mat-button type="button" class="add" (click)="schedule.set(add(schedule(), day))">
            <mat-icon aria-hidden="true">add</mat-icon>
            {{ 'schedule.addBlock' | transloco }}
          </button>
        }
      </fieldset>
    }
  `,
  styleUrl: './week-schedule-editor.scss',
})
export class WeekScheduleEditor {
  private readonly format = inject(LocaleFormatService);

  readonly schedule = model.required<WeekSchedule>();

  protected readonly days = WEEKDAYS;
  /** Problems per weekday, shown next to the blocks so the user sees what to fix before saving. */
  protected readonly problems = computed(() => scheduleProblems(this.schedule()));

  // Pure helpers, exposed for the template.
  protected readonly setOpen = setDayOpen;
  protected readonly copyPrevious = copyFromPrevious;
  protected readonly add = addBlock;
  protected readonly remove = removeBlock;
  protected readonly previous = previousDay;

  protected onTime(day: Weekday, index: number, field: 'start' | 'end', event: Event): void {
    this.schedule.set(
      updateBlock(this.schedule(), day, index, {
        [field]: (event.target as HTMLInputElement).value,
      }),
    );
  }

  protected dayName(day: Weekday): string {
    return this.format.weekdayName(day);
  }

  protected canAdd(day: Weekday): boolean {
    return (
      this.schedule()[day].length < MAX_BLOCKS_PER_DAY && nextBlock(this.schedule()[day]) !== null
    );
  }
}
