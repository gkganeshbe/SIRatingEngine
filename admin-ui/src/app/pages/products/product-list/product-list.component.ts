import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { isExpired } from '../../../core/utils/date.utils';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDividerModule } from '@angular/material/divider';
import { ProductService } from '../../../core/services/product.service';
import { ProductSummary } from '../../../core/models/api.models';
import { ProductFormComponent } from '../product-form/product-form.component';
import { ConfirmDialogComponent } from '../../../shared/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-product-list',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule, MatButtonModule, MatIconModule, MatDialogModule,
    MatChipsModule, MatTooltipModule, MatProgressSpinnerModule, MatDividerModule,
  ],
  template: `
    <div class="page-container">

      <!-- Page header -->
      <div class="action-bar">
        <div style="flex:1">
          <h2 style="margin:0 0 4px">Products</h2>
          <p style="margin:0;color:rgba(0,0,0,.54);font-size:13px">
            Each product groups coverage configurations. Select a product to view and manage its coverages,
            rating pipeline steps, and rate tables.
          </p>
        </div>
        <button mat-flat-button color="primary" (click)="openCreate()">
          <mat-icon>add</mat-icon> New Product
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
      </div>

      <!-- Empty state -->
      <div *ngIf="!loading && !error && products.length === 0"
           style="text-align:center;padding:64px;color:rgba(0,0,0,.38)">
        <mat-icon style="font-size:48px;width:48px;height:48px;margin-bottom:16px">inventory_2</mat-icon>
        <p style="font-size:16px;margin:0 0 8px">No products yet</p>
        <p style="margin:0 0 24px;font-size:13px">Create your first product to start configuring coverages.</p>
        <button mat-flat-button color="primary" (click)="openCreate()">
          <mat-icon>add</mat-icon> Create First Product
        </button>
      </div>

      <!-- Product cards -->
      <div *ngIf="!loading && !error && products.length > 0"
           style="display:grid;grid-template-columns:repeat(auto-fill,minmax(320px,1fr));gap:16px">

        <mat-card *ngFor="let p of products" [class.expired-row]="p.expireAt"
                  style="display:flex;flex-direction:column">
          <mat-card-content style="flex:1;padding:20px 20px 8px">

            <!-- Status chip -->
            <div style="display:flex;align-items:flex-start;justify-content:space-between;margin-bottom:12px">
              <mat-chip [color]="isExpired(p.expireAt) ? 'warn' : 'primary'" highlighted style="font-size:11px">
                {{isExpired(p.expireAt) ? 'Expired' : 'Active'}}
              </mat-chip>
              <span style="font-size:11px;color:rgba(0,0,0,.38)">v{{p.version}}</span>
            </div>

            <!-- Product code -->
            <div style="font-size:20px;font-weight:700;margin-bottom:4px;letter-spacing:.5px">
              {{p.productCode}}
            </div>

            <!-- Meta -->
            <div style="font-size:12px;color:rgba(0,0,0,.54);margin-bottom:4px">
              Effective from <strong>{{p.effStart}}</strong>
              <span *ngIf="isExpired(p.expireAt)"> · Expired <strong style="color:#f44336">{{p.expireAt}}</strong></span>
            </div>
            <div *ngIf="p.createdBy" style="font-size:12px;color:rgba(0,0,0,.38)">
              Created by {{p.createdBy}}
            </div>
          </mat-card-content>

          <mat-divider></mat-divider>

          <!-- Primary action -->
          <div style="padding:12px 16px 8px">
            <button mat-flat-button color="primary" style="width:100%" (click)="openDetail(p)">
              <mat-icon>rule</mat-icon>
              View Coverages &amp; Rating Config
            </button>
          </div>

          <!-- Secondary actions -->
          <div style="padding:0 8px 8px;display:flex;gap:4px">
            <button mat-button (click)="openEdit(p)" style="flex:1">
              <mat-icon>edit</mat-icon> Edit
            </button>
            <button mat-button (click)="expire(p)" [disabled]="isExpired(p.expireAt)" style="flex:1">
              <mat-icon>event_busy</mat-icon> Expire
            </button>
            <button mat-button color="warn" (click)="delete(p)" style="flex:1">
              <mat-icon>delete</mat-icon> Delete
            </button>
          </div>
        </mat-card>

      </div>
    </div>
  `
})
export class ProductListComponent implements OnInit {
  readonly isExpired = isExpired;
  products: ProductSummary[] = [];
  loading = true;
  error = '';

  constructor(
    private svc: ProductService,
    private dialog: MatDialog,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() { this.load(); }

  load() {
    this.loading = true;
    this.error = '';
    this.svc.list().subscribe({
      next:  d  => { this.products = d; this.loading = false; this.cdr.detectChanges(); },
      error: (e) => { this.loading = false; this.error = e?.message ?? 'Failed to load products'; this.cdr.detectChanges(); }
    });
  }

  openDetail(p: ProductSummary) {
    this.router.navigate(['/products', p.id], { queryParams: { pc: p.productCode, v: p.version } });
  }

  openCreate() {
    this.dialog.open(ProductFormComponent, { width: '560px', data: null })
      .afterClosed().subscribe(saved => { if (saved) this.load(); });
  }

  openEdit(p: ProductSummary) {
    this.dialog.open(ProductFormComponent, { width: '560px', data: p })
      .afterClosed().subscribe(saved => { if (saved) this.load(); });
  }

  expire(p: ProductSummary) {
    const today = new Date().toISOString().slice(0, 10);
    this.svc.expire(p.id, today).subscribe(() => this.load());
  }

  delete(p: ProductSummary) {
    this.dialog.open(ConfirmDialogComponent, {
      data: { title: 'Delete Product', message: `Delete ${p.productCode} v${p.version}? This cannot be undone.` }
    }).afterClosed().subscribe(ok => { if (ok) this.svc.delete(p.id).subscribe(() => this.load()); });
  }
}
