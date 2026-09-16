import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export interface ResetPasswordData {
  email: string;
}

export interface ResetPasswordResult {
  newPassword: string;
}

@Component({
  selector: 'app-reset-password-dialog',
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  template: `
    <h2 mat-dialog-title>Reset password</h2>

    <mat-dialog-content>
      <p class="dialog-note">
        Set a new password for <strong>{{ data.email }}</strong>. There is no reset email in this
        build, so you will need to pass the new password on yourself.
      </p>

      <form [formGroup]="form" class="dialog-form">
        <mat-form-field appearance="outline">
          <mat-label>New password</mat-label>
          <input matInput type="text" formControlName="newPassword" autocomplete="off" />
          <mat-hint>At least 8 characters.</mat-hint>
          @if (form.controls.newPassword.hasError('minlength')) {
            <mat-error>Use at least 8 characters.</mat-error>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Cancel</button>
      <button
        mat-flat-button
        color="primary"
        type="button"
        [disabled]="form.invalid"
        (click)="submit()"
      >
        Reset password
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .dialog-note {
      margin: 0 0 1rem;
      font: var(--mat-sys-body-small);
    }

    .dialog-form {
      min-width: 22rem;
    }
  `,
})
export class ResetPasswordDialog {
  private readonly formBuilder = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<ResetPasswordDialog, ResetPasswordResult>);

  protected readonly data = inject<ResetPasswordData>(MAT_DIALOG_DATA);

  protected readonly form = this.formBuilder.nonNullable.group({
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
  });

  protected submit(): void {
    if (this.form.invalid) {
      return;
    }

    this.dialogRef.close({ newPassword: this.form.getRawValue().newPassword });
  }
}
