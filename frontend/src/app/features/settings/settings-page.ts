import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';

const TABS = [
  { path: 'salon', labelKey: 'settings.tabs.salon' },
  { path: 'hours', labelKey: 'settings.tabs.hours' },
  { path: 'booking', labelKey: 'settings.tabs.booking' },
  { path: 'closures', labelKey: 'settings.tabs.closures' },
] as const;

/** Frame of the settings area: four tabs, each a route of its own (so they can be linked and reloaded). */
@Component({
  selector: 'app-settings-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatTabsModule, RouterLink, RouterLinkActive, RouterOutlet, TranslocoPipe],
  template: `
    <h1>{{ 'settings.title' | transloco }}</h1>
    <nav mat-tab-nav-bar [tabPanel]="panel" [attr.aria-label]="'settings.title' | transloco">
      @for (tab of tabs; track tab.path) {
        <a
          mat-tab-link
          [routerLink]="tab.path"
          routerLinkActive
          #active="routerLinkActive"
          [active]="active.isActive"
        >
          {{ tab.labelKey | transloco }}
        </a>
      }
    </nav>
    <mat-tab-nav-panel #panel>
      <div class="panel"><router-outlet /></div>
    </mat-tab-nav-panel>
  `,
  styles: `
    h1 {
      margin: 0 0 16px;
      font: var(--mat-sys-headline-medium);
    }

    .panel {
      max-width: 720px;
      padding: 24px 0;
    }
  `,
})
export class SettingsPage {
  protected readonly tabs = TABS;
}
