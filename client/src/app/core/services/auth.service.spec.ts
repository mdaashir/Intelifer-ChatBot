import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { installLocalStorage } from '../../../testing/in-memory-storage';
import { User } from '../models/user';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  const tokenKey = 'intellifer.accessToken';

  let http: HttpTestingController;

  const user = (roles: string[]): User => ({
    id: 'user-1',
    email: 'user@example.com',
    roles,
    createdAtUtc: '2026-01-01T00:00:00Z',
  });

  beforeEach(() => {
    installLocalStorage();

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    http = TestBed.inject(HttpTestingController);

    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  });

  afterEach(() => http.verify());

  it('starts signed out', () => {
    const service = TestBed.inject(AuthService);

    expect(service.user()).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
    expect(service.isAdmin()).toBe(false);
    expect(service.token).toBeNull();
  });

  it('posts the credentials and remembers the session', () => {
    const service = TestBed.inject(AuthService);

    let result: User | undefined;
    service.login('user@example.com', 'Password123').subscribe((value) => (result = value));

    const request = http.expectOne('/api/auth/login');

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ email: 'user@example.com', password: 'Password123' });

    request.flush({
      accessToken: 'token-123',
      expiresAtUtc: '2026-01-01T01:00:00Z',
      user: user(['User']),
    });

    expect(result?.email).toBe('user@example.com');
    expect(service.token).toBe('token-123');
    expect(service.isAuthenticated()).toBe(true);
  });

  it('reports admin only when the role is actually present', () => {
    const service = TestBed.inject(AuthService);

    service.login('admin@example.com', 'Password123').subscribe();
    http.expectOne('/api/auth/login').flush({
      accessToken: 'token-123',
      expiresAtUtc: '2026-01-01T01:00:00Z',
      user: user(['User', 'Admin']),
    });

    expect(service.isAdmin()).toBe(true);
  });

  it('registers through the register endpoint', () => {
    const service = TestBed.inject(AuthService);

    service.register('new@example.com', 'Password123').subscribe();

    const request = http.expectOne('/api/auth/register');

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ email: 'new@example.com', password: 'Password123' });

    request.flush({
      accessToken: 'token-123',
      expiresAtUtc: '2026-01-01T01:00:00Z',
      user: user(['User']),
    });

    expect(service.isAuthenticated()).toBe(true);
  });

  it('forgets the token and the user on logout', () => {
    const service = TestBed.inject(AuthService);

    service.login('admin@example.com', 'Password123').subscribe();
    http.expectOne('/api/auth/login').flush({
      accessToken: 'token-123',
      expiresAtUtc: '2026-01-01T01:00:00Z',
      user: user(['Admin']),
    });

    service.logout();

    expect(service.token).toBeNull();
    expect(service.user()).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
    expect(service.isAdmin()).toBe(false);
  });

  it('resolves to null without calling the API when there is no token', () => {
    const service = TestBed.inject(AuthService);

    let result: User | null | undefined;
    service.loadCurrentUser().subscribe((value) => (result = value));

    expect(result).toBeNull();
    http.expectNone('/api/auth/me');
  });

  it('resolves the current user once even when asked twice', () => {
    localStorage.setItem(tokenKey, 'token-123');

    const service = TestBed.inject(AuthService);
    const seen: (User | null)[] = [];

    service.loadCurrentUser().subscribe((value) => seen.push(value));
    service.loadCurrentUser().subscribe((value) => seen.push(value));

    const requests = http.match('/api/auth/me');

    expect(requests.length).toBe(1);

    requests[0].flush(user(['User']));

    expect(seen).toEqual([user(['User']), user(['User'])]);
  });

  it('clears the session when the token is rejected', () => {
    localStorage.setItem(tokenKey, 'expired');

    const service = TestBed.inject(AuthService);

    let result: User | null | undefined;
    service.loadCurrentUser().subscribe((value) => (result = value));

    http
      .expectOne('/api/auth/me')
      .flush({ errors: ['Unauthorized'] }, { status: 401, statusText: 'Unauthorized' });

    expect(result).toBeNull();
    expect(service.token).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
  });

  it('changes the password through the auth endpoint', () => {
    const service = TestBed.inject(AuthService);

    service.changePassword('Password123', 'Password456').subscribe();

    const request = http.expectOne('/api/auth/change-password');

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      currentPassword: 'Password123',
      newPassword: 'Password456',
    });
  });
});
