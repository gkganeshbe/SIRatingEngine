import { Component, Input, Output, EventEmitter, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { ColumnDefDetail, CreateRateTableRowRequest } from '../../../core/models/api.models';
import { parseExcelPaste } from '../../../core/utils/excel-parser.util';
import { ACTIVE_EXPIRE } from '../../../core/utils/date.utils';

@Component({
  selector: 'app-rate-table-excel-grid',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatTableModule, MatButtonModule, MatChipsModule],
  styles: [`
    .paste-zone {
      border: 2px dashed rgba(0,0,0,0.18);
      border-radius: 8px;
      padding: 32px 24px;
      text-align: center;
      cursor: pointer;
      outline: none;
      transition: background 0.15s, border-color 0.15s;
      color: rgba(0,0,0,0.6);
    }
    .paste-zone:hover, .paste-zone:focus {
      background: rgba(63,81,181,0.04);
      border-color: #3f51b5;
    }
    .paste-zone.has-data {
      background: #f1f8e9;
      border-color: #66bb6a;
      border-style: solid;
    }
    .col-pill {
      display: inline-block;
      background: #e8eaf6;
      color: #3f51b5;
      border-radius: 4px;
      padding: 2px 8px;
      font-size: 12px;
      font-weight: 500;
      margin: 2px 3px;
    }
    .preview-table-wrap {
      overflow-x: auto;
      margin-top: 12px;
      border: 1px solid #e0e0e0;
      border-radius: 6px;
    }
    .preview-table-wrap table {
      min-width: 100%;
    }
  `],
  template: `
    <!-- Drop / paste zone -->
    <div class="paste-zone" [class.has-data]="parsedRows.length > 0"
         tabindex="0" (paste)="onPaste($event)"
         (keydown.enter)="focusPaste()" (click)="focusPaste()">

      <ng-container *ngIf="parsedRows.length === 0">
        <mat-icon style="font-size:40px;width:40px;height:40px;color:#9fa8da;margin-bottom:8px">
          table_chart
        </mat-icon>
        <div style="font-size:15px;font-weight:500;margin-bottom:6px">Excel-First Rate Upload</div>
        <div style="font-size:13px;margin-bottom:12px">
          Select your data block in Excel, press <strong>Ctrl+C</strong>,
          then click here and press <strong>Ctrl+V</strong>.
        </div>
        <div style="font-size:12px;color:rgba(0,0,0,.42);margin-bottom:8px">Expected column order:</div>
        <div>
          <span *ngFor="let col of sortedDefs" class="col-pill">{{col.displayLabel || col.columnName}}</span>
        </div>
      </ng-container>

      <ng-container *ngIf="parsedRows.length > 0">
        <mat-icon style="font-size:36px;width:36px;height:36px;color:#43a047;margin-bottom:6px">
          check_circle
        </mat-icon>
        <div style="font-size:15px;font-weight:500;color:#2e7d32;margin-bottom:4px">
          {{parsedRows.length}} row{{parsedRows.length !== 1 ? 's' : ''}} parsed — ready to save
        </div>
        <div style="font-size:12px;color:rgba(0,0,0,.54)">
          Click here and paste again to replace, or click <strong>Clear</strong> to start over.
        </div>
      </ng-container>
    </div>

    <!-- Preview table -->
    <div class="preview-table-wrap" *ngIf="parsedRows.length > 0">
      <table mat-table [dataSource]="parsedRows.slice(0, 20)" style="width:100%">

        <ng-container *ngFor="let col of previewColumns" [matColumnDef]="col">
          <th mat-header-cell *matHeaderCellDef style="font-size:12px;padding:6px 12px">{{colLabel(col)}}</th>
          <td mat-cell *matCellDef="let row" style="font-size:12px;padding:6px 12px">
            {{getCellValue(row, col)}}
          </td>
        </ng-container>

        <tr mat-header-row *matHeaderRowDef="previewColumns"></tr>
        <tr mat-row *matRowDef="let row; columns: previewColumns;"></tr>
      </table>
      <div *ngIf="parsedRows.length > 20"
           style="padding:8px 12px;font-size:12px;color:rgba(0,0,0,.42);
                  background:#fafafa;border-top:1px solid #e0e0e0;text-align:center">
        Showing first 20 of {{parsedRows.length}} rows. All rows will be saved.
      </div>
    </div>

    <!-- Actions row -->
    <div *ngIf="parsedRows.length > 0"
         style="display:flex;gap:8px;margin-top:10px;justify-content:flex-end">
      <button mat-stroked-button (click)="clear()">
        <mat-icon>close</mat-icon> Clear
      </button>
    </div>
  `
})
export class RateTableExcelGridComponent implements OnChanges {
  @Input() columnDefs: ColumnDefDetail[] = [];
  @Input() effStart: string = new Date().toISOString().slice(0, 10);
  @Output() rowsParsed = new EventEmitter<CreateRateTableRowRequest[]>();

  parsedRows: CreateRateTableRowRequest[] = [];
  sortedDefs: ColumnDefDetail[] = [];

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['columnDefs']) {
      this.sortedDefs = [...this.columnDefs].sort((a, b) => a.sortOrder - b.sortOrder);
    }
  }

  onPaste(event: ClipboardEvent): void {
    event.preventDefault();
    const text = event.clipboardData?.getData('text');
    if (!text?.trim()) return;
    this.parsedRows = parseExcelPaste(text, this.columnDefs, this.effStart, ACTIVE_EXPIRE);
    this.rowsParsed.emit(this.parsedRows);
  }

  focusPaste(): void {
    // Ensure the zone is focused so the next Ctrl+V is captured
  }

  clear(): void {
    this.parsedRows = [];
    this.rowsParsed.emit([]);
  }

  // ── Preview helpers ───────────────────────────────────────────────────────

  get previewColumns(): string[] {
    const cols: string[] = [];
    this.sortedDefs.forEach(d => cols.push(d.columnName));
    cols.push('effStart');
    return cols;
  }

  colLabel(col: string): string {
    if (col === 'effStart') return 'Eff Start';
    const def = this.columnDefs.find(d => d.columnName === col);
    return def?.displayLabel || col;
  }

  getCellValue(row: CreateRateTableRowRequest, col: string): string {
    const key = col.charAt(0).toLowerCase() + col.slice(1);
    const map: Record<string, unknown> = {
      key1: row.key1, key2: row.key2, key3: row.key3, key4: row.key4, key5: row.key5,
      rangeFrom: row.rangeFrom, rangeTo: row.rangeTo,
      factor: row.factor, additionalUnit: row.additionalUnit, additionalRate: row.additionalRate,
      effStart: row.effStart,
    };
    const val = map[key] ?? map[col.toLowerCase()];
    return val !== null && val !== undefined ? String(val) : '—';
  }
}
