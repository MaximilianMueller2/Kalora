import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { ThemeService } from './theme.service';

describe('ThemeService', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.style.colorScheme = '';
  });

  it('follows the device by default', () => {
    const theme = TestBed.inject(ThemeService);

    expect(theme.mode()).toBe('system');
    expect(document.documentElement.style.colorScheme).toBe('light dark');
  });

  it('cycles system → light → dark → system and sets the page color scheme', () => {
    const theme = TestBed.inject(ThemeService);

    theme.cycle();
    expect([theme.mode(), document.documentElement.style.colorScheme]).toEqual(['light', 'light']);
    theme.cycle();
    expect([theme.mode(), document.documentElement.style.colorScheme]).toEqual(['dark', 'dark']);
    theme.cycle();
    expect(theme.mode()).toBe('system');
  });

  it('remembers the choice on this device', () => {
    TestBed.inject(ThemeService).set('dark');
    TestBed.resetTestingModule();

    expect(TestBed.inject(ThemeService).mode()).toBe('dark');
  });

  it('ignores an invalid stored value', () => {
    localStorage.setItem('kalora.theme', 'neon');

    expect(TestBed.inject(ThemeService).mode()).toBe('system');
  });
});
