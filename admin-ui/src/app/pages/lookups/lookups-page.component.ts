import { Component, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCardModule } from '@angular/material/card';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatChipsModule } from '@angular/material/chips';
import { MatExpansionModule } from '@angular/material/expansion';
import { LookupDimensionService } from '../../core/services/lookup-dimension.service';
import { DerivedKeyService } from '../../core/services/derived-key.service';
import {
  LookupDimensionSummary, LookupDimensionDetail, LookupDimensionValueDetail,
  DerivedKeyDetail
} from '../../core/models/api.models';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-lookups-page',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatTabsModule, MatTableModule, MatButtonModule, MatIconModule,
    MatFormFieldModule, MatInputModule, MatSelectModule,
    MatCardModule, MatDialogModule, MatTooltipModule, MatChipsModule,
    MatExpansionModule,
  ],
  template: `
    <div style="padding:24px;max-width:1000px">
      <h2 style="margin:0 0 6px">Lookups &amp; Keys</h2>
      <p style="margin:0 0 20px;color:#666;font-size:14px">
        Define categorical lookup dimensions (allowed dropdown values) and derived keys
        (composite fields calculated from the rating request before pipeline steps run).
      </p>

      <mat-tab-group animationDuration="0ms">

        <!-- ── TAB 1: Lookup Dimensions ──────────────────────────────── -->
        <mat-tab label="Lookup Dimensions">
          <div style="padding-top:20px">

            <div style="display:flex;justify-content:flex-end;margin-bottom:16px">
              <button mat-flat-button color="primary" (click)="startAddDim()">
                <mat-icon>add</mat-icon> New Dimension
              </button>
            </div>

            <!-- Add/Edit form -->
            <mat-card *ngIf="dimForm" style="margin-bottom:20px">
              <mat-card-header>
                <mat-card-title>{{editingDimId ? 'Edit Dimension' : 'New Lookup Dimension'}}</mat-card-title>
              </mat-card-header>
              <mat-card-content style="padding-top:16px">
                <form [formGroup]="dimForm">
                  <div class="form-row">
                    <mat-form-field style="flex:2">
                      <mat-label>Dimension Name</mat-label>
                      <input matInput formControlName="name" placeholder="e.g. ConstructionType">
                      <mat-hint>Short code used in pipeline steps (no spaces)</mat-hint>
                    </mat-form-field>
                    <mat-form-field style="flex:1">
                      <mat-label>Sort Order</mat-label>
                      <input matInput type="number" formControlName="sortOrder">
                    </mat-form-field>
                  </div>
                  <mat-form-field style="width:100%">
                    <mat-label>Description (optional)</mat-label>
                    <input matInput formControlName="description">
                  </mat-form-field>
                </form>
              </mat-card-content>
              <mat-card-actions align="end">
                <button mat-button (click)="cancelDim()">Cancel</button>
                <button mat-flat-button color="primary"
                        [disabled]="dimForm.invalid" (click)="saveDim()">
                  {{editingDimId ? 'Update' : 'Create'}}
                </button>
              </mat-card-actions>
            </mat-card>

            <!-- Dimensions list -->
            <mat-accordion multi>
              <mat-expansion-panel *ngFor="let dim of dimensions" style="margin-bottom:8px">
                <mat-expansion-panel-header (click)="loadDimDetail(dim.id)">
                  <mat-panel-title>
                    <strong>{{dim.name}}</strong>
                    <mat-chip style="margin-left:12px;font-size:11px">{{dim.sortOrder}}</mat-chip>
                    <span *ngIf="dim.description" style="margin-left:12px;color:#666;font-size:13px">
                      — {{dim.description}}
                    </span>
                  </mat-panel-title>
                  <mat-panel-description>
                    <span style="color:#666;font-size:12px">
                      {{dimDetails[dim.id]?.values?.length ?? '?'}} values
                    </span>
                  </mat-panel-description>
                </mat-expansion-panel-header>

                <ng-container *ngIf="dimDetails[dim.id] as detail">
                  <!-- Values table -->
                  <table mat-table [dataSource]="detail.values" style="width:100%;margin-bottom:16px">
                    <ng-container matColumnDef="value">
                      <th mat-header-cell *matHeaderCellDef>Value</th>
                      <td mat-cell *matCellDef="let v">
                        <code style="background:#f5f5f5;padding:2px 6px;border-radius:4px">{{v.value}}</code>
                      </td>
                    </ng-container>
                    <ng-container matColumnDef="displayLabel">
                      <th mat-header-cell *matHeaderCellDef>Display Label</th>
                      <td mat-cell *matCellDef="let v">{{v.displayLabel}}</td>
                    </ng-container>
                    <ng-container matColumnDef="sortOrder">
                      <th mat-header-cell *matHeaderCellDef>Order</th>
                      <td mat-cell *matCellDef="let v">{{v.sortOrder}}</td>
                    </ng-container>
                    <ng-container matColumnDef="actions">
                      <th mat-header-cell *matHeaderCellDef></th>
                      <td mat-cell *matCellDef="let v">
                        <button mat-icon-button color="warn" (click)="deleteValue(dim.id, v)"
                                matTooltip="Remove value">
                          <mat-icon>delete</mat-icon>
                        </button>
                      </td>
                    </ng-container>
                    <tr mat-header-row *matHeaderRowDef="valueColumns"></tr>
                    <tr mat-row *matRowDef="let r; columns: valueColumns"></tr>
                    <tr *matNoDataRow>
                      <td [colSpan]="4" style="padding:16px;text-align:center;color:#999">
                        No values yet — add one below.
                      </td>
                    </tr>
                  </table>

                  <!-- Add value form -->
                  <div *ngIf="valueFormsByDim[dim.id] as vf; else noValueForm">
                    <form [formGroup]="vf" class="form-row" style="align-items:center">
                      <mat-form-field style="flex:1">
                        <mat-label>Value</mat-label>
                        <input matInput formControlName="value" placeholder="e.g. FRAME">
                      </mat-form-field>
                      <mat-form-field style="flex:2">
                        <mat-label>Display Label</mat-label>
                        <input matInput formControlName="displayLabel" placeholder="e.g. Frame Construction">
                      </mat-form-field>
                      <mat-form-field style="flex:0 0 100px">
                        <mat-label>Order</mat-label>
                        <input matInput type="number" formControlName="sortOrder">
                      </mat-form-field>
                      <button mat-flat-button color="primary"
                              [disabled]="vf.invalid" (click)="addValue(dim.id)">Add</button>
                      <button mat-button (click)="cancelValueForm(dim.id)">Cancel</button>
                    </form>
                  </div>
                  <ng-template #noValueForm>
                    <div style="display:flex;gap:8px;justify-content:space-between;align-items:center">
                      <button mat-stroked-button (click)="startValueForm(dim.id)">
                        <mat-icon>add</mat-icon> Add Value
                      </button>
                      <div>
                        <button mat-icon-button (click)="startEditDim(dim)" matTooltip="Edit dimension">
                          <mat-icon>edit</mat-icon>
                        </button>
                        <button mat-icon-button color="warn" (click)="deleteDim(dim)" matTooltip="Delete dimension">
                          <mat-icon>delete</mat-icon>
                        </button>
                      </div>
                    </div>
                  </ng-template>
                </ng-container>

                <div *ngIf="!dimDetails[dim.id]" style="padding:16px;color:#999;text-align:center">
                  Loading...
                </div>
              </mat-expansion-panel>

              <div *ngIf="dimensions.length === 0" style="padding:32px;text-align:center;color:#999">
                No lookup dimensions defined yet. Click <strong>New Dimension</strong> to get started.
              </div>
            </mat-accordion>

          </div>
        </mat-tab>

        <!-- ── TAB 2: Derived Keys ────────────────────────────────────── -->
        <mat-tab label="Derived Keys">
          <div style="padding-top:20px">

            <div style="display:flex;justify-content:flex-end;margin-bottom:16px">
              <button mat-flat-button color="primary" (click)="startAddKey()">
                <mat-icon>add</mat-icon> New Derived Key
              </button>
            </div>

            <!-- Add/Edit form -->
            <mat-card *ngIf="keyForm" style="margin-bottom:20px">
              <mat-card-header>
                <mat-card-title>{{editingKeyId ? 'Edit Derived Key' : 'New Derived Key'}}</mat-card-title>
                <mat-card-subtitle>
                  A derived key aggregates a field across risk items before pipeline steps run,
                  making it available as a named input (e.g. <code>$derived.TotalInsuredValue</code>).
                </mat-card-subtitle>
              </mat-card-header>
              <mat-card-content style="padding-top:16px">
                <form [formGroup]="keyForm">
                  <div class="form-row">
                    <mat-form-field style="flex:1">
                      <mat-label>Key Name</mat-label>
                      <input matInput formControlName="name" placeholder="e.g. TotalInsuredValue">
                      <mat-hint>Short code used in pipeline steps (no spaces)</mat-hint>
                    </mat-form-field>
                    <mat-form-field style="flex:2">
                      <mat-label>Readable Name</mat-label>
                      <input matInput formControlName="readableName" placeholder="e.g. Total Insured Value">
                    </mat-form-field>
                  </div>
                  <div class="form-row">
                    <mat-form-field style="flex:1">
                      <mat-label>Aggregation Function</mat-label>
                      <mat-select formControlName="aggFunction">
                        <mat-option value="SUM">SUM — sum of all values</mat-option>
                        <mat-option value="COUNT">COUNT — number of items</mat-option>
                        <mat-option value="AVG">AVG — average value</mat-option>
                        <mat-option value="MAX">MAX — maximum value</mat-option>
                        <mat-option value="MIN">MIN — minimum value</mat-option>
                      </mat-select>
                    </mat-form-field>
                    <mat-form-field style="flex:2">
                      <mat-label>Source Field</mat-label>
                      <input matInput formControlName="sourceField"
                             placeholder="e.g. $risk.InsuredValue">
                      <mat-hint>Path expression from the risk bag to aggregate</mat-hint>
                    </mat-form-field>
                  </div>
                  <mat-form-field style="width:100%">
                    <mat-label>Description (optional)</mat-label>
                    <input matInput formControlName="description">
                  </mat-form-field>
                </form>
              </mat-card-content>
              <mat-card-actions align="end">
                <button mat-button (click)="cancelKey()">Cancel</button>
                <button mat-flat-button color="primary"
                        [disabled]="keyForm.invalid" (click)="saveKey()">
                  {{editingKeyId ? 'Update' : 'Create'}}
                </button>
              </mat-card-actions>
            </mat-card>

            <!-- Derived keys table -->
            <mat-card>
              <table mat-table [dataSource]="derivedKeys" style="width:100%">

                <ng-container matColumnDef="name">
                  <th mat-header-cell *matHeaderCellDef>Key Name</th>
                  <td mat-cell *matCellDef="let k">
                    <code style="background:#f5f5f5;padding:2px 6px;border-radius:4px">{{k.name}}</code>
                  </td>
                </ng-container>

                <ng-container matColumnDef="readableName">
                  <th mat-header-cell *matHeaderCellDef>Readable Name</th>
                  <td mat-cell *matCellDef="let k"><strong>{{k.readableName}}</strong></td>
                </ng-container>

                <ng-container matColumnDef="aggFunction">
                  <th mat-header-cell *matHeaderCellDef>Function</th>
                  <td mat-cell *matCellDef="let k">
                    <mat-chip style="font-size:11px">{{k.aggFunction}}</mat-chip>
                  </td>
                </ng-container>

                <ng-container matColumnDef="sourceField">
                  <th mat-header-cell *matHeaderCellDef>Source Field</th>
                  <td mat-cell *matCellDef="let k">
                    <code style="background:#f5f5f5;padding:2px 6px;border-radius:4px;font-size:12px">
                      {{k.sourceField}}
                    </code>
                  </td>
                </ng-container>

                <ng-container matColumnDef="scope">
                  <th mat-header-cell *matHeaderCellDef>Scope</th>
                  <td mat-cell *matCellDef="let k">
                    <mat-chip *ngIf="k.productManifestId; else globalKeyBadge" style="font-size:10px">
                      Product #{{k.productManifestId}}
                    </mat-chip>
                    <ng-template #globalKeyBadge>
                      <mat-chip style="font-size:10px;background:#e8f5e9;color:#388e3c">Global</mat-chip>
                    </ng-template>
                  </td>
                </ng-container>

                <ng-container matColumnDef="description">
                  <th mat-header-cell *matHeaderCellDef>Description</th>
                  <td mat-cell *matCellDef="let k" style="color:#666;font-size:13px">
                    {{k.description}}
                  </td>
                </ng-container>

                <ng-container matColumnDef="actions">
                  <th mat-header-cell *matHeaderCellDef></th>
                  <td mat-cell *matCellDef="let k">
                    <button mat-icon-button (click)="startEditKey(k)" matTooltip="Edit">
                      <mat-icon>edit</mat-icon>
                    </button>
                    <button mat-icon-button color="warn" (click)="deleteKey(k)" matTooltip="Delete">
                      <mat-icon>delete</mat-icon>
                    </button>
                  </td>
                </ng-container>

                <tr mat-header-row *matHeaderRowDef="keyColumns"></tr>
                <tr mat-row *matRowDef="let row; columns: keyColumns"></tr>
                <tr *matNoDataRow>
                  <td [colSpan]="keyColumns.length" style="padding:32px;text-align:center;color:#999">
                    No derived keys defined yet. Click <strong>New Derived Key</strong> to get started.
                  </td>
                </tr>
              </table>
            </mat-card>

          </div>
        </mat-tab>

      </mat-tab-group>
    </div>
  `,
  styles: [`.form-row { display:flex; gap:12px; align-items:flex-start; margin-bottom:4px; } code { font-family:monospace; }`]
})
export class LookupsPageComponent implements OnInit {
  // Dimensions
  dimensions: LookupDimensionSummary[] = [];
  dimDetails: Record<number, LookupDimensionDetail> = {};
  dimForm: FormGroup | null = null;
  editingDimId: number | null = null;
  valueFormsByDim: Record<number, FormGroup> = {};
  valueColumns = ['value', 'displayLabel', 'sortOrder', 'actions'];

