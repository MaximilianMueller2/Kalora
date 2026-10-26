import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormField, FormRoot, form, maxLength, required, validate } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiError } from '../../../core/http/api-error';
import { LocaleFormatService } from '../../../core/i18n/locale-format.service';
import { fieldErrorText } from '../../../shared/forms/field-error';
import { injectErrorMessage } from '../../../shared/forms/server-error';
import {
  CATALOG_LIMITS,
  Catalog,
  Category,
  SaveServiceRequest,
  ServiceItem,
} from '../catalog.models';
import { CatalogService } from '../catalog.service';
import { centsToInputText, minuteOptions, parsePriceToCents } from '../price';

export interface ServiceDialogData {
  categories: Category[];
  /** Set when editing. */
  service?: ServiceItem;
  /** Category preselected when adding. */
  categoryId?: string;
}

const DEFAULT_DURATION = 30;

interface ServiceModel {
  categoryId: string;
  name: string;
  description: string;
  price: string;
  priceIsFrom: boolean;
  durationMinutes: string;
  bufferBeforeMinutes: string;
  bufferAfterMinutes: string;
  isBookableOnline: boolean;
  isActive: boolean;
}

/** Creates or edits a service and closes with the new catalog. The price is typed in euros and sent as cents. */
@Component({
  selector: 'app-service-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormField,
    FormRoot,
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    TranslocoPipe,
  ],
  styleUrl: '../../../shared/dialogs/dialog.scss',
  template: `
    <h2 mat-dialog-title>
      {{ (data.service ? 'services.service.editTitle' : 'services.service.addTitle') | transloco }}
    </h2>
    <form [formRoot]="form">
      <mat-dialog-content>
        @if (errorMessage(); as message) {
          <p class="alert" role="alert">{{ message }}</p>
        }

        <mat-form-field appearance="outline">
          <mat-label>{{ 'services.service.name' | transloco }}</mat-label>
          <input matInput type="text" autocomplete="off" [formField]="form.name" />
          @if (errorText(form.name().errors()); as error) {
            <mat-error>{{ error.key | transloco: error.params }}</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'services.service.category' | transloco }}</mat-label>
          <select matNativeControl [formField]="form.categoryId">
            @for (category of data.categories; track category.id) {
              <option [value]="category.id">{{ category.name }}</option>
            }
          </select>
        </mat-form-field>

        <div class="row">
          <mat-form-field appearance="outline">
            <mat-label>{{ 'services.service.price' | transloco }}</mat-label>
            <input
              matInput
              type="text"
              inputmode="decimal"
              autocomplete="off"
              [formField]="form.price"
            />
            <span matTextSuffix>€</span>
            @if (errorText(form.price().errors()); as error) {
              <mat-error>{{ error.key | transloco: error.params }}</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>{{ 'services.service.duration' | transloco }}</mat-label>
            <select matNativeControl [formField]="form.durationMinutes">
              @for (minutes of durations; track minutes) {
                <option [value]="minutes">{{ minutesLabel(minutes) }}</option>
              }
            </select>
          </mat-form-field>
        </div>

        <mat-checkbox [formField]="form.priceIsFrom">{{
          'services.service.priceIsFrom' | transloco
        }}</mat-checkbox>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'services.service.description' | transloco }}</mat-label>
          <textarea matInput rows="2" [formField]="form.description"></textarea>
          @if (errorText(form.description().errors()); as error) {
            <mat-error>{{ error.key | transloco: error.params }}</mat-error>
          }
        </mat-form-field>

        <mat-checkbox [formField]="form.isBookableOnline">{{
          'services.service.bookableOnline' | transloco
        }}</mat-checkbox>
        <mat-checkbox [formField]="form.isActive">{{
          'services.service.active' | transloco
        }}</mat-checkbox>

        <details [open]="hasBuffers">
          <summary>{{ 'services.service.advanced' | transloco }}</summary>
          <p class="hint">{{ 'services.service.bufferHint' | transloco }}</p>
          <div class="row">
            <mat-form-field appearance="outline">
              <mat-label>{{ 'services.service.bufferBefore' | transloco }}</mat-label>
              <select matNativeControl [formField]="form.bufferBeforeMinutes">
                @for (minutes of buffers; track minutes) {
                  <option [value]="minutes">{{ minutesLabel(minutes) }}</option>
                }
              </select>
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>{{ 'services.service.bufferAfter' | transloco }}</mat-label>
              <select matNativeControl [formField]="form.bufferAfterMinutes">
                @for (minutes of buffers; track minutes) {
                  <option [value]="minutes">{{ minutesLabel(minutes) }}</option>
                }
              </select>
            </mat-form-field>
          </div>
        </details>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>
          {{ 'common.cancel' | transloco }}
        </button>
        <button mat-flat-button type="submit" [disabled]="form().submitting()">
          {{ 'services.save' | transloco }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    mat-checkbox {
      display: block;
    }

    details {
      margin-top: 8px;
    }

    summary {
      min-height: 44px;
      display: flex;
      align-items: center;
      cursor: pointer;
      font: var(--mat-sys-title-small);
    }

    .hint {
      margin: 0 0 12px;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
  `,
})
export class ServiceDialog {
  protected readonly data = inject<ServiceDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<ServiceDialog, Catalog>>(MatDialogRef);
  private readonly catalog = inject(CatalogService);
  private readonly format = inject(LocaleFormatService);

