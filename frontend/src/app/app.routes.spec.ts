import { describe, expect, it } from 'vitest';
import { routes } from './app.routes';

describe('routes', () => {
  const paths = routes.map((route) => route.path);
  const area = routes.find((route) => route.path === 'app')?.children ?? [];
  const pages = area.filter((route) => route.loadComponent);

  it('keeps static routes before the :slug route and the wildcard last', () => {
    expect(paths.indexOf('app')).toBeLessThan(paths.indexOf(':slug'));
    expect(paths.indexOf('auth')).toBeLessThan(paths.indexOf(':slug'));
    expect(paths.indexOf(':slug')).toBeLessThan(paths.indexOf('**'));
    expect(paths.at(-1)).toBe('**');
  });

  it('protects the whole salon area with the auth guard', () => {
    expect(routes.find((route) => route.path === 'app')?.canActivate).toHaveLength(1);
  });

  it('defines onboarding and the salon pages, each with a translated title and its own text scope', () => {
    expect(pages.map((route) => route.path)).toEqual([
      'onboarding',
      'dashboard',
      'calendar',
      'requests',
      'requests/:id',
      'services',
      'absences',
      'settings',
    ]);
    for (const page of pages) {
      expect(page.title).toBe(
        page.path === 'onboarding' ? 'title.onboarding' : `nav.${page.path?.split('/')[0]}`,
      );
      expect(page.providers).toHaveLength(1);
    }
  });

  it('restricts services, team and settings to owners and managers, but not the staff’s own absences', () => {
    const guarded = area.filter((route) => route.canActivate).map((route) => route.path);

    expect(guarded).toEqual(['services', 'team', 'settings']);
  });

  it('loads the team area with its own routes, titles and text scopes', () => {
    const team = area.find((route) => route.path === 'team');

    expect(team?.loadChildren).toBeDefined();
    expect(team?.loadComponent).toBeUndefined();
  });

  it('keeps login and registration for visitors and leaves reset, confirmation and invitation open', () => {
    const auth = routes.find((route) => route.path === 'auth')?.children ?? [];
    const byPath = (path: string) => auth.find((route) => route.path === path);

    for (const path of ['login', 'register', 'forgot-password']) {
      expect(byPath(path)?.canActivate, path).toHaveLength(1);
    }
    for (const path of ['reset-password', 'confirm-email', 'invitation/:token']) {
      expect(byPath(path)?.canActivate, path).toBeUndefined();
    }
  });

  it('loads every page lazily', () => {
    const eager = routes
      .flatMap((route) => [route, ...(route.children ?? [])])
      .filter((route) => route.component);

    expect(eager).toEqual([]);
  });
});
