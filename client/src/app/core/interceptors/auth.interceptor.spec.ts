import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { installLocalStorage } from '../../../testing/in-memory-storage';
import { AuthService } from '../services/auth.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  const tokenKey = 'intellifer.accessToken';

  let http: HttpClient;
  let controller: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    installLocalStorage();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });

    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);

    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  });

  afterEach(() => controller.verify());

  it('attaches the bearer token when there is one', () => {
    localStorage.setItem(tokenKey, 'token-123');

    http.get('/api/messages').subscribe();
    const request = controller.expectOne('/api/messages');

    expect(request.request.headers.get('Authorization')).toBe('Bearer token-123');

    request.flush([]);
  });

  it('sends no Authorization header when there is no token', () => {
    http.get('/api/messages').subscribe();
    const request = controller.expectOne('/api/messages');

    expect(request.request.headers.has('Authorization')).toBe(false);

    request.flush([]);
  });

  it('ends the session when the server answers 401', () => {
    localStorage.setItem(tokenKey, 'expired');

    const navigate = TestBed.inject(Router).navigate as ReturnType<typeof vi.fn>;

    http.get('/api/messages').subscribe({ error: () => undefined });

    controller
      .expectOne('/api/messages')
      .flush({ errors: ['Unauthorized'] }, { status: 401, statusText: 'Unauthorized' });

    expect(auth.token).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });

  it('leaves the session alone for any other failure', () => {
    localStorage.setItem(tokenKey, 'still-valid');

    http.get('/api/messages').subscribe({ error: () => undefined });

    controller
      .expectOne('/api/messages')
      .flush({ errors: ['boom'] }, { status: 500, statusText: 'Server Error' });

    expect(auth.token).toBe('still-valid');
  });
});
