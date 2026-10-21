import { BreakpointObserver } from '@angular/cdk/layout';
import { Signal, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';

/** From this width on the salon area shows the side navigation instead of the bottom bar. */
export const DESKTOP_QUERY = '(min-width: 900px)';

export function injectIsDesktop(): Signal<boolean> {
  const breakpoints = inject(BreakpointObserver);
  return toSignal(breakpoints.observe(DESKTOP_QUERY).pipe(map((state) => state.matches)), {
    initialValue: breakpoints.isMatched(DESKTOP_QUERY),
  });
}
