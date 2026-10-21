import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TitleStrategy, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { provideTestI18n } from '../../../testing/test-i18n';
import { LanguageService } from '../i18n/language.service';
import { AppTitleStrategy } from './title-strategy';

@Component({ template: '' })
class Page {}

describe('AppTitleStrategy', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideTestI18n(),
        provideRouter([
          { path: 'dashboard', title: 'nav.dashboard', component: Page },
          { path: 'plain', component: Page },
        ]),
        { provide: TitleStrategy, useExisting: AppTitleStrategy },
      ],
    });
  });

  it('shows the translated route title and follows language changes without ever showing the key', async () => {
    const language = TestBed.inject(LanguageService);
    await language.use('de');
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/dashboard');
    expect(document.title).toBe('Dashboard · Kalora');

    await language.use('tr');
    expect(document.title).toBe('Gösterge paneli · Kalora');
  });

  it('falls back to the app name when a route has no title', async () => {
    await TestBed.inject(LanguageService).use('de');
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/plain');

    expect(document.title).toBe('Kalora');
  });
});
