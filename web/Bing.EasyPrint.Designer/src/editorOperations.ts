import { translateElement } from './coordinates'
import { cloneTemplate, type BpTemplate, type GroupElement, type Row, type TemplateElement } from './model'
import { findCell, findComponent, findElement } from './tree'

function rowsAt(template: BpTemplate, parentPath: string): Row[] | undefined {
  if (!parentPath) return template.Rows
  const parent = findCell(template, parentPath) ?? findElement(template, parentPath) ?? findComponent(template, parentPath)
  if (!parent || !('Rows' in parent)) return undefined
  if ('Type' in parent && parent.Type === 6 && parent.ComponentId)
    return template.Components?.find((item) => item.Id === parent.ComponentId)?.Rows
  return parent.Rows
}

export function groupRowsAtPaths(template: BpTemplate, paths: string[]): string {
  const selected = [...new Set(paths)].map((path) => {
    const match = /^(.*?)(?:\.)?Rows\[(\d+)\]$/.exec(path)
    if (!match) throw new Error('请选择连续的行。')
    return { parentPath: match[1], index: Number(match[2]) }
  }).sort((left, right) => left.index - right.index)
  const parentPath = selected[0]?.parentPath
  const rows = parentPath === undefined ? undefined : rowsAt(template, parentPath)
  if (!rows || !selected.length || selected.some((item, position) => item.parentPath !== parentPath ||
    item.index < 0 || item.index >= rows.length || position > 0 && item.index !== selected[position - 1].index + 1))
    throw new Error('请选择同一容器中的连续行。')
  const content = selected.map((item) => rows[item.index])
  if (content.some((row) => row.IsHeader || row.Section && !['Body', 'TableHeader'].includes(row.Section)) ||
    content.at(-1)?.Section === 'TableHeader')
    throw new Error('页眉、页脚及孤立表头不能移入流式组合。')
  const group: TemplateElement = { Type: 6, Rows: cloneTemplate(content), Pagination: 'Split' }
  rows.splice(selected[0].index, selected.length, { Cells: [{ Elements: [group] }] })
  template.Version = 3
  return `${parentPath ? `${parentPath}.` : ''}Rows[${selected[0].index}].Cells[0].Elements[0]`
}

export function reorderFlowGroup(template: BpTemplate, path: string, direction: -1 | 1): string | undefined {
  const match = /^(.*?)(?:\.)?Rows\[(\d+)\]\.Cells\[(\d+)\]\.Elements\[(\d+)\]$/.exec(path)
  if (!match) return undefined
  const rows = rowsAt(template, match[1])
  const index = Number(match[2])
  const cellIndex = Number(match[3])
  const elementIndex = Number(match[4])
  const current = rows?.[index]?.Cells?.[cellIndex]?.Elements?.[elementIndex]
  if (!current || current.Type !== 6 || (current.StartCoordinate !== undefined)) return undefined
  if (!rows) return undefined

  const cells = rows[index]?.Cells
  const elements = cells?.[cellIndex]?.Elements
  if (!Array.isArray(elements)) return undefined

  // 只有组合行包装器可以交换整行；普通单元格中的组合只调整同级元素顺序。
  const definition = current.ComponentId ? template.Components?.find((item) => item.Id === current.ComponentId) : undefined
  const isFlowRows = current.Rows !== undefined || definition?.Rows !== undefined
  if (isFlowRows && cells?.length === 1 && elements.length === 1 && elementIndex === 0) {
    const destination = index + direction
    if (destination < 0 || destination >= rows.length) return undefined
    const isTablePair = (rowIndex: number) => rows[rowIndex]?.Section === 'TableHeader' ||
      rowIndex > 0 && rows[rowIndex - 1]?.Section === 'TableHeader' && Boolean(rows[rowIndex]?.RepeatSource)
    if (isTablePair(index) || isTablePair(destination)) return undefined
    ;[rows[index], rows[destination]] = [rows[destination], rows[index]]
    return `${match[1] ? `${match[1]}.` : ''}Rows[${destination}].Cells[${cellIndex}].Elements[${elementIndex}]`
  }

  const destination = elementIndex + direction
  if (destination < 0 || destination >= elements.length) return undefined
  if (elements[destination].StartCoordinate !== undefined) return undefined
  ;[elements[elementIndex], elements[destination]] = [elements[destination], elements[elementIndex]]
  return `${match[1] ? `${match[1]}.` : ''}Rows[${index}].Cells[${cellIndex}].Elements[${destination}]`
}

export function offsetFlowGroup(group: GroupElement, dx: number, dy: number): boolean {
  if (group.StartCoordinate) return false
  if (dx === 0 && dy === 0) return true
  group.Margin ??= {}
  if (dx !== 0) group.Margin.Left = Math.max(0, (group.Margin.Left ?? 0) + dx)
  if (dy !== 0) group.Margin.Top = Math.max(0, (group.Margin.Top ?? 0) + dy)
  return true
}

export function setFlowGroupPosition(group: GroupElement, axis: 'X' | 'Y', value: number): boolean {
  if (group.StartCoordinate) return false
  group.Margin ??= {}
  if (axis === 'X') group.Margin.Left = Math.max(0, value)
  else group.Margin.Top = Math.max(0, value)
  return true
}

export function duplicateElement(template: BpTemplate, path: string): string {
  const match = /^(.*)\.Elements\[(\d+)\]$/.exec(path)
  if (!match) throw new Error('请选择要复制的元素。')
  const parent = findCell(template, match[1]) ?? findElement(template, match[1]) ?? findComponent(template, match[1])
  if (!parent || !('Elements' in parent) || 'ComponentId' in parent && parent.ComponentId)
    throw new Error('请先进入组件定义再复制共享内容。')
  const elements = parent.Elements
  const index = Number(match[2])
  if (!elements?.[index]) throw new Error('所选元素不存在。')
  const copy = cloneTemplate(elements[index])
  if (copy.StartCoordinate) translateElement(copy, 8, 8)
  elements.splice(index + 1, 0, copy)
  return `${match[1]}.Elements[${index + 1}]`
}
