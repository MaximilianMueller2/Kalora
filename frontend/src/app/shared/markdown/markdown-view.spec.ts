import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { MarkdownView } from './markdown-view';

function render(source: string) {
  const fixture = TestBed.createComponent(MarkdownView);
  fixture.componentRef.setInput('source', source);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('MarkdownView', () => {
  it('draws headings, paragraphs and lists as elements', () => {
    const root = render('# Titel\n\nText mit **fett** und *kursiv*.\n\n- a\n- b\n\n1. eins');

    expect(root.querySelector('h2')?.textContent?.trim()).toBe('Titel');
    expect(root.querySelector('p strong')?.textContent).toBe('fett');
    expect(root.querySelector('p em')?.textContent).toBe('kursiv');
    expect(root.querySelectorAll('ul li')).toHaveLength(2);
    expect(root.querySelectorAll('ol li')).toHaveLength(1);
  });

  it('highlights what still has to be checked', () => {
    const root = render('Adresse: [BITTE PRÜFEN: Straße]');

    expect(root.querySelector('mark.check')?.textContent).toBe('[BITTE PRÜFEN: Straße]');
  });

  it('opens web links in a new tab without handing over the page, and keeps links of the app inside', () => {
    const root = render('[extern](https://example.com) und [intern](/legal/privacy)');

    const [external, internal] = Array.from(root.querySelectorAll('a'));
    expect([external.target, external.rel]).toEqual(['_blank', 'noopener noreferrer']);
    expect(internal.getAttribute('href')).toBe('/legal/privacy');
    expect(internal.target).toBe('');
  });

  it('never creates script, image or other elements from what is written', () => {
    const root = render(
      '<script>alert(1)</script><img src=x onerror=alert(1)> [x](javascript:alert(1))',
    );

    expect(root.querySelector('script, img, iframe, object')).toBeNull();
    expect(root.querySelector('a')).toBeNull();
    expect(root.textContent).toContain('<script>alert(1)</script>');
  });
});
