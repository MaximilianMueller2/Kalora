import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';

export type ThemeMode = 'system' | 'light' | 'dark';

const STORAGE_KEY = 'kalora.theme';
const MODES: readonly ThemeMode[] = ['system', 'light', 'dark'];
const COLOR_SCHEME: Record<ThemeMode, string> = {
  system: 'light dark',
  light: 'light',
  dark: 'dark',
};

/** Light, dark or follow the device. The Material theme reacts to the CSS `color-scheme` of the page. */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);

  private readonly selected = signal<ThemeMode>('system');
  readonly mode = this.selected.asReadonly();

  constructor() {
    this.set(this.readStored());
  }

  /** system → light → dark → system */
  cycle(): void {
    this.set(MODES[(MODES.indexOf(this.selected()) + 1) % MODES.length]);
  }

  set(mode: ThemeMode): void {
    this.selected.set(mode);
    this.document.documentElement.style.colorScheme = COLOR_SCHEME[mode];
    this.writeStored(mode);
  }

  private readStored(): ThemeMode {
    try {
      const stored = this.document.defaultView?.localStorage.getItem(STORAGE_KEY);
      return MODES.find((mode) => mode === stored) ?? 'system';
    } catch {
      return 'system';
    }
  }

  private writeStored(mode: ThemeMode): void {
    try {
      this.document.defaultView?.localStorage.setItem(STORAGE_KEY, mode);
    } catch {
      // Blocked storage only means the choice is not remembered.
    }
  }
}
