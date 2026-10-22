import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { DEFAULT_LANGUAGE, Language, isLanguage, resolveLanguage } from './languages';

const STORAGE_KEY = 'kalora.language';

@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly transloco = inject(TranslocoService);
  private readonly document = inject(DOCUMENT);

  private readonly active = signal<Language>(DEFAULT_LANGUAGE);
  readonly current = this.active.asReadonly();

  /** Picks the start language and loads its texts before the first render. `profile` comes from the signed-in user. */
  async initialize(profile?: string | null): Promise<void> {
    const language = resolveLanguage({
      stored: this.readStored(),
      profile,
      browser: this.document.defaultView?.navigator.languages ?? [],
    });
    await this.apply(language);
  }

  /** After sign-in: the profile language applies unless this device has an explicit choice. */
  async applyProfileLanguage(profile: string): Promise<void> {
    if (this.readStored() === null && isLanguage(profile) && profile !== this.active()) {
      await this.apply(profile);
    }
  }

  /** Switches the language and remembers the choice on this device. */
  async use(language: Language): Promise<void> {
    this.writeStored(language);
    await this.apply(language);
  }

  private async apply(language: Language): Promise<void> {
    // Load first, then activate: the screen switches in one step and never shows keys in between.
    await firstValueFrom(this.transloco.load(language));
    this.transloco.setActiveLang(language);
    this.document.documentElement.lang = language;
    this.active.set(language);
  }

  // Storage can be blocked (private mode); the language still works for the session then.
  private readStored(): string | null {
    try {
      return this.document.defaultView?.localStorage.getItem(STORAGE_KEY) ?? null;
    } catch {
      return null;
    }
  }

  private writeStored(language: Language): void {
    try {
      this.document.defaultView?.localStorage.setItem(STORAGE_KEY, language);
    } catch {
      // ignored on purpose, see readStored
    }
  }
}
