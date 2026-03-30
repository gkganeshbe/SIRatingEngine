export const environment = {
  production: false,
  apiUrl: 'http://localhost:7001',
  tenants: [
    { id: 'tenant-default', name: 'Default Tenant' }
  ],
  oidc: {
    issuer: 'http://localhost:7000',
    clientId: 'rating-engine-admin-ui',
    scope: 'openid profile email rating-engine.admin',
    redirectUri: window.location.origin + '/callback',
    postLogoutRedirectUri: window.location.origin,
    showDebugInformation: true,
  }
};
