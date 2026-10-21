import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'app-services-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe],
  template: `
    <h1>{{ 'services.title' | transloco }}</h1>
    <p>{{ 'services.placeholder' | transloco }}</p>
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
export class ServicesPage {}
