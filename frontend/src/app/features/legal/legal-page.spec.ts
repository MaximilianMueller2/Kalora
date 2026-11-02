import { TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom, of, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { provideTestI18n } from '../../../testing/test-i18n';
import { ApiClient } from '../../core/http/api-client';
import { LanguageService } from '../../core/i18n/language.service';
import { LEGAL_PAGES, isLegalPage, legalTextPath } from './legal';
import { LegalPageComponent } from './legal-page';

async function mount(page: string, options: { language?: 'de' | 'en'; fail?: boolean } = {}) {
  localStorage.clear();
  const api = {
    getAssetText: vi
      .fn()
      .mockReturnValue(
        options.fail
          ? throwError(() => new Error('missing'))
          : of('# Impressum\n\nAdresse: [BITTE PRÜFEN: Straße]'),
      ),
  };
  TestBed.configureTestingModule({
    providers: [provideTestI18n(), { provide: ApiClient, useValue: api }],
  });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use(options.language ?? 'de');
  const fixture = TestBed.createComponent(LegalPageComponent);
  fixture.componentRef.setInput('page', page);
  fixture.detectChanges();
  await fixture.whenStable();
  await new Promise((resolve) => setTimeout(resolve));
  fixture.detectChanges();
  return { fixture, api, root: fixture.nativeElement as HTMLElement };
}

describe('legal pages', () => {
  it('knows the four pages and where their texts are', () => {
    expect([...LEGAL_PAGES]).toEqual(['imprint', 'privacy', 'terms', 'avv']);
    expect(legalTextPath('terms')).toBe('/legal/terms.de.md');
    expect(isLegalPage('avv')).toBe(true);
    expect(isLegalPage('../secret')).toBe(false);
  });

  it('loads the German text of the page and shows it with the unfinished places highlighted', async () => {
    const { api, root } = await mount('imprint');

    expect(api.getAssetText).toHaveBeenCalledWith('/legal/imprint.de.md');
    expect(root.querySelector('h1')?.textContent?.trim()).toBe('legal.imprint');
    expect(root.querySelector('app-markdown-view h2')?.textContent?.trim()).toBe('Impressum');
    expect(root.querySelector('mark.check')?.textContent).toBe('[BITTE PRÜFEN: Straße]');
    expect(root.querySelector('.note')).toBeNull();
  });

  it('tells readers of other languages that the German text is the binding one', async () => {
    const { root } = await mount('privacy', { language: 'en' });

    expect(root.querySelector('.note')?.textContent).toContain('legal.germanOnly');
  });

  it('loads nothing for a page that does not exist, so no file name can be chosen through the address', async () => {
    const { api, root } = await mount('../../etc/passwd');

    expect(api.getAssetText).not.toHaveBeenCalled();
    expect(root.querySelector('[role="alert"]')?.textContent).toContain('legal.unknown');
  });

  it('says so when the text cannot be loaded', async () => {
    const { root } = await mount('terms', { fail: true });

    expect(root.querySelector('[role="alert"]')?.textContent).toContain('legal.loadFailed');
  });
});
