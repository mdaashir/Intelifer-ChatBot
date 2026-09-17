import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CanActivateFn, Router, UrlTree, provideRouter } from '@angular/router';
import { Observable, firstValueFrom } from 'rxjs';
import { installLocalStorage } from '../../../testing/in-memory-storage';
import { adminGuard } from './admin.guard';
import { authGuard } from './auth.guard';
import { guestGuard } from './guest.guard';

describe('route guards', () => {
  const tokenKey = 'intellifer.accessToken';

  let http: HttpTestingController;

  beforeEach(() => {
    installLocalStorage();

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    http = TestBed.inject(HttpTestingController);

    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  });

  afterEach(() => http.verify());

  const runGuard = async (guard: CanActivateFn): Promise<boolean | UrlTree> => {
    const result = TestBed.runInInjectionContext(() => guard({} as never, {} as never));

    return firstValueFrom(result as Observable<boolean | UrlTree>);
  };

  const signIn = (): void => {
    localStorage.setItem(tokenKey, 'token-123');
  };

  it('sends an anonymous visitor to the login page from a protected route', async () => {
    const result = await runGuard(authGuard);

    expect(result).toBeInstanceOf(UrlTree);
    expect(String(result)).toBe('/login');
  });

  it('lets a signed-in user through a protected route', async () => {
    signIn();

    const pending = runGuard(authGuard);

    http.expectOne('/api/auth/me').flush({
      id: 'user-1',
      email: 'user@example.com',
      roles: ['User'],
      createdAtUtc: '2026-01-01T00:00:00Z',
    });

    await expect(pending).resolves.toBe(true);
  });

  it('keeps a regular user out of the admin area', async () => {
    signIn();

    const pending = runGuard(adminGuard);

    http.expectOne('/api/auth/me').flush({
      id: 'user-1',
      email: 'user@example.com',
      roles: ['User'],
      createdAtUtc: '2026-01-01T00:00:00Z',
    });

    const result = await pending;

    expect(result).toBeInstanceOf(UrlTree);
    expect(String(result)).toBe('/chat');
  });

  it('lets an administrator into the admin area', async () => {
    signIn();

    const pending = runGuard(adminGuard);

    http.expectOne('/api/auth/me').flush({
      id: 'admin-1',
      email: 'admin@example.com',
      roles: ['User', 'Admin'],
      createdAtUtc: '2026-01-01T00:00:00Z',
    });

    await expect(pending).resolves.toBe(true);
  });

  it('lets an anonymous visitor see the login page', async () => {
    await expect(runGuard(guestGuard)).resolves.toBe(true);
  });

  it('sends a signed-in user away from the login page', async () => {
    signIn();

    const pending = runGuard(guestGuard);

    http.expectOne('/api/auth/me').flush({
      id: 'user-1',
      email: 'user@example.com',
      roles: ['User'],
      createdAtUtc: '2026-01-01T00:00:00Z',
    });

    const result = await pending;

    expect(result).toBeInstanceOf(UrlTree);
    expect(String(result)).toBe('/chat');
  });
});
