import { walkTemplateNodes } from './templateTraversal'

export type Point = { X: number; Y: number }

export type RotationAngle = 0 | 90 | 180 | 270

/** Type 3/4 的显式静区，单位为打印点；省略的边按 0 处理。 */
export type QuietZone = { Left?: number; Top?: number; Right?: number; Bottom?: number }
export const MAX_QUIET_ZONE_DOTS = 2_147_483_647

export type JsonValue = string | number | boolean | null | JsonValue[] | { [key: string]: JsonValue }

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

export type PrintFontKind = 0 | 1 | 2

export type PrintFont = {
  Kind: PrintFontKind
  Name?: string
}

export type FontElement = BaseElement & {
  Type: 1
  FontSize?: number
  FontStyle?: number
  FontName?: number
  FontFamily?: string
  DeviceFontName?: string
  Font?: PrintFont
  Rotation?: RotationAngle
  /** 旋转前的单行排版宽度；零或未指定时由布局自动选择。 */
  Width?: number
  Bold?: boolean
  Reverse?: boolean
  TextAlignment?: 0 | 1 | 2
}

/** 表示图片源文件中的像素裁切区域；省略时使用完整源图。 */
export type ImageCrop = {
  X: number
  Y: number
  Width: number
  Height: number
}

/** 图片宽高始终表示旋转前的资源尺寸；预览占位框会按 Rotation 交换 90/270 度的轴。 */
export type ImageElement = BaseElement & {
  Type: 2
  Width?: number
  Height?: number
  Rotation?: RotationAngle
  IsCompress?: boolean
  /** 渲染时先按源像素裁切，再按 Width/Height 缩放；省略或 null 表示完整源图。 */
  Crop?: ImageCrop | null
}
export type QrElement = BaseElement & {
  Type: 3
  UnitWidth?: number
  ErrorLevel?: 'L' | 'M' | 'Q' | 'H'
  Width?: number
  Height?: number
  Rotation?: RotationAngle
  QuietZone?: QuietZone | null
}
export type BarcodeElement = BaseElement & {
  Type: 4
  BarcodeType?: number
  LineWidth?: number
  Ratio?: number
  NarrowModuleWidth?: number
  WideModuleWidth?: number
  Width?: number
  Height?: number
  Rotation?: RotationAngle
  HumanReadable?: boolean
  HumanReadableReserveHeight?: number
  QuietZone?: QuietZone | null
}
export type LineElement = BaseElement & { Type: 5; LineWidth?: number; LineEndCoordinate?: Point }
export type ComponentParameterBinding = { Path?: string; Value?: JsonValue }
export type ComponentParameterDefinition = { Required?: boolean; DefaultValue?: JsonValue }
export type GroupPagination = 'KeepTogether' | 'Split'
export type GroupElement = BaseElement & {
  Type: 6
  ComponentId?: string
  Width?: number
  Height?: number
  Elements?: TemplateElement[]
  Rows?: Row[]
  DataPath?: string
  Parameters?: Record<string, ComponentParameterBinding>
  Pagination?: GroupPagination
}
export type RectangleElement = BaseElement & { Type: 7; Width: number; Height: number; LineWidth?: number; Filled?: boolean }
export type EllipseElement = BaseElement & { Type: 8; Width: number; Height: number; LineWidth?: number }
export type IconElement = BaseElement & { Type: 9; Icon: 'check' | 'cross' | 'info'; Size: number; LineWidth?: number }
export type MatrixElement = BaseElement & { Type: 10; Format: 'DataMatrix' | 'PDF417'; Width: number; Height: number }
export type TemplateElement = FontElement | ImageElement | QrElement | BarcodeElement | LineElement | GroupElement | RectangleElement | EllipseElement | IconElement | MatrixElement
export type ComponentDefinition = {
  Id: string
  Width?: number
  Height?: number
  Elements?: TemplateElement[]
  Rows?: Row[]
  Parameters?: Record<string, ComponentParameterDefinition>
}

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
  Version: 1 | 2 | 3
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
  definitionPath?: string | null
  componentInstancePath?: string | null
  fragmentIndex?: number
  kind?: 'element' | 'row' | 'cell' | 'group' | 'component'
  instanceIndices?: number[]
  type: number
  rotation?: RotationAngle
  x: number
  y: number
  width: number
  height: number
  text?: string | null
  resourceKey?: string | null
}

