import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTabsModule } from '@angular/material/tabs';
import { MatDividerModule } from '@angular/material/divider';
import { MatSelectModule } from '@angular/material/select';
import { isExpired } from '../../../core/utils/date.utils';
import { ProductService } from '../../../core/services/product.service';
import { CoverageService } from '../../../core/services/coverage.service';
import { ProductStateService } from '../../../core/services/product-state.service';
import { LobScopeService } from '../../../core/services/lob-scope.service';
import {
  ProductDetail, ProductSummary, CoverageRefDetail, LobRefDetail,
  ProductStateDetail, LobScopeDetail
} from '../../../core/models/api.models';
import { ConfirmDialogComponent } from '../../../shared/confirm-dialog/confirm-dialog.component';
import { ProductTimelineComponent } from '../product-timeline/product-timeline.component';

@Component({
  selector: 'app-product-detail',
  standalone: true,
  imports: [
    CommonModule, RouterModule, ReactiveFormsModule, FormsModule,
    ProductTimelineComponent,
    MatCardModule, MatButtonModule, MatIconModule, MatTableModule,
    MatChipsModule, MatProgressSpinnerModule, MatTooltipModule,
    MatDialogModule, MatFormFieldModule, MatInputModule, MatTabsModule,
    MatDividerModule, MatSelectModule,
  ],
  styles: [`
    /* ── Section within a tab ── */
    .section { margin-bottom: 28px; }
    .section-header {
      display: flex; align-items: center; gap: 10px;
      margin-bottom: 12px;
    }
    .section-title {
      font-size: 14px; font-weight: 600; color: rgba(0,0,0,.7);
      text-transform: uppercase; letter-spacing: .4px;
    }
    .section-header mat-divider { flex: 1; }

    /* ── LOB block on coverage catalog ── */
    .lob-block { margin-bottom: 20px; }
    .lob-block-header {
      display: flex; align-items: center; gap: 8px;
      padding: 10px 16px; background: #f5f5f5;
      border-left: 4px solid #3f51b5; border-radius: 2px 0 0 2px;
      margin-bottom: 2px;
    }
    .lob-block-title { font-size: 14px; font-weight: 600; color: rgba(0,0,0,.8); flex: 1; }

    /* ── Info card (Risk Fields prompt) ── */
    .info-card {
      display: flex; align-items: center; gap: 16px;
      padding: 16px 20px; background: #e8eaf6; border-radius: 4px;
      border-left: 4px solid #3f51b5;
    }
    .info-card mat-icon { color: #3f51b5; font-size: 28px; width: 28px; height: 28px; flex-shrink: 0; }

    /* ── Misc ── */
    .empty-state {
      padding: 40px 24px; text-align: center; color: #aaa;
      background: #fafafa; border-radius: 4px; border: 1px dashed #ddd;
    }
    .empty-icon { font-size: 40px; width: 40px; height: 40px; margin-bottom: 10px; }
    .inline-form { display: flex; gap: 12px; align-items: flex-start; flex-wrap: wrap; }
    .badge {
      display: inline-block; background: rgba(0,0,0,.1); border-radius: 10px;
      padding: 1px 8px; font-size: 11px; font-weight: 500;
    }
    .lob-badge {
      display: inline-block; background: #e8eaf6; color: #3f51b5;
      border-radius: 3px; padding: 1px 6px; font-size: 11px; font-weight: 600;
      margin-right: 6px;
    }
    .lob-btn-active {
      background: #3f51b5 !important; color: #fff !important;
      border-color: #3f51b5 !important;
    }
    .lob-btn-active mat-icon { color: #fff !important; }
    .lob-btn-inactive {
      background: #fff !important; color: #3f51b5 !important;
      border-color: #9fa8da !important;
    }
    .lob-btn-inactive mat-icon { color: #5c6bc0 !important; }
  `],
  template: `
    <div *ngIf="loading" style="text-align:center;padding:64px">
      <mat-spinner diameter="48" style="margin:auto"></mat-spinner>
    </div>

    <div class="page-container" *ngIf="!loading && product">

      <!-- ── Breadcrumb ── -->
      <div style="display:flex;align-items:center;gap:8px;margin-bottom:16px;color:rgba(0,0,0,.54)">
        <a routerLink="/products" style="color:inherit;text-decoration:none">Products</a>
        <mat-icon style="font-size:16px;width:16px;height:16px">chevron_right</mat-icon>
        <span style="color:rgba(0,0,0,.87)">{{product.productCode}} v{{product.version}}</span>
      </div>

      <!-- ── Product header ── -->
      <div style="display:flex;align-items:flex-start;gap:32px;flex-wrap:wrap;
                  padding:16px 20px;background:#fff;border-radius:4px;
                  box-shadow:0 1px 3px rgba(0,0,0,.12);margin-bottom:20px">
        <div>
          <div style="font-size:11px;color:rgba(0,0,0,.45);margin-bottom:2px">PRODUCT</div>
          <div style="font-size:22px;font-weight:700;letter-spacing:.5px">{{product.productCode}}</div>
        </div>
        <div style="padding-top:4px">
          <div style="font-size:11px;color:rgba(0,0,0,.45)">Version</div>
          <div style="font-size:16px;font-weight:500">{{product.version}}</div>
        </div>
        <div style="padding-top:4px">
          <div style="font-size:11px;color:rgba(0,0,0,.45)">Effective From</div>
          <div>{{product.effStart}}</div>
        </div>
        <div *ngIf="isExpired(product.expireAt)" style="padding-top:4px">
          <div style="font-size:11px;color:rgba(0,0,0,.45)">Expires</div>
          <div style="color:#e53935;font-weight:500">{{product.expireAt}}</div>
        </div>
        <div style="padding-top:4px">
          <div style="font-size:11px;color:rgba(0,0,0,.45)">Type</div>
          <mat-chip [color]="isCommercial ? 'primary' : undefined" [highlighted]="isCommercial">
            {{isCommercial ? 'Commercial' : 'Personal Lines'}}
          </mat-chip>
        </div>
      </div>

      <!-- ── Version timeline ── -->
      <app-product-timeline
        [versions]="allProductVersions"
        [selectedVersionId]="product.id"
        (versionSelected)="loadVersionData($event)">
      </app-product-timeline>

      <!-- ── Tabs ── -->
      <mat-tab-group animationDuration="150ms" (selectedTabChange)="onTabChange($event.index)">

        <!-- ══════════════════════════════════════════════════════════════
             Tab 0 — Product
        ══════════════════════════════════════════════════════════════ -->
        <mat-tab>
          <ng-template mat-tab-label>
            <mat-icon style="margin-right:6px">inventory_2</mat-icon>
            Product
          </ng-template>

          <div style="padding:20px 0">

            <!-- ── Risk Fields section ── -->
            <div class="section">
              <div class="section-header">
                <span class="section-title">Risk Fields</span>
                <mat-divider></mat-divider>
              </div>

              <div class="info-card">
                <mat-icon>tune</mat-icon>
                <div style="flex:1">
                  <div style="font-size:14px;font-weight:600;margin-bottom:2px">Product Risk Field Registry</div>
                  <div style="font-size:13px;color:rgba(0,0,0,.6)">
                    Define the <code>$risk.X</code> path expressions and human-readable labels
                    available when building pipeline steps for <strong>{{product.productCode}}</strong>.
                    Global system fields are always available; add product-specific ones here.
                  </div>
                </div>
                <button mat-flat-button color="primary" (click)="openRiskFields()">
                  <mat-icon>open_in_new</mat-icon> Manage Risk Fields
                </button>
              </div>
            </div>

          </div>
        </mat-tab>

        <!-- ══════════════════════════════════════════════════════════════
             Tab 1 — LOB
        ══════════════════════════════════════════════════════════════ -->
        <mat-tab>
          <ng-template mat-tab-label>
            <mat-icon style="margin-right:6px">category</mat-icon>
            LOB
          </ng-template>

          <div style="padding:20px 0">

            <!-- ── Lines of Business section ── -->
            <div class="section">
              <div class="section-header">
                <span class="section-title">Lines of Business</span>
                <mat-divider></mat-divider>
                <button mat-stroked-button (click)="showAddLobForm = !showAddLobForm">
                  <mat-icon>add</mat-icon> Add LOB
                </button>
              </div>

              <p style="margin:0 0 12px;font-size:13px;color:rgba(0,0,0,.54)">
                LOBs group coverages for <strong>commercial products</strong> (e.g. PROP, GL, AUTO).
                Leave empty for personal lines — coverages are added directly on the Coverage Catalog tab.
              </p>

              <!-- Add LOB form -->
              <mat-card *ngIf="showAddLobForm" style="margin-bottom:12px;padding:16px">
                <form [formGroup]="addLobForm" class="inline-form">
                  <mat-form-field style="flex:1;min-width:180px">
                    <mat-label>LOB Code</mat-label>
                    <input matInput formControlName="lobCode" placeholder="e.g. PROP, GL, AUTO">
                  </mat-form-field>
                  <mat-form-field style="width:110px">
                    <mat-label>Sort Order</mat-label>
                    <input matInput type="number" formControlName="sortOrder">
                  </mat-form-field>
                  <div style="display:flex;gap:8px;padding-top:6px">
                    <button mat-button (click)="cancelAddLob()">Cancel</button>
                    <button mat-flat-button color="primary"
                            [disabled]="addLobForm.invalid || addLobSaving"
                            (click)="saveLob()">{{addLobSaving ? 'Saving\u2026' : 'Add LOB'}}</button>
                  </div>
                </form>
              </mat-card>

              <div *ngIf="product.lobs.length === 0" class="empty-state">
                <mat-icon class="empty-icon">category</mat-icon>
                <p style="margin:0 0 4px;font-size:14px;font-weight:500">No LOBs defined</p>
                <p style="margin:0;font-size:12px">
                  This is a personal lines product. Click <strong>Add LOB</strong> to create a commercial structure.
                </p>
              </div>

              <div style="display:flex;flex-wrap:wrap;gap:12px" *ngIf="product.lobs.length > 0">
                <mat-card *ngFor="let lob of product.lobs"
                          style="flex:0 0 auto;min-width:260px;padding:12px 16px">
                  <div style="display:flex;align-items:center;gap:8px">
                    <mat-icon style="color:#3f51b5">category</mat-icon>
                    <span style="font-size:16px;font-weight:600;flex:1">{{lob.lobCode}}</span>
                    <button mat-icon-button color="warn" (click)="removeLob(lob)"
                            matTooltip="Delete LOB and all its coverages">
                      <mat-icon>delete</mat-icon>
                    </button>
                  </div>
                  <div style="margin-top:6px;font-size:12px;color:rgba(0,0,0,.54)">
                    {{lob.coverages.length}} coverage(s)
                  </div>
                  <!-- Permitted Aggregation Scopes -->
                  <div style="margin-top:10px">
                    <div style="font-size:11px;font-weight:600;color:rgba(0,0,0,.54);
                                text-transform:uppercase;letter-spacing:.4px;margin-bottom:6px">
                      Permitted Aggregation Scopes
                    </div>
                    <div style="display:flex;flex-wrap:wrap;gap:4px;margin-bottom:6px">
                      <mat-chip *ngFor="let s of getLobScopes(lob.id)"
                                [removable]="true" (removed)="removeLobScope(s.id, lob.id)"
                                style="font-size:11px">
                        {{s.scope}}
                        <button matChipRemove><mat-icon style="font-size:14px">cancel</mat-icon></button>
                      </mat-chip>
                      <span *ngIf="getLobScopes(lob.id).length === 0"
                            style="font-size:11px;color:#bbb;font-style:italic">None declared</span>
                    </div>
                    <div style="display:flex;gap:6px;align-items:flex-start">
                      <mat-form-field style="flex:1;font-size:12px" subscriptSizing="dynamic">
                        <mat-select [(ngModel)]="newScopeByLob[lob.id]" placeholder="Add scope…">
                          <mat-option value="PerBuilding">Per Building</mat-option>
                          <mat-option value="PerLocation">Per Location</mat-option>
                          <mat-option value="PerLOB">Per LOB</mat-option>
                          <mat-option value="PerBusinessClass">Per Business Class</mat-option>
                          <mat-option value="PerVehicle">Per Vehicle</mat-option>
                          <mat-option value="PerPolicy">Per Policy</mat-option>
                        </mat-select>
                      </mat-form-field>
                      <button mat-icon-button color="primary"
                              [disabled]="!newScopeByLob[lob.id]"
                              (click)="addLobScope(lob.id)"
                              style="margin-top:2px"
                              matTooltip="Add scope">
                        <mat-icon>add_circle</mat-icon>
                      </button>
                    </div>
                  </div>
                </mat-card>
              </div>
            </div>

          </div>
        </mat-tab>

        <!-- ══════════════════════════════════════════════════════════════
             Tab 2 — States Supported
        ══════════════════════════════════════════════════════════════ -->
        <mat-tab>
          <ng-template mat-tab-label>
            <mat-icon style="margin-right:6px">map</mat-icon>
            States Supported
            <span class="badge" style="margin-left:6px">{{productStates.length}}</span>
          </ng-template>

          <div style="padding:20px 0">
            <p style="margin:0 0 16px;font-size:13px;color:rgba(0,0,0,.54)">
              Declare which states this product is filed for.
              Leave this list empty if you have no state restrictions.
            </p>

            <!-- Add state form -->
            <div style="display:flex;gap:12px;align-items:flex-start;margin-bottom:16px;flex-wrap:wrap">
              <mat-form-field style="width:160px" subscriptSizing="dynamic">
                <mat-label>State Code</mat-label>
                <input matInput [(ngModel)]="newStateCode"
                       placeholder="e.g. NJ, CA, *"
                       (keydown.enter)="addState()">
              </mat-form-field>
              <button mat-flat-button color="primary"
                      style="margin-top:4px"
                      [disabled]="!newStateCode.trim()"
                      (click)="addState()">
                <mat-icon>add</mat-icon> Add State
              </button>
            </div>

            <!-- State chips -->
            <div *ngIf="productStates.length > 0"
                 style="display:flex;flex-wrap:wrap;gap:8px;margin-bottom:12px">
              <mat-chip *ngFor="let s of productStates"
                        [removable]="true"
                        (removed)="removeState(s.id)"
                        style="font-size:13px;font-weight:500">
                {{s.stateCode}}
                <button matChipRemove>
                  <mat-icon>cancel</mat-icon>
                </button>
              </mat-chip>
            </div>

            <div *ngIf="productStates.length === 0" class="empty-state">
              <mat-icon class="empty-icon">map</mat-icon>
              <p style="margin:0;font-size:13px">No states declared — all states are currently permitted.</p>
            </div>
          </div>
        </mat-tab>

        <!-- ══════════════════════════════════════════════════════════════
             Tab 3 — Coverage Catalog
        ══════════════════════════════════════════════════════════════ -->
        <mat-tab>
          <ng-template mat-tab-label>
            <mat-icon style="margin-right:6px">list_alt</mat-icon>
            Coverage Catalog
            <span class="badge" style="margin-left:6px">{{totalCoverages}}</span>
          </ng-template>

          <div style="padding:20px 0">
            <p style="margin:0 0 16px;font-size:13px;color:rgba(0,0,0,.54)">
              Define the coverage types this product offers. Coverage configurations (state + version + pipeline) are
              managed under the <strong>Coverages</strong> section in the navigation.
            </p>

            <!-- ── Commercial: LOB selector + filtered view ── -->
            <ng-container *ngIf="isCommercial">

              <div *ngIf="product.lobs.length === 0" class="empty-state">
                <mat-icon class="empty-icon">list_alt</mat-icon>
                <p style="margin:0 0 4px;font-size:14px;font-weight:500">No LOBs defined yet</p>
                <p style="margin:0;font-size:12px">Go to the <strong>LOB</strong> tab and add LOBs first.</p>
              </div>

              <ng-container *ngIf="product.lobs.length > 0">
                <!-- LOB selector bar -->
                <div style="display:flex;gap:8px;flex-wrap:wrap;margin-bottom:20px;align-items:center">
                  <span style="font-size:13px;color:rgba(0,0,0,.54);margin-right:4px">Line of Business:</span>
                  <button *ngFor="let lob of product.lobs"
                          [class.lob-btn-active]="selectedLobId === lob.id"
                          [class.lob-btn-inactive]="selectedLobId !== lob.id"
                          mat-stroked-button
                          (click)="selectLob(lob.id)">
                    <mat-icon style="font-size:16px;width:16px;height:16px;margin-right:4px">category</mat-icon>
                    {{lob.lobCode}}
                    <span style="margin-left:6px;font-size:11px;opacity:.8">({{lob.coverages.length}})</span>
                  </button>
                </div>

                <!-- Selected LOB coverages -->
                <ng-container *ngIf="selectedLob as lob">
                  <div class="lob-block">
                    <!-- LOB action bar -->
                    <div style="display:flex;align-items:center;gap:8px;margin-bottom:8px">
                      <mat-icon style="color:#3f51b5">category</mat-icon>
                      <span style="font-size:15px;font-weight:600;flex:1">{{lob.lobCode}}</span>
                      <span style="font-size:12px;color:rgba(0,0,0,.45)">{{lob.coverages.length}} coverage(s)</span>
                      <button mat-stroked-button style="height:32px;line-height:32px;font-size:12px"
                              (click)="openAddCoverageForLob(lob)">
                        <mat-icon style="font-size:16px;width:16px;height:16px">add</mat-icon> Add Coverage
                      </button>
                    </div>

                    <!-- Inline add form -->
                    <mat-card *ngIf="activeLobForAdd?.id === lob.id" style="margin-bottom:8px;padding:16px">
                      <form [formGroup]="addCoverageForm" class="inline-form">
                        <mat-form-field style="flex:1;min-width:180px">
                          <mat-label>Coverage Code</mat-label>
                          <input matInput formControlName="coverageCode" placeholder="e.g. BLDG, OCC">
                        </mat-form-field>
                        <mat-form-field style="width:110px">
                          <mat-label>Sort Order</mat-label>
                          <input matInput type="number" formControlName="sortOrder">
                        </mat-form-field>
                        <div style="display:flex;gap:8px;padding-top:6px">
                          <button mat-button (click)="cancelAddCoverage()">Cancel</button>
                          <button mat-flat-button color="primary"
                                  [disabled]="addCoverageForm.invalid || addCoverageSaving"
                                  (click)="saveCoverage(lob.id)">
                            {{addCoverageSaving ? 'Saving\u2026' : 'Add'}}
                          </button>
                        </div>
                      </form>
                    </mat-card>

                    <!-- Coverage rows -->
                    <mat-card>
                      <table mat-table [dataSource]="lob.coverages" style="width:100%">
                        <ng-container matColumnDef="coverageCode">
                          <th mat-header-cell *matHeaderCellDef>Coverage Code</th>
                          <td mat-cell *matCellDef="let c"><strong>{{c.coverageCode}}</strong></td>
                        </ng-container>
                        <ng-container matColumnDef="sortOrder">
                          <th mat-header-cell *matHeaderCellDef>Order</th>
                          <td mat-cell *matCellDef="let c" style="color:rgba(0,0,0,.45)">{{c.sortOrder}}</td>
                        </ng-container>
                        <ng-container matColumnDef="aggregationRule">
                          <th mat-header-cell *matHeaderCellDef>Aggregation Rule</th>
                          <td mat-cell *matCellDef="let c">
                            <mat-select [value]="c.aggregationRule"
                                        style="font-size:12px;min-width:140px"
                                        (selectionChange)="updateCoverageAggregation(c, $event.value, c.perilRollup)">
                              <mat-option [value]="null">— not set —</mat-option>
                              <mat-option value="PerBuilding">Per Building</mat-option>
                              <mat-option value="PerLocation">Per Location</mat-option>
                              <mat-option value="PerLOB">Per LOB</mat-option>
                              <mat-option value="PerBusinessClass">Per Business Class</mat-option>
                              <mat-option value="PerVehicle">Per Vehicle</mat-option>
                              <mat-option value="PerPolicy">Per Policy</mat-option>
                            </mat-select>
                          </td>
                        </ng-container>
                        <ng-container matColumnDef="actions">
                          <th mat-header-cell *matHeaderCellDef></th>
                          <td mat-cell *matCellDef="let c" style="text-align:right">
                            <button mat-icon-button color="warn" (click)="removeCoverage(c)"
                                    matTooltip="Remove from catalog">
                              <mat-icon>delete</mat-icon>
                            </button>
                          </td>
                        </ng-container>
                        <tr mat-header-row *matHeaderRowDef="catalogColumnsWithAgg"></tr>
                        <tr mat-row *matRowDef="let row; columns: catalogColumnsWithAgg"></tr>
                        <tr *matNoDataRow>
                          <td [colSpan]="catalogColumnsWithAgg.length"
                              style="padding:20px;text-align:center;color:#bbb;font-size:13px">
                            No coverages yet — click <strong>Add Coverage</strong> above.
                          </td>
                        </tr>
                      </table>
                    </mat-card>
                  </div>
                </ng-container>
              </ng-container>

              <!-- Unassigned (lobId = null despite having LOBs) -->
              <div *ngIf="product.coverages.length > 0" class="lob-block" style="margin-top:16px">
                <div class="lob-block-header" style="border-left-color:#f57c00">
                  <mat-icon style="color:#f57c00;font-size:18px;width:18px;height:18px">warning</mat-icon>
                  <span class="lob-block-title">Unassigned</span>
                  <span style="font-size:12px;color:rgba(0,0,0,.45)">Not linked to any LOB</span>
                </div>
                <mat-card style="border-radius:0 4px 4px 0">
                  <table mat-table [dataSource]="product.coverages" style="width:100%">
                    <ng-container matColumnDef="coverageCode">
                      <th mat-header-cell *matHeaderCellDef>Coverage Code</th>
                      <td mat-cell *matCellDef="let c"><strong>{{c.coverageCode}}</strong></td>
                    </ng-container>
                    <ng-container matColumnDef="sortOrder">
                      <th mat-header-cell *matHeaderCellDef>Order</th>
                      <td mat-cell *matCellDef="let c" style="color:rgba(0,0,0,.45)">{{c.sortOrder}}</td>
                    </ng-container>
                    <ng-container matColumnDef="actions">
                      <th mat-header-cell *matHeaderCellDef></th>
                      <td mat-cell *matCellDef="let c" style="text-align:right">
                        <button mat-icon-button color="warn" (click)="removeCoverage(c)"
                                matTooltip="Remove from catalog"><mat-icon>delete</mat-icon></button>
                      </td>
                    </ng-container>
                    <tr mat-header-row *matHeaderRowDef="catalogColumns"></tr>
                    <tr mat-row *matRowDef="let row; columns: catalogColumns"></tr>
                  </table>
                </mat-card>
              </div>
            </ng-container>

            <!-- ── Personal lines: flat list ── -->
            <ng-container *ngIf="!isCommercial">
              <div style="display:flex;justify-content:flex-end;margin-bottom:8px">
                <button mat-flat-button color="primary"
                        (click)="openAddCoverageForLob(null)">
                  <mat-icon>add</mat-icon> Add Coverage
                </button>
              </div>

              <!-- Inline add form -->
              <mat-card *ngIf="activeLobForAdd === null && showAddCoverageForm"
                        style="margin-bottom:12px;padding:16px">
                <form [formGroup]="addCoverageForm" class="inline-form">
                  <mat-form-field style="flex:1;min-width:180px">
                    <mat-label>Coverage Code</mat-label>
                    <input matInput formControlName="coverageCode" placeholder="e.g. PRIMARY">
                  </mat-form-field>
                  <mat-form-field style="width:110px">
                    <mat-label>Sort Order</mat-label>
                    <input matInput type="number" formControlName="sortOrder">
                  </mat-form-field>
                  <div style="display:flex;gap:8px;padding-top:6px">
                    <button mat-button (click)="cancelAddCoverage()">Cancel</button>
                    <button mat-flat-button color="primary"
                            [disabled]="addCoverageForm.invalid || addCoverageSaving"
                            (click)="saveCoverage(null)">
                      {{addCoverageSaving ? 'Saving\u2026' : 'Add'}}
                    </button>
                  </div>
                </form>
              </mat-card>

              <mat-card>
                <table mat-table [dataSource]="product.coverages" style="width:100%">
                  <ng-container matColumnDef="coverageCode">
                    <th mat-header-cell *matHeaderCellDef>Coverage Code</th>
                    <td mat-cell *matCellDef="let c"><strong>{{c.coverageCode}}</strong></td>
                  </ng-container>
                  <ng-container matColumnDef="sortOrder">
                    <th mat-header-cell *matHeaderCellDef>Order</th>
                    <td mat-cell *matCellDef="let c" style="color:rgba(0,0,0,.45)">{{c.sortOrder}}</td>
                  </ng-container>
                  <ng-container matColumnDef="actions">
                    <th mat-header-cell *matHeaderCellDef></th>
                    <td mat-cell *matCellDef="let c" style="text-align:right">
                      <button mat-icon-button color="warn" (click)="removeCoverage(c)"
                              matTooltip="Remove from catalog"><mat-icon>delete</mat-icon></button>
                    </td>
                  </ng-container>
                  <tr mat-header-row *matHeaderRowDef="catalogColumns"></tr>
                  <tr mat-row *matRowDef="let row; columns: catalogColumns"></tr>
                  <tr *matNoDataRow>
                    <td [colSpan]="catalogColumns.length"
                        style="padding:32px;text-align:center;color:#bbb">
                      No coverages yet. Click <strong>Add Coverage</strong> above.
                    </td>
                  </tr>
                </table>
              </mat-card>
            </ng-container>
          </div>
        </mat-tab>

      </mat-tab-group>
    </div>
  `
})
export class ProductDetailComponent implements OnInit {
  readonly isExpired = isExpired;
  product: ProductDetail | null = null;
  loading = true;
  catalogColumns        = ['coverageCode', 'sortOrder', 'actions'];
  catalogColumnsWithAgg = ['coverageCode', 'sortOrder', 'aggregationRule', 'actions'];

