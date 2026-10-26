import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../auth/auth.service';
import { AbsencesPanel } from './absences-panel';

/** Staff enter their own absences here; owners and managers do it per person on the team pages. */
@Component({
  selector: 'app-my-absences-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AbsencesPanel, TranslocoPipe],
  styles: `
    :host {
      display: block;
      max-width: 720px;
    }

    h1 {
      margin: 0 0 16px;
      font: var(--mat-sys-headline-medium);
    }
  `,
  template: `
    <h1>{{ 'nav.absences' | transloco }}</h1>
    @if (staffId(); as id) {
      <app-absences-panel [staffId]="id" />
    } @else {
      <p>{{ 'team.absences.noCalendar' | transloco }}</p>
    }
  `,
})
export class MyAbsencesPage {
  private readonly auth = inject(AuthService);
  protected readonly staffId = computed(() => this.auth.currentUser()?.staffId ?? null);
}
