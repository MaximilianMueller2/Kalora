import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

/**
 * The name after the page title in the browser tab. "Kalora" by default; a salon's public page sets the salon's
 * name, because that is what the customer is looking at.
 */
@Injectable({ providedIn: 'root' })
export class PageBrand {
  private readonly subject = new BehaviorSubject<string | null>(null);

  readonly name$ = this.subject.asObservable();

  set(name: string | null): void {
    this.subject.next(name);
  }
}
