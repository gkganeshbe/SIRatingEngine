import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { forkJoin } from 'rxjs';
import { ProductService } from '../../../core/services/product.service';
import { CoverageService } from '../../../core/services/coverage.service';
import { ProductStateService } from '../../../core/services/product-state.service';
import {
  ProductSummary, ProductDetail, CoverageRefDetail,
  CoverageSummary, ProductStateDetail,
} from '../../../core/models/api.models';
import { AddMappingDialogComponent, AddMappingDialogData } from './add-mapping-dialog.component';
import { ConfirmDialogComponent } from '../../../shared/confirm-dialog/confirm-dialog.component';
import { isExpired } from '../../../core/utils/date.utils';

export interface GridRow {
  type: 'config' | 'empty';
  lobCode?: string;
  coverageRef?: CoverageRefDetail;
  config?: CoverageSummary;
}

@Component({
  selector: 'app-coverage-mapping-page',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatSelectModule, MatFormFieldModule, MatButtonModule, MatIconModule,
    MatCardModule, MatTableModule, MatChipsModule, MatProgressSpinnerModule,
    MatTooltipModule, MatDialogModule,
  ],
  styles: [`
    .toolbar { display: flex; align-items: center; gap: 16px; flex-wrap: wrap; margin-bottom: 20px; }

    /* LOB filter bar */
    .lob-filter { display:flex; gap:8px; flex-wrap:wrap; align-items:center; padding:10px 16px; border-bottom:1px solid #e0e0e0; background:#fafafa; }
    .lob-btn-active  { background:#3f51b5 !important; color:#fff !important; border-color:#3f51b5 !important; }
    .lob-btn-active mat-icon { color:#fff !important; }
    .lob-btn-inactive { background:#fff !important; color:#3f51b5 !important; border-color:#9fa8da !important; }
    .lob-btn-inactive mat-icon { color:#5c6bc0 !important; }

    /* Unconfigured coverage row */
    .row-empty td { background: #fafafa; color: rgba(0,0,0,.38); font-style: italic; }
    .row-empty td:first-child { font-weight: 600; font-style: normal; color: rgba(0,0,0,.6); }

    /* Configured row — clickable */
    .row-config { cursor: pointer; }
    .row-config:hover td { background: #f3f4ff; }

    .state-chip {
      display: inline-flex; align-items: center; gap: 3px;
      padding: 2px 10px; border-radius: 12px; font-size: 12px; font-weight: 600;
      background: #e8f5e9; color: #2e7d32; border: 1px solid #a5d6a7;
    }
    .expire-chip {
      padding: 2px 8px; border-radius: 12px; font-size: 12px; font-weight: 500;
      background: #ffebee; color: #c62828; border: 1px solid #ef9a9a;
    }

    .empty-page {
      padding: 64px 24px; text-align: center; color: #bbb;
      border: 2px dashed #e0e0e0; border-radius: 8px;
    }
  `],
  template: `
    <div class="page-container">

      <!-- Header -->
      <div class="toolbar">
        <div style="flex:1">
          <h2 style="margin:0 0 2px">Coverage Mapping</h2>
          <p style="margin:0;font-size:13px;color:rgba(0,0,0,.54)">
            Map product coverages to supported states. Each entry has its own rating pipeline and rate tables.
          </p>
        </div>

        <!-- Product selector -->
        <mat-form-field style="width:300px" subscriptSizing="dynamic">
          <mat-label>Product</mat-label>
          <mat-select [(value)]="selectedProduct" (selectionChange)="onProductChange()">
            <mat-option *ngFor="let p of products" [value]="p">
              {{p.productCode}} &nbsp; v{{p.version}}
            </mat-option>
          </mat-select>
        </mat-form-field>

        <button mat-flat-button color="primary"
                [disabled]="!selectedProduct || !productDetail"
                (click)="openAddDialog(undefined)">
          <mat-icon>add</mat-icon> New Mapping
        </button>
      </div>

      <!-- Loading -->
      <div *ngIf="loading" style="text-align:center;padding:64px">
        <mat-spinner diameter="40" style="margin:auto"></mat-spinner>
      </div>

      <!-- No product selected -->
      <div *ngIf="!selectedProduct && !loading" class="empty-page">
        <mat-icon style="font-size:52px;width:52px;height:52px;color:#bdbdbd;margin-bottom:8px">map</mat-icon>
        <p style="margin:0;font-size:14px">Select a product to view its coverage mapping.</p>
      </div>

      <!-- Grid -->
      <mat-card *ngIf="selectedProduct && !loading && productDetail" style="overflow:hidden">

        <!-- Summary bar -->
        <div style="display:flex;gap:24px;padding:10px 16px;background:#f5f5f5;
                    border-bottom:1px solid #e0e0e0;font-size:12px;color:rgba(0,0,0,.54)">
          <span><strong style="color:rgba(0,0,0,.8)">{{totalCoverages}}</strong> coverage(s) in catalog</span>
          <span><strong style="color:rgba(0,0,0,.8)">{{configs.length}}</strong> configured mapping(s)</span>
          <span><strong style="color:rgba(0,0,0,.8)">{{productStates.length}}</strong> supported state(s)</span>
          <span *ngIf="productStates.length === 0" style="color:#e65100">
            <mat-icon style="font-size:13px;width:13px;height:13px;vertical-align:middle">warning</mat-icon>
            No states declared — go to the product to declare supported states
          </span>
        </div>

        <!-- LOB filter bar (commercial only) -->
        <div *ngIf="isCommercial" class="lob-filter">
          <span style="font-size:12px;color:rgba(0,0,0,.54);margin-right:4px">LOB:</span>
          <button mat-stroked-button
                  [class.lob-btn-active]="selectedLobCode === null"
                  [class.lob-btn-inactive]="selectedLobCode !== null"
                  (click)="selectedLobCode = null">
            All
          </button>
          <button *ngFor="let lob of productDetail!.lobs" mat-stroked-button
                  [class.lob-btn-active]="selectedLobCode === lob.lobCode"
                  [class.lob-btn-inactive]="selectedLobCode !== lob.lobCode"
                  (click)="selectedLobCode = lob.lobCode">
            <mat-icon style="font-size:15px;width:15px;height:15px;margin-right:4px">category</mat-icon>
            {{lob.lobCode}}
            <span style="margin-left:4px;opacity:.7;font-size:11px">({{lob.coverages.length}})</span>
          </button>
        </div>

        <table mat-table [dataSource]="gridRows" style="width:100%">

          <!-- LOB column (shown when no LOB filter active) -->
          <ng-container matColumnDef="lob">
            <th mat-header-cell *matHeaderCellDef style="width:90px">LOB</th>
            <td mat-cell *matCellDef="let row">
              <span style="display:inline-block;background:#e8eaf6;color:#3f51b5;
                           border-radius:3px;padding:1px 7px;font-size:11px;font-weight:600">
                {{row.lobCode}}
              </span>
            </td>
          </ng-container>

          <!-- Coverage column -->
          <ng-container matColumnDef="coverage">
            <th mat-header-cell *matHeaderCellDef>Coverage</th>
            <td mat-cell *matCellDef="let row">
              <strong>{{row.coverageRef?.coverageCode}}</strong>
            </td>
          </ng-container>

          <!-- State column -->
          <ng-container matColumnDef="state">
            <th mat-header-cell *matHeaderCellDef style="width:100px">State</th>
            <td mat-cell *matCellDef="let row">
              <span *ngIf="row.type === 'config'" class="state-chip">{{row.config?.state}}</span>
              <span *ngIf="row.type === 'empty'" style="font-size:12px;color:rgba(0,0,0,.38)">— not configured —</span>
            </td>
          </ng-container>

          <!-- Version column -->
          <ng-container matColumnDef="version">
            <th mat-header-cell *matHeaderCellDef style="width:90px">Version</th>
            <td mat-cell *matCellDef="let row" style="color:rgba(0,0,0,.6);font-size:13px">
              {{row.config?.version}}
            </td>
          </ng-container>

          <!-- Eff Start column -->
          <ng-container matColumnDef="effStart">
            <th mat-header-cell *matHeaderCellDef style="width:110px">Eff Start</th>
            <td mat-cell *matCellDef="let row" style="color:rgba(0,0,0,.6);font-size:13px">
              {{row.config?.effStart}}
            </td>
          </ng-container>

          <!-- Expires column -->
          <ng-container matColumnDef="expireAt">
            <th mat-header-cell *matHeaderCellDef style="width:110px">Expires</th>
            <td mat-cell *matCellDef="let row">
              <span *ngIf="isExpired(row.config?.expireAt)" class="expire-chip">{{row.config?.expireAt}}</span>
              <span *ngIf="row.type === 'config' && !isExpired(row.config?.expireAt)"
                    style="color:rgba(0,0,0,.25);font-size:13px">&mdash;</span>
            </td>
          </ng-container>

          <!-- Actions column -->
          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef style="width:220px;text-align:right">Actions</th>
            <td mat-cell *matCellDef="let row" style="text-align:right;white-space:nowrap">
              <ng-container *ngIf="row.type === 'config'">
                <button mat-icon-button matTooltip="Coverage Details" color="primary"
                        (click)="openConfig(row.config!, 0); $event.stopPropagation()">
                  <mat-icon>info_outline</mat-icon>
                </button>
                <button mat-icon-button matTooltip="Rate Tables"
                        (click)="openConfig(row.config!, 1); $event.stopPropagation()">
                  <mat-icon>table_chart</mat-icon>
                </button>
                <button mat-icon-button matTooltip="Rating Pipeline"
                        (click)="openConfig(row.config!, 2); $event.stopPropagation()">
                  <mat-icon>account_tree</mat-icon>
                </button>
                <button mat-icon-button matTooltip="Expire"
                        [disabled]="isExpired(row.config?.expireAt)"
                        (click)="expire(row.config!); $event.stopPropagation()">
                  <mat-icon>event_busy</mat-icon>
                </button>
                <button mat-icon-button matTooltip="Delete" color="warn"
                        (click)="delete(row.config!); $event.stopPropagation()">
                  <mat-icon>delete</mat-icon>
                </button>
              </ng-container>
              <button *ngIf="row.type === 'empty'" mat-stroked-button style="font-size:12px;height:32px"
                      (click)="addConfig(row.coverageRef!); $event.stopPropagation()">
                <mat-icon style="font-size:16px;width:16px;height:16px">add</mat-icon>
                Add State Config
              </button>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns; when: isConfigRow"
              class="row-config" (click)="openConfig(row.config!, 0)"></tr>
          <tr mat-row *matRowDef="let row; columns: columns; when: isEmptyRow"
              class="row-empty"></tr>

          <tr *matNoDataRow>
            <td [colSpan]="columns.length"
                style="padding:48px;text-align:center;color:#bbb;font-style:italic">
              No coverages in catalog. Add coverages to the product first.
            </td>
          </tr>
        </table>
      </mat-card>
    </div>
  `
})
export class CoverageMappingPageComponent implements OnInit {
  products: ProductSummary[] = [];
  selectedProduct: ProductSummary | null = null;
  productDetail: ProductDetail | null = null;
  configs: CoverageSummary[] = [];
  productStates: ProductStateDetail[] = [];
  loading = false;

