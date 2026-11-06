import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiError } from '../../../core/http/api-error';
import { Toast } from '../../../core/ui/toast';
import { injectErrorMessage } from '../../../shared/forms/server-error';
import { TwoFactorSetup } from '../../auth/auth.models';
import { AuthService } from '../../auth/auth.service';
import { SecurityService } from '../security.service';

/** Where the person stands: not set up, scanning the QR code, or looking at the new recovery codes. */
type Stage = 'idle' | 'setup' | 'codes';

/**
 * The second factor for signing in: an authenticator app. Switching it on shows a QR code, asks for the first code and then shows
 * the recovery codes once. Switching it off or getting new recovery codes asks for the password.
 */
@Component({
  selector: 'app-security-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, TranslocoPipe],
  styleUrl: '../../../shared/forms/panel-form.scss',
  styles: `
    .status {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: 0 0 16px;
      font: var(--mat-sys-title-medium);
    }

    .status mat-icon {
      color: var(--mat-sys-primary);
    }

    .qr {
      display: block;
      width: 200px;
      height: 200px;
      margin: 12px 0;
      /* A QR code has to be dark on light, also in the dark theme. */
      background: #fff;
      padding: 8px;
      box-sizing: border-box;
      image-rendering: pixelated;
    }

    .key {
      display: inline-block;
      padding: 4px 8px;
      border-radius: var(--mat-sys-corner-small);
      background: var(--mat-sys-surface-container-high);
      font-family: ui-monospace, monospace;
      overflow-wrap: anywhere;
      user-select: all;
    }

    .field {
      display: grid;
      gap: 4px;
      max-width: 320px;
      margin: 12px 0;
    }

    .field input {
      min-height: 48px;
      padding: 0 12px;
      border: 1px solid var(--mat-sys-outline);
      border-radius: var(--mat-sys-corner-small);
      background: var(--mat-sys-surface);
      color: var(--mat-sys-on-surface);
      font: var(--mat-sys-body-large);
      box-sizing: border-box;
    }

    .field input:focus-visible {
      outline: 3px solid var(--mat-sys-primary);
      outline-offset: 1px;
    }

    .codes {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
      gap: 8px;
      margin: 12px 0;
      padding: 16px;
      list-style: none;
      border-radius: var(--mat-sys-corner-medium);
      background: var(--mat-sys-surface-container-high);
      font-family: ui-monospace, monospace;
    }

    h3 {
      margin: 24px 0 4px;
      font: var(--mat-sys-title-medium);
    }
  `,
  template: `
    <h2>{{ 'settings.security.title' | transloco }}</h2>
    <p class="lead">{{ 'settings.security.lead' | transloco }}</p>

    @if (message(); as text) {
      <p class="alert" role="alert">{{ text }}</p>
    }

    @switch (stage()) {
      @case ('setup') {
        @if (setup(); as data) {
          <h3>{{ 'settings.security.scanTitle' | transloco }}</h3>
          <p class="hint">{{ 'settings.security.scan' | transloco }}</p>
          <img
            class="qr"
            width="200"
            height="200"
            [src]="'data:image/png;base64,' + data.qrPngBase64"
            [alt]="'settings.security.qrAlt' | transloco"
          />
          <p class="hint">
            {{ 'settings.security.manual' | transloco }}
            <span class="key">{{ data.sharedKey }}</span>
          </p>
          <form (submit)="$event.preventDefault(); enable()">
            <label class="field">
              <span>{{ 'settings.security.code' | transloco }}</span>
              <input
                name="code"
                type="text"
                inputmode="numeric"
                autocomplete="one-time-code"
                maxlength="12"
                [value]="code()"
                (input)="code.set(value($event))"
              />
            </label>
            <div class="actions">
              <button mat-flat-button type="submit" class="enable" [disabled]="busy()">
                {{ 'settings.security.enable' | transloco }}
              </button>
              <button mat-button type="button" class="cancel" (click)="cancel()">
                {{ 'common.cancel' | transloco }}
              </button>
            </div>
          </form>
        }
      }
      @case ('codes') {
        <h3>{{ 'settings.security.codesTitle' | transloco }}</h3>
        <p class="hint">{{ 'settings.security.codesHint' | transloco }}</p>
        <ul class="codes" [attr.aria-label]="'settings.security.codesTitle' | transloco">
          @for (recovery of recoveryCodes(); track recovery) {
            <li>{{ recovery }}</li>
          }
        </ul>
        <div class="actions">
          <button mat-stroked-button type="button" class="copy" (click)="copyCodes()">
            <mat-icon aria-hidden="true">content_copy</mat-icon>
            {{ 'settings.security.copy' | transloco }}
          </button>
          <button mat-flat-button type="button" class="done" (click)="finish()">
            {{ 'settings.security.saved' | transloco }}
          </button>
        </div>
      }
      @default {
        @if (enabled()) {
          <p class="status">
            <mat-icon aria-hidden="true">verified_user</mat-icon>
            {{ 'settings.security.on' | transloco }}
          </p>
          <form (submit)="$event.preventDefault()">
            <label class="field">
              <span>{{ 'settings.security.password' | transloco }}</span>
              <input
                name="password"
                type="password"
                autocomplete="current-password"
                [value]="password()"
                (input)="password.set(value($event))"
              />
            </label>
            <p class="hint">{{ 'settings.security.passwordHint' | transloco }}</p>
            <div class="actions">
              <button
                mat-stroked-button
                type="button"
                class="regenerate"
                [disabled]="busy() || !password()"
                (click)="regenerate()"
              >
                {{ 'settings.security.regenerate' | transloco }}
              </button>
              <button
                mat-stroked-button
                type="button"
                class="disable"
                [disabled]="busy() || !password()"
                (click)="disable()"
              >
                {{ 'settings.security.disable' | transloco }}
              </button>
            </div>
          </form>
        } @else {
          <p class="status">
            <mat-icon aria-hidden="true">shield</mat-icon>
            {{ 'settings.security.off' | transloco }}
          </p>
          <div class="actions">
            <button
              mat-flat-button
              type="button"
              class="start"
              [disabled]="busy()"
              (click)="begin()"
            >
              {{ 'settings.security.start' | transloco }}
            </button>
          </div>
        }
      }
    }
  `,
})
export class SecurityTab {
  private readonly security = inject(SecurityService);
  private readonly toast = inject(Toast);
  private readonly auth = inject(AuthService);

