const FALLBACK = '/app';

/**
 * Where to go after signing in. Only paths inside this app are accepted, otherwise a crafted
 * `?returnUrl=https://evil.example` link could send a freshly signed-in user to another site.
 */
export function safeReturnUrl(candidate: string | null | undefined): string {
  if (
    !candidate ||
    !candidate.startsWith('/') ||
    candidate.startsWith('//') ||
    candidate.includes('\\')
  ) {
    return FALLBACK;
  }
  return candidate.startsWith('/auth/') ? FALLBACK : candidate;
}