  // Derived Keys
  derivedKeys: DerivedKeyDetail[] = [];
  keyForm: FormGroup | null = null;
  editingKeyId: number | null = null;
  keyColumns = ['name', 'readableName', 'aggFunction', 'sourceField', 'scope', 'description', 'actions'];

  private dimSvc  = inject(LookupDimensionService);
  private keySvc  = inject(DerivedKeyService);
  private fb      = inject(FormBuilder);
  private dialog  = inject(MatDialog);
  private cdr     = inject(ChangeDetectorRef);

  ngOnInit() {
    this.loadDimensions();
    this.loadKeys();
  }

  // ── Dimensions ──────────────────────────────────────────────────────────────

  private loadDimensions() {
    this.dimSvc.list().subscribe(d => { this.dimensions = d; this.cdr.detectChanges(); });
  }

  loadDimDetail(id: number) {
    if (this.dimDetails[id]) return;
    this.dimSvc.get(id).subscribe(d => {
      this.dimDetails = { ...this.dimDetails, [id]: d };
      this.cdr.detectChanges();
    });
  }

  startAddDim() {
    this.editingDimId = null;
    this.dimForm = this.fb.group({
      name:        ['', Validators.required],
      description: [''],
      sortOrder:   [0],
    });
  }

  startEditDim(dim: LookupDimensionSummary) {
    this.editingDimId = dim.id;
    this.dimForm = this.fb.group({
      name:        [{ value: dim.name, disabled: true }],
      description: [dim.description ?? ''],
      sortOrder:   [dim.sortOrder],
    });
  }

