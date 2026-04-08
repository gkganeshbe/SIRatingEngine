import { Component, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatCardModule } from '@angular/material/card';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatChipsModule } from '@angular/material/chips';
import { ProductService } from '../../core/services/product.service';
import { RiskFieldService } from '../../core/services/risk-field.service';
import { RiskField, ProductSummary, CreateRiskFieldRequest } from '../../core/models/api.models';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';
import { Observable } from 'rxjs';

@Component({
  selector: 'app-risk-field-mapping-page',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatSelectModule, MatFormFieldModule, MatInputModule,
    MatButtonModule, MatIconModule, MatTableModule,
    MatCardModule, MatDialogModule, MatTooltipModule, MatChipsModule,
  ],
  template: `
    <div style="padding:24px;max-width:960px">
      <h2 style="margin:0 0 6px">Risk Field Mapping</h2>
      <p style="margin:0 0 20px;color:#666;font-size:14px">
        Map human-readable field names to the JSON path expressions used in pipeline steps.
        Business users select from these labels when configuring steps — no need to remember raw paths.
      </p>

      <!-- Product selector -->
      <mat-form-field style="width:320px;margin-bottom:20px">
        <mat-label>Product</mat-label>
        <mat-select [(value)]="selectedProduct" (selectionChange)="onProductChange()">
          <mat-option *ngFor="let p of products" [value]="p.productCode">
            {{p.productCode}} v{{p.version}}
          </mat-option>
        </mat-select>
        <mat-hint>Fields scoped to this product plus all global system fields</mat-hint>
      </mat-form-field>

      <!-- Add/Edit form -->
      <mat-card *ngIf="editForm" style="margin-bottom:20px">
        <mat-card-header>
          <mat-card-title>{{editingId ? 'Edit Field' : 'New Risk Field'}}</mat-card-title>
          <mat-card-subtitle *ngIf="!editingId && selectedProduct">
            This field will be scoped to product <strong>{{selectedProduct}}</strong>.
          </mat-card-subtitle>
        </mat-card-header>
        <mat-card-content style="padding-top:16px">
          <form [formGroup]="editForm">
            <div style="display:flex;gap:12px;flex-wrap:wrap">
              <mat-form-field style="flex:1;min-width:180px">
                <mat-label>Display Name</mat-label>
                <input matInput formControlName="displayName" placeholder="e.g. Construction Type">
              </mat-form-field>
              <mat-form-field style="flex:2;min-width:200px">
                <mat-label>Path Expression</mat-label>
                <input matInput formControlName="path" placeholder="e.g. $risk.Construction">
                <mat-hint>Exact expression used in pipeline steps</mat-hint>
              </mat-form-field>
            </div>
            <div style="display:flex;gap:12px;flex-wrap:wrap;margin-top:4px">
              <mat-form-field style="flex:1;min-width:160px">
                <mat-label>Category (optional)</mat-label>
                <mat-select formControlName="category">
                  <mat-option value="">— none —</mat-option>
                  <mat-option value="Policy">Policy</mat-option>
                  <mat-option value="Building">Building</mat-option>
                  <mat-option value="Location">Location</mat-option>
                  <mat-option value="Vehicle">Vehicle</mat-option>
                  <mat-option value="Coverage">Coverage</mat-option>
                  <mat-option value="Schedule Item">Schedule Item</mat-option>
                  <mat-option value="System">System</mat-option>
                </mat-select>
              </mat-form-field>
              <mat-form-field style="flex:0 0 100px">
                <mat-label>Sort Order</mat-label>
                <input matInput type="number" formControlName="sortOrder">
              </mat-form-field>
              <mat-form-field style="flex:2;min-width:180px">
                <mat-label>Description (optional)</mat-label>
                <input matInput formControlName="description">
              </mat-form-field>
            </div>
          </form>
        </mat-card-content>
        <mat-card-actions align="end">
          <button mat-button (click)="cancelEdit()">Cancel</button>
          <button mat-flat-button color="primary"
                  [disabled]="editForm.invalid" (click)="saveEdit()">
            {{editingId ? 'Update' : 'Add'}}
          </button>
        </mat-card-actions>
      </mat-card>

      <!-- Field table -->
      <mat-card *ngIf="selectedProduct">
        <mat-card-header style="display:flex;justify-content:space-between;align-items:center">
          <mat-card-title style="margin:0">Fields</mat-card-title>
          <button mat-flat-button color="primary" (click)="startAdd()" style="margin-left:auto">
            <mat-icon>add</mat-icon> Add Field
          </button>
        </mat-card-header>
        <table mat-table [dataSource]="fields" style="width:100%">

          <ng-container matColumnDef="displayName">
            <th mat-header-cell *matHeaderCellDef>Display Name</th>
            <td mat-cell *matCellDef="let f"><strong>{{f.displayName}}</strong></td>
          </ng-container>

          <ng-container matColumnDef="path">
            <th mat-header-cell *matHeaderCellDef>Path Expression</th>
            <td mat-cell *matCellDef="let f">
              <code style="background:#f5f5f5;padding:2px 6px;border-radius:4px;font-size:12px">
                {{f.path}}
              </code>
            </td>
          </ng-container>

          <ng-container matColumnDef="category">
            <th mat-header-cell *matHeaderCellDef>Category</th>
            <td mat-cell *matCellDef="let f">
              <mat-chip *ngIf="f.category" style="font-size:11px">{{f.category}}</mat-chip>
            </td>
          </ng-container>

          <ng-container matColumnDef="scope">
            <th mat-header-cell *matHeaderCellDef>Scope</th>
            <td mat-cell *matCellDef="let f">
              <span *ngIf="f.productCode; else globalBadge" style="font-size:12px">{{f.productCode}}</span>
              <ng-template #globalBadge>
                <mat-chip style="font-size:10px;background:#e8f5e9;color:#388e3c">Global</mat-chip>
              </ng-template>
            </td>
          </ng-container>

          <ng-container matColumnDef="description">
            <th mat-header-cell *matHeaderCellDef>Description</th>
            <td mat-cell *matCellDef="let f" style="color:#666;font-size:13px">{{f.description}}</td>
          </ng-container>

          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let f">
              <button mat-icon-button (click)="startEdit(f)" matTooltip="Edit">
                <mat-icon>edit</mat-icon>
              </button>
              <button mat-icon-button color="warn" (click)="delete(f)"
                      [disabled]="!f.productCode"
                      [matTooltip]="f.productCode ? 'Delete' : 'Global system fields cannot be deleted here'">
                <mat-icon>delete</mat-icon>
              </button>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns"></tr>
          <tr *matNoDataRow>
            <td [colSpan]="columns.length" style="padding:32px;text-align:center;color:#999">
              No fields configured. Click <strong>Add Field</strong> to get started.
            </td>
          </tr>
        </table>
      </mat-card>

      <div *ngIf="!selectedProduct"
           style="padding:48px;text-align:center;color:#bbb;border:2px dashed #e0e0e0;border-radius:8px">
        <mat-icon style="font-size:48px;width:48px;height:48px">tune</mat-icon>
        <p>Select a product above to view and manage its risk field mappings.</p>
      </div>
    </div>
  `
})
export class RiskFieldMappingPageComponent implements OnInit {
  products: ProductSummary[] = [];
  fields: RiskField[] = [];
  columns = ['displayName', 'path', 'category', 'scope', 'description', 'actions'];
  selectedProduct = '';
  editForm: FormGroup | null = null;
  editingId: number | null = null;

