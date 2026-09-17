import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { provideRouter } from '@angular/router';
import { Observable, Subject } from 'rxjs';
import { installLocalStorage } from '../../../../testing/in-memory-storage';
import { User } from '../../../core/models/user';
import { AuthService } from '../../../core/services/auth.service';
import { AdminUsers } from './admin-users';

describe('AdminUsers', () => {
  let fixture!: ComponentFixture<AdminUsers>;
  let http!: HttpTestingController;
  let dialogResult!: Subject<unknown>;
  let opened!: ReturnType<typeof vi.fn>;

  const admin: User = {
    id: 'admin-1',
    email: 'admin@example.com',
    roles: ['User', 'Admin'],
    createdAtUtc: '2026-01-01T00:00:00Z',
  };

  const member: User = {
    id: 'user-2',
    email: 'member@example.com',
    roles: ['User'],
    createdAtUtc: '2026-02-01T00:00:00Z',
  };

  beforeEach(async () => {
    installLocalStorage();
    dialogResult = new Subject<unknown>();

    await TestBed.configureTestingModule({
      imports: [AdminUsers],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    opened = vi.spyOn(TestBed.inject(MatSnackBar), 'open').mockReturnValue({} as never);

    // Stand in for every dialog, so the flows can be driven without an overlay.
    vi.spyOn(TestBed.inject(MatDialog), 'open').mockReturnValue({
      afterClosed: () => dialogResult.asObservable() as Observable<unknown>,
    } as never);

    vi.spyOn(TestBed.inject(AuthService), 'user').mockReturnValue(admin);

    fixture = TestBed.createComponent(AdminUsers);
    http = TestBed.inject(HttpTestingController);

    await settle();
  });

  afterEach(() => http.verify());

  it('lists the accounts it is given', async () => {
    flushUsers([admin, member]);

    await settle();

    expect(rows().length).toBe(2);
    expect(element().textContent).toContain('admin@example.com');
    expect(element().textContent).toContain('member@example.com');
  });

  it('will not let an administrator delete their own account', async () => {
    flushUsers([admin, member]);

    await settle();

    const ownRow = rows()[0];
    const deleteButton = [...ownRow.querySelectorAll('button')].find((button) =>
      button.textContent?.includes('Delete'),
    );

    expect(deleteButton?.disabled).toBe(true);
  });

  it('deletes an account once the confirmation comes back', async () => {
    flushUsers([admin, member]);

    await settle();

    clickInRow(rows()[1], 'Delete');

    http.expectNone('/api/admin/users/user-2');

    dialogResult.next(true);

    await settle();

    const request = http.expectOne('/api/admin/users/user-2');

    expect(request.request.method).toBe('DELETE');

    request.flush(null, { status: 204, statusText: 'No Content' });

    await settle();

    // The list is reloaded after a successful delete.
    http.expectOne((candidate) => candidate.url === '/api/admin/users').flush([admin]);

    await settle();

    expect(opened).toHaveBeenCalledWith(
      'Deleted member@example.com.',
      'Dismiss',
      expect.anything(),
    );
  });

  it('deletes nothing when the confirmation is dismissed', async () => {
    flushUsers([admin, member]);

    await settle();

    clickInRow(rows()[1], 'Delete');

    dialogResult.next(false);

    await settle();

    http.expectNone('/api/admin/users/user-2');
  });

  it('reports a failure from the API', async () => {
    http
      .expectOne((candidate) => candidate.url === '/api/admin/users')
      .flush({ errors: ['Forbidden.'] }, { status: 403, statusText: 'Forbidden' });

    await settle();

    expect(opened).toHaveBeenCalledWith('Forbidden.', 'Dismiss', expect.anything());
  });

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  // See the note in login.spec: let queued microtasks finish, then render.
  async function settle(): Promise<void> {
    await fixture.whenStable();
    fixture.detectChanges();
  }

  function rows(): HTMLElement[] {
    return [...element().querySelectorAll('tbody tr')] as HTMLElement[];
  }

  function clickInRow(row: HTMLElement, label: string): void {
    const button = [...row.querySelectorAll('button')].find((candidate) =>
      candidate.textContent?.includes(label),
    );

    button!.click();
  }

  function flushUsers(users: User[]): void {
    http.expectOne((candidate) => candidate.url === '/api/admin/users').flush(users);
  }
});
