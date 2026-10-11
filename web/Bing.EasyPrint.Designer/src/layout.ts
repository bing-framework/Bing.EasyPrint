import { conditionMatches, resolvePath, valueForField, type BindingContext } from './binding'
import { barcodeAutoOccupiedSize, quietZoneHorizontalExtent, quietZoneVerticalExtent } from './coordinates'
import type { BpTemplate, PreviewElement, RotationAngle, Row, TemplateElement } from './model'

export type LocalLayout = { pages: Array<{ width: number; height: number; elements: PreviewElement[] }>; errors: Array<{ sourcePath: string; message: string }> }

type LayoutContext = { root: unknown; item: unknown; index: number; instanceIndices: number[]; variables?: Record<string, string> }

function getPath(path: string, ctx: LayoutContext): unknown {
  return path === '$root' ? ctx.root : path === '$item' ? ctx.item : path === '$index' ? ctx.index : undefined
}

function bindingContext(ctx: LayoutContext): BindingContext {
  return { root: ctx.root, item: ctx.item, index: ctx.index, variables: ctx.variables }
}

function safeCondition(rowOrElement: { Condition?: Row['Condition'] }, ctx: LayoutContext, path: string, errors: LocalLayout['errors']): boolean {
  try { return conditionMatches(rowOrElement.Condition, bindingContext(ctx)) }
  catch (error) { errors.push({ sourcePath: path, message: error instanceof Error ? error.message : '条件预览失败。' }); return true }
}

function rotationOf(element: TemplateElement): RotationAngle {
  return element.Type === 1 || element.Type === 2 || element.Type === 3 || element.Type === 4 ? element.Rotation ?? 0 : 0
}

function occupiedSize(width: number, height: number, rotation: RotationAngle): { width: number; height: number } {
  return rotation === 90 || rotation === 270 ? { width: height, height: width } : { width, height }
}

function dimension(element: TemplateElement, cellWidth: number, text: string): { width: number; height: number } {
  const start = element.StartCoordinate ?? { X: 0, Y: 0 }
  const end = element.EndCoordinate
  if (element.Type === 1) {
    // EndCoordinate 保存的是旋转后的占位框；已有端点时直接使用，不能再次交换宽高。
    if (end) return { width: Math.max(1, end.X - start.X), height: Math.max(1, end.Y - start.Y) }
    const size = (element.FontSize || 24)
    const characters = [...text].length
    const estimated = Math.ceil(characters * size * 0.72)
    const width = element.Width !== undefined && element.Width > 0
      ? element.Width
      : Math.max(size, cellWidth - start.X)
    const lineCount = element.Field?.NeedFeedLine === false ? 1 : Math.max(1, Math.ceil(estimated / Math.max(width, 1)))
    return occupiedSize(width, lineCount * (size + 2), rotationOf(element))
  }
  if (element.Type === 5) {
    const endPoint = element.LineEndCoordinate ?? end ?? start
    return { width: Math.abs(endPoint.X - start.X) + (element.LineWidth || 1), height: Math.abs(endPoint.Y - start.Y) + (element.LineWidth || 1) }
  }
  if (element.Type === 4 && end)
    return { width: Math.max(1, end.X - start.X), height: Math.max(1, end.Y - start.Y) }
  const width = element.Type === 9 ? element.Size : element.Width
  const height = element.Type === 9 ? element.Size : element.Height
  const widthMissing = !width || width <= 0
  const heightMissing = !height || height <= 0
  const fallbackWidth = width || (end ? end.X - start.X : 112)
  const fallbackHeight = height || (end ? end.Y - start.Y : 112)
  if (element.Type === 2 && end && (widthMissing || heightMissing)) {
    // 图片的 EndCoordinate 是旋转后的外占位；缺少原始宽高时先还原资源尺寸，
    // 避免 90/270 度在离线布局中再次交换轴。
    const occupiedWidth = Math.max(1, end.X - start.X)
    const occupiedHeight = Math.max(1, end.Y - start.Y)
    const quarterTurn = rotationOf(element) === 90 || rotationOf(element) === 270
    const rawWidth = widthMissing ? (quarterTurn ? occupiedHeight : occupiedWidth) : fallbackWidth
    const rawHeight = heightMissing ? (quarterTurn ? occupiedWidth : occupiedHeight) : fallbackHeight
    return occupiedSize(rawWidth, rawHeight, rotationOf(element))
  }
  if (element.Type === 3) {
    // EndCoordinate 是既有外占位时直接沿用；没有端点的自动尺寸才在编码体估算上追加显式静区。
    const widthWithQuiet = widthMissing && !end ? fallbackWidth + quietZoneHorizontalExtent(element.QuietZone) : fallbackWidth
    const heightWithQuiet = heightMissing && !end ? fallbackHeight + quietZoneVerticalExtent(element.QuietZone) : fallbackHeight
    return { width: widthWithQuiet, height: heightWithQuiet }
  }
  if (element.Type === 4)
    return barcodeAutoOccupiedSize(fallbackWidth, fallbackHeight, element.HumanReadableReserveHeight || 0,
      rotationOf(element), element.QuietZone, !widthMissing, {
        barcodeType: element.BarcodeType,
        lineWidth: element.LineWidth,
        narrowModuleWidth: element.NarrowModuleWidth,
      })
  return occupiedSize(fallbackWidth, fallbackHeight, rotationOf(element))
}

