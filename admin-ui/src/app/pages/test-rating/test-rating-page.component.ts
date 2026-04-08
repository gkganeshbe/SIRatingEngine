import { Component, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ProductService } from '../../core/services/product.service';
import { CoverageService } from '../../core/services/coverage.service';
import { TestRatingService } from '../../core/services/test-rating.service';
import {
  ProductSummary, CoverageSummary,
  AdminTestRateRequest, AdminTestRateResponse, PerilResult
} from '../../core/models/api.models';

@Component({
  selector: 'app-test-rating-page',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatCardModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatButtonModule, MatIconModule, MatTableModule, MatProgressSpinnerModule,
    MatExpansionModule, MatChipsModule, MatTooltipModule,
  ],
  template: `
    <div style="padding:24px;max-width:1000px">
      <h2 style="margin:0 0 6px">Testing Sandbox</h2>
      <p style="margin:0 0 20px;color:#666;font-size:14px">
        Run a coverage pipeline against a sample risk bag and inspect the step-by-step rating trace.
        All lookups are live — results reflect the current rate tables in the database.
      </p>

      <div style="display:grid;grid-template-columns:1fr 1fr;gap:20px">

        <!-- ── Request Form ─────────────────────────────────────────── -->
        <mat-card>
          <mat-card-header>
            <mat-card-title>Rating Request</mat-card-title>
          </mat-card-header>
          <mat-card-content style="padding-top:16px">
            <form [formGroup]="form">

              <mat-form-field style="width:100%;margin-bottom:4px">
                <mat-label>Product</mat-label>
                <mat-select formControlName="productCode" (selectionChange)="onProductChange()">
                  <mat-option *ngFor="let p of products" [value]="p.productCode">
                    {{p.productCode}} v{{p.version}}
                  </mat-option>
                </mat-select>
              </mat-form-field>

              <mat-form-field style="width:100%;margin-bottom:4px">
                <mat-label>State</mat-label>
                <input matInput formControlName="state" placeholder="e.g. NJ">
              </mat-form-field>

              <mat-form-field style="width:100%;margin-bottom:4px">
                <mat-label>Coverage</mat-label>
                <mat-select formControlName="coverageCode">
                  <mat-option *ngFor="let c of coverages" [value]="c.coverageCode">
                    {{c.coverageCode}} ({{c.state}})
                  </mat-option>
                  <mat-option *ngIf="coverages.length === 0" disabled>
                    Select a product and state first
                  </mat-option>
                </mat-select>
              </mat-form-field>

              <mat-form-field style="width:100%;margin-bottom:4px">
                <mat-label>Effective Date</mat-label>
                <input matInput formControlName="effDate" placeholder="YYYY-MM-DD">
              </mat-form-field>

              <mat-form-field style="width:100%;margin-bottom:4px">
                <mat-label>Starting Premium (optional)</mat-label>
                <input matInput type="number" formControlName="startingPremium" placeholder="0">
                <mat-hint>Leave at 0 to start from scratch (first step sets base premium)</mat-hint>
              </mat-form-field>

              <mat-form-field style="width:100%;margin-bottom:4px">
                <mat-label>Peril (optional)</mat-label>
                <input matInput formControlName="peril"
                       placeholder="Leave blank to rate all perils">
                <mat-hint>Blank = rate all perils configured in the coverage</mat-hint>
              </mat-form-field>

              <!-- Risk Fields -->
              <div style="margin-top:12px;margin-bottom:8px">
                <div style="font-size:13px;font-weight:600;margin-bottom:8px">Risk Bag Fields</div>
                <div formArrayName="riskFields">
                  <div *ngFor="let rf of riskFieldControls; let i = index"
                       [formGroupName]="i"
                       style="display:flex;gap:8px;align-items:flex-start;margin-bottom:4px">
                    <mat-form-field style="flex:1">
                      <mat-label>Key</mat-label>
                      <input matInput formControlName="key" placeholder="e.g. ConstructionType">
                    </mat-form-field>
                    <mat-form-field style="flex:1">
                      <mat-label>Value</mat-label>
                      <input matInput formControlName="value" placeholder="e.g. FRAME">
                    </mat-form-field>
                    <button mat-icon-button color="warn" type="button" (click)="removeRiskField(i)">
                      <mat-icon>remove_circle</mat-icon>
                    </button>
                  </div>
                </div>
                <button mat-stroked-button type="button" (click)="addRiskField()" style="width:100%">
                  <mat-icon>add</mat-icon> Add Risk Field
                </button>
              </div>

            </form>
          </mat-card-content>
          <mat-card-actions align="end" style="padding-right:16px;padding-bottom:16px">
            <button mat-flat-button color="primary"
                    [disabled]="form.invalid || loading"
                    (click)="rate()">
              <mat-icon>play_arrow</mat-icon>
              <ng-container *ngIf="!loading">Run Rating</ng-container>
              <ng-container *ngIf="loading">Rating...</ng-container>
            </button>
          </mat-card-actions>
        </mat-card>

        <!-- ── Results ─────────────────────────────────────────────── -->
        <div>
          <div *ngIf="loading" style="display:flex;align-items:center;gap:12px;padding:40px 0">
            <mat-spinner diameter="32"></mat-spinner>
            <span style="color:#666">Running pipeline...</span>
          </div>

          <div *ngIf="error && !loading" style="padding:16px;background:#fce4ec;border-radius:8px;color:#c62828;margin-bottom:16px">
            <mat-icon style="vertical-align:middle;margin-right:8px">error</mat-icon>
            {{error}}
          </div>

          <ng-container *ngIf="result && !loading">
            <!-- Summary -->
            <mat-card style="margin-bottom:16px">
              <mat-card-content style="padding:16px">
                <div style="display:flex;justify-content:space-between;align-items:center">
                  <div>
                    <div style="font-size:12px;color:#666">Coverage Premium</div>
                    <div style="font-size:28px;font-weight:600;color:#1976d2">
                      \${{result.coveragePremium | number:'1.2-2'}}
                    </div>
                  </div>
                  <div style="text-align:right;font-size:12px;color:#666">
                    <div>{{result.productCode}} / {{result.coverageCode}}</div>
                    <div>{{result.state}} · v{{result.version}}</div>
                    <div>Eff. {{result.effDate}}</div>
                  </div>
                </div>
              </mat-card-content>
            </mat-card>

            <!-- Peril traces -->
            <mat-accordion multi>
              <mat-expansion-panel *ngFor="let p of result.perils" [expanded]="result.perils.length === 1">
                <mat-expansion-panel-header>
                  <mat-panel-title>
                    <strong>{{p.peril}}</strong>
                    <mat-chip style="margin-left:12px;font-size:12px">
                      \${{p.premium | number:'1.2-2'}}
                    </mat-chip>
                  </mat-panel-title>
                  <mat-panel-description>
                    {{p.trace.length}} steps
                  </mat-panel-description>
                </mat-expansion-panel-header>

                <table mat-table [dataSource]="p.trace" style="width:100%;font-size:12px">

                  <ng-container matColumnDef="step">
                    <th mat-header-cell *matHeaderCellDef>Step</th>
                    <td mat-cell *matCellDef="let t">
                      <div style="font-weight:600">{{t.stepName}}</div>
                      <div style="color:#999;font-size:11px">{{t.stepId}}</div>
                    </td>
                  </ng-container>

                  <ng-container matColumnDef="rateTable">
                    <th mat-header-cell *matHeaderCellDef>Rate Table</th>
                    <td mat-cell *matCellDef="let t">
                      <code *ngIf="t.rateTable" style="font-size:11px">{{t.rateTable}}</code>
                    </td>
                  </ng-container>

                  <ng-container matColumnDef="factor">
                    <th mat-header-cell *matHeaderCellDef>Factor</th>
                    <td mat-cell *matCellDef="let t">
                      <span *ngIf="t.factor != null" style="font-family:monospace">
                        {{t.factor | number:'1.4-4'}}
                      </span>
                    </td>
                  </ng-container>

                  <ng-container matColumnDef="before">
                    <th mat-header-cell *matHeaderCellDef>Before</th>
                    <td mat-cell *matCellDef="let t" style="font-family:monospace">
                      \${{t.before | number:'1.2-2'}}
                    </td>
                  </ng-container>

                  <ng-container matColumnDef="after">
                    <th mat-header-cell *matHeaderCellDef>After</th>
                    <td mat-cell *matCellDef="let t" style="font-family:monospace;font-weight:600">
                      \${{t.after | number:'1.2-2'}}
                    </td>
                  </ng-container>

                  <ng-container matColumnDef="note">
                    <th mat-header-cell *matHeaderCellDef>Note</th>
                    <td mat-cell *matCellDef="let t" style="color:#666;font-size:11px">
                      {{t.note}}
                    </td>
                  </ng-container>

                  <tr mat-header-row *matHeaderRowDef="traceColumns"></tr>
                  <tr mat-row *matRowDef="let r; columns: traceColumns"
                      [style.background]="r.after !== r.before ? 'rgba(25,118,210,.04)' : ''"></tr>
                </table>

              </mat-expansion-panel>
            </mat-accordion>
          </ng-container>

          <div *ngIf="!result && !loading && !error"
               style="padding:48px;text-align:center;color:#bbb;border:2px dashed #e0e0e0;border-radius:8px">
            <mat-icon style="font-size:48px;width:48px;height:48px">science</mat-icon>
            <p>Configure the request and click <strong>Run Rating</strong> to see results.</p>
          </div>
        </div>

      </div>
    </div>
  `,
})
export class TestRatingPageComponent implements OnInit {
  form!: FormGroup;
  products: ProductSummary[] = [];
  coverages: CoverageSummary[] = [];
  traceColumns = ['step', 'rateTable', 'factor', 'before', 'after', 'note'];

