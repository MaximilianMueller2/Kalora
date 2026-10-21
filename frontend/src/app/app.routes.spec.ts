import { describe, expect, it } from 'vitest';
import { routes } from './app.routes';

describe('routes', () => {
  const paths = routes.map((route) => route.path);

  it('keeps static routes before the :slug route and the wildcard last', () => {
    expect(paths.indexOf('app')).toBeLessThan(paths.indexOf(':slug'));
    expect(paths.indexOf(':slug')).toBeLessThan(paths.indexOf('**'));
    expect(paths.at(-1)).toBe('**');
  });

  it('defines all six salon areas, each with a translated title and its own text scope', () => {
    const area = routes.find((route) => route.path === 'app')?.children ?? [];
    const pages = area.filter((route) => route.loadComponent);

    expect(pages.map((route) => route.path)).toEqual([
      'dashboard',
      'calendar',
      'requests',
      'services',
      'team',
      'settings',
    ]);
    for (const page of pages) {
      expect(page.title).toBe(`nav.${page.path}`);
      expect(page.providers).toHaveLength(1);
    }
  });

  it('loads every page lazily', () => {
    const lazy = routes
      .flatMap((route) => [route, ...(route.children ?? [])])
      .filter((route) => route.component);

    expect(lazy).toEqual([]);
  });
});
