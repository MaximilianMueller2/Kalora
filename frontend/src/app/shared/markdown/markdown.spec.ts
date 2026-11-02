import { describe, expect, it } from 'vitest';
import { fillTemplate, parseInline, parseMarkdown } from './markdown';

describe('parseInline', () => {
  it('keeps plain text as it is', () => {
    expect(parseInline('Hallo Welt')).toEqual([{ kind: 'text', text: 'Hallo Welt' }]);
  });

  it('reads bold, italic and links between text', () => {
    expect(parseInline('Das **ist** *wichtig*, siehe [hier](https://example.com/x).')).toEqual([
      { kind: 'text', text: 'Das ' },
      { kind: 'strong', text: 'ist' },
      { kind: 'text', text: ' ' },
      { kind: 'em', text: 'wichtig' },
      { kind: 'text', text: ', siehe ' },
      { kind: 'link', text: 'hier', href: 'https://example.com/x' },
      { kind: 'text', text: '.' },
    ]);
  });

  it('marks what still has to be checked, with and without a hint', () => {
    expect(parseInline('Name: [BITTE PRÜFEN: Firma] und [BITTE PRÜFEN]')).toEqual([
      { kind: 'text', text: 'Name: ' },
      { kind: 'check', text: '[BITTE PRÜFEN: Firma]' },
      { kind: 'text', text: ' und ' },
      { kind: 'check', text: '[BITTE PRÜFEN]' },
    ]);
  });

  it.each([
    'mailto:hallo@example.com',
    'https://example.com',
    'http://example.com',
    '/legal/privacy',
  ])('allows the link target %s', (href) => {
    expect(parseInline(`[x](${href})`)).toEqual([{ kind: 'link', text: 'x', href }]);
  });

  it.each([
    'javascript:alert',
    'JavaScript:alert',
    'data:text/html;base64,AAAA',
    '//evil.example',
    'vbscript:x',
    'file:///etc/passwd',
  ])('shows a link to %s as plain text, never as a link', (href) => {
    expect(parseInline(`[Klick](${href})`)).toEqual([{ kind: 'text', text: 'Klick' }]);
  });

  it('does not turn HTML into markup: it stays text', () => {
    const parts = parseInline('<script>alert(1)</script> <b>fett</b>');

    expect(parts).toEqual([{ kind: 'text', text: '<script>alert(1)</script> <b>fett</b>' }]);
  });

  it('leaves a lone asterisk and unfinished markers alone', () => {
    expect(parseInline('5 * 3 und **offen')).toEqual([{ kind: 'text', text: '5 * 3 und **offen' }]);
  });
});

describe('parseMarkdown', () => {
  it('reads headings of three levels', () => {
    expect(parseMarkdown('# Eins\n## Zwei\n### Drei\n#### Vier')).toEqual([
      { kind: 'heading', level: 1, inline: [{ kind: 'text', text: 'Eins' }] },
      { kind: 'heading', level: 2, inline: [{ kind: 'text', text: 'Zwei' }] },
      { kind: 'heading', level: 3, inline: [{ kind: 'text', text: 'Drei' }] },
      { kind: 'paragraph', inline: [{ kind: 'text', text: '#### Vier' }] },
    ]);
  });

  it('keeps the lines of a paragraph (an address) and splits paragraphs at blank lines', () => {
    expect(parseMarkdown('Zeile 1\nZeile 2\n\nNeu')).toEqual([
      { kind: 'paragraph', inline: [{ kind: 'text', text: 'Zeile 1\nZeile 2' }] },
      { kind: 'paragraph', inline: [{ kind: 'text', text: 'Neu' }] },
    ]);
  });

  it('reads bullet and numbered lists and ends them at a blank line or a change of kind', () => {
    const blocks = parseMarkdown('- a\n- **b**\n\n1. eins\n2) zwei\n* c');

    expect(blocks.map((b) => (b.kind === 'list' ? [b.ordered, b.items.length] : b.kind))).toEqual([
      [false, 2],
      [true, 2],
      [false, 1],
    ]);
    const first = blocks[0];
    expect(first.kind === 'list' && first.items[1]).toEqual([{ kind: 'strong', text: 'b' }]);
  });

  it('reads a rule', () => {
    expect(parseMarkdown('a\n\n---\n\nb').map((b) => b.kind)).toEqual([
      'paragraph',
      'rule',
      'paragraph',
    ]);
  });

  it('handles Windows line breaks and an empty text', () => {
    expect(parseMarkdown('# A\r\n\r\nB')).toHaveLength(2);
    expect(parseMarkdown('')).toEqual([]);
    expect(parseMarkdown('\n\n  \n')).toEqual([]);
  });

  it('reads a paragraph directly after a list or heading without a blank line', () => {
    expect(parseMarkdown('# Titel\nText\n- punkt\nWieder Text').map((b) => b.kind)).toEqual([
      'heading',
      'paragraph',
      'list',
      'paragraph',
    ]);
  });
});

describe('fillTemplate', () => {
  it('fills the names given and leaves the others as they are', () => {
    expect(
      fillTemplate('Hallo {{salon}}, {{phone}} und {{rest}}', { salon: 'Haarwerk', phone: '123' }),
    ).toBe('Hallo Haarwerk, 123 und {{rest}}');
  });

  it('fills a name every time it occurs', () => {
    expect(fillTemplate('{{a}}-{{a}}', { a: 'x' })).toBe('x-x');
  });

  it('does not read values as templates again', () => {
    expect(fillTemplate('{{a}}', { a: '{{b}}', b: 'boom' })).toBe('{{b}}');
  });
});
