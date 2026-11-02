import { ComponentFixture } from '@angular/core/testing';

/** Lets resources, promises and the change detection that follows them run. */
export async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    fixture.detectChanges();
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve));
  }
  fixture.detectChanges();
}

export const root = (fixture: ComponentFixture<unknown>) => fixture.nativeElement as HTMLElement;

export function q<E extends HTMLElement>(fixture: ComponentFixture<unknown>, selector: string) {
  return root(fixture).querySelector(selector) as E | null;
}

export function qa<E extends HTMLElement>(fixture: ComponentFixture<unknown>, selector: string) {
  return Array.from(root(fixture).querySelectorAll(selector)) as E[];
}

export function typeInto(input: HTMLInputElement | HTMLSelectElement, value: string): void {
  input.value = value;
  input.dispatchEvent(new Event(input instanceof HTMLSelectElement ? 'change' : 'input'));
}
