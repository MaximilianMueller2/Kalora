import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiError } from '../../core/http/api-error';
import { Toast } from '../../core/ui/toast';
import { injectErrorMessage } from '../../shared/forms/server-error';
import { targetValue } from '../../shared/forms/target-value';
import { ServiceItem } from '../services/catalog.models';
import { CatalogService } from '../services/catalog.service';
import { TeamService } from '../team/team.service';
import { fromTimeInput, toTimeInput } from './appointment-panel';
import { instantAtMinutes, localDate, minutesOfDay } from './calendar-geometry';
import { CalendarService } from './calendar.service';
import { CustomerHit } from './calendar.models';

export interface QuickCreateData {
  /** The tapped place, as an instant. */
  startUtc: string;
  staffId: string;
  people: { staffId: string; displayName: string }[];
  zone: string;
  slotMinutes: number;
}

type Mode = 'appointment' | 'block';

const SEARCH_DELAY_MS = 250;
const DEFAULT_BLOCK_MINUTES = 30;

/** What a tap on free space leads to: a phone or walk-in appointment, or a blocker (break, private time). Closes with `true` once something was saved. */
@Component({
  selector: 'app-quick-create-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, MatButtonModule, MatDialogModule, TranslocoPipe],
  styleUrl: './quick-create-dialog.scss',
  templateUrl: './quick-create-dialog.html',
})
export class QuickCreateDialog {
  protected readonly data = inject<QuickCreateData>(MAT_DIALOG_DATA);
  private readonly ref = inject<MatDialogRef<QuickCreateDialog, boolean>>(MatDialogRef);
  private readonly calendar = inject(CalendarService);
  private readonly catalog = inject(CatalogService);
  private readonly team = inject(TeamService);
  private readonly toast = inject(Toast);

  protected readonly valueOf = targetValue;
  protected readonly mode = signal<Mode>('appointment');
  protected readonly staffId = signal(this.data.staffId);
  protected readonly date = signal(localDate(this.data.startUtc, this.data.zone));
  protected readonly start = signal(
    toTimeInput(
      minutesOfDay(
        this.data.startUtc,
        localDate(this.data.startUtc, this.data.zone),
        this.data.zone,
      ),
    ),
  );
  protected readonly end = signal('');
  protected readonly step = this.data.slotMinutes * 60;

  protected readonly serviceId = signal('');
  protected readonly customerName = signal('');
  protected readonly customerEmail = signal('');
  protected readonly customerPhone = signal('');
  protected readonly chosenCustomer = signal<CustomerHit | null>(null);
  protected readonly hits = signal<CustomerHit[]>([]);
  protected readonly note = signal('');
  protected readonly label = signal('');

  protected readonly busy = signal(false);
  protected readonly errorCode = signal<string | null>(null);
  protected readonly error = injectErrorMessage(this.errorCode);

  private readonly services = signal<ServiceItem[]>([]);
  private readonly offered = signal<Set<string> | null>(null);
  protected readonly servicesOfPerson = computed(() => {
    const offered = this.offered();
    return offered ? this.services().filter((service) => offered.has(service.id)) : [];
  });
  protected readonly canSave = computed(() =>
    this.mode() === 'block'
      ? this.end() !== '' && this.start() !== ''
      : this.serviceId() !== '' &&
        (this.chosenCustomer() !== null || this.customerName().trim() !== ''),
  );

  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    const startMinute = minutesOfDay(this.data.startUtc, this.date(), this.data.zone);
    this.end.set(toTimeInput(startMinute + DEFAULT_BLOCK_MINUTES));
    void this.catalog
      .get()
      .then((catalog) =>
        this.services.set(
          catalog.categories
            .flatMap((category) => category.services)
            .filter((service) => service.isActive),
        ),
      );
    effect(() => {
      const id = this.staffId();
      untracked(() => void this.loadOffered(id));
    });
  }

  protected typeName(value: string): void {
    this.customerName.set(value);
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => void this.search(value), SEARCH_DELAY_MS);
  }

  protected choose(hit: CustomerHit): void {
    this.chosenCustomer.set(hit);
    this.hits.set([]);
  }

  protected clearCustomer(): void {
    this.chosenCustomer.set(null);
  }

  protected async save(): Promise<void> {
    this.busy.set(true);
    this.errorCode.set(null);
    try {
      const startMinute = fromTimeInput(this.start());
      const startUtc = instantAtMinutes(this.date(), startMinute, this.data.zone);
      if (this.mode() === 'block') {
        await this.calendar.createBlock({
          staffId: this.staffId(),
          startUtc,
          endUtc: instantAtMinutes(this.date(), fromTimeInput(this.end()), this.data.zone),
          label: this.label().trim() || null,
        });
        this.toast.show('calendar.create.blockSaved');
      } else {
        const known = this.chosenCustomer();
        await this.calendar.createAppointment({
          serviceId: this.serviceId(),
          staffId: this.staffId(),
          startUtc,
          customerId: known?.id ?? null,
          customerName: known ? null : this.customerName().trim(),
          customerEmail: known ? null : this.customerEmail().trim() || null,
          customerPhone: known ? null : this.customerPhone().trim() || null,
          note: this.note().trim() || null,
        });
        this.toast.show('calendar.create.appointmentSaved');
      }
      this.ref.close(true);
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    } finally {
      this.busy.set(false);
    }
  }

  private async loadOffered(staffId: string): Promise<void> {
    this.offered.set(null);
    this.serviceId.set('');
    try {
      const skills = await this.team.getSkills(staffId);
      this.offered.set(new Set(skills.skills.map((skill) => skill.serviceId)));
    } catch {
      this.offered.set(new Set());
    }
  }

  private async search(text: string): Promise<void> {
    if (text.trim().length < 2 || this.chosenCustomer()) {
      this.hits.set([]);
      return;
    }
    try {
      this.hits.set(await this.calendar.searchCustomers(text.trim()));
    } catch {
      this.hits.set([]);
    }
  }
}
