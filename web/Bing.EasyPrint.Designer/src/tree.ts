import type { BpTemplate, Cell, Row, TemplateElement } from './model'

export type TreeEntry = { key: string; path: string; kind: 'row' | 'cell' | 'element'; type?: number; label: string; depth: number }

export function listTemplateTree(template: BpTemplate): TreeEntry[] {
  const result: TreeEntry[] = []
  const walkRows = (rows: Row[], prefix: string, depth: number) => rows.forEach((row, rowIndex) => {
    const path = `${prefix}Rows[${rowIndex}]`
    result.push({ key: path, path, kind: 'row', label: row.RepeatSource ? `明细 · ${row.RepeatSource}` : `行 ${rowIndex + 1}`, depth })
    ;(row.Cells ?? []).forEach((cell, cellIndex) => walkCell(cell, `${path}.Cells[${cellIndex}]`, depth + 1, cellIndex))
  })
  const walkCell = (cell: Cell, path: string, depth: number, index: number) => {
    result.push({ key: path, path, kind: 'cell', label: `列 ${index + 1}${cell.Width ? ` · ${cell.Width} 点` : ''}`, depth })
    const walkElements = (elements: TemplateElement[], prefix: string, level: number) => elements.forEach((element, elementIndex) => {
      const elementPath = `${prefix}.Elements[${elementIndex}]`
      result.push({ key: elementPath, path: elementPath, kind: 'element', type: element.Type, label: elementLabel(element), depth: level })
      if (element.Type === 6) {
        const children = element.ComponentId ? template.Components?.find((item) => item.Id === element.ComponentId)?.Elements : element.Elements
        if (children) walkElements(children, elementPath, level + 1)
      }
    })
    walkElements(cell.Elements ?? [], path, depth + 1)
    walkRows(cell.Rows ?? [], `${path}.`, depth + 1)
  }
  walkRows(template.Rows ?? [], '', 0)
  return result
}

export function elementLabel(element: TemplateElement): string {
  const labels: Record<number, string> = { 1: '文字', 2: '图片', 3: '二维码', 4: '条码', 5: '线段', 6: '组合', 7: '矩形', 8: '椭圆', 9: '图标', 10: '二维点阵码' }
  if (element.Type === 6 && element.ComponentId) return `组件 · ${element.ComponentId}`
  return `${labels[element.Type] ?? '元素'}${element.Field?.Name ? ` · ${element.Field.Name}` : element.Field?.Value ? ` · ${element.Field.Value}` : ''}`
}

export function findElement(template: BpTemplate, path: string): TemplateElement | undefined {
  const match = /^(.*\.Cells\[\d+\])((?:\.Elements\[\d+\])+)$/.exec(path)
  if (!match) return undefined
  const cell = findCell(template, match[1])
  const indices = [...match[2].matchAll(/\.Elements\[(\d+)\]/g)].map((part) => Number(part[1]))
  let element = cell?.Elements?.[indices[0]]
  for (const index of indices.slice(1)) {
    if (element?.Type !== 6) return undefined
    const id = element.ComponentId
    const children = id ? template.Components?.find((item) => item.Id === id)?.Elements : element.Elements
    element = children?.[index]
  }
  return element
}

export function findRow(template: BpTemplate, path: string): Row | undefined {
  const match = /^Rows\[(\d+)\](.*)$/.exec(path)
  if (!match) return undefined
  let row: Row | undefined = template.Rows[Number(match[1])]
  let rest = match[2]
  while (row && rest) {
    const cellMatch = /^\.Cells\[(\d+)\](.*)$/.exec(rest)
    if (!cellMatch) return undefined
    const cell: Cell | undefined = row.Cells?.[Number(cellMatch[1])]
    rest = cellMatch[2]
    const rowMatch = /^\.Rows\[(\d+)\](.*)$/.exec(rest)
    if (!rowMatch) return undefined
    row = cell?.Rows?.[Number(rowMatch[1])]
    rest = rowMatch[2]
  }
  return row
}

export function findCell(template: BpTemplate, path: string): Cell | undefined {
  const match = /^Rows\[(\d+)\](.*)$/.exec(path)
  if (!match) return undefined
  let row: Row | undefined = template.Rows[Number(match[1])]
  let rest = match[2]
  while (row && rest) {
    const cellMatch = /^\.Cells\[(\d+)\](.*)$/.exec(rest)
    if (!cellMatch) return undefined
    const cell: Cell | undefined = row.Cells?.[Number(cellMatch[1])]
    rest = cellMatch[2]
    if (!rest) return cell
    const rowMatch = /^\.Rows\[(\d+)\](.*)$/.exec(rest)
    if (!rowMatch) return undefined
    row = cell?.Rows?.[Number(rowMatch[1])]
    rest = rowMatch[2]
  }
  return undefined
}
