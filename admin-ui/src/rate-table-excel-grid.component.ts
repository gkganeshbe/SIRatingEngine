import { Component, Input, Output, EventEmitter } from '@angular/core';
import { ColumnDefDetail, CreateRateTableRowRequest } from '../../../../core/models/api.models';
import { parseExcelPaste } from '../../../../core/utils/excel-parser.util';

@Component({
  selector: 'app-rate-table-excel-grid',
  templateUrl: './rate-table-excel-grid.component.html'
})
export class RateTableExcelGridComponent {
  @Input() columnDefs: ColumnDefDetail[] = [];
  @Input() effStart: string = new Date().toISOString().split('T')[0];
  @Output() rowsParsed = new EventEmitter<CreateRateTableRowRequest[]>();

  parsedRows: CreateRateTableRowRequest[] = [];

  onPaste(event: ClipboardEvent): void {
    // Prevent the default browser paste action
    event.preventDefault();
    
    const clipboardData = event.clipboardData?.getData('text');
    if (!clipboardData) return;

    // Parse and emit the typed records
    this.parsedRows = parseExcelPaste(clipboardData, this.columnDefs, this.effStart);
    this.rowsParsed.emit(this.parsedRows);
  }
}