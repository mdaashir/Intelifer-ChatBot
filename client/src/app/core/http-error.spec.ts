import { HttpErrorResponse } from '@angular/common/http';
import { extractErrors } from './http-error';

describe('extractErrors', () => {
  it('returns the errors the API reported', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { errors: ['Email is required.', 'Password is required.'] },
    });

    expect(extractErrors(error)).toEqual(['Email is required.', 'Password is required.']);
  });

  it('falls back to a single message when the body carries no errors', () => {
    const error = new HttpErrorResponse({
      status: 500,
      statusText: 'Server Error',
      url: '/api/messages',
    });

    expect(extractErrors(error)).toHaveLength(1);
  });

  it('handles a failure that never reached the server', () => {
    expect(extractErrors(new Error('boom'))).toEqual([
      'Something went wrong. Please try again.',
    ]);
  });
});
