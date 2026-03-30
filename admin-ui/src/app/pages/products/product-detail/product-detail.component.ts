import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { ProductService } from '../../../core/services/product.service';
import { CoverageService } from '../../../core/services/coverage.service';
import { ProductDetail, CoverageSummary } from '../../../core/models/api.models';
import { CoverageFormComponent } from '../../coverages/coverage-form/coverage-form.component';
import { ConfirmDialogComponent } from '../../../shared/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-product-detail',
  standalone: true,
  imports: [
    CommonModule, RouterModule,
    MatCardModule, MatButtonModule, MatIconModule, MatTableModule,
    MatChipsModule, MatProgressSpinnerModule, MatTooltipModule,
    MatDialogModule, MatDividerModule,
  ],
  template: `
    <div *ngIf="loading" style="text-align:center;padding:64px">
      <mat-spinner diameter="48" style="margin:auto"></mat-spinner>
    </div>

    <div class="page-container" *ngIf="!loading && product">

      <!-- Breadcrumb -->
      <div style="display:flex;align-items:center;gap:8px;margin-bottom:16px;color:rgba(0,0,0,.6)">
        <a routerLink="/products" style="color:inherit;text-decoration:none">Products</a>
        <mat-icon style="font-size:16px;width:16px;height:16px">chevron_right</mat-icon>
        <span>{{product.productCode}} v{{product.version}}</span>
      </div>

      <!-- Product header -->
      <mat-card style="margin-bottom:24px">
        <mat-card-content>
          <div style="display:flex;align-items:flex-start;gap:32px;flex-wrap:wrap">
            <div>
              <div style="font-size:11px;color:rgba(0,0,0,.54);margin-bottom:2px">Product Code</div>
              <div style="font-size:20px;font-weight:600">{{product.productCode}}</div>
            </div>
            <div>
              <div style="font-size:11px;color:rgba(0,0,0,.54);margin-bottom:2px">Version</div>
              <div style="font-size:16px;font-weight:500">{{product.version}}</div>
            </div>
            <div>
              <div style="font-size:11px;color:rgba(0,0,0,.54);margin-bottom:2px">Eff Start</div>
              <div>{{product.effStart}}</div>
            </div>
            <div *ngIf="product.expireAt">
              <div style="font-size:11px;color:rgba(0,0,0,.54);margin-bottom:2px">Expires</div>
              <div style="color:#f44336;font-weight:500">{{product.expireAt}}</div>
            </div>
            <div *ngIf="product.createdBy">
              <div style="font-size:11px;color:rgba(0,0,0,.54);margin-bottom:2px">Created By</div>
              <div>{{product.createdBy}}</div>
            </div>
          </div>
        </mat-card-content>
      </mat-card>

      <!-- Coverages section -->
      <div class="action-bar" style="margin-bottom:4px">
        <div style="flex:1">
          <h3 style="margin:0 0 2px">Coverage Configurations</h3>
          <p style="margin:0;font-size:12px;color:rgba(0,0,0,.54)">
            Each coverage defines perils, a rating pipeline, and rate tables.
            Click a coverage to configure its pipeline steps and rate tables.
          </p>
        </div>
        <button mat-flat-button color="primary" (click)="addCoverage()" style="margin-left:16px">
          <mat-icon>add</mat-icon> Add Coverage
        </button>
      </div>

      <mat-card style="margin-top:12px">
        <mat-card-content>
          <div *ngIf="coveragesLoading" style="text-align:center;padding:32px">
            <mat-spinner diameter="36" style="margin:auto"></mat-spinner>
          </div>

          <div *ngIf="!coveragesLoading && coverages.length === 0"
               style="text-align:center;padding:48px;color:rgba(0,0,0,.38)">
            <mat-icon style="font-size:36px;width:36px;height:36px;margin-bottom:12px">rule</mat-icon>
            <p style="margin:0 0 6px;font-size:15px">No coverages yet</p>
            <p style="margin:0 0 20px;font-size:12px">
              Add a coverage to define what perils are rated, how the premium is calculated,
              and which rate tables are used.
            </p>
            <button mat-flat-button color="primary" (click)="addCoverage()">
              <mat-icon>add</mat-icon> Add First Coverage
            </button>
          </div>

          <table mat-table [dataSource]="coverages" *ngIf="!coveragesLoading && coverages.length > 0" style="width:100%">

            <ng-container matColumnDef="state">
              <th mat-header-cell *matHeaderCellDef>State</th>
              <td mat-cell *matCellDef="let c"><mat-chip>{{c.state}}</mat-chip></td>
            </ng-container>

            <ng-container matColumnDef="coverageCode">
              <th mat-header-cell *matHeaderCellDef>Coverage Code</th>
              <td mat-cell *matCellDef="let c"><strong>{{c.coverageCode}}</strong></td>
            </ng-container>

            <ng-container matColumnDef="version">
              <th mat-header-cell *matHeaderCellDef>Version</th>
              <td mat-cell *matCellDef="let c">{{c.version}}</td>
            </ng-container>

            <ng-container matColumnDef="effStart">
              <th mat-header-cell *matHeaderCellDef>Eff Start</th>
              <td mat-cell *matCellDef="let c">{{c.effStart}}</td>
            </ng-container>

            <ng-container matColumnDef="expireAt">
              <th mat-header-cell *matHeaderCellDef>Expires</th>
              <td mat-cell *matCellDef="let c">
                <mat-chip *ngIf="c.expireAt" color="warn" highlighted>{{c.expireAt}}</mat-chip>
                <span *ngIf="!c.expireAt" style="color:rgba(0,0,0,.38)">—</span>
              </td>
            </ng-container>

            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let c" style="text-align:right" (click)="$event.stopPropagation()">
                <button mat-icon-button matTooltip="Pipeline & Rate Tables" color="primary"
                        (click)="openCoverage(c)">
                  <mat-icon>tune</mat-icon>
                </button>
                <button mat-icon-button matTooltip="Edit" (click)="editCoverage(c)">
                  <mat-icon>edit</mat-icon>
                </button>
                <button mat-icon-button matTooltip="Expire" (click)="expireCoverage(c)"
                        [disabled]="!!c.expireAt">
                  <mat-icon>event_busy</mat-icon>
                </button>
                <button mat-icon-button matTooltip="Delete" color="warn" (click)="deleteCoverage(c)">
                  <mat-icon>delete</mat-icon>
                </button>
              </td>
            </ng-container>

            <tr mat-header-row *matHeaderRowDef="coverageColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: coverageColumns;"
                [class.expired-row]="row.expireAt"
                style="cursor:pointer" (click)="openCoverage(row)"></tr>
          </table>
        </mat-card-content>
      </mat-card>
    </div>
  `
})
export class ProductDetailComponent implements OnInit {
  product: ProductDetail | null = null;
  coverages: CoverageSummary[] = [];
  loading = true;
  coveragesLoading = false;
  coverageColumns = ['state', 'coverageCode', 'version', 'effStart', 'expireAt', 'actions'];

