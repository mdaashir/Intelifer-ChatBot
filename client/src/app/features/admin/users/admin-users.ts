import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { firstValueFrom } from 'rxjs';
import { extractErrors } from '../../../core/http-error';
import { RoleNames, User } from '../../../core/models/user';
import { AuthService } from '../../../core/services/auth.service';
import { UserManagementService } from '../../../core/services/user-management.service';
import { ConfirmData, ConfirmDialog } from './confirm-dialog';
import {
  ResetPasswordData,
  ResetPasswordDialog,
  ResetPasswordResult,
} from './reset-password-dialog';
import { UserFormData, UserFormDialog, UserFormResult } from './user-form-dialog';

@Component({
  selector: 'app-admin-users',
  imports: [DatePipe, MatButtonModule, MatCardModule, MatProgressBarModule, MatTableModule],
  templateUrl: './admin-users.html',
  styleUrl: './admin-users.css',
})
export class AdminUsers implements OnInit {
  private readonly userManagement = inject(UserManagementService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly columns = ['email', 'roles', 'createdAtUtc', 'actions'];
  protected readonly users = signal<User[]>([]);
  protected readonly loading = signal(true);
  protected readonly currentUserId = computed(() => this.auth.user()?.id);

  ngOnInit(): void {
    this.reload();
  }

  protected reload(): void {
    this.loading.set(true);

    this.userManagement.getAll().subscribe({
      next: (users) => {
        this.users.set(users);
        this.loading.set(false);
      },
      error: (error) => {
        this.loading.set(false);
        this.notify(extractErrors(error));
      },
    });
  }

  protected async addUser(): Promise<void> {
    const result = await firstValueFrom(
      this.dialog
        .open<UserFormDialog, UserFormData, UserFormResult>(UserFormDialog, {
          data: { user: null },
          width: '28rem',
        })
        .afterClosed(),
    );

    if (!result) {
      return;
    }

    this.userManagement
      .create({ email: result.email, password: result.password ?? '', role: result.role })
      .subscribe({
        next: () => {
          this.notify(`Created ${result.email}.`);
          this.reload();
        },
        error: (error) => this.notify(extractErrors(error)),
      });
  }

  protected async editUser(user: User): Promise<void> {
    const result = await firstValueFrom(
      this.dialog
        .open<UserFormDialog, UserFormData, UserFormResult>(UserFormDialog, {
          data: { user },
          width: '28rem',
        })
        .afterClosed(),
    );

    if (!result) {
      return;
    }

    this.userManagement.update(user.id, { email: result.email, role: result.role }).subscribe({
      next: () => {
        this.notify(`Updated ${result.email}.`);
        this.reload();
      },
      error: (error) => this.notify(extractErrors(error)),
    });
  }

  protected async resetPassword(user: User): Promise<void> {
    const result = await firstValueFrom(
      this.dialog
        .open<ResetPasswordDialog, ResetPasswordData, ResetPasswordResult>(ResetPasswordDialog, {
          data: { email: user.email },
          width: '28rem',
        })
        .afterClosed(),
    );

    if (!result) {
      return;
    }

    this.userManagement.resetPassword(user.id, result.newPassword).subscribe({
      next: () => this.notify(`Password reset for ${user.email}.`),
      error: (error) => this.notify(extractErrors(error)),
    });
  }

  protected async deleteUser(user: User): Promise<void> {
    const confirmed = await firstValueFrom(
      this.dialog
        .open<ConfirmDialog, ConfirmData, boolean>(ConfirmDialog, {
          data: {
            title: 'Delete user',
            message: `Delete ${user.email}? Every conversation belonging to this account is deleted with it.`,
            confirmLabel: 'Delete',
          },
          width: '26rem',
        })
        .afterClosed(),
    );

    if (!confirmed) {
      return;
    }

    this.userManagement.remove(user.id).subscribe({
      next: () => {
        this.notify(`Deleted ${user.email}.`);
        this.reload();
      },
      error: (error) => this.notify(extractErrors(error)),
    });
  }

  protected isAdministrator(user: User): boolean {
    return user.roles.includes(RoleNames.Admin);
  }

  private notify(messages: string[] | string): void {
    const text = Array.isArray(messages) ? messages.join(' ') : messages;

    this.snackBar.open(text, 'Dismiss', { duration: 5000 });
  }
}