  selectedLobCode: string | null = null;

  get isCommercial(): boolean { return (this.productDetail?.lobs?.length ?? 0) > 0; }

  get totalCoverages(): number {
    if (!this.productDetail) return 0;
    return this.productDetail.lobs.reduce((n, l) => n + l.coverages.length, 0)
         + this.productDetail.coverages.length;
  }

  get columns(): string[] {
    if (!this.isCommercial || this.selectedLobCode !== null)
      return ['coverage', 'state', 'version', 'effStart', 'expireAt', 'actions'];
    return ['lob', 'coverage', 'state', 'version', 'effStart', 'expireAt', 'actions'];
  }

  /** Flat row list for mat-table: config rows + empty rows (no group headers). */
  get gridRows(): GridRow[] {
    if (!this.productDetail) return [];
    const rows: GridRow[] = [];

    if (this.isCommercial) {
      const lobs = this.selectedLobCode
        ? this.productDetail.lobs.filter(l => l.lobCode === this.selectedLobCode)
        : this.productDetail.lobs;
      for (const lob of lobs) {
        for (const cov of lob.coverages) {
          const cfgs = this.configs.filter(c => c.coverageRefId === cov.id);
          if (cfgs.length > 0) {
            cfgs.forEach(cfg => rows.push({ type: 'config', lobCode: lob.lobCode, coverageRef: cov, config: cfg }));
          } else {
            rows.push({ type: 'empty', lobCode: lob.lobCode, coverageRef: cov });
          }
        }
      }
    }

    // Personal-lines coverages (always shown, not LOB-filtered)
    if (!this.isCommercial) {
      for (const cov of this.productDetail.coverages) {
        const cfgs = this.configs.filter(c => c.coverageRefId === cov.id);
        if (cfgs.length > 0) {
          cfgs.forEach(cfg => rows.push({ type: 'config', coverageRef: cov, config: cfg }));
        } else {
          rows.push({ type: 'empty', coverageRef: cov });
        }
      }
    }

    return rows;
  }

