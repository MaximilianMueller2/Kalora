import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDropList } from '@angular/cdk/drag-drop';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  linkedSignal,
  resource,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { ApiError } from '../../core/http/api-error';
import { ErrorNotifier } from '../../core/http/error-notifier';
import { LocaleFormatService } from '../../core/i18n/locale-format.service';
import { Toast } from '../../core/ui/toast';
import { Catalog, Category, ServiceItem } from './catalog.models';
import { categoryIds, moveCategory, moveService, serviceIds } from './catalog-order';
import { CatalogService } from './catalog.service';
import { CategoryDialog, CategoryDialogData } from './dialogs/category-dialog';
import { ConfirmDialog, ConfirmDialogData } from './dialogs/confirm-dialog';
import { ServiceDialog, ServiceDialogData } from './dialogs/service-dialog';

@Component({
  selector: 'app-services-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CdkDrag,
    CdkDragHandle,
    CdkDropList,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    TranslocoPipe,
  ],
  styleUrl: './services-page.scss',
  template: `
    <header class="head">
      <h1>{{ 'services.title' | transloco }}</h1>
      @if (catalog(); as current) {
        @if (current.categories.length > 0) {
          <div class="head-actions">
            <button mat-stroked-button type="button" (click)="addCategory()">
              {{ 'services.category.add' | transloco }}
            </button>
            <button mat-flat-button type="button" (click)="addService()">
              {{ 'services.service.add' | transloco }}
            </button>
          </div>
        }
      }
    </header>

    @if (catalog(); as current) {
      @if (current.categories.length === 0) {
        <section class="empty" aria-labelledby="empty-title">
          <mat-icon aria-hidden="true">content_cut</mat-icon>
          <h2 id="empty-title">{{ 'services.empty.title' | transloco }}</h2>
          <p>{{ 'services.empty.text' | transloco }}</p>
          <div class="empty-actions">
            <button mat-flat-button type="button" [disabled]="busy()" (click)="applyTemplate()">
              {{ 'services.empty.template' | transloco }}
            </button>
            <button mat-stroked-button type="button" (click)="addCategory()">
              {{ 'services.empty.start' | transloco }}
            </button>
          </div>
        </section>
      } @else {
        <div
          cdkDropList
          class="categories"
          [cdkDropListData]="current"
          (cdkDropListDropped)="dropCategory($event)"
        >
          @for (
            category of current.categories;
            track category.id;
            let first = $first, last = $last
          ) {
            <section class="category" cdkDrag [cdkDragData]="category">
              <div class="category-head">
                <span class="handle" cdkDragHandle [attr.aria-label]="'services.drag' | transloco"
                  ><mat-icon aria-hidden="true">drag_indicator</mat-icon></span
                >
                <h2>{{ category.name }}</h2>
                <button
                  mat-icon-button
                  type="button"
                  [matMenuTriggerFor]="categoryMenu"
                  [matMenuTriggerData]="{ category: category, first: first, last: last }"
                  [attr.aria-label]="
                    'services.category.actions' | transloco: { name: category.name }
                  "
                >
                  <mat-icon aria-hidden="true">more_vert</mat-icon>
                </button>
              </div>

              @if (category.services.length === 0) {
                <p class="no-services">{{ 'services.category.empty' | transloco }}</p>
              } @else {
                <ul
                  cdkDropList
                  class="services"
                  [cdkDropListData]="category"
                  (cdkDropListDropped)="dropService(category, $event)"
                >
                  @for (
                    service of category.services;
                    track service.id;
                    let sFirst = $first, sLast = $last
                  ) {
                    <li cdkDrag [class.inactive]="!service.isActive">
                      <span
                        class="handle"
                        cdkDragHandle
                        [attr.aria-label]="'services.drag' | transloco"
                        ><mat-icon aria-hidden="true">drag_indicator</mat-icon></span
                      >
                      <button type="button" class="service-main" (click)="editService(service)">
                        <span class="service-name">{{ service.name }}</span>
                        <span class="service-meta">
                          {{ duration(service) }} ·
                          @if (service.priceIsFrom) {
                            {{
                              'services.service.priceFrom' | transloco: { price: price(service) }
                            }}
                          } @else {
                            {{ price(service) }}
                          }
                          @if (!service.isActive) {
                            · {{ 'services.service.inactiveBadge' | transloco }}
                          } @else if (!service.isBookableOnline) {
                            · {{ 'services.service.internalBadge' | transloco }}
                          }
                        </span>
                      </button>
                      <button
                        mat-icon-button
                        type="button"
                        [matMenuTriggerFor]="serviceMenu"
                        [matMenuTriggerData]="{ service: service, first: sFirst, last: sLast }"
                        [attr.aria-label]="
                          'services.service.actions' | transloco: { name: service.name }
                        "
                      >
                        <mat-icon aria-hidden="true">more_vert</mat-icon>
                      </button>
                    </li>
                  }
                </ul>
              }
              <button mat-button type="button" class="add-inline" (click)="addService(category.id)">
                <mat-icon aria-hidden="true">add</mat-icon>
                {{ 'services.service.add' | transloco }}
              </button>
            </section>
          }
        </div>
      }
    } @else if (loaded.error()) {
      <p class="alert" role="alert">{{ 'errors.unknown' | transloco }}</p>
    } @else {
      <p role="status">{{ 'common.loading' | transloco }}</p>
    }

    <mat-menu #categoryMenu="matMenu">
      <ng-template matMenuContent let-category="category" let-first="first" let-last="last">
        <button mat-menu-item type="button" (click)="renameCategory(category)">
          <mat-icon aria-hidden="true">edit</mat-icon>{{ 'services.category.rename' | transloco }}
        </button>
        <button
          mat-menu-item
          type="button"
          [disabled]="first"
          (click)="shiftCategory(category, -1)"
        >
          <mat-icon aria-hidden="true">arrow_upward</mat-icon>{{ 'services.moveUp' | transloco }}
        </button>
        <button mat-menu-item type="button" [disabled]="last" (click)="shiftCategory(category, 1)">
          <mat-icon aria-hidden="true">arrow_downward</mat-icon
          >{{ 'services.moveDown' | transloco }}
        </button>
        <button
          mat-menu-item
          type="button"
          [disabled]="category.services.length > 0"
          (click)="deleteCategory(category)"
        >
          <mat-icon aria-hidden="true">delete</mat-icon>{{ 'services.category.delete' | transloco }}
        </button>
      </ng-template>
    </mat-menu>

    <mat-menu #serviceMenu="matMenu">
      <ng-template matMenuContent let-service="service" let-first="first" let-last="last">
        <button mat-menu-item type="button" (click)="editService(service)">
          <mat-icon aria-hidden="true">edit</mat-icon>{{ 'services.service.edit' | transloco }}
        </button>
        <button mat-menu-item type="button" [disabled]="first" (click)="shiftService(service, -1)">
          <mat-icon aria-hidden="true">arrow_upward</mat-icon>{{ 'services.moveUp' | transloco }}
        </button>
        <button mat-menu-item type="button" [disabled]="last" (click)="shiftService(service, 1)">
          <mat-icon aria-hidden="true">arrow_downward</mat-icon
          >{{ 'services.moveDown' | transloco }}
        </button>
        <button mat-menu-item type="button" (click)="toggleActive(service)">
          <mat-icon aria-hidden="true">{{
            service.isActive ? 'visibility_off' : 'visibility'
          }}</mat-icon>
          {{
            (service.isActive ? 'services.service.deactivate' : 'services.service.activate')
              | transloco
          }}
        </button>
        @if (!service.hasAppointments) {
          <button mat-menu-item type="button" (click)="deleteService(service)">
            <mat-icon aria-hidden="true">delete</mat-icon
            >{{ 'services.service.delete' | transloco }}
          </button>
        }
      </ng-template>
    </mat-menu>
  `,
})
export class ServicesPage {
  private readonly api = inject(CatalogService);
  private readonly dialog = inject(MatDialog);
  private readonly toast = inject(Toast);
  private readonly format = inject(LocaleFormatService);
  private readonly errors = inject(ErrorNotifier);

