import { Component, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatCardModule } from '@angular/material/card';
import { UserService } from '../../core/services/user.service';
import { UserSummary, CreateUserRequest, ResetPasswordRequest } from '../../core/models/api.models';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';

// ── Create User Dialog ────────────────────────────────────────────────────────

@Component({
  selector: 'app-create-user-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatDialogModule, MatFormFieldModule, MatInputModule,
    MatSelectModule, MatButtonModule, MatIconModule,
  ],
  template: `
    <h2 mat-dialog-title>Create User</h2>
    <mat-dialog-content style="min-width:400px">
      <form [formGroup]="form" style="display:flex;flex-direction:column;gap:12px;padding-top:8px">

        <mat-form-field appearance="outline">
          <mat-label>Email</mat-label>
          <input matInput formControlName="email" type="email" placeholder="user@example.com" autocomplete="off">
          <mat-error *ngIf="form.get('email')?.hasError('required')">Email is required</mat-error>
          <mat-error *ngIf="form.get('email')?.hasError('email')">Enter a valid email address</mat-error>
        </mat-form-field>

        <div style="display:flex;gap:12px">
          <mat-form-field appearance="outline" style="flex:1">
            <mat-label>First Name</mat-label>
            <input matInput formControlName="firstName" autocomplete="off">
          </mat-form-field>
          <mat-form-field appearance="outline" style="flex:1">
            <mat-label>Last Name</mat-label>
            <input matInput formControlName="lastName" autocomplete="off">
          </mat-form-field>
        </div>

        <mat-form-field appearance="outline">
          <mat-label>Roles</mat-label>
          <mat-select formControlName="roles" multiple>
            <mat-option value="admin">Admin</mat-option>
            <mat-option value="user">User</mat-option>
            <mat-option value="readonly">Read Only</mat-option>
          </mat-select>
          <mat-hint>Roles control what the user can access</mat-hint>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Password</mat-label>
          <input matInput [type]="hidePassword ? 'password' : 'text'" formControlName="password" autocomplete="new-password">
          <button mat-icon-button matSuffix (click)="hidePassword = !hidePassword" type="button" [attr.aria-label]="'Toggle password'">
            <mat-icon>{{hidePassword ? 'visibility_off' : 'visibility'}}</mat-icon>
          </button>
          <mat-error *ngIf="form.get('password')?.hasError('required')">Password is required</mat-error>
          <mat-error *ngIf="form.get('password')?.hasError('minlength')">At least 8 characters</mat-error>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Confirm Password</mat-label>
          <input matInput [type]="hideConfirm ? 'password' : 'text'" formControlName="confirmPassword" autocomplete="new-password">
          <button mat-icon-button matSuffix (click)="hideConfirm = !hideConfirm" type="button" [attr.aria-label]="'Toggle confirm password'">
            <mat-icon>{{hideConfirm ? 'visibility_off' : 'visibility'}}</mat-icon>
          </button>
          <mat-error *ngIf="form.get('confirmPassword')?.hasError('required')">Please confirm the password</mat-error>
          <mat-error *ngIf="form.hasError('passwordMismatch') && !form.get('confirmPassword')?.hasError('required')">
            Passwords do not match
          </mat-error>
        </mat-form-field>

      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button color="primary" [disabled]="form.invalid || saving" (click)="submit()">
        <mat-icon>person_add</mat-icon> Create User
      </button>
    </mat-dialog-actions>
  `
})
export class CreateUserDialogComponent {
  readonly dialogRef = inject(MatDialogRef<CreateUserDialogComponent>);
  private fb = inject(FormBuilder);

  hidePassword = true;
  hideConfirm  = true;
  saving       = false;

  form: FormGroup = this.fb.group({
    email:           ['', [Validators.required, Validators.email]],
    firstName:       [''],
    lastName:        [''],
    roles:           [['user']],
    password:        ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', Validators.required],
  }, { validators: passwordMatchValidator });

  submit() {
    if (this.form.invalid) return;
    const { email, firstName, lastName, roles, password } = this.form.value;
    const req: CreateUserRequest = {
      email,
      password,
      firstName: firstName || null,
      lastName:  lastName  || null,
      roles:     roles     ?? [],
    };
    this.dialogRef.close(req);
  }
}

function passwordMatchValidator(control: AbstractControl): ValidationErrors | null {
  const pw  = control.get('password')?.value;
  const cpw = control.get('confirmPassword')?.value;
  return pw && cpw && pw !== cpw ? { passwordMismatch: true } : null;
}

// ── Reset Password Dialog ─────────────────────────────────────────────────────

