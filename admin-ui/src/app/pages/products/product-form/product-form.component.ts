import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ACTIVE_EXPIRE } from '../../../core/utils/date.utils';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { ProductService } from '../../../core/services/product.service';
import { ProductSummary } from '../../../core/models/api.models';

@Component({
  selector: 'app-product-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatDialogModule,
    MatFormFieldModule, MatInputModule, MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>{{isEdit ? 'Edit Product' : 'New Product'}}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="dialog-form">
        <div class="form-row">
          <mat-form-field>
            <mat-label>Product Code</mat-label>
            <input matInput formControlName="productCode" placeholder="e.g. HO-PRIMARY">
          </mat-form-field>
          <mat-form-field>
            <mat-label>Version</mat-label>
            <input matInput formControlName="version" placeholder="e.g. 2026.02">
          </mat-form-field>
        </div>
        <div class="form-row">
          <mat-form-field>
            <mat-label>Effective From (YYYY-MM-DD)</mat-label>
            <input matInput formControlName="effStart" placeholder="2026-02-01">
          </mat-form-field>
          <mat-form-field>
            <mat-label>Effective To (optional)</mat-label>
            <input matInput formControlName="expireAt" placeholder="YYYY-MM-DD">
          </mat-form-field>
        </div>
        <!-- Documentation (collapsible) -->
        <details style="margin-top:12px">
          <summary style="cursor:pointer;font-size:13px;color:rgba(0,0,0,.6);user-select:none">
            Documentation / Notes (optional)
          </summary>
          <mat-form-field style="width:100%;margin-top:8px">
            <mat-label>Notes</mat-label>
            <textarea matInput formControlName="notes" rows="3"
                      placeholder="Describe the purpose of this product, filing reference, or special handling instructions…"></textarea>
          </mat-form-field>
        </details>
        <p style="margin:8px 0 0;font-size:12px;color:rgba(0,0,0,.54)">
          Lines of Business and Coverage types are managed on the product detail page after creation.
        </p>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button color="primary" (click)="save()" [disabled]="form.invalid || saving">
        {{saving ? 'Saving\u2026' : 'Save'}}
      </button>
    </mat-dialog-actions>
  `
})
export class ProductFormComponent implements OnInit {
  form!: FormGroup;
  isEdit = false;
  saving = false;

  private fb         = inject(FormBuilder);
  private svc        = inject(ProductService);
  readonly dialogRef = inject(MatDialogRef<ProductFormComponent>);
  readonly data      = inject<ProductSummary | null>(MAT_DIALOG_DATA);

  ngOnInit() {
    this.isEdit = !!this.data;
    this.form = this.fb.group({
      productCode: [{ value: this.data?.productCode ?? '', disabled: this.isEdit }, Validators.required],
      version:     [{ value: this.data?.version     ?? '', disabled: this.isEdit }, Validators.required],
      effStart:    [this.data?.effStart ?? '', Validators.required],
      expireAt:    [this.data?.expireAt ?? ACTIVE_EXPIRE],
      notes:       [(this.data as any)?.notes ?? ''],
    });
  }

  save() {
    if (this.form.invalid) return;
    this.saving = true;
    const v = this.form.getRawValue();

    const notes = v.notes?.trim() || null;
    const obs: import('rxjs').Observable<unknown> = this.isEdit
      ? this.svc.update(this.data!.id, { effStart: v.effStart, expireAt: v.expireAt || ACTIVE_EXPIRE, notes, coverages: [], lobs: [] })
      : this.svc.create({ productCode: v.productCode, version: v.version, effStart: v.effStart, expireAt: v.expireAt || ACTIVE_EXPIRE, notes, coverages: [] });

    obs.subscribe({
      next:  () => this.dialogRef.close(true),
      error: () => { this.saving = false; }
    });
  }
}
