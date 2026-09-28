export type Point = { X: number; Y: number }

export type Field = {
  Title?: string
  Name?: string
  Value?: string
  NeedFeedLine?: boolean
  Fit?: boolean
}

export type Condition = {
  Path?: string
  Operator: 'eq' | 'ne' | 'gt' | 'ge' | 'lt' | 'le' | 'isNull' | 'notNull' | 'and' | 'or'
  Value?: unknown
  Conditions?: Condition[]
}

export type BaseElement = {
  Type: 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10
  Field?: Field
  Condition?: Condition
  StartCoordinate?: Point
  EndCoordinate?: Point
  Margin?: { Top?: number; Left?: number; Right?: number; Bottom?: number }
}

export type FontElement = BaseElement & {
  Type: 1
  FontSize?: number
  FontStyle?: number
  FontName?: number
  Bold?: boolean
  TextAlignment?: 0 | 1 | 2
}

export type ImageElement = BaseElement & { Type: 2; Width?: number; Height?: number; IsCompress?: boolean }
export type QrElement = BaseElement & { Type: 3; UnitWidth?: number; ErrorLevel?: 'L' | 'M' | 'Q' | 'H'; Width?: number; Height?: number }
export type BarcodeElement = BaseElement & { Type: 4; BarcodeType?: number; LineWidth?: number; Ratio?: number; Width?: number; Height?: number }
export type LineElement = BaseElement & { Type: 5; LineWidth?: number; LineEndCoordinate?: Point }
export type GroupElement = BaseElement & { Type: 6; ComponentId?: string; Width?: number; Height?: number; Elements?: TemplateElement[] }
export type RectangleElement = BaseElement & { Type: 7; Width: number; Height: number; LineWidth?: number }
export type EllipseElement = BaseElement & { Type: 8; Width: number; Height: number; LineWidth?: number }
export type IconElement = BaseElement & { Type: 9; Icon: 'check' | 'cross' | 'info'; Size: number; LineWidth?: number }
export type MatrixElement = BaseElement & { Type: 10; Format: 'DataMatrix' | 'PDF417'; Width: number; Height: number }
export type TemplateElement = FontElement | ImageElement | QrElement | BarcodeElement | LineElement | GroupElement | RectangleElement | EllipseElement | IconElement | MatrixElement
export type ComponentDefinition = { Id: string; Width: number; Height: number; Elements: TemplateElement[] }

export type Border = {
  TopStyle?: number
  RightStyle?: number
  LeftStyle?: number
  BottomStyle?: number
  Color?: number
}

export type Cell = {
  Width?: number
  Border?: Border
  Rows?: Row[]
  Elements?: TemplateElement[]
  ElementAlls?: unknown[]
}

export type Row = {
  RepeatSource?: string
  IsHeader?: boolean
  Section?: 'Body' | 'DocumentHeader' | 'PageHeader' | 'DocumentFooter' | 'PageFooter' | 'TableHeader'
  Condition?: Condition
  Height?: number
  Border?: Border
  Cells?: Cell[]
}

export type BpTemplate = {
  Version: 1 | 2
  Variables?: Record<string, string>
  Components?: ComponentDefinition[]
  AutoHeight?: boolean
  PaperType?: number
  Width: number
  Height: number
  Color?: number
  Border?: Border
  Rows: Row[]
}

export type ElementPath = string

export type PreviewElement = {
  sourcePath: string
  kind?: 'element' | 'row' | 'cell'
  instanceIndices?: number[]
  type: number
  x: number
  y: number
  width: number
  height: number
  text?: string | null
  resourceKey?: string | null
}

export type PreviewPage = { width: number; height: number; elements: PreviewElement[] }
export type PreviewResponse = { pages: PreviewPage[]; errors?: Array<{ sourcePath?: string; message: string }> }

export type TemplateResource = { key: string; fileName: string; mimeType: string; dataUrl: string }

export function cloneTemplate<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

