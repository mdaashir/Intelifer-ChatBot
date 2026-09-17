import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormGroup } from '@angular/forms';
import { Router, provideRouter } from '@angular/router';
import { installLocalStorage } from '../../../../testing/in-memory-storage';
import { Register } from './register';

describe('Register', () => {
  let fixture!: ComponentFixture<Register>;
  let http!: HttpTestingController;
  let navigate!: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    installLocalStorage();

    await TestBed.configureTestingModule({
      imports: [Register],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    fixture = TestBed.createComponent(Register);
    http = TestBed.inject(HttpTestingController);

    await settle();
  });

  afterEach(() => http.verify());

  it('renders an email and a password field', () => {
    expect(element().querySelectorAll('input').length).toBe(2);
  });

  it('refuses a password shorter than eight characters', async () => {
    const form = (fixture.componentInstance as unknown as { form: FormGroup }).form;

    form.setValue({ email: 'new@example.com', password: 'short' });
    form.controls['password'].markAsTouched();

    await submit();

    http.expectNone('/api/auth/register');
    expect(form.controls['password'].invalid).toBe(true);
    expect(element().textContent).toContain('at least 8 characters');
  });

  it('creates the account and goes to the chat', async () => {
    fill('new@example.com', 'Password123');

    await submit();

    const request = http.expectOne('/api/auth/register');

    expect(request.request.body).toEqual({ email: 'new@example.com', password: 'Password123' });

    request.flush({
      accessToken: 'token-123',
      expiresAtUtc: '2026-01-01T01:00:00Z',
      user: {
        id: 'user-1',
        email: 'new@example.com',
        roles: ['User'],
        createdAtUtc: '2026-01-01T00:00:00Z',
      },
    });

    await settle();

    expect(navigate).toHaveBeenCalledWith(['/chat']);
  });

  it('shows the error the server reported', async () => {
    fill('taken@example.com', 'Password123');

    await submit();

    http
      .expectOne('/api/auth/register')
      .flush(
        { errors: ["Email 'taken@example.com' is already taken."] },
        { status: 400, statusText: 'Bad Request' },
      );

    await settle();

    expect(element().textContent).toContain('already taken');
  });

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  // See the note in login.spec: let queued microtasks finish, then render.
  async function settle(): Promise<void> {
    await fixture.whenStable();
    fixture.detectChanges();
  }

  function fill(email: string, password: string): void {
    (fixture.componentInstance as unknown as { form: FormGroup }).form.setValue({ email, password });
  }

  async function submit(): Promise<void> {
    element().querySelector('form')!.dispatchEvent(new Event('submit'));

    await settle();
  }
});
