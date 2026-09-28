import { translateElement } from './coordinates'
import type { BpTemplate, Cell, Row, TemplateElement } from './model'
import { cloneTemplate } from './model'
import { findCell, findElement } from './tree'

export function enableV2(template: BpTemplate): void {
  template.Version = 2
  template.Variables ??= {}
  template.Components ??= []
}

export type ElementBounds = { left: number; top: number; right: number; bottom: number }

function bounds(element: TemplateElement, measured?: ElementBounds): ElementBounds {
  const start = element.StartCoordinate
  if (!start) {
    if (measured) return { ...measured, right: measured.right + (element.Margin?.Right ?? 0),
      bottom: measured.bottom + (element.Margin?.Bottom ?? 0) }
    throw new Error('自动排列的元素需要先完成引擎预览再编组。')
  }
  const left = start.X + (element.Margin?.Left ?? 0)
  const top = start.Y + (element.Margin?.Top ?? 0)
  if (element.Type === 5) {
    const end = element.LineEndCoordinate ?? element.EndCoordinate
    if (!end) throw new Error('线段缺少终点。')
    return { left: Math.min(start.X, end.X) + (element.Margin?.Left ?? 0),
      top: Math.min(start.Y, end.Y) + (element.Margin?.Top ?? 0),
      right: Math.max(start.X, end.X) + (element.Margin?.Left ?? 0) + (element.LineWidth ?? 1) + (element.Margin?.Right ?? 0),
      bottom: Math.max(start.Y, end.Y) + (element.Margin?.Top ?? 0) + (element.LineWidth ?? 1) + (element.Margin?.Bottom ?? 0) }
  }
  const width = element.Type === 9 ? element.Size : 'Width' in element ? element.Width : undefined
  const height = element.Type === 9 ? element.Size : 'Height' in element ? element.Height : undefined
  const right = element.EndCoordinate?.X ?? (width ? left + width : measured?.right)
  const bottom = element.EndCoordinate?.Y ?? (height ? top + height : measured?.bottom)
  if (right === undefined || bottom === undefined) throw new Error('元素尺寸需要引擎预览后才能编组。')
  return { left, top, right: right + (element.Margin?.Right ?? 0),
    bottom: bottom + (element.Margin?.Bottom ?? 0) }
}

export function groupCellElements(template: BpTemplate, cellPath: string, indices?: number[],
  measured?: Map<number, ElementBounds>, cellWidth?: number): string {
  const cell = findCell(template, cellPath)
  if (!cell?.Elements?.length) throw new Error('当前单元格没有可组合的元素。')
  const selected = indices?.length ? [...new Set(indices)].sort((a, b) => a - b) : cell.Elements.map((_, index) => index)
  if (selected.some((index) => !Number.isInteger(index) || index < 0 || index >= cell.Elements!.length))
    throw new Error('选中的元素不在当前单元格。')
  if (selected.some((index, position) => position > 0 && index !== selected[position - 1] + 1) &&
    cell.Elements.some((element, index) => index >= selected[0] && index <= selected[selected.length - 1] && !element.StartCoordinate))
    throw new Error('自动排列的元素需要连续选择后才能编组。')
  const boxes = selected.map((index) => bounds(cell.Elements![index], measured?.get(index)))
  if (boxes.some((box) => [box.left, box.top, box.right, box.bottom].some((value) => !Number.isInteger(value)) ||
    box.left < 0 || box.top < 0 || box.right <= box.left || box.bottom <= box.top))
    throw new Error('选中元素的实际边界无效。')
  const hasAlignedAutomaticText = selected.some((index) => {
    const element = cell.Elements![index]
    return element.Type === 1 && !element.StartCoordinate && Boolean(element.TextAlignment)
  })
  const left = hasAlignedAutomaticText ? 0 : Math.min(...boxes.map((box) => box.left))
  const top = Math.min(...boxes.map((box) => box.top))
  const right = Math.max(...boxes.map((box) => box.right))
  const bottom = Math.max(...boxes.map((box) => box.bottom))
  const hasFlowingText = selected.some((index) => cell.Elements![index]?.Type === 1 && !cell.Elements![index]?.EndCoordinate)
  const children = selected.map((index, selectedIndex) => {
    const child = cloneTemplate(cell.Elements![index])
    const marginLeft = child.Margin?.Left ?? 0
    const marginTop = child.Margin?.Top ?? 0
    if (child.StartCoordinate) {
      child.StartCoordinate.X += marginLeft
      child.StartCoordinate.Y += marginTop
      if (child.Type === 5 && child.LineEndCoordinate) {
        child.LineEndCoordinate.X += marginLeft; child.LineEndCoordinate.Y += marginTop
      }
    } else child.StartCoordinate = { X: child.Type === 1 && child.TextAlignment ? marginLeft : boxes[selectedIndex].left,
      Y: boxes[selectedIndex].top }
    if (child.Margin) { child.Margin.Left = 0; child.Margin.Top = 0 }
    translateElement(child, -left, -top)
    return child
  })
  enableV2(template)
  for (const index of [...selected].reverse()) cell.Elements.splice(index, 1)
  const insertAt = selected[0]
  const width = hasFlowingText && cellWidth ? Math.max(right - left, cellWidth - left) : right - left
  cell.Elements.splice(insertAt, 0, { Type: 6, Width: width, Height: bottom - top,
    StartCoordinate: { X: left, Y: top }, Elements: children })
  return `${cellPath}.Elements[${insertAt}]`
}

