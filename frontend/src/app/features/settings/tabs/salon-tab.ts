import { DOCUMENT } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  resource,
  signal,
  untracked,
} from '@angular/core';
import {
  FormField,
  FormRoot,
  disabled,
  form,
  maxLength,
  required,
  validate,
} from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiError } from '../../../core/http/api-error';
import { LANGUAGE_NAMES, SUPPORTED_LANGUAGES } from '../../../core/i18n/languages';
import { Toast } from '../../../core/ui/toast';
import { fieldErrorText } from '../../../shared/forms/field-error';
import { injectErrorMessage } from '../../../shared/forms/server-error';
import { SALON_INDUSTRIES, SalonIndustry } from '../../auth/auth.models';
import { AuthService } from '../../auth/auth.service';
import { SalonProfile, TIME_ZONES } from '../settings.models';
import { SettingsService } from '../settings.service';

const SLUG_PATTERN = /^[a-z0-9]+(-[a-z0-9]+)*$/;
const PHONE_PATTERN = /^[0-9+()./ -]{3,50}$/;

interface ProfileModel {
  name: string;
  slug: string;
  industry: SalonIndustry;
  street: string;
  postalCode: string;
  city: string;
  phone: string;
  timeZone: string;
  defaultLanguage: string;
  imprintText: string;
  privacyPolicyUrl: string;
}

const EMPTY: ProfileModel = {
  name: '',
  slug: '',
  industry: 'Hair',
  street: '',
  postalCode: '',
  city: '',
  phone: '',
  timeZone: 'Europe/Berlin',
  defaultLanguage: 'de',
  imprintText: '',
  privacyPolicyUrl: '',
};

const toModel = (profile: SalonProfile): ProfileModel => ({
  ...profile,
  imprintText: profile.imprintText ?? '',
  privacyPolicyUrl: profile.privacyPolicyUrl ?? '',
});

const toProfile = (model: ProfileModel): SalonProfile => ({
  ...model,
  imprintText: model.imprintText.trim() || null,
  privacyPolicyUrl: model.privacyPolicyUrl.trim() || null,
});

function isHttpUrl(value: string): boolean {
  try {
    return ['http:', 'https:'].includes(new URL(value).protocol);
  } catch {
    return false;
  }
}

