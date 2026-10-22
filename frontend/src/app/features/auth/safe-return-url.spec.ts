import { describe, expect, it } from 'vitest';
import { safeReturnUrl } from './safe-return-url';

describe('safeReturnUrl', () => {
  it('accepts paths inside the app', () => {
    expect(safeReturnUrl('/app/team')).toBe('/app/team');
    expect(safeReturnUrl('/app/requests?filter=open')).toBe('/app/requests?filter=open');
  });

  it.each([
    'https://evil.example',
    '//evil.example',
    '/\\evil.example',
    'javascript:alert(1)',
    'app/team',
    '',
  ])('rejects %j and falls back to the app', (candidate) => {
    expect(safeReturnUrl(candidate)).toBe('/app');
  });

  it('falls back when nothing or an auth page is given, to avoid login loops', () => {
    expect(safeReturnUrl(undefined)).toBe('/app');
    expect(safeReturnUrl(null)).toBe('/app');
    expect(safeReturnUrl('/auth/login')).toBe('/app');
  });
});