  protected readonly errorText = fieldErrorText;
  protected readonly errorCode = signal<string | null>(null);
  protected readonly errorMessage = injectErrorMessage(this.errorCode);

  protected readonly durations = minuteOptions(
    CATALOG_LIMITS.stepMinutes,
    CATALOG_LIMITS.maxDurationMinutes,
  );
  protected readonly buffers = minuteOptions(0, CATALOG_LIMITS.maxBufferMinutes);
  protected readonly hasBuffers =
    (this.data.service?.bufferBeforeMinutes ?? 0) + (this.data.service?.bufferAfterMinutes ?? 0) >
    0;

  private readonly model = signal<ServiceModel>(this.initialModel());

  protected readonly form = form(
    this.model,
    (path) => {
      required(path.name);
      maxLength(path.name, 200);
      maxLength(path.description, 2000);
      validate(path.price, ({ value }) =>
        parsePriceToCents(value()) === null ? { kind: 'priceInvalid' } : undefined,
      );
    },
    { submission: { action: () => this.save() } },
  );

  protected minutesLabel(minutes: number): string {
    return this.format.formatDuration(minutes);
  }

  private initialModel(): ServiceModel {
    const service = this.data.service;
    const separator = this.format.formatNumber(1.5).charAt(1);
    return {
      categoryId: service?.categoryId ?? this.data.categoryId ?? this.data.categories[0]?.id ?? '',
      name: service?.name ?? '',
      description: service?.description ?? '',
      price: service ? centsToInputText(service.priceCents, separator) : '',
      priceIsFrom: service?.priceIsFrom ?? false,
      durationMinutes: String(service?.durationMinutes ?? DEFAULT_DURATION),
      bufferBeforeMinutes: String(service?.bufferBeforeMinutes ?? 0),
      bufferAfterMinutes: String(service?.bufferAfterMinutes ?? 0),
      isBookableOnline: service?.isBookableOnline ?? true,
      isActive: service?.isActive ?? true,
    };
  }

  private async save(): Promise<undefined> {
    this.errorCode.set(null);
    const model = this.model();
    const request: SaveServiceRequest = {
      categoryId: model.categoryId,
      name: model.name.trim(),
      description: model.description.trim() || null,
      priceCents: parsePriceToCents(model.price) ?? 0,
      priceIsFrom: model.priceIsFrom,
      durationMinutes: Number(model.durationMinutes),
      bufferBeforeMinutes: Number(model.bufferBeforeMinutes),
      bufferAfterMinutes: Number(model.bufferAfterMinutes),
      isBookableOnline: model.isBookableOnline,
      isActive: model.isActive,
    };
    try {
      const result = this.data.service
        ? await this.catalog.updateService(this.data.service.id, request)
        : await this.catalog.createService(request);
      this.dialogRef.close(result);
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    }
    return undefined;
  }
}
