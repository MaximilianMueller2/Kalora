import { ComponentFixture } from '@angular/core/testing';

/** Types into an input the way a user does, so Signal Forms see the change. */
export function type(fixture: ComponentFixture<unknown>, selector: string, value: string): void {
  const input: HTMLInputElement = fixture.nativeElement.querySelector(selector);
  input.value = value;
  input.dispatchEvent(new Event('input'));
  fixture.detectChanges();
}

export function click(fixture: ComponentFixture<unknown>, selector: string): void {
  (fixture.nativeElement.querySelector(selector) as HTMLElement).click();
  fixture.detectChanges();
}

/** Submits the form and waits until the submit action (async) has finished. */
export async function submit(fixture: ComponentFixture<unknown>): Promise<void> {
  fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
  await fixture.whenStable();
  fixture.detectChanges();
}

export const texts = (fixture: ComponentFixture<unknown>, selector: string): string[] =>
  Array.from<HTMLElement>(fixture.nativeElement.querySelectorAll(selector)).map(
    (el) => el.textContent?.trim() ?? '',
  );