@Component({
  selector: 'app-reset-password-dialog',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatDialogModule, MatFormFieldModule, MatInputModule,
    MatButtonModule, MatIconModule,
  ],
  template: `
    <h2 mat-dialog-title>Reset Password</h2>
    <mat-dialog-content style="min-width:360px">
      <p style="margin:0 0 16px;color:rgba(0,0,0,.6);font-size:13px">
        Set a new password for <strong>{{data.email}}</strong>.
      </p>
      <form [formGroup]="form" style="display:flex;flex-direction:column;gap:12px">
        <mat-form-field appearance="outline">
          <mat-label>New Password</mat-label>
          <input matInput [type]="hideNew ? 'password' : 'text'" formControlName="newPassword" autocomplete="new-password">
          <button mat-icon-button matSuffix (click)="hideNew = !hideNew" type="button">
            <mat-icon>{{hideNew ? 'visibility_off' : 'visibility'}}</mat-icon>
          </button>
          <mat-error *ngIf="form.get('newPassword')?.hasError('required')">Password is required</mat-error>
          <mat-error *ngIf="form.get('newPassword')?.hasError('minlength')">At least 8 characters</mat-error>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Confirm Password</mat-label>
          <input matInput [type]="hideConfirm ? 'password' : 'text'" formControlName="confirmPassword" autocomplete="new-password">
          <button mat-icon-button matSuffix (click)="hideConfirm = !hideConfirm" type="button">
            <mat-icon>{{hideConfirm ? 'visibility_off' : 'visibility'}}</mat-icon>
          </button>
          <mat-error *ngIf="form.get('confirmPassword')?.hasError('required')">Please confirm the password</mat-error>
          <mat-error *ngIf="form.hasError('passwordMismatch') && !form.get('confirmPassword')?.hasError('required')">
            Passwords do not match
          </mat-error>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button color="primary" [disabled]="form.invalid" (click)="submit()">
        <mat-icon>lock_reset</mat-icon> Reset Password
      </button>
    </mat-dialog-actions>
  `
})
export class ResetPasswordDialogComponent {
  readonly dialogRef = inject(MatDialogRef<ResetPasswordDialogComponent>);
  readonly data      = inject<{ email: string }>(MAT_DIALOG_DATA);
  private fb         = inject(FormBuilder);

  hideNew     = true;
  hideConfirm = true;

  form: FormGroup = this.fb.group({
    newPassword:     ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', Validators.required],
  }, { validators: resetPasswordMatchValidator });

  submit() {
    if (this.form.invalid) return;
    const req: ResetPasswordRequest = { newPassword: this.form.value.newPassword };
    this.dialogRef.close(req);
  }
}

function resetPasswordMatchValidator(control: AbstractControl): ValidationErrors | null {
  const pw  = control.get('newPassword')?.value;
  const cpw = control.get('confirmPassword')?.value;
  return pw && cpw && pw !== cpw ? { passwordMismatch: true } : null;
}

// ── User List Page ────────────────────────────────────────────────────────────

