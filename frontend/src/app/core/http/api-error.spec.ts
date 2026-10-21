import { HttpErrorResponse } from '@angular/common/http';
import { describe, expect, it } from 'vitest';
import { NETWORK_ERROR_CODE, toApiError } from './api-error';

const failure = (status: number, error: unknown) => new HttpErrorResponse({ status, error });

describe('toApiError', () => {
  it('takes code and parameter from the ProblemDetails body', () => {
    const error = toApiError(
      failure(409, { code: 'booking.slot_unavailable', parameter: 'startUtc' }),
    );

    expect(error.status).toBe(409);
    expect(error.code).toBe('booking.slot_unavailable');
    expect(error.parameter).toBe('startUtc');
  });

  it('falls back to the HTTP status when the body has no code', () => {
    expect(toApiError(failure(500, null)).code).toBe('http.500');
    expect(toApiError(failure(404, 'plain text')).code).toBe('http.404');
  });

  it('maps ASP.NET model validation errors to validation.invalid', () => {
    expect(toApiError(failure(400, { errors: { Email: ['required'] } })).code).toBe(
      'validation.invalid',
    );
  });

  it('recognises a missing connection', () => {
    const error = toApiError(failure(0, new ProgressEvent('error')));

    expect(error.isNetworkError).toBe(true);
    expect(error.code).toBe(NETWORK_ERROR_CODE);
  });
});
