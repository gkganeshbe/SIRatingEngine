import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Observable } from 'rxjs';
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
import { RiskFieldService } from '../../core/services/risk-field.service';
import { RiskField, CreateRiskFieldRequest } from '../../core/models/api.models';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-risk-field-list',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatTableModule, MatButtonModule, MatIconModule,
    MatFormFieldModule, MatInputModule, MatSelectModule,
    MatCardModule, MatDialogModule, MatTooltipModule, MatChipsModule,
  ],
  template: `
    <div style="padding:24px;max-width:900px">

      <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:20px">
        <div>
          <h2 style="margin:0">Risk Field Registry — <span style="color:#1976d2">{{productCode}}</span></h2>
          <p style="margin:4px 0 0;color:#666;font-size:14px">
            Define human-readable labels for path expressions used in pipeline step configuration for this product.
            <strong>System fields</strong> (&#36;peril, *) are global and always visible here.
          </p>
        </div>
        <button mat-flat-button color="primary" (click)="startAdd()">
          <mat-icon>add</mat-icon> Add Field
        </button>
      </div>

      <!-- Inline add / edit form -->
      <mat-card *ngIf="editForm" style="margin-bottom:20px">
        <mat-card-header>
          <mat-card-title>{{editingId ? 'Edit Field' : 'New Field'}}</mat-card-title>
          <mat-card-subtitle *ngIf="!editingId">
            This field will be scoped to product <strong>{{productCode}}</strong>.
            Select category <em>System</em> only for engine-level expressions that apply to all products.
          </mat-card-subtitle>
        </mat-card-header>
        <mat-card-content style="padding-top:16px">
          <form [formGroup]="editForm" class="field-form">
            <div class="form-row">
              <mat-form-field style="flex:2">
                <mat-label>Display Name</mat-label>
                <input matInput formControlName="displayName" placeholder="e.g. Construction Type">
                <mat-hint>The label shown to users in the step form</mat-hint>
              </mat-form-field>
              <mat-form-field style="flex:3">
                <mat-label>Path Expression</mat-label>
                <input matInput formControlName="path" placeholder="e.g. &#36;risk.Construction">
                <mat-hint>Exact expression stored in the pipeline step (&#36;risk.X, &#36;peril, *)</mat-hint>
              </mat-form-field>
            </div>
            <div class="form-row">
              <mat-form-field style="flex:1">
                <mat-label>Category (optional)</mat-label>
                <mat-select formControlName="category">
                  <mat-option value="">— none —</mat-option>
                  <mat-optgroup label="Risk Data">
                    <mat-option value="Policy">Policy — top-level fields shared across all LOBs</mat-option>
                    <mat-option value="Building">Building — building-level risk attributes</mat-option>
                    <mat-option value="Location">Location — location-level risk attributes</mat-option>
                    <mat-option value="Vehicle">Vehicle — vehicle-level attributes (Auto LOB)</mat-option>
                  </mat-optgroup>
                  <mat-optgroup label="Pipeline Inputs">
                    <mat-option value="Coverage">Coverage — parameters from CoverageInput.Params (e.g. limits, deductibles)</mat-option>
                    <mat-option value="Schedule Item">Schedule Item — per-item fields in SCHEDLEVEL coverages (e.g. ItemValue, ClassCode)</mat-option>
                  </mat-optgroup>
                  <mat-optgroup label="Engine">
                    <mat-option value="System">System — engine-level expressions not from the risk bag (&#36;peril, *)</mat-option>
                  </mat-optgroup>
                </mat-select>
              </mat-form-field>
              <mat-form-field style="flex:1">
                <mat-label>Sort Order</mat-label>
                <input matInput type="number" formControlName="sortOrder">
              </mat-form-field>
              <mat-form-field style="flex:3">
                <mat-label>Description (optional)</mat-label>
                <input matInput formControlName="description"
                       placeholder="e.g. ISO construction class of the building">
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
      <mat-card>
        <table mat-table [dataSource]="fields" style="width:100%">

          <ng-container matColumnDef="displayName">
            <th mat-header-cell *matHeaderCellDef>Display Name</th>
            <td mat-cell *matCellDef="let f">
              <strong>{{f.displayName}}</strong>
            </td>
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
            <td mat-cell *matCellDef="let f" style="font-size:12px;color:#666">
              <span *ngIf="f.productCode; else globalBadge">{{f.productCode}}</span>
              <ng-template #globalBadge>
                <mat-chip style="font-size:10px;background:#e8f5e9;color:#388e3c">Global</mat-chip>
              </ng-template>
            </td>
          </ng-container>

          <ng-container matColumnDef="description">
            <th mat-header-cell *matHeaderCellDef>Description</th>
            <td mat-cell *matCellDef="let f" style="color:#666;font-size:13px">
              {{f.description}}
            </td>
          </ng-container>

          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let f">
              <button mat-icon-button (click)="startEdit(f)" matTooltip="Edit">
                <mat-icon>edit</mat-icon>
              </button>
              <button mat-icon-button color="warn" (click)="delete(f)" matTooltip="Delete"
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
              No fields configured yet. Click <strong>Add Field</strong> to get started.
            </td>
          </tr>
        </table>
      </mat-card>
    </div>
  `,
  styles: [`
    .form-row { display:flex; gap:12px; align-items:flex-start; margin-bottom:4px; }
    code { font-family: monospace; }
  `]
})
export class RiskFieldListComponent implements OnInit {
  fields: RiskField[] = [];
  columns = ['displayName', 'path', 'category', 'scope', 'description', 'actions'];

  editForm: FormGroup | null = null;
  editingId: number | null = null;
  productCode = '';

  private svc    = inject(RiskFieldService);
  private fb     = inject(FormBuilder);
  private dialog = inject(MatDialog);
  private route  = inject(ActivatedRoute);

  ngOnInit() {
    this.productCode = this.route.snapshot.paramMap.get('productCode') ?? '';
    this.load();
  }

  private load() {
    this.svc.list(this.productCode).subscribe(f => this.fields = f);
  }

  startAdd() {
    this.editingId = null;
    this.editForm = this.buildForm();
  }

  startEdit(f: RiskField) {
    this.editingId = f.id;
    this.editForm = this.buildForm(f);
  }

  cancelEdit() {
    this.editForm  = null;
    this.editingId = null;
  }

  saveEdit() {
    if (!this.editForm || this.editForm.invalid) return;
    const v = this.editForm.getRawValue();
    const req: CreateRiskFieldRequest = {
      displayName: v.displayName.trim(),
      path:        v.path.trim(),
      description: v.description?.trim() || null,
      category:    v.category || null,
      sortOrder:   +v.sortOrder,
      // For updates, preserve the original productCode from the record being edited.
      productCode: this.editingId ? v.productCode : undefined,
    };

    const op: Observable<unknown> = this.editingId
      ? this.svc.update(this.editingId, req)
      : this.svc.create(this.productCode, req);

    op.subscribe(() => { this.cancelEdit(); this.load(); });
  }

  delete(f: RiskField) {
    if (!f.productCode) return;  // global system fields are protected
    this.dialog.open(ConfirmDialogComponent, {
      data: { message: `Delete "${f.displayName}"?` }
    }).afterClosed().subscribe(ok => {
      if (ok) this.svc.delete(f.id).subscribe(() => this.load());
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