  loading = false;
  error: string | null = null;
  result: AdminTestRateResponse | null = null;

  private productSvc  = inject(ProductService);
  private coverageSvc = inject(CoverageService);
  private testSvc     = inject(TestRatingService);
  private fb          = inject(FormBuilder);
  private cdr         = inject(ChangeDetectorRef);

  ngOnInit() {
    const today = new Date().toISOString().slice(0, 10);
    this.form = this.fb.group({
      productCode:     ['', Validators.required],
      state:           ['', Validators.required],
      coverageCode:    ['', Validators.required],
      effDate:         [today],
      startingPremium: [0],
      peril:           [''],
      riskFields:      this.fb.array([]),
    });

    this.productSvc.list().subscribe(p => { this.products = p; this.cdr.detectChanges(); });

    // When state changes, also reload coverages
    this.form.get('state')!.valueChanges.subscribe(() => this.loadCoverages());
  }

  onProductChange() {
    this.form.patchValue({ coverageCode: '' });
    this.loadCoverages();
  }

  private loadCoverages() {
    const pc = this.form.get('productCode')?.value;
    const st = this.form.get('state')?.value;
    if (!pc) { this.coverages = []; return; }
    this.coverageSvc.listAll(pc).subscribe(c => {
      this.coverages = st ? c.filter(x => x.state === st || x.state === '*') : c;
      this.cdr.detectChanges();
    });
  }

