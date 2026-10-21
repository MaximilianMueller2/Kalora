import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'app-not-found-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, RouterLink, TranslocoPipe],
  template: `
    <main>
      <h1>{{ 'notFound.title' | transloco }}</h1>
      <p>{{ 'notFound.text' | transloco }}</p>
      <a mat-flat-button routerLink="/">{{ 'notFound.home' | transloco }}</a>
    </main>
  `,
  styles: `
    main {
      max-width: 480px;
      margin: 0 auto;
      padding: 64px 16px;
      text-align: center;
    }

    h1 {
      font: var(--mat-sys-headline-medium);
    }

    p {
      margin-bottom: 24px;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class NotFoundPage {}