export function assertTemplate(value: unknown): asserts value is BpTemplate {
  if (!value || typeof value !== 'object') throw new Error('模板必须是 JSON 对象。')
  const template = value as Partial<BpTemplate>
  if (template.Version !== 1 && template.Version !== 2) throw new Error('仅支持 Version: 1 或 2 模板。')
  if (!Number.isInteger(template.Width) || (template.Width ?? 0) <= 0) throw new Error('模板 Width 必须为正整数打印点。')
  if (!Number.isInteger(template.Height) || (template.Height ?? 0) < 0) throw new Error('模板 Height 必须为非负整数打印点。')
  if (!Array.isArray(template.Rows)) throw new Error('模板 Rows 必须为数组。')
  if (template.Version === 1 && (template.Variables !== undefined || template.Components !== undefined))
    throw new Error('Version: 1 不支持变量或组件。')
  if (template.Version === 2 && template.Components !== undefined && !Array.isArray(template.Components))
    throw new Error('Components 必须为数组。')
  if (template.Version === 2 && template.Variables !== undefined &&
    (!template.Variables || Array.isArray(template.Variables) || typeof template.Variables !== 'object'))
    throw new Error('Variables 必须为名称到字段路径的对象。')

  const stack: Array<{ value: unknown; depth: number }> = [{ value, depth: 0 }]
  const seen = new WeakSet<object>()
  let nodeCount = 0
  while (stack.length) {
    const current = stack.pop()!
    if (!current.value || typeof current.value !== 'object') continue
    if (seen.has(current.value)) throw new Error('模板不能包含循环引用。')
    seen.add(current.value)
    if (current.depth > 16) throw new Error('模板嵌套深度不能超过 16 层。')
    if (++nodeCount > 20000) throw new Error('模板结构节点不能超过 20,000 个。')
    if (Array.isArray(current.value)) {
      if (current.value.length > 10000) throw new Error('模板数组不能超过 10,000 项。')
      for (const child of current.value) stack.push({ value: child, depth: current.depth + 1 })
    } else {
      const object = current.value as Record<string, unknown>
      for (const [key, child] of Object.entries(object)) {
        if (key === 'Type' && typeof child === 'number' && ![1, 2, 3, 4, 5, 6, 7, 8, 9, 10].includes(child)) throw new Error(`不支持的元素类型：${child}`)
        if (template.Version === 1 && key === 'Type' && typeof child === 'number' && child > 5)
          throw new Error('Version: 1 不支持此元素类型。')
        if (template.Version === 1 && key === 'Section' && child !== undefined)
          throw new Error('Version: 1 不支持页面分区。')
        if (child && typeof child === 'object') stack.push({ value: child, depth: current.depth + 1 })
      }
    }
  }

  if (template.Version === 2) {
    const rows = template.Rows as Row[]
    rows.forEach((row, index) => {
      if (row.Section !== 'TableHeader') return
      const detail = rows[index + 1]
      if (row.RepeatSource || !detail?.RepeatSource || (detail.Section && detail.Section !== 'Body'))
        throw new Error(`Rows[${index}] 表格表头必须紧接一行正文重复明细。`)
    })
    const definitions = new Map<string, ComponentDefinition>()
    for (const component of template.Components ?? []) {
      if (!component || typeof component.Id !== 'string' || !component.Id || definitions.has(component.Id) ||
        !Array.isArray(component.Elements) || !Number.isInteger(component.Width) || component.Width <= 0 ||
        !Number.isInteger(component.Height) || component.Height <= 0)
        throw new Error('组件定义无效或标识重复。')
      definitions.set(component.Id, component)
    }
    let expanded = 0
    const visitElements = (elements: TemplateElement[], active: Set<string>, depth: number): void => {
      if (depth > 16 || ++expanded > 20000) throw new Error('组件展开超过深度或节点上限。')
      for (const element of elements) {
        if (element.Type !== 6) continue
        if (element.ComponentId) {
          const definition = definitions.get(element.ComponentId)
          if (!definition) throw new Error(`组件不存在：${element.ComponentId}`)
          if (active.has(element.ComponentId)) throw new Error(`组件存在循环引用：${element.ComponentId}`)
          visitElements(definition.Elements, new Set([...active, element.ComponentId]), depth + 1)
        } else if (element.Elements) visitElements(element.Elements, active, depth + 1)
      }
    }
    for (const [id, component] of definitions) visitElements(component.Elements, new Set([id]), 0)
    const visitRows = (rows: Row[]): void => {
      for (const row of rows) for (const cell of row.Cells ?? []) {
        visitElements(cell.Elements ?? [], new Set(), 0)
        visitRows(cell.Rows ?? [])
      }
    }
    visitRows(template.Rows)
  }
}
