import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { describe, expect, it } from 'vitest';
import { provideTestI18n } from '../../../testing/test-i18n';
import { LanguageService } from '../i18n/language.service';
import { AuthLayout } from './auth-layout';
import { PublicLayout } from './public-layout/public-layout';

async function footerLinks(layout: typeof AuthLayout | typeof PublicLayout) {
  localStorage.clear();
  TestBed.configureTestingModule({ providers: [provideTestI18n(), provideRouter([])] });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  const fixture = TestBed.createComponent(layout);
  fixture.detectChanges();
  return Array.from<HTMLAnchorElement>(fixture.nativeElement.querySelectorAll('footer a')).map(
    (a) => a.getAttribute('href'),
  );
}

describe('legal links in the footer', () => {
  it.each([
    ['public pages', PublicLayout],
    ['sign-in and registration pages', AuthLayout],
  ])('are on %s: imprint, privacy and terms of Kalora', async (_name, layout) => {
    expect(await footerLinks(layout)).toEqual(['/legal/imprint', '/legal/privacy', '/legal/terms']);
  });
});