  readonly isExpired = isExpired;

  // Row type predicates for matRowDef `when`
  isConfigRow = (_: number, row: GridRow) => row.type === 'config';
  isEmptyRow  = (_: number, row: GridRow) => row.type === 'empty';

  constructor(
    private route:      ActivatedRoute,
    private router:     Router,
    private productSvc: ProductService,
    private coverSvc:   CoverageService,
    private stateSvc:   ProductStateService,
    private dialog:     MatDialog,
    private cdr:        ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.productSvc.list().subscribe(list => {
      this.products = list;
      this.cdr.detectChanges();
    });
  }

  onProductChange() {
    if (!this.selectedProduct) return;
    this.loading = true;
    this.productDetail = null;
    this.configs = [];
    this.productStates = [];
    this.selectedLobCode = null;
    this.cdr.detectChanges();

    const p = this.selectedProduct;
    forkJoin({
      detail:  this.productSvc.get(p.productCode, p.version),
      configs: this.coverSvc.listByProduct(p.id),
      states:  this.stateSvc.list(p.id),
    }).subscribe({
      next: ({ detail, configs, states }) => {
        this.productDetail = detail;
        this.configs       = configs;
        this.productStates = states;
        this.loading       = false;
        this.cdr.detectChanges();
      },
      error: () => { this.loading = false; this.cdr.detectChanges(); }
    });
  }

