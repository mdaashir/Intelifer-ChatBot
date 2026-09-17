import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormGroup } from '@angular/forms';
import { MatSnackBar } from '@angular/material/snack-bar';
import { provideRouter } from '@angular/router';
import { installLocalStorage } from '../../../testing/in-memory-storage';
import { AuthService } from '../../core/services/auth.service';
import { Profile } from './profile';

describe('Profile', () => {
  let fixture!: ComponentFixture<Profile>;
  let http!: HttpTestingController;
  let opened!: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    installLocalStorage();

    await TestBed.configureTestingModule({
      imports: [Profile],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    opened = vi.spyOn(TestBed.inject(MatSnackBar), 'open').mockReturnValue({} as never);

    // Show the account details without a round trip.
    vi.spyOn(TestBed.inject(AuthService), 'user').mockReturnValue({
      id: 'user-1',
      email: 'user@example.com',
      roles: ['User'],
      createdAtUtc: '2026-01-01T00:00:00Z',
    });

    fixture = TestBed.createComponent(Profile);
    http = TestBed.inject(HttpTestingController);

    await settle();
  });

  afterEach(() => http.verify());

  it('shows the signed-in account and its roles', () => {
    expect(element().textContent).toContain('user@example.com');
    expect(element().textContent).toContain('User');
  });

  it('does not call the API while the form is empty', async () => {
    await submit();

    http.expectNone('/api/auth/change-password');
  });

  it('changes the password and confirms it', async () => {
    fill('Password123', 'Password456');

    await submit();

    const request = http.expectOne('/api/auth/change-password');

    expect(request.request.body).toEqual({
      currentPassword: 'Password123',
      newPassword: 'Password456',
    });

    request.flush(null, { status: 204, statusText: 'No Content' });

    await settle();

    expect(opened).toHaveBeenCalledWith(
      'Your password has been changed.',
      'Dismiss',
      expect.anything(),
    );
  });

  it('shows the error the server reported', async () => {
    fill('wrong-current', 'Password456');

    await submit();

    http
      .expectOne('/api/auth/change-password')
      .flush({ errors: ['Incorrect password.'] }, { status: 400, statusText: 'Bad Request' });

    await settle();

    expect(element().textContent).toContain('Incorrect password.');
  });

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  // See the note in login.spec: let queued microtasks finish, then render.
  async function settle(): Promise<void> {
    await fixture.whenStable();
    fixture.detectChanges();
  }

  function fill(currentPassword: string, newPassword: string): void {
    (fixture.componentInstance as unknown as { form: FormGroup }).form.setValue({
      currentPassword,
      newPassword,
    });
  }

  async function submit(): Promise<void> {
    element().querySelector('form')!.dispatchEvent(new Event('submit'));

    await settle();
  }
});