  // LOB form
  showAddLobForm = false;
  addLobForm!: FormGroup;
  addLobSaving   = false;

  // Coverage form — activeLobForAdd: null = personal-lines top-level, LobRefDetail = specific LOB
  showAddCoverageForm = false;
  activeLobForAdd: LobRefDetail | null | undefined = undefined; // undefined = form closed
  addCoverageForm!: FormGroup;
  addCoverageSaving   = false;

  // Coverage Catalog — selected LOB for filtered view
  selectedLobId: number | null = null;

  get selectedLob(): LobRefDetail | null {
    return this.product?.lobs.find(l => l.id === this.selectedLobId) ?? null;
  }

  selectLob(id: number) {
    this.selectedLobId = id;
    this.cancelAddCoverage();
  }

  // LOB Aggregation Scopes — loaded when LOB tab is shown
  lobScopesMap: Map<number, LobScopeDetail[]> = new Map();
  newScopeByLob: Record<number, string> = {};

  // States Supported (Tab 2)
  productStates: ProductStateDetail[] = [];
  newStateCode = '';

  get isCommercial(): boolean { return (this.product?.lobs?.length ?? 0) > 0; }

  get totalCoverages(): number {
    if (!this.product) return 0;
    return this.product.lobs.reduce((n, l) => n + l.coverages.length, 0)
         + this.product.coverages.length;
  }