export function buildLocalLayout(template: BpTemplate, data: unknown): LocalLayout {
  const errors: LocalLayout['errors'] = []
  const elements: PreviewElement[] = []
  const appendGroupChildren = (group: Extract<TemplateElement, { Type: 6 }>, path: string, x: number, y: number, ctx: LayoutContext, depth = 0): void => {
    if (depth > 16) { errors.push({ sourcePath: path, message: '组合嵌套层数超过限制。' }); return }
    const children = group.ComponentId ? template.Components?.find((item) => item.Id === group.ComponentId)?.Elements : group.Elements
    if (!children) { errors.push({ sourcePath: path, message: '组件定义不存在。' }); return }
    children.forEach((child, index) => {
      const childPath = `${path}.Elements[${index}]`
      if (!safeCondition(child, ctx, childPath, errors)) return
      let display = ''
      try { display = valueForField(child.Field, bindingContext(ctx)) }
      catch (error) { errors.push({ sourcePath: childPath, message: error instanceof Error ? error.message : '字段预览失败。' }) }
      const start = child.StartCoordinate ?? { X: 0, Y: 0 }
      const box = dimension(child, group.Width || template.Width, display)
      const childX = x + start.X + (child.Margin?.Left ?? 0)
      const childY = y + start.Y + (child.Margin?.Top ?? 0)
      elements.push({ sourcePath: childPath, instanceIndices: [...ctx.instanceIndices], type: child.Type,
        rotation: rotationOf(child), x: childX, y: childY, width: box.width, height: box.height,
        text: child.Type === 1 || child.Type === 3 || child.Type === 4 || child.Type === 10 ? display : undefined,
        resourceKey: child.Type === 2 ? display : undefined })
      if (child.Type === 6) appendGroupChildren(child, childPath, childX, childY, ctx, depth + 1)
    })
  }
  const visitRow = (row: Row, width: number, x: number, y: number, path: string, ctx: LayoutContext): number => {
    const repeat = row.RepeatSource ? resolveRepeater(row.RepeatSource, ctx, path, errors) : null
    if (row.RepeatSource && repeat) {
      let cursor = y
      repeat.forEach((item, index) => {
        const child = { ...ctx, item, index, instanceIndices: [...ctx.instanceIndices, index] }
        if (safeCondition(row, child, path, errors)) cursor += visitRowContent(row, width, x, cursor, path, child)
      })
      return cursor - y
    }
    if (row.RepeatSource && !repeat) return 0
    return safeCondition(row, ctx, path, errors) ? visitRowContent(row, width, x, y, path, ctx) : 0
  }

  const visitRowContent = (row: Row, width: number, x: number, y: number, path: string, ctx: LayoutContext): number => {
    const cells = row.Cells ?? []
    const totalExplicit = cells.reduce((sum, cell) => sum + (cell.Width || 0), 0)
    const auto = cells.filter((cell) => !cell.Width).length
    if (totalExplicit > width) errors.push({ sourcePath: path, message: '单元格宽度合计超过父级宽度。' })
    let remaining = Math.max(0, width - totalExplicit)
    let offsetX = x
    let rowContentHeight = 0
    cells.forEach((cell, cellIndex) => {
      const cellPath = `${path}.Cells[${cellIndex}]`
      const cellWidth = cell.Width || (auto ? Math.floor(remaining / auto) : width)
      if (!cell.Width && auto) remaining -= cellWidth
      const localElements: Array<{ element: TemplateElement; path: string; context: LayoutContext }> = []
      let nestedCursor = 0
      ;(cell.Rows ?? []).forEach((nested, nestedIndex) => {
        nestedCursor += visitRow(nested, cellWidth, offsetX, y + nestedCursor, `${cellPath}.Rows[${nestedIndex}]`, ctx)
      })
      ;(cell.Elements ?? []).forEach((element, elementIndex) => localElements.push({ element, path: `${cellPath}.Elements[${elementIndex}]`, context: ctx }))
      for (const node of localElements) {
        if (!safeCondition(node.element, node.context, node.path, errors)) continue
        let display = ''
        try { display = valueForField(node.element.Field, bindingContext(node.context)) }
        catch (error) { errors.push({ sourcePath: node.path, message: error instanceof Error ? error.message : '字段预览失败。' }) }
        const start = node.element.StartCoordinate ?? { X: 0, Y: nestedCursor }
        const box = dimension(node.element, cellWidth, display)
        const margin = node.element.Margin ?? {}
        const left = margin.Left ?? 0
        const top = margin.Top ?? 0
        const end = node.element.Type === 5 ? (node.element.LineEndCoordinate ?? node.element.EndCoordinate ?? start) : null
        elements.push({
          sourcePath: node.path,
          instanceIndices: [...node.context.instanceIndices],
          type: node.element.Type,
          rotation: rotationOf(node.element),
          x: offsetX + start.X + left,
          y: y + start.Y + top,
          width: box.width,
          height: box.height,
          text: node.element.Type === 1 ? display : node.element.Type === 2 ? (node.element.Field?.Name || 'image') : node.element.Type === 3 ? display : node.element.Type === 4 ? display : null,
          resourceKey: node.element.Type === 2 ? node.element.Field?.Name : null,
        })
        if (node.element.Type === 6) appendGroupChildren(node.element, node.path,
          offsetX + start.X + left, y + start.Y + top, node.context)
        if (end && node.element.Type === 5) {
          const placed = elements[elements.length - 1]
          placed.width = Math.abs(end.X - start.X) + (node.element.LineWidth || 1)
          placed.height = Math.abs(end.Y - start.Y) + (node.element.LineWidth || 1)
          if (end.X < start.X) placed.x = offsetX + end.X
          if (end.Y < start.Y) placed.y = y + end.Y
        }
      }
      rowContentHeight = Math.max(rowContentHeight, nestedCursor)
      for (const element of cell.Elements ?? []) {
        const start = element.StartCoordinate ?? { X: 0, Y: nestedCursor }
        const size = dimension(element, cellWidth, '')
        rowContentHeight = Math.max(rowContentHeight, start.Y + size.height + (element.Margin?.Bottom ?? 0))
      }
      offsetX += cellWidth
    })
    return row.Height || Math.max(rowContentHeight, 1)
  }

  const resolveRepeater = (path: string, ctx: LayoutContext, sourcePath: string, targetErrors: LocalLayout['errors']): unknown[] | null => {
    let value = getPath(path, ctx)
    if (value === undefined) {
      try { value = (awaitlessResolve(path, ctx)) }
      catch (error) { targetErrors.push({ sourcePath, message: error instanceof Error ? error.message : '重复数据源不存在。' }); return null }
    }
    if (value == null) return null
    if (!Array.isArray(value)) { targetErrors.push({ sourcePath, message: 'RepeatSource 必须指向数组。' }); return null }
    return value
  }

  const rootContext: LayoutContext = { root: data, item: data, index: -1, instanceIndices: [], variables: template.Variables }
  let cursor = 0
  const rows = template.Rows ?? []
  const ordered = rows.map((row, index) => ({ row, index })).sort((a, b) => {
    const order = (row: Row) => row.IsHeader || row.Section === 'PageHeader' ? 0 : row.Section === 'DocumentHeader' ? 1 : row.Section === 'DocumentFooter' ? 3 : row.Section === 'PageFooter' ? 4 : 2
    return order(a.row) - order(b.row) || a.index - b.index
  })
  for (const { row, index } of ordered) {
    cursor += visitRow(row, template.Width, 0, cursor, `Rows[${index}]`, rootContext)
  }
  const pageHeight = template.AutoHeight ? Math.max(cursor, 1) : template.Height
  const pages: LocalLayout['pages'] = []
  if (template.AutoHeight || cursor <= pageHeight) pages.push({ width: template.Width, height: pageHeight || cursor || 1, elements })
  else {
    let page = { width: template.Width, height: pageHeight, elements: [] as PreviewElement[] }
    for (const element of elements) {
      if (element.y + element.height > pageHeight && page.elements.length) {
        pages.push(page)
        page = { width: template.Width, height: pageHeight, elements: [] }
      }
      page.elements.push({ ...element, y: element.y >= pageHeight ? element.y % pageHeight : element.y })
    }
    pages.push(page)
  }
  return { pages, errors }
}

function awaitlessResolve(path: string, ctx: LayoutContext): unknown {
  if (path.startsWith('$var.')) return resolvePath(path, bindingContext(ctx))
  if (path === '$root') return ctx.root
  if (path === '$item') return ctx.item
  if (path === '$index') return ctx.index
  const root = path.startsWith('$root.')
  const item = path.startsWith('$item.')
  const base = root ? ctx.root : ctx.item
  const lookup = root || item ? path.slice(path.indexOf('.') + 1) : path
  let value = base
  for (const key of lookup.split('.')) {
    if (value == null) return null
    if (typeof value !== 'object' || !Object.prototype.hasOwnProperty.call(value, key)) throw new Error(`字段不存在：${lookup}`)
    value = (value as Record<string, unknown>)[key]
  }
  return value
}
