import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { CoverageService } from '../../../core/services/coverage.service';
import { CoverageSummary } from '../../../core/models/api.models';

@Component({
  selector: 'app-coverage-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatDialogModule,
    MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule,
  ],
  template: `
    <h2 mat-dialog-title>{{isEdit ? 'Edit Coverage' : 'New Coverage'}}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="dialog-form">
        <div class="form-row">
          <mat-form-field>
            <mat-label>Product Code</mat-label>
            <input matInput formControlName="productCode" placeholder="e.g. CONDO-IL">
          </mat-form-field>
          <mat-form-field>
            <mat-label>State</mat-label>
            <input matInput formControlName="state" placeholder="IL or * for all">
          </mat-form-field>
        </div>
        <div class="form-row">
          <mat-form-field>
            <mat-label>Coverage Code</mat-label>
            <input matInput formControlName="coverageCode" placeholder="e.g. BUILDINGCVG">
          </mat-form-field>
          <mat-form-field>
            <mat-label>Version</mat-label>
            <input matInput formControlName="version" placeholder="e.g. 2026.02">
          </mat-form-field>
        </div>
        <div class="form-row">
          <mat-form-field>
            <mat-label>Eff Start (YYYY-MM-DD)</mat-label>
            <input matInput formControlName="effStart">
          </mat-form-field>
          <mat-form-field>
            <mat-label>Expire At (optional)</mat-label>
            <input matInput formControlName="expireAt">
          </mat-form-field>
        </div>

        <!-- Perils -->
        <div class="section-title" style="margin-top:8px">Perils (one per line)</div>
        <mat-form-field class="full-width">
          <mat-label>Perils</mat-label>
          <textarea matInput formControlName="perilsText" rows="3"
                    placeholder="GRP1&#10;GRP2&#10;SPL"></textarea>
          <mat-hint>One peril code per line, e.g. GRP1</mat-hint>
        </mat-form-field>
      </form>
      <p style="color:rgba(0,0,0,.6);font-size:12px;margin-top:8px">
        Pipeline steps are managed on the Coverage Detail page after creating the coverage.
      </p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button color="primary" (click)="save()" [disabled]="form.invalid || saving">
        {{saving ? 'Saving…' : 'Save'}}
      </button>
    </mat-dialog-actions>
  `
})
export class CoverageFormComponent implements OnInit {
  form!: FormGroup;
  isEdit = false;
  saving = false;

  private fb         = inject(FormBuilder);
  private svc        = inject(CoverageService);
  readonly dialogRef = inject(MatDialogRef<CoverageFormComponent>);
  // data may be: null (new), CoverageSummary (edit), or { productCode, state } (new with prefill)
  readonly data = inject<CoverageSummary | { productCode: string; state?: string } | null>(MAT_DIALOG_DATA);

  private get existing(): CoverageSummary | null {
    return this.data && 'id' in this.data ? this.data as CoverageSummary : null;
  }

  ngOnInit() {
    this.isEdit = !!this.existing;

    // If editing, load full detail to get perils
    const ex = this.existing;
    this.form = this.fb.group({
      productCode:  [{ value: ex?.productCode  ?? this.data?.productCode  ?? '', disabled: this.isEdit }, Validators.required],
      state:        [{ value: ex?.state        ?? (this.data as any)?.state ?? '*', disabled: this.isEdit }, Validators.required],
      coverageCode: [{ value: ex?.coverageCode ?? '', disabled: this.isEdit }, Validators.required],
      version:      [{ value: ex?.version      ?? '', disabled: this.isEdit }, Validators.required],
      effStart:     [ex?.effStart ?? '', Validators.required],
      expireAt:     [ex?.expireAt ?? ''],
      perilsText:   [''],
    });

    if (this.isEdit && ex) {
      this.svc.get(ex.productCode, ex.coverageCode, ex.version)
        .subscribe(d => {
          this.form.patchValue({ perilsText: d.perils.join('\n') });
        });
    }
  }

  save() {
    if (this.form.invalid) return;
    this.saving = true;
    const v = this.form.getRawValue();
    const perils = (v.perilsText as string)
      .split('\n')
      .map((s: string) => s.trim())
      .filter((s: string) => s.length > 0);

    if (this.isEdit && this.existing) {
      this.svc.update(this.existing.id, {
        effStart: v.effStart,
        expireAt: v.expireAt || null,
        perils,
        pipeline: [], // pipeline managed separately on detail page
      }).subscribe({
        next:  () => this.dialogRef.close(true),
        error: () => { this.saving = false; }
      });
    } else {
      this.svc.create({
        productCode:  v.productCode,
        state:        v.state,
        coverageCode: v.coverageCode,
        version:      v.version,
        effStart:     v.effStart,
        expireAt:     v.expireAt || null,
        perils,
        pipeline: [],
      }).subscribe({
        next:  () => this.dialogRef.close(true),
        error: () => { this.saving = false; }
      });
    }
  }
}
