import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { Subscription } from 'rxjs';

const APP_NAME = 'Kalora';

/** Route titles are translation keys; the page title follows the language, also when it changes later. */
@Injectable({ providedIn: 'root' })
export class AppTitleStrategy extends TitleStrategy {
  private readonly transloco = inject(TranslocoService);
  private readonly document = inject(DOCUMENT);
  private subscription?: Subscription;

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const key = this.buildTitle(snapshot);
    this.subscription?.unsubscribe();
    if (!key) {
      this.document.title = APP_NAME;
      return;
    }

    // selectTranslate waits until the texts of the active language are loaded, so no raw key ever shows up.
    this.subscription = this.transloco.selectTranslate(key).subscribe((text) => {
      this.document.title = `${text} · ${APP_NAME}`;
    });
  }
}
