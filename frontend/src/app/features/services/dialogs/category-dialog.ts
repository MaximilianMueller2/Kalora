import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormField, FormRoot, form, maxLength, required } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiError } from '../../../core/http/api-error';
import { fieldErrorText } from '../../../shared/forms/field-error';
import { injectErrorMessage } from '../../../shared/forms/server-error';
import { Catalog, Category } from '../catalog.models';
import { CatalogService } from '../catalog.service';

export interface CategoryDialogData {
  /** Set when renaming. */
  category?: Category;
}

/** Creates or renames a category and closes with the new catalog. */
@Component({
  selector: 'app-category-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormField,
    FormRoot,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    TranslocoPipe,
  ],
  styleUrl: '../../../shared/dialogs/dialog.scss',
  template: `
    <h2 mat-dialog-title>
      {{
        (data.category ? 'services.category.renameTitle' : 'services.category.addTitle') | transloco
      }}
    </h2>
    <form [formRoot]="form">
      <mat-dialog-content>
        @if (errorMessage(); as message) {
          <p class="alert" role="alert">{{ message }}</p>
        }
        <mat-form-field appearance="outline">
          <mat-label>{{ 'services.category.name' | transloco }}</mat-label>
          <input matInput type="text" autocomplete="off" [formField]="form.name" />
          @if (errorText(form.name().errors()); as error) {
            <mat-error>{{ error.key | transloco: error.params }}</mat-error>
          }
        </mat-form-field>
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
})
export class CategoryDialog {
  protected readonly data = inject<CategoryDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<CategoryDialog, Catalog>>(MatDialogRef);
  private readonly catalog = inject(CatalogService);

  protected readonly errorText = fieldErrorText;
  protected readonly errorCode = signal<string | null>(null);
  protected readonly errorMessage = injectErrorMessage(this.errorCode);

  private readonly model = signal({ name: this.data.category?.name ?? '' });

  protected readonly form = form(
    this.model,
    (path) => {
      required(path.name);
      maxLength(path.name, 100);
    },
    { submission: { action: () => this.save() } },
  );

  private async save(): Promise<undefined> {
    this.errorCode.set(null);
    const name = this.model().name.trim();
    try {
      const result = this.data.category
        ? await this.catalog.renameCategory(this.data.category.id, name)
        : await this.catalog.createCategory(name);
      this.dialogRef.close(result);
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    }
    return undefined;
  }
}
