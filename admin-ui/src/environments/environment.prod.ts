export const environment = {
  production: true,
  // URL of the deployed Rating Engine Admin API.
  // Update this to match wherever your Admin API is hosted on IIS.
  apiUrl: 'http://localhost:7001',
  tenants: [
    { id: 'QARatingEngine', name: 'QA Rating Engine' }
  ],
  oidc: {
    issuer: 'http://localhost:7000',
    clientId: 'rating-engine-admin-ui',
    scope: 'openid profile email rating-engine.admin',
    redirectUri: window.location.origin + '/callback',
    postLogoutRedirectUri: window.location.origin,
    showDebugInformation: false,
  }
};