  protected readonly loaded = resource({ loader: () => this.api.get() });
  /** The catalog as shown: the loaded one, replaced by the answer of every change (and by drag & drop right away). */
  protected readonly catalog = linkedSignal<Catalog | undefined>(() =>
    this.loaded.hasValue() ? this.loaded.value() : undefined,
  );
  protected readonly busy = signal(false);

  protected price(service: ServiceItem): string {
    return this.format.formatMoney(service.priceCents, service.currency);
  }

  protected duration(service: ServiceItem): string {
    return this.format.formatDuration(service.durationMinutes);
  }

  protected async addCategory(): Promise<void> {
    await this.openEditor<CategoryDialogData>(CategoryDialog, {});
  }

  protected async renameCategory(category: Category): Promise<void> {
    await this.openEditor<CategoryDialogData>(CategoryDialog, { category });
  }

  protected async addService(categoryId?: string): Promise<void> {
    const categories = this.catalog()?.categories ?? [];
    await this.openEditor<ServiceDialogData>(ServiceDialog, { categories, categoryId });
  }

  protected async editService(service: ServiceItem): Promise<void> {
    const categories = this.catalog()?.categories ?? [];
    await this.openEditor<ServiceDialogData>(ServiceDialog, { categories, service });
  }

  protected async deleteCategory(category: Category): Promise<void> {
    const confirmed = await this.confirm({
      titleKey: 'services.category.deleteTitle',
      textKey: 'services.category.deleteText',
      textParams: { name: category.name },
      confirmKey: 'services.category.delete',
    });
    if (confirmed) {
      await this.change(() => this.api.deleteCategory(category.id), 'services.removed');
    }
  }

