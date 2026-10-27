import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterNextRender,
  input,
  viewChild,
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';

export const BOOKING_STEPS = 4;

/** Back link, "Step 2 of 4" and the title of a step. The title takes focus so screen readers announce the new step. */
@Component({
  selector: 'app-step-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, RouterLink, TranslocoPipe],
  template: `
    <div class="top">
      @if (backLink(); as link) {
        <a class="back" [routerLink]="link">
          <mat-icon aria-hidden="true">arrow_back</mat-icon>
          {{ 'booking.back' | transloco }}
        </a>
      }
      <span class="count">{{ 'booking.stepOf' | transloco: { step: step(), total: total } }}</span>
    </div>
    <div class="bar" aria-hidden="true">
      @for (n of dots; track n) {
        <span [class.done]="n <= step()"></span>
      }
    </div>
    <h2 #title tabindex="-1">{{ titleKey() | transloco }}</h2>
  `,
  styles: `
    :host {
      display: block;
      margin-bottom: 16px;
    }

    .top {
      display: flex;
      align-items: center;
      justify-content: space-between;
      min-height: 44px;
    }

    .back {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      min-height: 44px;
      padding-right: 12px;
      color: var(--mat-sys-primary);
      text-decoration: none;

      &:focus-visible {
        outline: 2px solid var(--mat-sys-primary);
        outline-offset: 2px;
      }
    }

    .count {
      margin-left: auto;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-label-large);
    }

    .bar {
      display: flex;
      gap: 6px;
      margin: 4px 0 16px;

      span {
        flex: 1;
        height: 4px;
        border-radius: 2px;
        background: var(--mat-sys-outline-variant);

        &.done {
          background: var(--mat-sys-primary);
        }
      }
    }

    h2 {
      margin: 0;
      font: var(--mat-sys-headline-small);
      outline: none;
    }
  `,
})
export class StepHeader {
  readonly step = input.required<number>();
  readonly titleKey = input.required<string>();
  readonly backLink = input<string | readonly string[] | null>(null);

  protected readonly total = BOOKING_STEPS;
  protected readonly dots = Array.from({ length: BOOKING_STEPS }, (_, index) => index + 1);
  private readonly title = viewChild.required<ElementRef<HTMLElement>>('title');

  constructor() {
    afterNextRender(() => this.title().nativeElement.focus({ preventScroll: true }));
  }
}
