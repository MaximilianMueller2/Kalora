import { ChangeDetectionStrategy, Component } from '@angular/core';
import { AbsencesPanel } from './absences-panel';
import { injectStaffId } from './staff-id';

/** The absences tab of `team/:id`: hands the route's staff ID to the panel. */
@Component({
  selector: 'app-absences-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AbsencesPanel],
  template: `<app-absences-panel [staffId]="staffId()" />`,
})
export class AbsencesTab {
  protected readonly staffId = injectStaffId();
}
