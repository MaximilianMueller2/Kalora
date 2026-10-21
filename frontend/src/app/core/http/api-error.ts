import { HttpErrorResponse } from '@angular/common/http';

/** Shape of the ProblemDetails the backend sends (see ProblemDetailsMapper); `code` is the stable translation key. */
interface ProblemDetailsBody {
  code?: string;
  parameter?: string;
  title?: string;
  errors?: Record<string, string[]>;
}

/** A failed API call, reduced to what the UI needs: a stable code it can translate, never raw server text. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
    readonly parameter?: string,
  ) {
    super(code);
    this.name = 'ApiError';
  }

  get isNetworkError(): boolean {
    return this.status === 0;
  }
}

export const NETWORK_ERROR_CODE = 'network.unreachable';

export function toApiError(response: HttpErrorResponse): ApiError {
  if (response.status === 0) {
    return new ApiError(0, NETWORK_ERROR_CODE);
  }

  const body = isProblemDetails(response.error) ? response.error : undefined;
  const code = body?.code ?? (body?.errors ? 'validation.invalid' : `http.${response.status}`);
  return new ApiError(response.status, code, body?.parameter);
}

function isProblemDetails(value: unknown): value is ProblemDetailsBody {
  return typeof value === 'object' && value !== null;
}
