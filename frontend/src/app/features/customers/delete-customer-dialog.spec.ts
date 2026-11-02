import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { OverlayContainer } from '@angular/cdk/overlay';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { describe, expect, it } from 'vitest';
import { provideTestI18n } from '../../../testing/test-i18n';
import { LanguageService } from '../../core/i18n/language.service';
import { DeleteCustomerDialog } from './delete-customer-dialog';

@Component({ selector: 'app-host', template: '' })
class Host {}

async function open() {
  localStorage.clear();
  TestBed.configureTestingModule({ providers: [provideTestI18n()] });
  await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
  await TestBed.inject(LanguageService).use('de');
  TestBed.createComponent(Host);
  const ref = TestBed.inject(MatDialog).open<DeleteCustomerDialog, undefined, boolean>(
    DeleteCustomerDialog,
  );
  const result = firstValueFrom(ref.afterClosed());
  await new Promise((resolve) => setTimeout(resolve));
  const overlay = TestBed.inject(OverlayContainer).getContainerElement();
  return { overlay, result };
}

const buttons = (overlay: HTMLElement) => Array.from(overlay.querySelectorAll('button'));

describe('DeleteCustomerDialog', () => {
  it('says what is removed, what stays and that it cannot be undone', async () => {
    const { overlay } = await open();

    const text = overlay.textContent ?? '';
    for (const key of ['title', 'text', 'removed', 'kept', 'final', 'export']) {
      expect(text).toContain(`customers.delete.${key}`);
    }
  });

  it('closes with true only when confirmed', async () => {
    const { overlay, result } = await open();

    buttons(overlay)
      .find((b) => b.classList.contains('danger'))
      ?.click();

    expect(await result).toBe(true);
  });

  it('closes without confirmation when cancelled', async () => {
    const { overlay, result } = await open();

    buttons(overlay)
      .find((b) => b.textContent?.includes('Abbrechen'))
      ?.click();

    expect(await result).toBeFalsy();
  });
});