  allProductVersions: ProductSummary[] = [];

  private productId   = 0;
  private productCode = '';
  private version     = '';

  constructor(
    private route:         ActivatedRoute,
    private router:        Router,
    private productSvc:    ProductService,
    private coverageSvc:   CoverageService,
    private stateSvc:      ProductStateService,
    private lobScopeSvc:   LobScopeService,
    private dialog:        MatDialog,
    private fb:            FormBuilder,
    private cdr:           ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.productId   = +this.route.snapshot.paramMap.get('productId')!;
    this.productCode = this.route.snapshot.queryParamMap.get('pc') ?? '';
    this.version     = this.route.snapshot.queryParamMap.get('v')  ?? '';

    this.addLobForm = this.fb.group({
      lobCode:   ['', Validators.required],
      sortOrder: [0],
    });
    this.addCoverageForm = this.fb.group({
      coverageCode: ['', Validators.required],
      sortOrder:    [0],
    });

    this.loadProduct();
    this.loadAllVersions();
  }

  onTabChange(index: number) {
    if (index === 1) this.loadLobScopes();
    if (index === 2) this.loadStates();
  }

  private loadAllVersions() {
    this.productSvc.list().subscribe(all => {
      this.allProductVersions = all.filter(p => p.productCode === this.productCode);
      this.cdr.detectChanges();
    });
  }