  private productSvc = inject(ProductService);
  private riskSvc    = inject(RiskFieldService);
  private fb         = inject(FormBuilder);
  private dialog     = inject(MatDialog);
  private cdr        = inject(ChangeDetectorRef);

  ngOnInit() {
    this.productSvc.list().subscribe(p => {
      // Deduplicate by productCode (show latest version per product)
      const seen = new Set<string>();
      this.products = p.filter(x => { if (seen.has(x.productCode)) return false; seen.add(x.productCode); return true; });
      this.cdr.detectChanges();
    });
  }

  onProductChange() {
    if (this.selectedProduct) this.load();
    else this.fields = [];
    this.cancelEdit();
  }

  private load() {
    this.riskSvc.list(this.selectedProduct).subscribe(f => { this.fields = f; this.cdr.detectChanges(); });
  }

  startAdd() {
    this.editingId = null;
    this.editForm = this.buildForm();
  }

  startEdit(f: RiskField) {
    this.editingId = f.id;
    this.editForm = this.buildForm(f);
  }

  cancelEdit() { this.editForm = null; this.editingId = null; }

  saveEdit() {
    if (!this.editForm || this.editForm.invalid) return;
    const v = this.editForm.getRawValue();
    const req: CreateRiskFieldRequest = {
      displayName: v.displayName.trim(),
      path:        v.path.trim(),
      description: v.description?.trim() || null,
      category:    v.category || null,
      sortOrder:   +v.sortOrder,
      productCode: this.editingId ? v.productCode : undefined,
    };

    const op: Observable<unknown> = this.editingId
      ? this.riskSvc.update(this.editingId, req)
      : this.riskSvc.create(this.selectedProduct, req);

    op.subscribe(() => { this.cancelEdit(); this.load(); });
  }

  delete(f: RiskField) {
    if (!f.productCode) return;
    this.dialog.open(ConfirmDialogComponent, {
      data: { message: `Delete "${f.displayName}"?` }
    }).afterClosed().subscribe(ok => {
      if (ok) this.riskSvc.delete(f.id).subscribe(() => this.load());
    });
  }

  private buildForm(f?: RiskField): FormGroup {
    return this.fb.group({
      displayName:  [f?.displayName  ?? '',  Validators.required],
      path:         [f?.path         ?? '',  Validators.required],
      description:  [f?.description  ?? ''],
      category:     [f?.category     ?? ''],
      sortOrder:    [f?.sortOrder     ?? 0],
      productCode:  [f?.productCode  ?? null],
    });
  }
}
