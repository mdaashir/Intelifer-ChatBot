import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const token = auth.token;

  const authorized = token
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : request;

  return next(authorized).pipe(
    catchError((error: HttpErrorResponse) => {
      // An expired or rejected token ends the session. Bad credentials answer 400
      // rather than 401 on the server precisely so this branch cannot fire on the
      // login screen and bounce the user in a loop.
      if (error.status === 401) {
        auth.logout();
      }

      return throwError(() => error);
    }),
  );
};