@Component({
  selector: 'app-salon-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormField,
    FormRoot,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    TranslocoPipe,
  ],
  styleUrl: '../settings-tabs.scss',
  template: `
    <h2>{{ 'settings.salon.title' | transloco }}</h2>
    <p class="lead">{{ 'settings.salon.lead' | transloco }}</p>

    @if (profile.isLoading()) {
      <p role="status">{{ 'common.loading' | transloco }}</p>
    } @else if (profile.error()) {
      <p class="alert" role="alert">{{ 'errors.unknown' | transloco }}</p>
    } @else {
      @if (errorMessage(); as message) {
        <p class="alert" role="alert">{{ message }}</p>
      }

      <form [formRoot]="form">
        <mat-form-field appearance="outline">
          <mat-label>{{ 'settings.salon.name' | transloco }}</mat-label>
          <input matInput type="text" autocomplete="organization" [formField]="form.name" />
          @if (errorText(form.name().errors()); as error) {
            <mat-error>{{ error.key | transloco: error.params }}</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'settings.salon.industry' | transloco }}</mat-label>
          <select matNativeControl [formField]="form.industry">
            @for (industry of industries; track industry) {
              <option [value]="industry">
                {{ 'auth.register.industries.' + industry | transloco }}
              </option>
            }
          </select>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'settings.salon.slug' | transloco }}</mat-label>
          <span matTextPrefix>{{ origin }}/</span>
          <input
            matInput
            type="text"
            autocapitalize="none"
            spellcheck="false"
            [formField]="form.slug"
          />
          <mat-hint>
            {{
              (isOwner() ? 'settings.salon.slugWarning' : 'settings.salon.slugOwnerOnly')
                | transloco
            }}
          </mat-hint>
          @if (errorText(form.slug().errors()); as error) {
            <mat-error>{{ error.key | transloco: error.params }}</mat-error>
          }
        </mat-form-field>
        @if (isOwner()) {
          <div class="actions">
            <button mat-stroked-button type="button" (click)="suggestSlug()">
              {{ 'settings.salon.suggest' | transloco }}
            </button>
          </div>
        }

        <mat-form-field appearance="outline">
          <mat-label>{{ 'settings.salon.street' | transloco }}</mat-label>
          <input matInput type="text" autocomplete="street-address" [formField]="form.street" />
        </mat-form-field>

        <div class="row">
          <mat-form-field appearance="outline">
            <mat-label>{{ 'settings.salon.postalCode' | transloco }}</mat-label>
            <input matInput type="text" autocomplete="postal-code" [formField]="form.postalCode" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'settings.salon.city' | transloco }}</mat-label>
            <input matInput type="text" autocomplete="address-level2" [formField]="form.city" />
          </mat-form-field>
        </div>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'settings.salon.phone' | transloco }}</mat-label>
          <input matInput type="tel" autocomplete="tel" [formField]="form.phone" />
          @if (errorText(form.phone().errors()); as error) {
            <mat-error>{{ error.key | transloco: error.params }}</mat-error>
          }
        </mat-form-field>

        <div class="row">
          <mat-form-field appearance="outline">
            <mat-label>{{ 'settings.salon.timeZone' | transloco }}</mat-label>
            <select matNativeControl [formField]="form.timeZone">
              @for (zone of timeZones(); track zone) {
                <option [value]="zone">{{ zone }}</option>
              }
            </select>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'settings.salon.language' | transloco }}</mat-label>
            <select matNativeControl [formField]="form.defaultLanguage">
              @for (code of languages; track code) {
                <option [value]="code">{{ languageNames[code] }}</option>
              }
            </select>
          </mat-form-field>
        </div>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'settings.salon.imprint' | transloco }}</mat-label>
          <textarea matInput rows="5" [formField]="form.imprintText"></textarea>
          <mat-hint>{{ 'settings.salon.imprintHint' | transloco }}</mat-hint>
          @if (errorText(form.imprintText().errors()); as error) {
            <mat-error>{{ error.key | transloco: error.params }}</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'settings.salon.privacyUrl' | transloco }}</mat-label>
          <input
            matInput
            type="url"
            inputmode="url"
            autocapitalize="none"
            [formField]="form.privacyPolicyUrl"
          />
          <mat-hint>{{ 'settings.salon.privacyHint' | transloco }}</mat-hint>
          @if (errorText(form.privacyPolicyUrl().errors()); as error) {
            <mat-error>{{ error.key | transloco: error.params }}</mat-error>
          }
        </mat-form-field>

        <div class="actions">
          <button mat-flat-button type="submit" [disabled]="form().submitting()">
            {{ 'settings.save' | transloco }}
          </button>
        </div>
      </form>
    }
  `,
})
export class SalonTab {
  private readonly settings = inject(SettingsService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(Toast);

  protected readonly origin = inject(DOCUMENT).location.origin;
  protected readonly industries = SALON_INDUSTRIES;
  protected readonly languages = SUPPORTED_LANGUAGES;
  protected readonly languageNames = LANGUAGE_NAMES;
  protected readonly errorText = fieldErrorText;
  protected readonly isOwner = computed(() => this.auth.hasAnyRole(['Owner']));

  protected readonly profile = resource({ loader: () => this.settings.getProfile() });
  protected readonly errorCode = signal<string | null>(null);
  protected readonly errorMessage = injectErrorMessage(this.errorCode);

  private readonly model = signal<ProfileModel>(EMPTY);

  /** The offered time zones plus the salon's own, in case it was set to one that is not in the list. */
  protected readonly timeZones = computed(() => {
    const current = this.model().timeZone;
    return (TIME_ZONES as readonly string[]).includes(current)
      ? TIME_ZONES
      : [...TIME_ZONES, current];
  });

  protected readonly form = form(
    this.model,
    (path) => {
      required(path.name);
      maxLength(path.name, 200);
      required(path.slug);
      validate(path.slug, ({ value }) =>
        value().length >= 3 && value().length <= 40 && SLUG_PATTERN.test(value())
          ? undefined
          : { kind: 'slugInvalid' },
      );
      disabled(path.slug, () => !this.isOwner());
      validate(path.phone, ({ value }) =>
        !value() || PHONE_PATTERN.test(value()) ? undefined : { kind: 'phoneInvalid' },
      );
      maxLength(path.imprintText, 4000);
      validate(path.privacyPolicyUrl, ({ value }) =>
        !value() || isHttpUrl(value()) ? undefined : { kind: 'urlInvalid' },
      );
    },
    { submission: { action: () => this.save() } },
  );

  constructor() {
    effect(() => {
      const loaded = this.profile.value();
      if (loaded) {
        untracked(() => this.model.set(toModel(loaded)));
      }
    });
  }

  protected async suggestSlug(): Promise<void> {
    try {
      const slug = await this.settings.suggestSlug(this.model().name);
      this.model.update((value) => ({ ...value, slug }));
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    }
  }

  private async save(): Promise<undefined> {
    this.errorCode.set(null);
    try {
      this.model.set(toModel(await this.settings.updateProfile(toProfile(this.model()))));
      await this.auth.reload().catch(() => undefined);
      this.toast.show('settings.saved');
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    }
    return undefined;
  }
}