  cancelDim() { this.dimForm = null; this.editingDimId = null; }

  saveDim() {
    if (!this.dimForm || this.dimForm.invalid) return;
    const v = this.dimForm.getRawValue();
    if (this.editingDimId) {
      this.dimSvc.update(this.editingDimId, { description: v.description || null, sortOrder: +v.sortOrder })
        .subscribe(() => { this.cancelDim(); this.loadDimensions(); });
    } else {
      this.dimSvc.create({ name: v.name.trim(), description: v.description || null, sortOrder: +v.sortOrder })
        .subscribe(() => { this.cancelDim(); this.loadDimensions(); });
    }
  }

  deleteDim(dim: LookupDimensionSummary) {
    this.dialog.open(ConfirmDialogComponent, {
      data: { message: `Delete dimension "${dim.name}" and all its values?` }
    }).afterClosed().subscribe(ok => {
      if (ok) this.dimSvc.delete(dim.id).subscribe(() => this.loadDimensions());
    });
  }

  // ── Values ──────────────────────────────────────────────────────────────────

  startValueForm(dimId: number) {
    this.valueFormsByDim = {
      ...this.valueFormsByDim,
      [dimId]: this.fb.group({
        value:        ['', Validators.required],
        displayLabel: [''],
        sortOrder:    [0],
      })
    };
  }