  loadVersionData(v: ProductSummary) {
    this.router.navigate(['/products', v.id], {
      queryParams: { pc: v.productCode, v: v.version }
    });
  }

  private loadProduct() {
    this.productSvc.get(this.productCode, this.version).subscribe({
      next: d => {
        this.product = d;
        this.loading = false;
        // Auto-select first LOB for Coverage Catalog view
        if (d.lobs.length > 0 && this.selectedLobId === null) {
          this.selectedLobId = d.lobs[0].id;
        }
        this.cdr.detectChanges();
        this.loadLobScopes();
      },
      error: () => { this.loading = false; this.cdr.detectChanges(); }
    });
  }

  // ── States Supported ─────────────────────────────────────────────────────────

  private loadStates() {
    this.stateSvc.list(this.productId).subscribe({
      next: d => { this.productStates = d; this.cdr.detectChanges(); }
    });
  }

  addState() {
    const code = this.newStateCode.trim().toUpperCase();
    if (!code) return;
    this.stateSvc.add(this.productId, code).subscribe({
      next: () => { this.newStateCode = ''; this.loadStates(); },
    });
  }

  removeState(id: number) {
    this.stateSvc.delete(id).subscribe({ next: () => this.loadStates() });
  }

  // ── LOB Aggregation Scopes ────────────────────────────────────────────────────

