import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { ProductService } from '../../../core/services/product.service';
import { ProductSummary } from '../../../core/models/api.models';

@Component({
  selector: 'app-product-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatDialogModule,
    MatFormFieldModule, MatInputModule, MatButtonModule,
    MatIconModule, MatTableModule,
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
            <mat-label>Eff Start (YYYY-MM-DD)</mat-label>
            <input matInput formControlName="effStart" placeholder="2026-02-01">
          </mat-form-field>
          <mat-form-field>
            <mat-label>Expire At (optional)</mat-label>
            <input matInput formControlName="expireAt" placeholder="YYYY-MM-DD">
          </mat-form-field>
        </div>

        <!-- Coverage refs -->
        <div class="section-title" style="margin-top:8px">Coverage References</div>
        <div formArrayName="coverages">
          <div *ngFor="let cRef of coverageRefs.controls; let i=index"
               [formGroupName]="i" class="keys-row">
            <mat-form-field>
              <mat-label>Coverage Code</mat-label>
              <input matInput formControlName="coverageCode">
            </mat-form-field>
            <mat-form-field>
              <mat-label>Version</mat-label>
              <input matInput formControlName="coverageVersion">
            </mat-form-field>
            <button mat-icon-button color="warn" type="button" (click)="removeRef(i)">
              <mat-icon>remove_circle</mat-icon>
            </button>
          </div>
        </div>
        <button mat-stroked-button type="button" (click)="addRef()" style="margin-bottom:8px">
          <mat-icon>add</mat-icon> Add Coverage Ref
        </button>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button color="primary" (click)="save()" [disabled]="form.invalid || saving">
        {{saving ? 'Saving…' : 'Save'}}
      </button>
    </mat-dialog-actions>
  `
})
export class ProductFormComponent implements OnInit {
  form!: FormGroup;
  isEdit = false;
  saving = false;

  private fb        = inject(FormBuilder);
  private svc       = inject(ProductService);
  readonly dialogRef = inject(MatDialogRef<ProductFormComponent>);
  readonly data      = inject<ProductSummary | null>(MAT_DIALOG_DATA);

  ngOnInit() {
    this.isEdit = !!this.data;
    this.form = this.fb.group({
      productCode: [{ value: this.data?.productCode ?? '', disabled: this.isEdit }, Validators.required],
      version:     [{ value: this.data?.version     ?? '', disabled: this.isEdit }, Validators.required],
      effStart:    [this.data?.effStart  ?? '', Validators.required],
      expireAt:    [this.data?.expireAt  ?? ''],
      coverages:   this.fb.array([]),
    });

    if (this.isEdit) {
      // Load full detail to get coverage refs
      this.svc.get(this.data!.productCode, this.data!.version).subscribe(d => {
        d.coverages.forEach(c => this.addRef(c.coverageCode, c.coverageVersion));
      });
    }
  }

  get coverageRefs(): FormArray { return this.form.get('coverages') as FormArray; }

  addRef(coverageCode = '', coverageVersion = '') {
    this.coverageRefs.push(this.fb.group({
      coverageCode:    [coverageCode,    Validators.required],
      coverageVersion: [coverageVersion, Validators.required],
    }));
  }

  removeRef(i: number) { this.coverageRefs.removeAt(i); }

  save() {
    if (this.form.invalid) return;
    this.saving = true;
    const v = this.form.getRawValue();

    if (this.isEdit) {
      this.svc.update(this.data!.id, {
        effStart:  v.effStart,
        expireAt:  v.expireAt || null,
        coverages: v.coverages,
      }).subscribe({
        next:  () => this.dialogRef.close(true),
        error: () => { this.saving = false; }
      });
    } else {
      this.svc.create({
        productCode: v.productCode,
        version:     v.version,
        effStart:    v.effStart,
        expireAt:    v.expireAt || null,
        coverages:   v.coverages,
      }).subscribe({
        next:  () => this.dialogRef.close(true),
        error: () => { this.saving = false; }
      });
    }
  }
}
