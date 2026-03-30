import { Injectable } from '@angular/core';
import { OAuthService, AuthConfig } from 'angular-oauth2-oidc';
import { BehaviorSubject } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private _userName = new BehaviorSubject<string>('');
  userName$ = this._userName.asObservable();

  constructor(private oauthService: OAuthService) {}

  init() {
    // Skip OIDC in non-production environments — API runs with RequireAuth:false
    if (!environment.production) return;

    const config: AuthConfig = {
      issuer:                environment.oidc.issuer,
      clientId:              environment.oidc.clientId,
      scope:                 environment.oidc.scope,
      responseType:          'code',
      redirectUri:           environment.oidc.redirectUri,
      postLogoutRedirectUri: environment.oidc.postLogoutRedirectUri,
      showDebugInformation:  environment.oidc.showDebugInformation,
      useSilentRefresh:      false,
    };

    this.oauthService.configure(config);
    this.oauthService.loadDiscoveryDocumentAndTryLogin()
      .then(() => {
        if (this.oauthService.hasValidAccessToken()) {
          const claims = this.oauthService.getIdentityClaims() as Record<string, string>;
          this._userName.next(claims?.['name'] ?? claims?.['email'] ?? 'User');
        }
      })
      .catch(() => {
        console.info('[Auth] OIDC discovery unavailable — running in unauthenticated dev mode.');
      });
  }

  login()  { if (environment.production) this.oauthService.initCodeFlow(); }
  logout() { if (environment.production) this.oauthService.logOut(); }

  get token():  string  {
    return environment.production ? this.oauthService.getAccessToken() : '';
  }
  get userId(): string  {
    if (!environment.production) return '';
    const claims = this.oauthService.getIdentityClaims() as Record<string, string>;
    return claims?.['sub'] ?? claims?.['email'] ?? '';
  }
  get isLoggedIn(): boolean {
    return environment.production ? this.oauthService.hasValidAccessToken() : true;
  }
}
