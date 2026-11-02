import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  resource,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiError } from '../../core/http/api-error';
import { LocaleFormatService } from '../../core/i18n/locale-format.service';
import { Toast } from '../../core/ui/toast';
import { injectErrorMessage } from '../../shared/forms/server-error';
import { AdminService } from './admin.service';
import {
  AdminSalonDetail,
  PLAN_CODES,
  PlanCode,
  SALON_STATUSES,
  SalonStatus,
} from './admin.models';

/** Days the trial can be extended with one click. */
const EXTENSIONS = [7, 14, 30] as const;

/** One salon: master data, subscription and the admin's actions. Billing is manual, so the admin sets plan and status here. */
@Component({
  selector: 'app-admin-salon-detail-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, RouterLink, TranslocoPipe],
  templateUrl: './salon-detail-page.html',
  styleUrl: './admin.scss',
})
export class SalonDetailPage {
  private readonly service = inject(AdminService);
  private readonly toast = inject(Toast);
  private readonly format = inject(LocaleFormatService);

  /** From the route: `/admin/salons/:id`. */
  readonly id = input.required<string>();

  protected readonly plans = PLAN_CODES;
  protected readonly statuses = SALON_STATUSES;
  protected readonly extensions = EXTENSIONS;

  private readonly loaded = resource({
    params: () => this.id(),
    loader: ({ params }) => this.service.get(params),
  });

  /** The latest answer from the server: what was loaded, or what the last change returned. */
  private readonly changed = signal<AdminSalonDetail | null>(null);
  protected readonly salon = computed(
    () => this.changed() ?? (this.loaded.hasValue() ? this.loaded.value() : null),
  );
  protected readonly loading = computed(() => this.loaded.isLoading() && !this.salon());
  protected readonly failed = computed(() => this.loaded.status() === 'error' && !this.salon());

  protected readonly note = signal('');
  protected readonly busy = signal(false);
  protected readonly errorCode = signal<string | null>(null);
  protected readonly error = injectErrorMessage(this.errorCode);
  protected readonly suspended = computed(() => this.salon()?.status === 'Suspended');
  protected readonly inTrial = computed(() => this.salon()?.status === 'Trial');

  constructor() {
    // Another salon (the address changed) starts fresh.
    effect(() => {
      this.id();
      this.changed.set(null);
    });
    // The note field follows the server until the admin types in it.
    effect(() => {
      this.note.set(this.salon()?.adminNote ?? '');
    });
  }

  protected date(iso: string): string {
    return this.format.formatDate(iso, 'date');
  }

  protected dateTime(iso: string): string {
    return `${this.format.formatDate(iso, 'date')}, ${this.format.formatDate(iso, 'time')}`;
  }

  protected setPlan(event: Event): Promise<void> {
    const plan = (event.target as HTMLSelectElement).value as PlanCode;
    return this.run((id) => this.service.setPlan(id, plan));
  }

  protected setStatus(event: Event): Promise<void> {
    const status = (event.target as HTMLSelectElement).value as SalonStatus;
    return this.run((id) => this.service.setStatus(id, status));
  }

  protected extend(days: number): Promise<void> {
    return this.run((id) => this.service.extendTrial(id, days));
  }

  protected toggleSuspension(): Promise<void> {
    return this.run((id) =>
      this.suspended() ? this.service.unsuspend(id) : this.service.suspend(id),
    );
  }

  protected onNote(event: Event): void {
    this.note.set((event.target as HTMLTextAreaElement).value);
  }

  protected saveNote(): Promise<void> {
    return this.run((id) => this.service.setNote(id, this.note()));
  }

  private async run(action: (id: string) => Promise<AdminSalonDetail>): Promise<void> {
    this.busy.set(true);
    this.errorCode.set(null);
    try {
      this.changed.set(await action(this.id()));
      this.toast.show('admin.saved');
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    } finally {
      this.busy.set(false);
    }
  }
}
