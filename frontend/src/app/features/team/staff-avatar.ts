import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { initials, readableTextColor } from './staff-display';

/** A round badge with the person's initials on their calendar color. Decorative: the name is always next to it. */
@Component({
  selector: 'app-staff-avatar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    'aria-hidden': 'true',
    '[style.background]': 'color()',
    '[style.color]': 'textColor()',
    '[style.width.px]': 'size()',
    '[style.height.px]': 'size()',
    '[style.font-size.px]': 'size() * 0.4',
  },
  template: `{{ letters() }}`,
  styles: `
    :host {
      display: inline-flex;
      flex: none;
      align-items: center;
      justify-content: center;
      border-radius: 50%;
      font-weight: 600;
      letter-spacing: 0.02em;
      user-select: none;
    }
  `,
})
export class StaffAvatar {
  readonly name = input.required<string>();
  readonly color = input.required<string>();
  readonly size = input(40);

  protected readonly letters = computed(() => initials(this.name()));
  protected readonly textColor = computed(() => readableTextColor(this.color()));
}