  protected readonly enabled = computed(() => this.auth.currentUser()?.twoFactorEnabled ?? false);
  protected readonly stage = signal<Stage>('idle');
  protected readonly setup = signal<TwoFactorSetup | null>(null);
  protected readonly recoveryCodes = signal<string[]>([]);
  protected readonly code = signal('');
  protected readonly password = signal('');
  protected readonly busy = signal(false);
  private readonly errorCode = signal<string | null>(null);
  protected readonly message = injectErrorMessage(this.errorCode);

  protected value(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected begin(): Promise<void> {
    return this.run(async () => {
      this.setup.set(await this.security.setup());
      this.code.set('');
      this.stage.set('setup');
    });
  }

  protected enable(): Promise<void> {
    return this.run(async () => {
      const result = await this.security.enable(this.code().trim());
      this.recoveryCodes.set(result.recoveryCodes);
      this.setup.set(null);
      this.stage.set('codes');
    });
  }

  protected cancel(): void {
    this.errorCode.set(null);
    this.setup.set(null);
    this.stage.set('idle');
  }

  protected disable(): Promise<void> {
    return this.run(async () => {
      await this.security.disable(this.password());
      this.password.set('');
      this.toast.show('settings.security.disabled');
    });
  }

  protected regenerate(): Promise<void> {
    return this.run(async () => {
      const result = await this.security.regenerateRecoveryCodes(this.password());
      this.password.set('');
      this.recoveryCodes.set(result.recoveryCodes);
      this.stage.set('codes');
    });
  }

  protected async copyCodes(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.recoveryCodes().join('\n'));
      this.toast.show('settings.security.copied');
    } catch {
      // The codes stay selectable on the page.
      this.toast.show('settings.security.copyFailed');
    }
  }

  /** The person says the codes are safe: they disappear from the screen for good. */
  protected finish(): void {
    this.recoveryCodes.set([]);
    this.stage.set('idle');
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.errorCode.set(null);
    try {
      await action();
    } catch (error) {
      this.errorCode.set(error instanceof ApiError ? error.code : 'unknown');
    } finally {
      this.busy.set(false);
    }
  }
}