  private productId = 0;
  private productCode = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private productSvc: ProductService,
    private coverageSvc: CoverageService,
    private dialog: MatDialog,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.productId   = +this.route.snapshot.paramMap.get('productId')!;
    this.productCode = this.route.snapshot.queryParamMap.get('pc') ?? '';
    const version    = this.route.snapshot.queryParamMap.get('v') ?? '';

    this.productSvc.get(this.productCode, version).subscribe({
      next: d => {
        this.product = d;
        this.loading = false;
        this.cdr.detectChanges();
        this.loadCoverages();
      },
      error: () => { this.loading = false; this.cdr.detectChanges(); }
    });
  }

  loadCoverages() {
    this.coveragesLoading = true;
    this.coverageSvc.list(this.productCode).subscribe({
      next: d => { this.coverages = d; this.coveragesLoading = false; this.cdr.detectChanges(); },
      error: () => { this.coveragesLoading = false; this.cdr.detectChanges(); }
    });
  }

  openCoverage(c: CoverageSummary) {
    this.router.navigate(['/coverages', c.id], {
      queryParams: { pc: c.productCode, cc: c.coverageCode, v: c.version, productId: this.productId }
    });
  }

  addCoverage() {
    this.dialog.open(CoverageFormComponent, {
      width: '560px',
      data: { productCode: this.productCode }
    }).afterClosed().subscribe(saved => { if (saved) this.loadCoverages(); });
  }

  editCoverage(c: CoverageSummary) {
    this.dialog.open(CoverageFormComponent, { width: '560px', data: c })
      .afterClosed().subscribe(saved => { if (saved) this.loadCoverages(); });
  }

  expireCoverage(c: CoverageSummary) {
    const today = new Date().toISOString().slice(0, 10);
    this.coverageSvc.expire(c.id, today).subscribe(() => this.loadCoverages());
  }

  deleteCoverage(c: CoverageSummary) {
    this.dialog.open(ConfirmDialogComponent, {
      data: { title: 'Delete Coverage', message: `Delete ${c.coverageCode} v${c.version}?` }
    }).afterClosed().subscribe(ok => {
      if (ok) this.coverageSvc.delete(c.id).subscribe(() => this.loadCoverages());
    });
  }
}
