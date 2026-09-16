import { HttpErrorResponse } from '@angular/common/http';
import { ApiError } from './models/api-error';

export function extractErrors(error: unknown): string[] {
  if (error instanceof HttpErrorResponse) {
    const payload = error.error as ApiError | null;

    if (payload?.errors?.length) {
      return payload.errors;
    }

    return [error.message || 'The request failed.'];
  }

  return ['Something went wrong. Please try again.'];
}