  protected async deleteService(service: ServiceItem): Promise<void> {
    const confirmed = await this.confirm({
      titleKey: 'services.service.deleteTitle',
      textKey: 'services.service.deleteText',
      textParams: { name: service.name },
      confirmKey: 'services.service.delete',
    });
    if (confirmed) {
      await this.change(() => this.api.deleteService(service.id), 'services.removed');
    }
  }

  protected toggleActive(service: ServiceItem): Promise<void> {
    return this.change(
      () => this.api.updateService(service.id, { ...service, isActive: !service.isActive }),
      'services.saved',
    );
  }

  protected applyTemplate(): Promise<void> {
    return this.change(() => this.api.applyTemplate(null), 'services.templateApplied');
  }

  protected dropCategory(event: CdkDragDrop<Catalog>): void {
    this.reorder(moveCategory(event.container.data, event.previousIndex, event.currentIndex), (c) =>
      this.api.reorderCategories(categoryIds(c)),
    );
  }

  protected dropService(category: Category, event: CdkDragDrop<Category>): void {
    this.shiftServiceTo(category, event.previousIndex, event.currentIndex);
  }

  protected shiftCategory(category: Category, offset: number): void {
    const current = this.catalog();
    const from = current?.categories.findIndex((c) => c.id === category.id) ?? -1;
    if (current && from >= 0) {
      this.reorder(moveCategory(current, from, from + offset), (c) =>
        this.api.reorderCategories(categoryIds(c)),
      );
    }
  }

  protected shiftService(service: ServiceItem, offset: number): void {
    const category = this.catalog()?.categories.find((c) => c.id === service.categoryId);
    const from = category?.services.findIndex((s) => s.id === service.id) ?? -1;
    if (category && from >= 0) {
      this.shiftServiceTo(category, from, from + offset);
    }
  }

  private shiftServiceTo(category: Category, from: number, to: number): void {
    const current = this.catalog();
    if (current) {
      this.reorder(moveService(current, category.id, from, to), (c) =>
        this.api.reorderServices(category.id, serviceIds(c, category.id)),
      );
    }
  }

  /** Shows the new order at once and keeps the server's answer; if saving fails the previous order comes back. */
  private async reorder(next: Catalog, save: (next: Catalog) => Promise<Catalog>): Promise<void> {
    const previous = this.catalog();
    this.catalog.set(next);
    try {
      this.catalog.set(await save(next));
    } catch {
      this.catalog.set(previous);
      this.loaded.reload();
    }
  }

  /** Runs a change; failures are shown as a snackbar with the translated error code. */
  private async change(run: () => Promise<Catalog>, successKey: string): Promise<void> {
    this.busy.set(true);
    try {
      this.catalog.set(await run());
      this.toast.show(successKey);
    } catch (error) {
      if (error instanceof ApiError) {
        this.errors.show(error);
      }
    } finally {
      this.busy.set(false);
    }
  }

  private async openEditor<D>(
    component: new (...args: never[]) => unknown,
    data: D,
  ): Promise<void> {
    const result = await firstValueFrom(
      this.dialog
        .open<unknown, D, Catalog | undefined>(component, {
          data,
          width: '560px',
          maxWidth: '95vw',
        })
        .afterClosed(),
    );
    if (result) {
      this.catalog.set(result);
      this.toast.show('services.saved');
    }
  }

  private async confirm(data: ConfirmDialogData): Promise<boolean> {
    const confirmed = await firstValueFrom(
      this.dialog
        .open<ConfirmDialog, ConfirmDialogData, boolean>(ConfirmDialog, { data, maxWidth: '95vw' })
        .afterClosed(),
    );
    return confirmed === true;
  }
}