  cancelValueForm(dimId: number) {
    const { [dimId]: _, ...rest } = this.valueFormsByDim;
    this.valueFormsByDim = rest;
  }

  addValue(dimId: number) {
    const vf = this.valueFormsByDim[dimId];
    if (!vf || vf.invalid) return;
    const v = vf.getRawValue();
    this.dimSvc.addValue(dimId, {
      value:        v.value.trim(),
      displayLabel: v.displayLabel?.trim() || null,
      sortOrder:    +v.sortOrder,
    }).subscribe(() => {
      this.cancelValueForm(dimId);
      this.dimSvc.get(dimId).subscribe(d => {
        this.dimDetails = { ...this.dimDetails, [dimId]: d };
        this.cdr.detectChanges();
      });
    });
  }

  deleteValue(dimId: number, val: LookupDimensionValueDetail) {
    this.dialog.open(ConfirmDialogComponent, {
      data: { message: `Remove value "${val.value}"?` }
    }).afterClosed().subscribe(ok => {
      if (ok) this.dimSvc.deleteValue(val.id).subscribe(() => {
        this.dimSvc.get(dimId).subscribe(d => {
          this.dimDetails = { ...this.dimDetails, [dimId]: d };
          this.cdr.detectChanges();
        });
      });
    });
  }

