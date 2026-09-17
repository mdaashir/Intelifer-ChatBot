import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormGroup } from '@angular/forms';
import { Router, provideRouter } from '@angular/router';
import { installLocalStorage } from '../../../../testing/in-memory-storage';
import { Login } from './login';

describe('Login', () => {
  let fixture!: ComponentFixture<Login>;
  let http!: HttpTestingController;
  let navigate!: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    installLocalStorage();

    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    fixture = TestBed.createComponent(Login);
    http = TestBed.inject(HttpTestingController);

    await settle();
  });

  afterEach(() => http.verify());

  it('renders an email and a password field', () => {
    const inputs = element().querySelectorAll('input');

    expect(inputs.length).toBe(2);
    expect(inputs[0].getAttribute('type')).toBe('email');
    expect(inputs[1].getAttribute('type')).toBe('password');
  });

  it('does not call the API while the form is empty', async () => {
    await submit();

    http.expectNone('/api/auth/login');
    expect(navigate).not.toHaveBeenCalled();
  });

  it('tells the visitor when the email is not valid', async () => {
    const form = (fixture.componentInstance as unknown as { form: FormGroup }).form;

    form.setValue({ email: 'not-an-email', password: 'Password123' });

    // Material only surfaces a field error once the control has been touched.
    form.controls['email'].markAsTouched();

    await settle();

    expect(form.controls['email'].invalid).toBe(true);
    expect(element().textContent).toContain('valid email');
  });

  it('signs in and goes to the chat', async () => {
    fill('user@example.com', 'Password123');

    await submit();

    http.expectOne('/api/auth/login').flush({
      accessToken: 'token-123',
      expiresAtUtc: '2026-01-01T01:00:00Z',
      user: {
        id: 'user-1',
        email: 'user@example.com',
        roles: ['User'],
        createdAtUtc: '2026-01-01T00:00:00Z',
      },
    });

    await settle();

    expect(navigate).toHaveBeenCalledWith(['/chat']);
  });

  it('shows the error the server reported', async () => {
    fill('user@example.com', 'wrong-password');

    await submit();

    http
      .expectOne('/api/auth/login')
      .flush({ errors: ['Invalid email or password.'] }, { status: 400, statusText: 'Bad Request' });

    await settle();

    expect(element().textContent).toContain('Invalid email or password.');
    expect(navigate).not.toHaveBeenCalled();
  });

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  // Zoneless components do not re-render on their own in a test. Let any queued
  // microtask - a rejected request, a resolved promise - finish first, then render.
  // The other order renders the state from before the change.
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
