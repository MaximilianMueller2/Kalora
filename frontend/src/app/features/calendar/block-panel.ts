import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiError } from '../../core/http/api-error';
import { LocaleFormatService } from '../../core/i18n/locale-format.service';
import { Toast } from '../../core/ui/toast';
import { injectErrorMessage } from '../../shared/forms/server-error';
import { CalendarService } from './calendar.service';
import { CalendarBlock } from './calendar.models';

/** A blocker (break, private time): when it is, and removing it. */
@Component({
  selector: 'app-block-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, TranslocoPipe],
  styleUrl: './panel.scss',
  template: `
    <header class="top">
      <h2>{{ block().label || ('calendar.block.default' | transloco) }}</h2>
      <button
        mat-icon-button
        type="button"
        [attr.aria-label]="'common.close' | transloco"
        (click)="closed.emit()"
      >
        <mat-icon aria-hidden="true">close</mat-icon>
      </button>
    </header>
    @if (error(); as message) {
      <p class="alert" role="alert">{{ message }}</p>
    }
    <dl>
      <dt>{{ 'calendar.panel.when' | transloco }}</dt>
      <dd>{{ when() }}</dd>
    </dl>
    <div class="actions">
      <button
        mat-stroked-button
        type="button"
        class="danger"
        [disabled]="busy()"
        (click)="remove()"
      >
        <mat-icon aria-hidden="true">delete</mat-icon>
        {{ 'calendar.block.remove' | transloco }}
      </button>
    </div>
  `,
})
export class BlockPanel {
  private readonly calendar = inject(CalendarService);
  private readonly toast = inject(Toast);
  private readonly format = inject(LocaleFormatService);

  readonly block = input.required<CalendarBlock>();
  readonly zone = input.required<string>();

  readonly closed = output<void>();
  readonly changed = output<void>();

  protected readonly busy = signal(false);
  protected readonly errorCode = signal<string | null>(null);
  protected readonly error = injectErrorMessage(this.errorCode);
  protected readonly when = computed(() => {
    const block = this.block();
    const zone = this.zone();
    return `${this.format.formatDate(block.startUtc, 'dateTime', zone)} – ${this.format.formatDate(block.endUtc, 'dateTime', zone)}`;
  });

  protected async remove(): Promise<void> {
    this.busy.set(true);
    try {
      await this.calendar.deleteBlock(this.block().id);
      this.toast.show('calendar.block.removed');
      this.changed.emit();
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    } finally {
      this.busy.set(false);
    }
  }
}