  // ── Derived Keys ────────────────────────────────────────────────────────────

  private loadKeys() {
    this.keySvc.list().subscribe(k => { this.derivedKeys = k; this.cdr.detectChanges(); });
  }

  startAddKey() {
    this.editingKeyId = null;
    this.keyForm = this.buildKeyForm();
  }

  startEditKey(k: DerivedKeyDetail) {
    this.editingKeyId = k.id;
    this.keyForm = this.buildKeyForm(k);
  }

  cancelKey() { this.keyForm = null; this.editingKeyId = null; }

  saveKey() {
    if (!this.keyForm || this.keyForm.invalid) return;
    const v = this.keyForm.getRawValue();
    const req = {
      name:         v.name.trim(),
      readableName: v.readableName.trim(),
      aggFunction:  v.aggFunction,
      sourceField:  v.sourceField.trim(),
      description:  v.description?.trim() || null,
    };
    if (this.editingKeyId) {
      const { name: _n, ...updateReq } = req;
      this.keySvc.update(this.editingKeyId, updateReq).subscribe(() => { this.cancelKey(); this.loadKeys(); });
    } else {
      this.keySvc.create(req).subscribe(() => { this.cancelKey(); this.loadKeys(); });
    }
  }

  deleteKey(k: DerivedKeyDetail) {
    this.dialog.open(ConfirmDialogComponent, {
      data: { message: `Delete derived key "${k.readableName}"?` }
    }).afterClosed().subscribe(ok => {
      if (ok) this.keySvc.delete(k.id).subscribe(() => this.loadKeys());
    });
  }

  private buildKeyForm(k?: DerivedKeyDetail): FormGroup {
    return this.fb.group({
      name:         [{ value: k?.name ?? '', disabled: !!k }, Validators.required],
      readableName: [k?.readableName ?? '', Validators.required],
      aggFunction:  [k?.aggFunction ?? 'SUM', Validators.required],
      sourceField:  [k?.sourceField ?? '', Validators.required],
      description:  [k?.description ?? ''],
    });
  }
}
