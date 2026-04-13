import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { TenantService } from '../../core/services/tenant.service';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule
  ],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss']
})
export class LoginComponent implements OnInit {
  loginForm: FormGroup;
  returnUrl: string = '/';
  isLoading = false;
  error = '';

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private tenantService: TenantService,
    private router: Router,
    private route: ActivatedRoute
  ) {
    this.loginForm = this.fb.group({
      tenantId: [this.tenantService.tenantId || 'tenant-acme', Validators.required]
    });
  }

  ngOnInit(): void {
    // Get return url from route parameters or default to products page
    this.returnUrl = this.route.snapshot.queryParams['returnUrl'] || '/products';

    // If we are currently handling an OIDC callback or are already authenticated,
    // show the loading spinner to prevent the tenant selection form from flashing.
    const isOidcCallback = window.location.search.includes('code=') || this.router.url.includes('code=') || this.router.url.includes('/callback');
    if (isOidcCallback || this.authService.isAuthenticated()) {
      this.isLoading = true;
      // The actual redirect will be handled by AuthService once the token exchange finishes.
      return;
    }
  }

  onSubmit(): void {
    if (this.loginForm.invalid) return;

    this.isLoading = true;
    this.error = '';

    const { tenantId } = this.loginForm.value;

    // Set the tenant scope, then redirect to the Identity Server login page
    this.tenantService.setTenantId(tenantId);
    this.authService.login();
  }
}