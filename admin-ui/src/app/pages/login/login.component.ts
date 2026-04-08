import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, MatCardModule],
  template: `
    <div style="display:flex;align-items:center;justify-content:center;
                min-height:100vh;background:#f5f5f5">
      <mat-card style="max-width:400px;width:100%;padding:40px 32px;text-align:center">
        <mat-icon style="font-size:48px;width:48px;height:48px;color:#3f51b5;margin-bottom:16px">
          policy
        </mat-icon>
        <h1 style="margin:0 0 8px;font-size:22px;font-weight:700;color:rgba(0,0,0,.87)">
          Rating Engine Admin
        </h1>
        <p style="margin:0 0 32px;font-size:14px;color:rgba(0,0,0,.54)">
          Sign in with your organisation account to continue.
        </p>
        <button mat-flat-button color="primary" style="width:100%;height:44px;font-size:15px"
                (click)="signIn()">
          <mat-icon style="margin-right:8px">login</mat-icon>
          Sign In
        </button>
      </mat-card>
    </div>
  `
})
export class LoginComponent implements OnInit {
  private returnUrl = '/products';

  constructor(private route: ActivatedRoute, private router: Router) {}

  ngOnInit() {
    this.returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/products';

    // If a token is already present (e.g. returning from OIDC callback) skip login
    const token = localStorage.getItem('access_token') || sessionStorage.getItem('access_token');
    if (token) this.router.navigateByUrl(this.returnUrl);
  }

  signIn() {
    // TODO: Replace with OIDC redirect once AuthService is wired up.
    // e.g. this.authService.initiateLogin(this.returnUrl);
    //
    // For now, redirect to the OIDC issuer's authorisation endpoint using the
    // values already configured in environment.oidc.
    const { issuer, clientId, scope, redirectUri } = environment.oidc;
    const params = new URLSearchParams({
      response_type: 'code',
      client_id:     clientId,
      redirect_uri:  redirectUri,
      scope,
      state: this.returnUrl,
    });
    window.location.href = `${issuer}/connect/authorize?${params}`;
  }
}
