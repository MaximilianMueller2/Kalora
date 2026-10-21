import { TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';
import { beforeEach, describe, expect, it } from 'vitest';
import { provideTestI18n } from '../../../testing/test-i18n';
import { LanguageService } from './language.service';
import { pickBrowserLanguage, resolveLanguage } from './languages';

describe('resolveLanguage', () => {
  it('prefers the explicit choice, then the profile, then the browser, then German', () => {
    expect(resolveLanguage({ stored: 'tr', profile: 'en', browser: ['de'] })).toBe('tr');
    expect(resolveLanguage({ profile: 'en', browser: ['tr'] })).toBe('en');
    expect(resolveLanguage({ browser: ['fr-FR', 'tr-TR'] })).toBe('tr');
    expect(resolveLanguage({ browser: ['fr'] })).toBe('de');
    expect(resolveLanguage({})).toBe('de');
  });

  it('ignores unsupported values', () => {
    expect(resolveLanguage({ stored: 'xx', profile: 'yy', browser: ['en-GB'] })).toBe('en');
  });

  it('matches regional browser languages by their primary subtag', () => {
    expect(pickBrowserLanguage(['EN-us'])).toBe('en');
    expect(pickBrowserLanguage([])).toBeUndefined();
  });
});

describe('LanguageService', () => {
  let service: LanguageService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideTestI18n()] });
    service = TestBed.inject(LanguageService);
  });

  it('starts with the profile language and applies it to Transloco and the document', async () => {
    await service.initialize('en');

    expect(service.current()).toBe('en');
    expect(TestBed.inject(TranslocoService).getActiveLang()).toBe('en');
    expect(document.documentElement.lang).toBe('en');
  });

  it('remembers an explicit choice and prefers it on the next start', async () => {
    await service.initialize('en');
    await service.use('tr');
    expect(localStorage.getItem('kalora.language')).toBe('tr');

    await service.initialize('en');

    expect(service.current()).toBe('tr');
  });
});
