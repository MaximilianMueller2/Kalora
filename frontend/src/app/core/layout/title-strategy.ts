import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { Observable, Subscription, combineLatest, of } from 'rxjs';
import { PageBrand } from './page-brand';

const APP_NAME = 'Kalora';

/** Route titles are translation keys; the page title follows the language, also when it changes later. */
@Injectable({ providedIn: 'root' })
export class AppTitleStrategy extends TitleStrategy {
  private readonly transloco = inject(TranslocoService);
  private readonly document = inject(DOCUMENT);
  private readonly brand = inject(PageBrand);
  private subscription?: Subscription;

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const key = this.buildTitle(snapshot);
    this.subscription?.unsubscribe();

    // selectTranslate waits until the texts of the active language are loaded, so no raw key ever shows up.
    const text$: Observable<string | null> = key ? this.transloco.selectTranslate(key) : of(null);
    this.subscription = combineLatest([text$, this.brand.name$]).subscribe(([text, brand]) => {
      const name = brand ?? APP_NAME;
      this.document.title = text ? `${text} · ${name}` : name;
    });
  }
}
