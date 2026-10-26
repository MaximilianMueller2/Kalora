import { inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { map } from 'rxjs';

/** The `:id` of the surrounding `team/:id` route, for the tab pages below it. */
export function injectStaffId() {
  const route = inject(ActivatedRoute);
  return toSignal(route.parent!.paramMap.pipe(map((params) => params.get('id') ?? '')), {
    initialValue: route.parent!.snapshot.paramMap.get('id') ?? '',
  });
}
