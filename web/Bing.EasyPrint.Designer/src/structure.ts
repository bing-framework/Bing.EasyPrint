import type { BpTemplate, Row } from './model'
import { findCell } from './tree'

export function addNestedRow(template: BpTemplate, cellPath: string): string | undefined {
  if (!Array.isArray(template?.Rows)) return undefined

  const cell = findCell(template, cellPath)
  if (!cell) return undefined

  const rows = cell.Rows ?? (cell.Rows = [])
  const row: Row = {
    Cells: [{ Width: Number.isFinite(cell.Width) && (cell.Width ?? 0) > 0 ? cell.Width : 0, Elements: [] }],
  }
  const rowIndex = rows.push(row) - 1

  return `${cellPath}.Rows[${rowIndex}]`
}
