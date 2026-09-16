import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, map, of, shareReplay, tap } from 'rxjs';
import { AuthResponse } from '../models/auth';
import { RoleNames, User } from '../models/user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private static readonly TokenKey = 'intellifer.accessToken';

  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly currentUser = signal<User | null>(null);
  private pendingLoad: Observable<User | null> | null = null;

  readonly user = this.currentUser.asReadonly();
  readonly isAuthenticated = computed(() => this.currentUser() !== null);
  readonly isAdmin = computed(
    () => this.currentUser()?.roles.includes(RoleNames.Admin) ?? false,
  );

  get token(): string | null {
    return localStorage.getItem(AuthService.TokenKey);
  }

  login(email: string, password: string): Observable<User> {
    return this.http
      .post<AuthResponse>('/api/auth/login', { email, password })
      .pipe(
        tap((response) => this.store(response)),
        map((response) => response.user),
      );
  }

  register(email: string, password: string): Observable<User> {
    return this.http
      .post<AuthResponse>('/api/auth/register', { email, password })
      .pipe(
        tap((response) => this.store(response)),
        map((response) => response.user),
      );
  }

  changePassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.http.post<void>('/api/auth/change-password', {
      currentPassword,
      newPassword,
    });
  }

  /**
   * Resolves the signed-in user once per session. The route guards await this so
   * that a role check never runs against a user that has not been loaded yet.
   */
  loadCurrentUser(): Observable<User | null> {
    const cached = this.currentUser();

    if (cached) {
      return of(cached);
    }

    if (!this.token) {
      return of(null);
    }

    this.pendingLoad ??= this.http.get<User>('/api/auth/me').pipe(
      tap((user) => this.currentUser.set(user)),
      catchError(() => {
        this.clear();

        return of(null);
      }),
      shareReplay(1),
    );

    return this.pendingLoad;
  }

  logout(): void {
    this.clear();

    void this.router.navigate(['/login']);
  }

  private store(response: AuthResponse): void {
    localStorage.setItem(AuthService.TokenKey, response.accessToken);
    this.currentUser.set(response.user);
    this.pendingLoad = null;
  }

  private clear(): void {
    localStorage.removeItem(AuthService.TokenKey);
    this.currentUser.set(null);
    this.pendingLoad = null;
  }
}
