import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { provideTestI18n } from '../../../testing/test-i18n';
import { ApiError } from './api-error';
import { ErrorNotifier } from './error-notifier';

describe('ErrorNotifier', () => {
  const open = vi.fn();
  let notifier: ErrorNotifier;

  beforeEach(async () => {
    open.mockReset();
    TestBed.configureTestingModule({
      providers: [provideTestI18n(), { provide: MatSnackBar, useValue: { open } }],
    });
    await firstValueFrom(TestBed.inject(TranslocoService).load('de'));
    notifier = TestBed.inject(ErrorNotifier);
  });

  it('translates known error codes', () => {
    expect(notifier.messageFor(new ApiError(409, 'booking.slot_unavailable'))).toBe(
      'Dieser Termin ist nicht mehr frei.',
    );
  });

  it('uses a generic text for unknown codes', () => {
    expect(notifier.messageFor(new ApiError(500, 'http.500'))).toBe('Unbekannter Fehler');
  });

  it('uses the connection text when the server cannot be reached', () => {
    expect(notifier.messageFor(new ApiError(0, 'network.unreachable'))).toBe('Keine Verbindung');
  });

  it('shows the message in a snackbar with a translated close action', () => {
    notifier.show(new ApiError(409, 'booking.slot_unavailable'));

    expect(open).toHaveBeenCalledWith(
      'Dieser Termin ist nicht mehr frei.',
      'Schließen',
      expect.objectContaining({ duration: 6000 }),
    );
  });
});