@Component({
  selector: 'app-user-list',
  standalone: true,
  imports: [
    CommonModule,
    MatTableModule, MatButtonModule, MatIconModule, MatDialogModule,
    MatChipsModule, MatProgressSpinnerModule, MatSnackBarModule,
    MatTooltipModule, MatCardModule,
  ],
  template: `
    <div class="page-container">

      <!-- Page header -->
      <div class="action-bar">
        <div style="flex:1">
          <h2 style="margin:0 0 4px">Users</h2>
          <p style="margin:0;color:rgba(0,0,0,.54);font-size:13px">
            Manage user accounts for this tenant. New users will be created in the identity server
            and can log in to this portal using their email and password.
          </p>
        </div>
        <button mat-flat-button color="primary" (click)="openCreate()">
          <mat-icon>person_add</mat-icon> New User
        </button>
      </div>

      <!-- Loading -->
      <div *ngIf="loading" style="text-align:center;padding:48px">
        <mat-spinner diameter="40" style="margin:auto"></mat-spinner>
      </div>

      <!-- Error -->
      <div *ngIf="error" style="text-align:center;padding:32px;color:#c62828">
        <mat-icon>error_outline</mat-icon>
        <p>{{error}}</p>
        <button mat-button color="primary" (click)="load()">Retry</button>
      </div>

      <!-- Empty state -->
      <div *ngIf="!loading && !error && users.length === 0"
           style="text-align:center;padding:64px;color:rgba(0,0,0,.38)">
        <mat-icon style="font-size:48px;width:48px;height:48px;margin-bottom:16px">group</mat-icon>
        <p style="font-size:16px;margin:0 0 8px">No users yet</p>
        <p style="margin:0 0 24px;font-size:13px">
          Create the first user for this tenant. The tenant admin account was created by the platform.
        </p>
        <button mat-flat-button color="primary" (click)="openCreate()">
          <mat-icon>person_add</mat-icon> Create First User
        </button>
      </div>

      <!-- User table -->
      <mat-card *ngIf="!loading && !error && users.length > 0" style="padding:0;overflow:hidden">
        <table mat-table [dataSource]="users" style="width:100%">

          <!-- Name column -->
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef style="width:200px">Name</th>
            <td mat-cell *matCellDef="let u">
              <div style="font-weight:500">{{fullName(u)}}</div>
              <div style="font-size:12px;color:rgba(0,0,0,.54)">{{u.email}}</div>
            </td>
          </ng-container>

          <!-- Roles column -->
          <ng-container matColumnDef="roles">
            <th mat-header-cell *matHeaderCellDef>Roles</th>
            <td mat-cell *matCellDef="let u">
              <mat-chip-set>
                <mat-chip *ngFor="let r of u.roles" style="font-size:11px">{{r}}</mat-chip>
              </mat-chip-set>
            </td>
          </ng-container>

          <!-- Created column -->
          <ng-container matColumnDef="createdAt">
            <th mat-header-cell *matHeaderCellDef style="width:160px">Created</th>
            <td mat-cell *matCellDef="let u" style="font-size:12px;color:rgba(0,0,0,.54)">
              {{u.createdAt | date:'medium'}}
            </td>
          </ng-container>

          <!-- Actions column -->
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef style="width:120px;text-align:right">Actions</th>
            <td mat-cell *matCellDef="let u" style="text-align:right">
              <button mat-icon-button
                      matTooltip="Reset password"
                      (click)="openResetPassword(u)">
                <mat-icon>lock_reset</mat-icon>
              </button>
              <button mat-icon-button
                      color="warn"
                      matTooltip="Delete user"
                      (click)="deleteUser(u)">
                <mat-icon>delete</mat-icon>
              </button>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns;"></tr>
        </table>
      </mat-card>

    </div>
  `,
  styles: [`
    .page-container { padding: 24px; max-width: 960px; }
    .action-bar     { display: flex; align-items: flex-start; margin-bottom: 24px; gap: 16px; }
  `]
})
export class UserListComponent implements OnInit {
  users: UserSummary[] = [];
  loading = true;
  error   = '';
  columns = ['name', 'roles', 'createdAt', 'actions'];

  constructor(
    private svc:    UserService,
    private dialog: MatDialog,
    private snack:  MatSnackBar,
    private cdr:    ChangeDetectorRef
  ) {}

  ngOnInit() { this.load(); }

  load() {
    this.loading = true;
    this.error   = '';
    this.svc.list().subscribe({
      next:  d  => { this.users = d; this.loading = false; this.cdr.detectChanges(); },
      error: e  => { this.loading = false; this.error = e?.message ?? 'Failed to load users'; this.cdr.detectChanges(); }
    });
  }

  fullName(u: UserSummary): string {
    const parts = [u.firstName, u.lastName].filter(Boolean);
    return parts.length ? parts.join(' ') : u.email;
  }

  openCreate() {
    this.dialog.open(CreateUserDialogComponent, { width: '480px' })
      .afterClosed().subscribe((req: CreateUserRequest | undefined) => {
        if (!req) return;
        this.svc.create(req).subscribe({
          next:  () => { this.snack.open('User created', 'Dismiss', { duration: 3000 }); this.load(); },
          error: e  => this.snack.open(e?.error?.message ?? 'Failed to create user', 'Dismiss', { duration: 5000 })
        });
      });
  }

  openResetPassword(u: UserSummary) {
    this.dialog.open(ResetPasswordDialogComponent, { width: '420px', data: { email: u.email } })
      .afterClosed().subscribe((req: ResetPasswordRequest | undefined) => {
        if (!req) return;
        this.svc.resetPassword(u.id, req).subscribe({
          next:  () => this.snack.open('Password reset successfully', 'Dismiss', { duration: 3000 }),
          error: e  => this.snack.open(e?.error?.message ?? 'Failed to reset password', 'Dismiss', { duration: 5000 })
        });
      });
  }

  deleteUser(u: UserSummary) {
    this.dialog.open(ConfirmDialogComponent, {
      data: {
        title:        'Delete User',
        message:      `Delete ${u.email}? This will remove their account from the identity server and cannot be undone.`,
        confirmLabel: 'Delete',
        confirmColor: 'warn',
      }
    }).afterClosed().subscribe(ok => {
      if (!ok) return;
      this.svc.delete(u.id).subscribe({
        next:  () => { this.snack.open('User deleted', 'Dismiss', { duration: 3000 }); this.load(); },
        error: e  => this.snack.open(e?.error?.message ?? 'Failed to delete user', 'Dismiss', { duration: 5000 })
      });
    });
  }
}
