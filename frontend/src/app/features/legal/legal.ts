/** The legal pages of Kalora; each has a Markdown text in `public/legal/<page>.de.md`. */
export const LEGAL_PAGES = ['imprint', 'privacy', 'terms', 'avv'] as const;

export type LegalPage = (typeof LEGAL_PAGES)[number];

export function isLegalPage(value: string): value is LegalPage {
  return (LEGAL_PAGES as readonly string[]).includes(value);
}

export function legalTextPath(page: LegalPage): string {
  return `/legal/${page}.de.md`;
}
