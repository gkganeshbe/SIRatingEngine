import { Routes } from '@angular/router';
import { adminAuthGuard } from './admin-auth.guard';

export const routes: Routes = [
  // ── Public routes (no guard) ──────────────────────────────────────────────
  { path: '', redirectTo: 'products', pathMatch: 'full' },
  {
    path: 'login',
    loadComponent: () => import('./pages/login/login.component')
      .then(m => m.LoginComponent)
  },

  // ── Protected routes — all guarded by adminAuthGuard ─────────────────────
  {
    path: '',
    canActivate: [adminAuthGuard],
    children: [
      {
        path: 'products',
        loadComponent: () => import('./pages/products/product-list/product-list.component')
          .then(m => m.ProductListComponent)
      },
      {
        path: 'products/:productId',
        loadComponent: () => import('./pages/products/product-detail/product-detail.component')
          .then(m => m.ProductDetailComponent)
      },
      {
        path: 'coverages',
        loadComponent: () => import('./pages/coverages/coverage-mapping/coverage-mapping-page.component')
          .then(m => m.CoverageMappingPageComponent)
      },
      {
        path: 'coverage-catalog/:coverageRefId',
        loadComponent: () => import('./pages/coverages/coverage-states/coverage-states.component')
          .then(m => m.CoverageStatesComponent)
      },
      {
        path: 'coverages/:id',
        loadComponent: () => import('./pages/coverages/coverage-detail/coverage-detail.component')
          .then(m => m.CoverageDetailComponent)
      },
      {
        // Legacy product-scoped route — still works from product-detail links
        path: 'products/:productCode/risk-fields',
        loadComponent: () => import('./pages/risk-fields/risk-field-list.component')
          .then(m => m.RiskFieldListComponent)
      },
      {
        // Global Risk Field Mapping page (nav entry)
        path: 'risk-fields',
        loadComponent: () => import('./pages/risk-fields/risk-field-mapping-page.component')
          .then(m => m.RiskFieldMappingPageComponent)
      },
      {
        path: 'lookups',
        loadComponent: () => import('./pages/lookups/lookups-page.component')
          .then(m => m.LookupsPageComponent)
      },
      {
        path: 'rate-tables/:coverageId/:name',
        loadComponent: () => import('./pages/rate-tables/rate-table-detail/rate-table-detail.component')
          .then(m => m.RateTableDetailComponent)
      },
      {
        path: 'test-rating',
        loadComponent: () => import('./pages/test-rating/test-rating-page.component')
          .then(m => m.TestRatingPageComponent)
      },
    ]
  },

  // ── Fallback ──────────────────────────────────────────────────────────────
  { path: '**', redirectTo: 'products' }
];