export type PreviewPage = {
  width: number
  height: number
  elements: PreviewElement[]
  /** 任务输出中对应的逻辑页索引；旧响应省略。 */
  sourcePageIndex?: number | null
  /** 任务输出中的零基份数索引；旧响应或非任务响应省略。 */
  copyIndex?: number | null
}
/** 引擎诊断中的索引从零开始；静态预检尚未展开实例时不提供运行时位置。 */
export type DesignerDiagnostic = {
  message: string
  sourcePath?: string | null
  definitionPath?: string | null
  componentInstancePath?: string | null
  instanceIndices?: number[] | null
  pageIndex?: number | null
  elementIndex?: number | null
  code?: string | null
  reason?: string | null
  phase?: string | null
  fieldPath?: string | null
  resourceName?: string | null
  sourcePixelWidth?: number | null
  sourcePixelHeight?: number | null
  renderedPixelWidth?: number | null
  renderedPixelHeight?: number | null
  cropX?: number | null
  cropY?: number | null
  cropWidth?: number | null
  cropHeight?: number | null
  requestedBytes?: number | null
  currentBytes?: number | null
  maxBytes?: number | null
}
export type PreviewResponse = { pages: PreviewPage[]; errors?: DesignerDiagnostic[] }

export type TemplateResource = { key: string; fileName: string; mimeType: string; dataUrl: string }

export function cloneTemplate<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

function validComponentDimension(value: unknown, isV3: boolean): boolean {
  if (value === undefined) return isV3
  return typeof value === 'number' && Number.isInteger(value) && (isV3 ? value >= 0 : value > 0)
}