export function detachComponent(template: BpTemplate, elementPath: string): void {
  const element = findElement(template, elementPath)
  if (element?.Type !== 6 || !element.ComponentId) throw new Error('请选择组件实例。')
  const definition = template.Components?.find((item) => item.Id === element.ComponentId)
  if (!definition) throw new Error('组件定义不存在。')
  if (!element.Width) element.Width = definition.Width
  if (!element.Height) element.Height = definition.Height
  element.Elements = cloneTemplate(definition.Elements)
  delete element.ComponentId
}

export function ungroupElement(template: BpTemplate, elementPath: string, measured?: Map<number, ElementBounds>,
  renderedGroup?: { width: number; height: number; position?: { X: number; Y: number } }): string {
  const match = /^(.*)\.Elements\[(\d+)\]$/.exec(elementPath)
  const cell = match && findCell(template, match[1])
  const parent = match && findElement(template, match[1])
  const target = cell?.Elements ?? (parent?.Type === 6
    ? parent.ComponentId ? template.Components?.find((item) => item.Id === parent.ComponentId)?.Elements : parent.Elements
    : undefined)
  const index = match ? Number(match[2]) : -1
  const group = target?.[index]
  if (!target || group?.Type !== 6) throw new Error('请选择组合。')
  const definition = group.ComponentId ? template.Components?.find((item) => item.Id === group.ComponentId) : undefined
  const children = cloneTemplate(definition?.Elements ?? group.Elements ?? [])
  if (!children.length) throw new Error('组合没有子元素。')
  if (group.Margin?.Bottom || group.Margin?.Right || group.EndCoordinate)
    throw new Error('请先清除组合的右侧或底部间距和结束坐标，再拆开组合。')
  const groupWidth = group.Width || definition?.Width || renderedGroup?.width || 0
  const groupHeight = group.Height || definition?.Height || renderedGroup?.height || 0
  if (children.some((child, childIndex) => !child.StartCoordinate && !measured?.has(childIndex)))
    throw new Error('自动排列的组合子元素需要先完成引擎预览再拆组。')
  if (children.some((child) => !child.StartCoordinate && child.Type === 1) && (!groupWidth || !groupHeight))
    throw new Error('自动排列的文字需要明确的组合宽高才能拆组。')
  const origin = group.StartCoordinate ?? renderedGroup?.position ?? { X: 0, Y: 0 }
  for (const [childIndex, child] of children.entries()) {
    if (!child.StartCoordinate) {
      const box = measured!.get(childIndex)!
      child.StartCoordinate = { X: child.Type === 1 ? 0 : box.left - (child.Margin?.Left ?? 0),
        Y: box.top - (child.Margin?.Top ?? 0) }
      if (child.Type === 1) child.EndCoordinate = { X: groupWidth, Y: groupHeight }
    }
    translateElement(child, origin.X + (group.Margin?.Left ?? 0), origin.Y + (group.Margin?.Top ?? 0))
    if (group.Condition) child.Condition = child.Condition
      ? { Operator: 'and', Conditions: [cloneTemplate(group.Condition), child.Condition] }
      : cloneTemplate(group.Condition)
  }
  if (measured && groupHeight > 0) {
    const visible = [...measured.entries()].map(([index, box]) => ({ index,
      bottom: box.bottom + (children[index]?.Margin?.Bottom ?? 0) }))
    if (visible.length) {
      const furthest = visible.reduce((left, right) => right.bottom > left.bottom ? right : left)
      const gap = groupHeight - furthest.bottom
      if (gap > 0) {
        children[furthest.index].Margin ??= {}
        children[furthest.index].Margin!.Bottom = (children[furthest.index].Margin!.Bottom ?? 0) + gap
      }
    }
  }
  target.splice(index, 1, ...children)
  return `${match![1]}.Elements[${index}]`
}

export function renameComponent(template: BpTemplate, oldId: string, newId: string): void {
  if (!/^[a-zA-Z][a-zA-Z0-9_-]{0,63}$/.test(newId)) throw new Error('组件标识无效。')
  const definition = template.Components?.find((item) => item.Id === oldId)
  if (!definition || template.Components?.some((item) => item.Id === newId && item !== definition)) throw new Error('组件不存在或新标识已占用。')
  const visitElements = (elements: TemplateElement[]) => elements.forEach((element) => {
    if (element.Type !== 6) return
    if (element.ComponentId === oldId) element.ComponentId = newId
    visitElements(element.Elements ?? [])
  })
  const visitRows = (rows: Row[]) => rows.forEach((row) => row.Cells?.forEach((cell) => {
    visitElements(cell.Elements ?? []); visitRows(cell.Rows ?? [])
  }))
  visitRows(template.Rows)
  template.Components?.forEach((item) => visitElements(item.Elements))
  definition.Id = newId
}

