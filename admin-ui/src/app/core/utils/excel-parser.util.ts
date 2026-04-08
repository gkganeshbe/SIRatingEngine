import { CreateRateTableRowRequest, ColumnDefDetail } from '../models/api.models';

export function parseExcelPaste(
  clipboardText: string,
  columnDefs: ColumnDefDetail[],
  effStart: string,
  expireAt: string | null = null
): CreateRateTableRowRequest[] {
  const rows = clipboardText.split(/\r?\n/).filter(r => r.trim() !== '');
  const sortedDefs = [...columnDefs].sort((a, b) => a.sortOrder - b.sortOrder);

  return rows.map(row => {
    const cells = row.split('\t');
    const req: CreateRateTableRowRequest = {
      key1: null, key2: null, key3: null, key4: null, key5: null,
      rangeFrom: null, rangeTo: null,
      factor: 0,
      additionalUnit: null, additionalRate: null,
      effStart, expireAt
    };

    sortedDefs.forEach((def, index) => {
      const rawValue = cells[index] !== undefined ? cells[index].trim() : '';
      const value = rawValue === '' ? null : rawValue;
      if (value === null) return;

      const colName = def.columnName;
      
      if (colName.startsWith('Key')) {
        const keyIndex = parseInt(colName.replace('Key', ''), 10);
        if (keyIndex >= 1 && keyIndex <= 5) {
          (req as any)[`key${keyIndex}`] = value;
        }
      } else if (colName === 'RangeFrom') {
        req.rangeFrom = parseFloat(value);
      } else if (colName === 'RangeTo') {
        req.rangeTo = parseFloat(value);
      } else if (colName === 'Factor' || colName === 'Additive') {
        req.factor = parseFloat(value);
      } else if (colName === 'AdditionalRate') {
        req.additionalRate = parseFloat(value);
      } else if (colName === 'AdditionalUnit') {
        req.additionalUnit = parseFloat(value);
      }
    });

    return req;
  });
}