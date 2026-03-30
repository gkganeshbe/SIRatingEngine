import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: 'products', pathMatch: 'full' },
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
    path: 'coverages/:id',
    loadComponent: () => import('./pages/coverages/coverage-detail/coverage-detail.component')
      .then(m => m.CoverageDetailComponent)
  },
  {
    path: 'rate-tables/:coverageId/:name',
    loadComponent: () => import('./pages/rate-tables/rate-table-detail/rate-table-detail.component')
      .then(m => m.RateTableDetailComponent)
  },
  { path: '**', redirectTo: 'products' }
];
