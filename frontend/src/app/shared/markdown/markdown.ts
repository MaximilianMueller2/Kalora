/**
 * A small Markdown reader for the legal texts and the salon's privacy notice. It makes data, not HTML: the page draws the
 * blocks itself, so nothing a salon types can become markup or script. Supported: headings (#, ##, ###), paragraphs, bullet and
 * numbered lists, a rule (---), **bold**, *italic*, links ([text](url), only http, https, mailto and paths of the app) and the
 * marker [BITTE PRÜFEN: …] that shows what still has to be filled in or checked.
 */
export type Inline =
  | { kind: 'text'; text: string }
  | { kind: 'strong'; text: string }
  | { kind: 'em'; text: string }
  | { kind: 'link'; text: string; href: string }
  /** A passage the author still has to check, drawn highlighted. */
  | { kind: 'check'; text: string };

export type Block =
  | { kind: 'heading'; level: 1 | 2 | 3; inline: Inline[] }
  | { kind: 'paragraph'; inline: Inline[] }
  | { kind: 'list'; ordered: boolean; items: Inline[][] }
  | { kind: 'rule' };

const CHECK = String.raw`\[BITTE PRÜFEN[^\]]*\]`;
const LINK = String.raw`\[([^\]]+)\]\(([^)\s]+)\)`;
const STRONG = String.raw`\*\*([^*]+)\*\*`;
const EM = String.raw`\*([^*\s][^*]*)\*`;
const TOKENS = new RegExp(`(${CHECK})|${LINK}|${STRONG}|${EM}`, 'gu');

const SAFE_LINK = /^(https?:\/\/|mailto:|\/(?!\/))/iu;

export function parseInline(source: string): Inline[] {
  const result: Inline[] = [];
  let last = 0;
  for (const match of source.matchAll(TOKENS)) {
    if (match.index > last) {
      result.push({ kind: 'text', text: source.slice(last, match.index) });
    }
    if (match[1] !== undefined) {
      result.push({ kind: 'check', text: match[1] });
    } else if (match[2] !== undefined) {
      // A link that does not lead to the web, a mail address or a page of the app is shown as plain text.
      result.push(
        SAFE_LINK.test(match[3])
          ? { kind: 'link', text: match[2], href: match[3] }
          : { kind: 'text', text: match[2] },
      );
    } else if (match[4] !== undefined) {
      result.push({ kind: 'strong', text: match[4] });
    } else if (match[5] !== undefined) {
      result.push({ kind: 'em', text: match[5] });
    }
    last = match.index + match[0].length;
  }
  if (last < source.length) {
    result.push({ kind: 'text', text: source.slice(last) });
  }
  return result;
}

export function parseMarkdown(source: string): Block[] {
  const blocks: Block[] = [];
  const lines = source.replace(/\r\n?/gu, '\n').split('\n');
  let paragraph: string[] = [];
  let list: { ordered: boolean; items: Inline[][] } | null = null;

  const closeParagraph = () => {
    if (paragraph.length > 0) {
      blocks.push({ kind: 'paragraph', inline: parseInline(paragraph.join('\n')) });
      paragraph = [];
    }
  };
  const closeList = () => {
    if (list) {
      blocks.push({ kind: 'list', ordered: list.ordered, items: list.items });
      list = null;
    }
  };

  for (const raw of lines) {
    const line = raw.trimEnd();
    const heading = /^(#{1,3})\s+(.+)$/u.exec(line);
    const bullet = /^\s*[-*]\s+(.+)$/u.exec(line);
    const numbered = /^\s*\d+[.)]\s+(.+)$/u.exec(line);
    if (line.trim() === '') {
      closeParagraph();
      closeList();
    } else if (/^-{3,}$/u.test(line.trim())) {
      closeParagraph();
      closeList();
      blocks.push({ kind: 'rule' });
    } else if (heading) {
      closeParagraph();
      closeList();
      blocks.push({
        kind: 'heading',
        level: heading[1].length as 1 | 2 | 3,
        inline: parseInline(heading[2]),
      });
    } else if (bullet || numbered) {
      closeParagraph();
      const ordered = !bullet;
      if (list && list.ordered !== ordered) {
        closeList();
      }
      list ??= { ordered, items: [] };
      list.items.push(parseInline((bullet ?? numbered)![1]));
    } else {
      closeList();
      paragraph.push(line);
    }
  }
  closeParagraph();
  closeList();
  return blocks;
}

/** Fills `{{name}}` placeholders of a template; names that are not given stay as they are. */
export function fillTemplate(template: string, values: Record<string, string>): string {
  return template.replace(/\{\{(\w+)\}\}/gu, (whole, name: string) => values[name] ?? whole);
}