export function removeComponent(template: BpTemplate, id: string): void {
  const index = template.Components?.findIndex((item) => item.Id === id) ?? -1
  if (index < 0) throw new Error('组件不存在。')
  const referenced = (elements: TemplateElement[]): boolean => elements.some((element) =>
    element.Type === 6 && (element.ComponentId === id || referenced(element.Elements ?? [])))
  const rowsReference = (rows: Row[]): boolean => rows.some((row) => row.Cells?.some((cell) =>
    referenced(cell.Elements ?? []) || rowsReference(cell.Rows ?? [])))
  if (rowsReference(template.Rows) || template.Components?.some((item) => item.Id !== id && referenced(item.Elements)))
    throw new Error('组件仍被模板引用，请先删除或解除引用。')
  template.Components!.splice(index, 1)
}

export function saveGroupComponent(template: BpTemplate, elementPath: string, id: string): void {
  const element = findElement(template, elementPath)
  if (!element || element.Type !== 6 || !element.Elements?.length) throw new Error('请先选择内联组合。')
  if (!/^[a-zA-Z][a-zA-Z0-9_-]{0,63}$/.test(id)) throw new Error('组件标识应以字母开头，只能包含字母、数字、下划线和连字符。')
  enableV2(template)
  if (template.Components!.some((component) => component.Id === id)) throw new Error('组件标识已存在。')
  template.Components!.push({ Id: id, Width: element.Width || 1, Height: element.Height || 1, Elements: cloneTemplate(element.Elements) })
  element.ComponentId = id
  delete element.Elements
}

export function appendComponent(template: BpTemplate, cellPath: string, id: string): string {
  const cell = findCell(template, cellPath)
  const definition = template.Components?.find((item) => item.Id === id)
  if (!cell || !definition) throw new Error('目标单元格或组件不存在。')
  cell.Elements ??= []
  cell.Elements.push({ Type: 6, ComponentId: id, Width: definition.Width, Height: definition.Height,
    StartCoordinate: { X: 16, Y: 16 + cell.Elements.length * 12 } })
  return `${cellPath}.Elements[${cell.Elements.length - 1}]`
}

function tableCell(width: number, field: { Name?: string; Value?: string }): Cell {
  return { Width: width, Border: { RightStyle: 1, BottomStyle: 1 }, Elements: [{ Type: 1, FontSize: 24,
    TextAlignment: 0, Field: field, StartCoordinate: { X: 4, Y: 3 } }] }
}

export function appendDataTable(template: BpTemplate, source: string, columns: Array<{ title: string; path: string; width?: number; alignment?: 0 | 1 | 2 }>, repeatHeader = true): void {
  if (!source.trim() || !columns.length || columns.length > 64 || columns.some((column) => !column.title.trim() || !column.path.trim()))
    throw new Error('表格需要有效的明细数据源和列定义。')
  const fixed = columns.reduce((sum, column) => sum + (column.width ?? 0), 0)
  const autoCount = columns.filter((column) => !column.width).length
  if (columns.some((column) => column.width !== undefined && (!Number.isInteger(column.width) || column.width < 1)) ||
    fixed > template.Width || (autoCount && template.Width - fixed < autoCount)) throw new Error('表格列宽超过纸张宽度。')
  enableV2(template)
  const autoWidth = autoCount ? Math.floor((template.Width - fixed) / autoCount) : 0
  let remainder = template.Width - fixed - autoWidth * autoCount
  const widths = columns.map((column) => column.width ?? (autoWidth + (remainder-- > 0 ? 1 : 0)))
  const header: Row = { Section: repeatHeader ? 'TableHeader' : 'Body', Height: 32,
    Cells: columns.map((column, index) => {
      const cell = tableCell(widths[index], { Value: column.title })
      if (cell.Elements?.[0].Type === 1) cell.Elements[0].TextAlignment = column.alignment ?? 0
      return cell
    }) }
  const detail: Row = { Section: 'Body', RepeatSource: source, Height: 32,
    Cells: columns.map((column, index) => {
      const cell = tableCell(widths[index], { Name: column.path })
      if (cell.Elements?.[0].Type === 1) cell.Elements[0].TextAlignment = column.alignment ?? 0
      return cell
    }) }
  template.Rows.push(header, detail)
}

export function appendStaticTable(template: BpTemplate, headers: string[], rows: string[][]): void {
  if (!headers.length || headers.length > 64 || rows.length > 10000) throw new Error('表格尺寸超过限制。')
  enableV2(template)
  const width = Math.floor(template.Width / headers.length)
  const widths = headers.map((_, index) => index === headers.length - 1 ? template.Width - width * index : width)
  const makeRow = (values: string[]): Row => ({ Section: 'Body', Height: 32,
    Cells: values.map((value, index) => tableCell(widths[index], { Value: value })) })
  template.Rows.push(makeRow(headers), ...rows.map(makeRow))
}
