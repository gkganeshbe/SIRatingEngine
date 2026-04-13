import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { AuthConfig, OAuthService } from 'angular-oauth2-oidc';
import { BehaviorSubject, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

function buildAuthConfig(): AuthConfig {
  const oidc = environment.oidc;
  return {
    issuer:               oidc.issuer,
    redirectUri:          oidc.redirectUri,
    postLogoutRedirectUri: oidc.postLogoutRedirectUri,
    clientId:             oidc.clientId,
    responseType:         'code',
    scope:                oidc.scope,
    requireHttps:         false,  // Allow http identity servers (local IIS)
    showDebugInformation: oidc.showDebugInformation ?? false,

    // Do not compare the issuer in the discovery document against the
    // configured issuer URL. Local IIS deployments often return a different
    // scheme/port/trailing-slash than what the client has configured.
    strictDiscoveryDocumentValidation: false,
    skipIssuerCheck: true,
  };
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private userNameSubject = new BehaviorSubject<string>('');
  public userName$: Observable<string> = this.userNameSubject.asObservable();

  constructor(private oauthService: OAuthService, private router: Router) {}

  public init(): void {
    this.oauthService.configure(buildAuthConfig());
    this.oauthService.setupAutomaticSilentRefresh();

    // Load the discovery document and complete the code exchange if the browser
    // is returning from the identity server (?code=... in the URL).
    // Errors are caught so a transient discovery failure does not break the app —
    // the user can still click "Continue to Sign In" to retry.
    this.oauthService.loadDiscoveryDocumentAndTryLogin()
      .then(() => {
        if (this.oauthService.hasValidAccessToken()) {
          this.loadUserProfile();

          // If we just finished processing an OIDC callback, navigate away from login/callback
          // to prevent the user from being stuck on the Tenant ID selection screen.
          if (window.location.search.includes('code=') || this.router.url.includes('/login') || this.router.url.includes('/callback')) {
            this.router.navigateByUrl('/products');
          }
        }
      })
      .catch(err => console.warn('[Auth] Discovery document load failed on startup:', err));
  }

  public login(): void {
    // Re-fetch the discovery document then redirect to the identity server.
    // This makes login work even when the initial document load at startup
    // failed — the button click always does a fresh fetch before redirecting.
    this.oauthService.loadDiscoveryDocumentAndLogin();
  }

  public logout(): void {
    this.oauthService.logOut();
  }

  public isAuthenticated(): boolean {
    return this.oauthService.hasValidAccessToken();
  }

  public getAccessToken(): string {
    return this.oauthService.getAccessToken();
  }

  private loadUserProfile(): void {
    const claims: any = this.oauthService.getIdentityClaims();
    if (claims) {
      this.userNameSubject.next(claims['name'] || claims['preferred_username'] || claims['sub'] || 'Admin User');
    }
  }
}