export function assertTemplate(value: unknown): asserts value is BpTemplate {
  if (!value || typeof value !== 'object') throw new Error('模板必须是 JSON 对象。')
  const template = value as Partial<BpTemplate>
  if (template.Version !== 1 && template.Version !== 2 && template.Version !== 3) throw new Error('仅支持 Version: 1、2 或 3 模板。')
  if (!Number.isInteger(template.Width) || (template.Width ?? 0) <= 0) throw new Error('模板 Width 必须为正整数打印点。')
  if (!Number.isInteger(template.Height) || (template.Height ?? 0) < 0) throw new Error('模板 Height 必须为非负整数打印点。')
  if (!Array.isArray(template.Rows)) throw new Error('模板 Rows 必须为数组。')
  if (template.Version === 1 && (template.Variables !== undefined || template.Components !== undefined))
    throw new Error('Version: 1 不支持变量或组件。')
  if (template.Version !== 1 && template.Components !== undefined && !Array.isArray(template.Components))
    throw new Error('Components 必须为数组。')
  if (template.Version !== 1 && template.Variables !== undefined &&
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
    if (current.depth > 16 * 8 + 16) throw new Error('模板 JSON 嵌套深度不能超过 144 层。')
    if (++nodeCount > 20000) throw new Error('模板结构节点不能超过 20,000 个。')
    if (Array.isArray(current.value)) {
      if (current.value.length > 10000) throw new Error('模板数组不能超过 10,000 项。')
      for (const child of current.value) stack.push({ value: child, depth: current.depth + 1 })
    } else {
      const object = current.value as Record<string, unknown>
      for (const child of Object.values(object)) {
        if (child && typeof child === 'object') stack.push({ value: child, depth: current.depth + 1 })
      }
    }
  }

  const definitions = new Map<string, ComponentDefinition>()
  for (const component of template.Components ?? []) {
    const isV3 = template.Version === 3
    if (!component || typeof component.Id !== 'string' || !component.Id || definitions.has(component.Id) ||
      !validComponentDimension(component.Width, isV3) ||
      !validComponentDimension(component.Height, isV3) ||
      (!isV3 && !Array.isArray(component.Elements)) ||
      (component.Elements !== undefined && !Array.isArray(component.Elements)) ||
      (component.Rows !== undefined && !Array.isArray(component.Rows)) ||
      (component.Elements !== undefined && component.Rows !== undefined) ||
      (!isV3 && (component.Rows !== undefined || component.Parameters !== undefined)))
      throw new Error('组件定义无效或标识重复。')
    definitions.set(component.Id, component)
  }

  const own = (target: object, key: string) => Object.prototype.hasOwnProperty.call(target, key)
  const assertJsonValue = (json: unknown, path: string, seen = new WeakSet<object>()): void => {
    if (json === null || typeof json === 'string' || typeof json === 'boolean') return
    if (typeof json === 'number') {
      if (Number.isFinite(json)) return
      throw new Error(`${path}: JSON 数值必须为有限数字。`)
    }
    if (!json || typeof json !== 'object') throw new Error(`${path}: 参数值必须是 JSON 数据。`)
    if (seen.has(json)) throw new Error(`${path}: 参数值不能包含循环引用。`)
    seen.add(json)
    if (Array.isArray(json)) json.forEach((child, index) => assertJsonValue(child, `${path}[${index}]`, seen))
    else {
      const prototype = Object.getPrototypeOf(json)
      if (prototype !== Object.prototype && prototype !== null) throw new Error(`${path}: 参数值必须是 JSON 对象。`)
      for (const [key, child] of Object.entries(json)) assertJsonValue(child, `${path}.${key}`, seen)
    }
    seen.delete(json)
  }

  const validateParameterDefinitions = (parameters: unknown, path: string): void => {
    if (parameters === undefined) return
    if (!parameters || typeof parameters !== 'object' || Array.isArray(parameters)) throw new Error(`${path}: Parameters 必须为对象。`)
    for (const [name, definition] of Object.entries(parameters)) {
      if (!name.trim() || name.includes('.') || !definition || typeof definition !== 'object' || Array.isArray(definition))
        throw new Error(`${path}.${name}: 组件参数定义无效。`)
      const entry = definition as Record<string, unknown>
      if (entry.Required !== undefined && typeof entry.Required !== 'boolean') throw new Error(`${path}.${name}.Required 必须为布尔值。`)
      if (own(entry, 'DefaultValue')) assertJsonValue(entry.DefaultValue, `${path}.${name}.DefaultValue`)
    }
  }

  for (const [id, component] of definitions) {
    validateParameterDefinitions(component.Parameters, `Components.${id}.Parameters`)
  }

  walkTemplateNodes(template as BpTemplate, (node, context) => {
    if (!node || typeof node !== 'object' || Array.isArray(node)) throw new Error(`${context.path}: 模板节点必须为对象。`)
    if (context.kind === 'component') {
      const component = node as ComponentDefinition
      if (component.Elements !== undefined && !Array.isArray(component.Elements)) throw new Error(`${context.path}.Elements 必须为数组。`)
      if (component.Rows !== undefined && !Array.isArray(component.Rows)) throw new Error(`${context.path}.Rows 必须为数组。`)
      return
    }
    if (context.kind === 'row') {
      const row = node as Row
      if (row.Cells !== undefined && !Array.isArray(row.Cells)) throw new Error(`${context.path}.Cells 必须为数组。`)
      if (template.Version === 1 && row.Section !== undefined) throw new Error('Version: 1 不支持页面分区。')
      if (row.Section !== undefined) {
        const validSections = ['Body', 'DocumentHeader', 'PageHeader', 'DocumentFooter', 'PageFooter', 'TableHeader']
        if (!validSections.includes(row.Section)) throw new Error(`${context.path}.Section 无效。`)
        if (template.Version === 2 && !context.topLevelRow) throw new Error('Version: 2 仅支持顶层页面分区。')
        if (template.Version === 3 && !context.topLevelRow && row.Section !== 'Body' && row.Section !== 'TableHeader')
          throw new Error(`${context.path}.Section 仅支持 Body 或 TableHeader。`)
      }
      if (row.Section === 'TableHeader') {
        const detail = context.rowCollection?.[Number(context.rowIndex) + 1]
        if (row.RepeatSource || !detail?.RepeatSource || (detail.Section !== undefined && detail.Section !== 'Body'))
          throw new Error(`${context.sourcePath} 表格表头必须紧接一行正文重复明细。`)
      }
      if (row.Cells !== undefined && !Array.isArray(row.Cells)) throw new Error(`${context.path}.Cells 必须为数组。`)
      return
    }
    if (context.kind === 'cell') {
      const cell = node as Cell
      if (cell.Rows !== undefined && !Array.isArray(cell.Rows)) throw new Error(`${context.path}.Rows 必须为数组。`)
      if (cell.Elements !== undefined && !Array.isArray(cell.Elements)) throw new Error(`${context.path}.Elements 必须为数组。`)
      return
    }

    const element = node as TemplateElement
    const validTypes = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]
    if (!Number.isInteger(element.Type) || !validTypes.includes(element.Type)) throw new Error(`不支持的元素类型：${String(element.Type)}`)
    if (template.Version === 1 && element.Type > 5) throw new Error('Version: 1 不支持此元素类型。')
    const rawElement = element as unknown as Record<string, unknown>
    if (own(rawElement, 'Filled')) {
      if (typeof rawElement.Filled !== 'boolean') throw new Error(`${context.path}.Filled 必须为布尔值。`)
      if (element.Type !== 7 && rawElement.Filled === true) throw new Error(`${context.path}.Filled 仅支持矩形元素。`)
    }
    if (rawElement.Rotation !== undefined) {
      const rotation = rawElement.Rotation
      if (!Number.isInteger(rotation) || ![0, 90, 180, 270].includes(rotation as number))
        throw new Error(`${context.path}.Rotation 必须是 0、90、180 或 270。`)
      if (element.Type !== 1 && element.Type !== 2 && element.Type !== 3 && element.Type !== 4 && rotation !== 0)
        throw new Error(`${context.path}.Rotation 仅支持文字、二维码、一维条码以及图片元素。`)
    }
    if (element.Type === 1 && rawElement.Width !== undefined &&
      (!Number.isInteger(rawElement.Width) || (rawElement.Width as number) < 0))
      throw new Error(`${context.path}.Width 必须为非负整数打印点。`)
    if (element.Type === 2) {
      for (const key of ['Width', 'Height'] as const) {
        const value = rawElement[key]
        if (value !== undefined && (!Number.isInteger(value) || (value as number) < 0))
          throw new Error(`${context.path}.${key} 必须为非负整数打印点。`)
      }
      if (own(rawElement, 'Crop')) {
        const crop = rawElement.Crop
        if (crop === null) {
          // null 与省略保持相同的完整源图语义，兼容后端可空属性。
        } else {
          if (!crop || typeof crop !== 'object' || Array.isArray(crop))
            throw new Error(`${context.path}.Crop 必须为对象或 null。`)
          const cropRecord = crop as Record<string, unknown>
          const cropKeys = ['X', 'Y', 'Width', 'Height']
          for (const key of Object.keys(cropRecord)) {
            if (!cropKeys.includes(key)) throw new Error(`${context.path}.Crop.${key} 不受支持。`)
          }
          for (const key of ['X', 'Y'] as const) {
            const value = cropRecord[key]
            if (!Number.isInteger(value) || (value as number) < 0)
              throw new Error(`${context.path}.Crop.${key} 必须为非负整数源像素。`)
          }
          for (const key of ['Width', 'Height'] as const) {
            const value = cropRecord[key]
            if (!Number.isInteger(value) || (value as number) <= 0)
              throw new Error(`${context.path}.Crop.${key} 必须为正整数源像素。`)
          }
        }
      }
    }
    if (element.Type === 1 && own(rawElement, 'Font')) {
      const fontPath = `${context.path}.Font`
      const font = rawElement.Font
      if (!font || typeof font !== 'object' || Array.isArray(font)) throw new Error(`${fontPath} 必须为对象。`)
      const fontRecord = font as Record<string, unknown>
      for (const key of Object.keys(fontRecord)) {
        if (key !== 'Kind' && key !== 'Name') throw new Error(`${fontPath}.${key} 不受支持。`)
      }
      if (!Number.isInteger(fontRecord.Kind) || ![0, 1, 2].includes(fontRecord.Kind as number))
        throw new Error(`${fontPath}.Kind 必须为 0、1 或 2。`)
      const kind = fontRecord.Kind as PrintFontKind
      const hasName = own(fontRecord, 'Name')
      if (kind === 0 && hasName) throw new Error(`${fontPath}.Name 不能用于协议默认字体。`)
      if (kind !== 0 && (!hasName || typeof fontRecord.Name !== 'string' || !fontRecord.Name.trim()))
        throw new Error(`${fontPath}.Name 不能为空白。`)
      const hasLegacyFontName = own(rawElement, 'FontName') && rawElement.FontName !== 0
      const hasLegacyFontFamily = typeof rawElement.FontFamily === 'string' && rawElement.FontFamily.length > 0
      const hasLegacyDeviceFont = own(rawElement, 'DeviceFontName')
      if (hasLegacyFontName || hasLegacyFontFamily || hasLegacyDeviceFont)
        throw new Error(`${fontPath} 不能与旧字体字段同时指定。`)
    }
    const hasQuietZone = own(rawElement, 'QuietZone')
    if (hasQuietZone) {
      if (element.Type !== 3 && element.Type !== 4)
        throw new Error(`${context.path}.QuietZone 仅支持二维码和一维条码元素。`)
      const quietZone = rawElement.QuietZone
      if (quietZone === null) return
      if (!quietZone || typeof quietZone !== 'object' || Array.isArray(quietZone))
        throw new Error(`${context.path}.QuietZone 必须为对象或 null。`)
      const quietRecord = quietZone as Record<string, unknown>
      const quietKeys = ['Left', 'Top', 'Right', 'Bottom']
      for (const key of Object.keys(quietRecord)) {
        if (!quietKeys.includes(key)) throw new Error(`${context.path}.QuietZone.${key} 不受支持。`)
      }
      for (const key of quietKeys) {
        const side = quietRecord[key]
        if (side !== undefined && (!Number.isInteger(side) || (side as number) < 0 || (side as number) > MAX_QUIET_ZONE_DOTS))
          throw new Error(`${context.path}.QuietZone.${key} 必须为非负整数打印点。`)
      }
    }
    if (element.Type !== 6) return

    const group = element as GroupElement
    const hasElements = group.Elements !== undefined
    const hasRows = group.Rows !== undefined
    if (hasElements && !Array.isArray(group.Elements)) throw new Error(`${context.path}.Elements 必须为数组。`)
    if (hasRows && !Array.isArray(group.Rows)) throw new Error(`${context.path}.Rows 必须为数组。`)
    if (hasElements && hasRows) throw new Error(`${context.path} 不能同时指定 Elements 和 Rows。`)
    if (template.Version !== 3 && (hasRows || own(group, 'DataPath') || own(group, 'Parameters') || own(group, 'Pagination')))
      throw new Error('Version: 2 及更早版本不支持 V3 组合属性。')
    if (group.ComponentId !== undefined) {
      if (typeof group.ComponentId !== 'string' || !group.ComponentId.trim()) throw new Error(`${context.path}.ComponentId 无效。`)
      if (hasElements || hasRows) throw new Error(`${context.path} 组件引用不能同时包含内联内容。`)
    }
    if (group.DataPath !== undefined && (typeof group.DataPath !== 'string' || !group.DataPath.trim()))
      throw new Error(`${context.path}.DataPath 不能为空。`)
    if (group.Pagination !== undefined && group.Pagination !== 'KeepTogether' && group.Pagination !== 'Split')
      throw new Error(`${context.path}.Pagination 无效。`)
    if (group.Parameters !== undefined) {
      if (!group.Parameters || typeof group.Parameters !== 'object' || Array.isArray(group.Parameters))
        throw new Error(`${context.path}.Parameters 必须为对象。`)
      for (const [name, binding] of Object.entries(group.Parameters)) {
        if (!name.trim() || name.includes('.') || !binding || typeof binding !== 'object' || Array.isArray(binding))
          throw new Error(`${context.path}.Parameters.${name} 参数绑定无效。`)
        const hasPath = own(binding, 'Path')
        const hasValue = own(binding, 'Value')
        if (hasPath === hasValue || (hasPath && (typeof binding.Path !== 'string' || !binding.Path.trim())))
          throw new Error(`${context.path}.Parameters.${name} 必须且只能指定 Path 或 Value。`)
        if (hasValue) assertJsonValue(binding.Value, `${context.path}.Parameters.${name}.Value`)
      }
    }
  }
  )
}