  private loadLobScopes() {
    if (!this.product) return;
    for (const lob of this.product.lobs) {
      this.lobScopeSvc.listScopes(lob.id).subscribe({
        next: d => {
          this.lobScopesMap = new Map(this.lobScopesMap).set(lob.id, d);
          this.cdr.detectChanges();
        }
      });
    }
  }

  getLobScopes(lobId: number): LobScopeDetail[] {
    return this.lobScopesMap.get(lobId) ?? [];
  }

  addLobScope(lobId: number) {
    const scope = this.newScopeByLob[lobId];
    if (!scope) return;
    this.lobScopeSvc.addScope(lobId, scope).subscribe({
      next: () => {
        this.newScopeByLob[lobId] = '';
        this.lobScopeSvc.listScopes(lobId).subscribe(d => {
          this.lobScopesMap = new Map(this.lobScopesMap).set(lobId, d);
          this.cdr.detectChanges();
        });
      }
    });
  }

  removeLobScope(scopeId: number, lobId: number) {
    this.lobScopeSvc.deleteScope(scopeId).subscribe({
      next: () => {
        this.lobScopeSvc.listScopes(lobId).subscribe(d => {
          this.lobScopesMap = new Map(this.lobScopesMap).set(lobId, d);
          this.cdr.detectChanges();
        });
      }
    });
  }