  get riskFieldControls() {
    return (this.form.get('riskFields') as FormArray).controls;
  }

  addRiskField() {
    (this.form.get('riskFields') as FormArray).push(
      this.fb.group({ key: ['', Validators.required], value: [''] })
    );
  }

  removeRiskField(i: number) {
    (this.form.get('riskFields') as FormArray).removeAt(i);
  }

  rate() {
    if (this.form.invalid) return;
    this.loading = true;
    this.error = null;
    this.result = null;

    const v = this.form.getRawValue();
    const riskFields: { key: string; value: string }[] = v.riskFields;
    const risk: Record<string, string> = {};
    for (const rf of riskFields) {
      if (rf.key.trim()) risk[rf.key.trim()] = rf.value;
    }

    const req: AdminTestRateRequest = {
      productCode:      v.productCode,
      state:            v.state,
      coverageCode:     v.coverageCode,
      rateEffectiveDate: v.effDate || null,
      peril:            v.peril || null,
      startingPremium:  v.startingPremium != null ? +v.startingPremium : null,
      risk,
    };

    this.testSvc.rate(req).subscribe({
      next: res => { this.result = res; this.loading = false; this.cdr.detectChanges(); },
      error: err => {
        this.error = err?.error?.message ?? 'Rating failed. Check the console for details.';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }
}
