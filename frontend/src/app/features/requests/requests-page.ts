import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'app-requests-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe],
  template: `
    <h1>{{ 'requests.title' | transloco }}</h1>
    <p>{{ 'requests.placeholder' | transloco }}</p>
  `,
  styles: `
    h1 {
      margin: 0 0 8px;
      font: var(--mat-sys-headline-medium);
    }

    p {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class RequestsPage {}