  updateCoverageAggregation(c: CoverageRefDetail, aggregationRule: string | null, perilRollup: string | null) {
    this.lobScopeSvc.updateCoverageAggregation(c.id, { aggregationRule, perilRollup }).subscribe({
      next: () => {
        c.aggregationRule = aggregationRule;
        this.cdr.detectChanges();
      }
    });
  }

  // ── LOB ──────────────────────────────────────────────────────────────────────

  saveLob() {
    if (this.addLobForm.invalid) return;
    this.addLobSaving = true;
    const v = this.addLobForm.getRawValue();
    this.productSvc.addLob(this.productId, {
      lobCode: v.lobCode.trim().toUpperCase(), sortOrder: +v.sortOrder,
    }).subscribe({
      next:  () => { this.addLobSaving = false; this.cancelAddLob(); this.loadProduct(); },
      error: () => { this.addLobSaving = false; }
    });
  }

  cancelAddLob() {
    this.showAddLobForm = false;
    this.addLobForm.reset({ lobCode: '', sortOrder: 0 });
  }

  removeLob(lob: LobRefDetail) {
    this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Remove LOB',
        message: `Remove "${lob.lobCode}" and all its coverages? This also deletes all state configs and rate tables.`
      }
    }).afterClosed().subscribe(ok => {
      if (!ok) return;
      this.productSvc.deleteLob(lob.id).subscribe(() => this.loadProduct());
    });
  }

  // ── Coverage Catalog ─────────────────────────────────────────────────────────

  openAddCoverageForLob(lob: LobRefDetail | null) {
    this.activeLobForAdd    = lob;
    this.showAddCoverageForm = true;
    this.addCoverageForm.reset({ coverageCode: '', sortOrder: 0 });
  }

  cancelAddCoverage() {
    this.activeLobForAdd     = undefined;
    this.showAddCoverageForm = false;
    this.addCoverageForm.reset({ coverageCode: '', sortOrder: 0 });
  }

  saveCoverage(lobId: number | null) {
    if (this.addCoverageForm.invalid) return;
    this.addCoverageSaving = true;
    const v = this.addCoverageForm.getRawValue();
    this.coverageSvc.addToCatalog(this.productId, {
      coverageCode: v.coverageCode.trim().toUpperCase(),
      lobId,
      sortOrder: +v.sortOrder,
    }).subscribe({
      next:  () => { this.addCoverageSaving = false; this.cancelAddCoverage(); this.loadProduct(); },
      error: () => { this.addCoverageSaving = false; }
    });
  }

  removeCoverage(c: CoverageRefDetail) {
    this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Remove Coverage',
        message: `Remove "${c.coverageCode}"? This deletes all state configs and rate tables for this coverage.`
      }
    }).afterClosed().subscribe(ok => {
      if (!ok) return;
      this.coverageSvc.removeFromCatalog(c.id).subscribe(() => this.loadProduct());
    });
  }

  // ── Navigation ───────────────────────────────────────────────────────────────

  openRiskFields() {
    this.router.navigate(['/products', this.productCode, 'risk-fields']);
  }
}