  openConfig(cfg: CoverageSummary, tab: 0 | 1 | 2 = 0) {
    this.router.navigate(['/coverages', cfg.id], {
      queryParams: { pc: cfg.productCode, cc: cfg.coverageCode, v: cfg.version, tab }
    });
  }

  openAddDialog(preselectedCoverageRefId?: number) {
    if (!this.productDetail) return;
    this.dialog.open(AddMappingDialogComponent, {
      width: '500px',
      data: {
        lobs:                     this.productDetail.lobs,
        coverages:                this.productDetail.coverages,
        productStates:            this.productStates,
        preselectedCoverageRefId,
      } as AddMappingDialogData,
    }).afterClosed().subscribe(saved => {
      if (saved) this.onProductChange();
    });
  }

  addConfig(cov: CoverageRefDetail) {
    this.openAddDialog(cov.id);
  }

  expire(cfg: CoverageSummary) {
    const today = new Date().toISOString().slice(0, 10);
    this.coverSvc.expire(cfg.id, today).subscribe(() => this.onProductChange());
  }

  delete(cfg: CoverageSummary) {
    this.dialog.open(ConfirmDialogComponent, {
      data: {
        title:   'Delete Coverage Config',
        message: `Delete ${cfg.coverageCode} / ${cfg.state} v${cfg.version}? This removes the pipeline and rate tables.`,
      }
    }).afterClosed().subscribe(ok => {
      if (ok) this.coverSvc.delete(cfg.id).subscribe(() => this.onProductChange());
    });
  }
}
