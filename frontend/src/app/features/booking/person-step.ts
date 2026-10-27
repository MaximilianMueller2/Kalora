import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { StaffAvatar } from '../../shared/ui/staff-avatar';
import { BookingService } from './booking.service';
import { PublicStaff } from './booking.models';
import { StepHeader } from './step-header';

@Component({
  selector: 'app-person-step',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, StaffAvatar, StepHeader, TranslocoPipe],
  styleUrl: './skeleton.scss',
  styles: `
    fieldset {
      display: grid;
      gap: 8px;
      margin: 0;
      padding: 0;
      border: 0;
      min-width: 0;
    }

    label {
      display: flex;
      align-items: center;
      gap: 12px;
      min-height: 64px;
      padding: 8px 16px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--mat-sys-corner-large);
      cursor: pointer;

      &:has(input:checked) {
        border: 2px solid var(--mat-sys-primary);
        padding: 7px 15px;
        background: var(--mat-sys-primary-container);
        color: var(--mat-sys-on-primary-container);
      }

      &:has(input:focus-visible) {
        outline: 2px solid var(--mat-sys-primary);
        outline-offset: 2px;
      }
    }

    input {
      position: absolute;
      opacity: 0;
      width: 1px;
      height: 1px;
    }

    .any {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 40px;
      height: 40px;
      border-radius: 50%;
      background: var(--mat-sys-surface-container-high);
      color: var(--mat-sys-on-surface);
    }

    .text {
      display: grid;
    }

    .name {
      font: var(--mat-sys-title-medium);
    }

    .hint {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }

    .skeleton {
      height: 64px;
      margin-bottom: 8px;
    }
  `,
  template: `
    <app-step-header
      [step]="2"
      titleKey="booking.steps.person.title"
      backLink="/{{ booking.slug() }}"
    />

    @if (booking.staff.hasValue()) {
      <fieldset>
        <legend class="visually-hidden">{{ 'booking.steps.person.title' | transloco }}</legend>
        <label>
          <input
            type="radio"
            name="person"
            [checked]="booking.selectedStaff() === null"
            (change)="pick(null)"
          />
          <span class="any" aria-hidden="true"><mat-icon>groups</mat-icon></span>
          <span class="text">
            <span class="name">{{ 'booking.steps.person.anyone' | transloco }}</span>
            <span class="hint">{{ 'booking.steps.person.anyoneHint' | transloco }}</span>
          </span>
        </label>
        @for (person of booking.staff.value(); track person.id) {
          <label>
            <input
              type="radio"
              name="person"
              [checked]="booking.selectedStaff()?.id === person.id"
              (change)="pick(person)"
            />
            <app-staff-avatar [name]="person.displayName" [color]="person.color" />
            <span class="name">{{ person.displayName }}</span>
          </label>
        }
      </fieldset>
      <div class="primary-action">
        <button mat-flat-button type="button" (click)="next()">
          {{ 'booking.next' | transloco }}
        </button>
      </div>
    } @else if (booking.staff.error()) {
      <p class="alert" role="alert">{{ 'errors.unknown' | transloco }}</p>
      <button mat-stroked-button type="button" (click)="booking.staff.reload()">
        {{ 'booking.retry' | transloco }}
      </button>
    } @else {
      <div aria-busy="true" role="status">
        <span class="visually-hidden">{{ 'common.loading' | transloco }}</span>
        @for (n of [1, 2, 3]; track n) {
          <span class="skeleton"></span>
        }
      </div>
    }
  `,
})
export class PersonStep {
  protected readonly booking = inject(BookingService);
  private readonly router = inject(Router);

  protected pick(person: PublicStaff | null): void {
    this.booking.selectStaff(person);
  }

  protected next(): void {
    void this.router.navigate(['/', this.booking.slug(), 'time']);
  }
}
