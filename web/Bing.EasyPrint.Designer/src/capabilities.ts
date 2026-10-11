import type { FontElement, TemplateElement } from './model'
import type {
  DesignerCapabilitiesRequest, EscPosLayoutMode, EscPosNativeFlowMode, PrintEncoding, PrintProtocol, EscPosDeviceRequest,
  EscPosCharacterRange, EscPosNativeTextProfile, EscPosTextCharacterMode, EscPosRasterFontCapability,
  EscPosRasterFontsCapability, PrintCutMode, PrintJobFeedUnit, TsplCutStrategy, TsplMediaRequest, ZplMediaRequest, ZplCutStrategy,
  EscPosBarcodeTextPositionName,
  RotationAngleName,
} from './api'
import { BARCODE_TYPE_NAMES, ESC_POS_BARCODE_TEXT_POSITION_NAMES, ROTATION_ANGLE_NAMES } from './api'

export type CapabilityRange = { minimum: number; maximum: number | null }

export type DeviceFontCapability = {
  name: string
  size: CapabilityRange
  sizes: number[] | null
}

export type LabelProfileCapability = {
  name: string
  firmwareVersion: string | null
  printableWidthDots: number | null
  maxLabelHeightDots: number | null
  maxImageDataBytes: number | null
  supportsNativeBarcodeHumanReadable?: boolean
  supportedNativeBarcodeHumanReadableTypes?: string[] | null
  supportedNativeQrCodeRotations?: RotationAngleName[] | null
  encodingCodePages: number[]
  characterRanges: EscPosCharacterRange[] | null
}

export type TsplCutterCapability = {
  strategy: TsplCutStrategy
  supportedCutModes: PrintCutMode[]
  supportsEndOfJob: true
  cutPositionFeedDots: number | null
}

export type DesignerCapabilities = {
  protocol: PrintProtocol
  escPosLayoutMode: EscPosLayoutMode | null
  nativeFlow: EscPosNativeFlowMode | null
  nativeText: EscPosNativeTextProfile | null
  rasterFonts: EscPosRasterFontsCapability | null
  rasterBarcodeHumanReadableFontSize?: number | null
  supportedNativeBarcodeHumanReadableTypes?: string[] | null
  supportedNativeBarcodeHumanReadablePositions?: EscPosBarcodeTextPositionName[] | null
  labelProfile?: LabelProfileCapability | null
  jobFeedUnits: PrintJobFeedUnit[]
  cutModes: PrintCutMode[]
  cutFeedUnits: PrintJobFeedUnit[]
  cutFeedMotionUnits: CapabilityRange | null
  cutFeedDots: CapabilityRange | null
  tsplCutter: TsplCutterCapability | null
  zplCutter?: ZplCutterCapability | null
  supportsIndependentOutputSegments?: boolean
  dpi: number
  elementTypes: number[]
  fontKinds: number[]
  deviceFonts: DeviceFontCapability[]
  textSizes: number[] | null
  textSize: CapabilityRange | null
  textRotations: number[]
  /** 图片旋转能力；旧 Host 缺少该字段时保守降级为 [0]。 */
  imageRotations: number[]
  barcodeTypes: number[]
  barcodeRotations: number[]
  /** 二维码旋转能力；旧 Host 缺少该字段时保守降级为 [0]。 */
  qrCodeRotations?: number[]
  qrModuleSize: CapabilityRange | null
  barcodeModuleWidth: CapabilityRange | null
  barcodeHeight: CapabilityRange | null
  supportsExplicitBarcodeModuleWidth: boolean
  supportsWideModuleWidth: boolean
  supportsBoldText: boolean
  supportsUnderlineText: boolean
  /** 反白文字能力；旧 Host 未返回时按不支持处理。 */
  supportsReverseText?: boolean
  supportsRotatedUnderlineText: boolean
  supportsHostFontFamily: boolean
  supportsLegacyDeviceFont: boolean
  supportsBarcodeHumanReadable: boolean
  supportsCut: boolean
  supportsExtraFeed: boolean
  supportsFilledRectangles: boolean
  supportsCoordinates: boolean
  supportsExplicitQuietZone: boolean
}

export type ZplCutterCapability = {
  strategy: ZplCutStrategy
  supportedCutModes: PrintCutMode[]
  supportsTearOff: true
  kioskPartialUncutMillimeters: number | null
  kioskCutMarginMillimeters: number | null
}

export type CapabilitiesState = 'idle' | 'loading' | 'ready' | 'error'
export type CapabilityFontSource = 'legacy' | 'default' | 'device' | 'host'

// Vue 的响应式代理会改变对象身份；私有标记随代理读取，且不会进入 JSON 或项目快照。
const knownCutCapabilities = Symbol('knownCutCapabilities')

export type CapabilityQuery = Pick<DesignerCapabilitiesRequest,
  'protocol' | 'escPosLayoutMode' | 'encoding' | 'escPosDevice' | 'tsplMedia' | 'zplMedia' | 'allowRasterizedLines' | 'requireCutContract'>

export const ELEMENT_TYPES = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10] as const

const protocolLabels: Record<string, string> = {
  Cpcl: 'CPCL', EscPos: 'ESC/POS', Tspl: 'TSPL', Zpl: 'ZPL',
}

function isFiniteNumber(value: unknown): value is number {
  return typeof value === 'number' && Number.isFinite(value)
}

function parseRange(value: unknown, path: string): CapabilityRange {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error(`能力响应的 ${path} 无效。`)
  const range = value as Record<string, unknown>
  if (!isFiniteNumber(range.minimum) || range.minimum < 0 ||
    (range.maximum !== null && !isFiniteNumber(range.maximum)) ||
    (range.maximum !== null && range.maximum < range.minimum)) {
    throw new Error(`能力响应的 ${path} 无效。`)
  }
  return { minimum: range.minimum, maximum: range.maximum as number | null }
}

function parseNumberArray(value: unknown, path: string): number[] {
  if (!Array.isArray(value) || value.some((item) => !isFiniteNumber(item))) throw new Error(`能力响应的 ${path} 无效。`)
  return [...new Set(value as number[])]
}

function parseRotationArray(value: unknown, path: string, fallback: number[] = [0]): number[] {
  const rotations = value === undefined ? fallback : parseNumberArray(value, path)
  if (rotations.some((item) => !Number.isInteger(item) || ![0, 90, 180, 270].includes(item))) {
    throw new Error(`能力响应的 ${path} 必须只包含 0、90、180 或 270。`)
  }
  if (!rotations.includes(0)) throw new Error(`能力响应的 ${path} 必须包含 0 度。`)
  return rotations
}

function parseNullableNumberArray(value: unknown, path: string): number[] | null {
  return value === null ? null : parseNumberArray(value, path)
}

function parseBarcodeTextPositions(value: unknown, path: string): EscPosBarcodeTextPositionName[] | null | undefined {
  if (value === undefined) return undefined
  if (value === null) return null
  if (!Array.isArray(value)) throw new Error(`能力响应的 ${path} 必须为数组或 null。`)
  const seen = new Set<string>()
  return value.map((item, index) => {
    if (typeof item !== 'string' || !(ESC_POS_BARCODE_TEXT_POSITION_NAMES as readonly string[]).includes(item)) {
      throw new Error(`能力响应的 ${path}[${index}] 包含无效的 HRI 位置。`)
    }
    if (seen.has(item)) throw new Error(`能力响应的 ${path}[${index}] 不能重复。`)
    seen.add(item)
    return item as EscPosBarcodeTextPositionName
  })
}

function parseBoolean(value: unknown, path: string): boolean {
  if (typeof value !== 'boolean') throw new Error(`能力响应的 ${path} 无效。`)
  return value
}

function parseProtocol(value: unknown): PrintProtocol {
  if (value === 'Cpcl' || value === 'EscPos' || value === 'Tspl' || value === 'Zpl') return value
  throw new Error('能力响应的 protocol 无效。')
}

function parseLayoutMode(value: unknown): EscPosLayoutMode | null {
  if (value === null) return null
  if (value === 'Native' || value === 'Raster') return value
  throw new Error('能力响应的 escPosLayoutMode 无效。')
}

function parseNativeFlow(value: unknown): EscPosNativeFlowMode | null {
  if (value === null) return null
  if (value === 'Rows' || value === 'Sequential') return value
  throw new Error('能力响应的 nativeFlow 无效。')
}

function parseNullablePositiveInteger(value: unknown, path: string): number | null {
  if (value === undefined || value === null) return null
  if (!isFiniteNumber(value) || !Number.isInteger(value) || value < 1 || value > 255) {
    throw new Error(`能力响应的 ${path} 必须为 1–255 整数。`)
  }
  return value
}

function parseCharacterRange(value: unknown, path: string): EscPosCharacterRange {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error(`能力响应的 ${path} 无效。`)
  const range = value as Record<string, unknown>
  for (const key of Object.keys(range)) if (key !== 'first' && key !== 'last') throw new Error(`能力响应的 ${path}.${key} 包含不支持的字段。`)
  if (!isFiniteNumber(range.first) || !Number.isInteger(range.first) ||
    !isFiniteNumber(range.last) || !Number.isInteger(range.last)) {
    throw new Error(`能力响应的 ${path} 无效。`)
  }
  const first = range.first
  const last = range.last
  if (first < 0 || last > 0x10FFFF || first > last || (first <= 0xDFFF && last >= 0xD800)) {
    throw new Error(`能力响应的 ${path} 无效。`)
  }
  return { first, last }
}

function parseNativeText(value: unknown): EscPosNativeTextProfile | null {
  // nativeText 是新增可选能力；旧 Host 响应缺字段时保持“未声明”语义。
  if (value === undefined || value === null) return null
  if (typeof value !== 'object' || Array.isArray(value)) throw new Error('能力响应的 nativeText 无效。')
  const input = value as Record<string, unknown>
  const allowed = new Set([
    'encodingCodePage', 'codePageIndex', 'characterMode', 'characterRanges',
    'halfCellWidthDots', 'fullCellWidthDots', 'lineHeightDots',
  ])
  for (const key of Object.keys(input)) if (!allowed.has(key)) throw new Error(`能力响应的 nativeText.${key} 包含不支持的字段。`)
  if (!isFiniteNumber(input.encodingCodePage) || !Number.isInteger(input.encodingCodePage) || input.encodingCodePage <= 0) {
    throw new Error('能力响应的 nativeText.encodingCodePage 无效。')
  }
  const codePageIndex = input.codePageIndex
  if (codePageIndex !== undefined &&
    (!isFiniteNumber(codePageIndex) || !Number.isInteger(codePageIndex) || codePageIndex < 0 || codePageIndex > 255)) {
    throw new Error('能力响应的 nativeText.codePageIndex 无效。')
  }
  if (input.characterMode !== undefined && input.characterMode !== 'DeviceDefault' &&
    input.characterMode !== 'SingleByte' && input.characterMode !== 'FsKanji') {
    throw new Error('能力响应的 nativeText.characterMode 无效。')
  }
  if (!Array.isArray(input.characterRanges) || input.characterRanges.length === 0) {
    throw new Error('能力响应的 nativeText.characterRanges 无效。')
  }
  const characterRanges = input.characterRanges.map((item, index) => parseCharacterRange(item, `nativeText.characterRanges[${index}]`))
  if (input.characterMode === 'FsKanji' && ![936, 54936, 950, 949].includes(input.encodingCodePage as number)) {
    throw new Error('能力响应的 nativeText FsKanji 仅支持代码页 936、54936、950 或 949。')
  }
  for (const [field, valueToCheck] of [['halfCellWidthDots', input.halfCellWidthDots], ['fullCellWidthDots', input.fullCellWidthDots]] as const) {
    if (valueToCheck !== undefined && (!isFiniteNumber(valueToCheck) || !Number.isInteger(valueToCheck) || valueToCheck <= 0)) {
      throw new Error(`能力响应的 nativeText.${field} 无效。`)
    }
  }
  if (input.lineHeightDots !== undefined && input.lineHeightDots !== 24 && input.lineHeightDots !== 26) {
    throw new Error('能力响应的 nativeText.lineHeightDots 无效。')
  }
  const characterMode = input.characterMode as EscPosTextCharacterMode | undefined
  return {
    encodingCodePage: input.encodingCodePage,
    ...(codePageIndex === undefined ? {} : { codePageIndex }),
    ...(characterMode === undefined ? {} : { characterMode }),
    characterRanges,
    ...(input.halfCellWidthDots === undefined ? {} : { halfCellWidthDots: input.halfCellWidthDots as number }),
    ...(input.fullCellWidthDots === undefined ? {} : { fullCellWidthDots: input.fullCellWidthDots as number }),
    ...(input.lineHeightDots === undefined ? {} : { lineHeightDots: input.lineHeightDots as 24 | 26 }),
  }
}

function parseRasterFonts(value: unknown): EscPosRasterFontsCapability | null {
  if (value === undefined || value === null) return null
  if (typeof value !== 'object' || Array.isArray(value)) throw new Error('能力响应的 rasterFonts 无效。')
  const input = value as Record<string, unknown>
  const allowed = new Set(['defaultFont', 'fallbackFonts', 'fonts'])
  for (const key of Object.keys(input)) if (!allowed.has(key)) throw new Error(`能力响应的 rasterFonts.${key} 包含不支持的字段。`)
  if (typeof input.defaultFont !== 'string' || !input.defaultFont.trim()) {
    throw new Error('能力响应的 rasterFonts.defaultFont 无效。')
  }
  if (!Array.isArray(input.fallbackFonts) || input.fallbackFonts.some((item) => typeof item !== 'string' || !item.trim())) {
    throw new Error('能力响应的 rasterFonts.fallbackFonts 无效。')
  }
  if (!Array.isArray(input.fonts) || input.fonts.length === 0 || input.fonts.length > 64) throw new Error('能力响应的 rasterFonts.fonts 无效。')
  const fonts: EscPosRasterFontCapability[] = []
  const names = new Set<string>()
  for (const [index, item] of input.fonts.entries()) {
    if (!item || typeof item !== 'object' || Array.isArray(item)) throw new Error(`能力响应的 rasterFonts.fonts[${index}] 无效。`)
    const font = item as Record<string, unknown>
    for (const key of Object.keys(font)) if (!['name', 'version', 'hasBold'].includes(key)) {
      throw new Error(`能力响应的 rasterFonts.fonts[${index}].${key} 包含不支持的字段。`)
    }
    if (typeof font.name !== 'string' || !font.name.trim() || names.has(font.name)) {
      throw new Error(`能力响应的 rasterFonts.fonts[${index}].name 无效或重复。`)
    }
    if (typeof font.version !== 'string' || !font.version.trim()) {
      throw new Error(`能力响应的 rasterFonts.fonts[${index}].version 无效。`)
    }
    if (typeof font.hasBold !== 'boolean') throw new Error(`能力响应的 rasterFonts.fonts[${index}].hasBold 无效。`)
    names.add(font.name)
    fonts.push({ name: font.name, version: font.version, hasBold: font.hasBold })
  }
  if (!names.has(input.defaultFont)) throw new Error('能力响应的 rasterFonts.defaultFont 未引用已声明资源。')
  const fallbackFonts = [...(input.fallbackFonts as string[])]
  if (new Set(fallbackFonts).size !== fallbackFonts.length || fallbackFonts.some((name) => !names.has(name))) {
    throw new Error('能力响应的 rasterFonts.fallbackFonts 必须引用已声明且不重复的资源。')
  }
  return { defaultFont: input.defaultFont, fallbackFonts, fonts }
}

function parseJobFeedUnits(value: unknown): PrintJobFeedUnit[] {
  if (!Array.isArray(value) || value.some((item) => item !== 'Lines' && item !== 'Dots' && item !== 'MotionUnits')) {
    throw new Error('能力响应的 jobFeedUnits 无效。')
  }
  return [...new Set(value as PrintJobFeedUnit[])]
}

function parseCutModes(value: unknown, protocol: PrintProtocol): PrintCutMode[] {
  if (value === undefined) return []
  if (!Array.isArray(value)) throw new Error('能力响应的 cutModes 无效。')
  const seen = new Set<PrintCutMode>()
  const modes = value.map((item, index) => {
    if (item !== 'Full' && item !== 'Partial') throw new Error(`能力响应的 cutModes[${index}] 无效。`)
    if (seen.has(item)) throw new Error(`能力响应的 cutModes[${index}] 不能重复。`)
    seen.add(item)
    return item
  })
  if (protocol !== 'EscPos' && protocol !== 'Tspl' && protocol !== 'Zpl' && modes.length > 0) throw new Error('能力响应的 cutModes 仅能用于 ESC/POS、TSPL 或 ZPL。')
  return modes
}

function parseCutFeedUnits(value: unknown, protocol: PrintProtocol): PrintJobFeedUnit[] {
  if (value === undefined) return []
  if (!Array.isArray(value)) throw new Error('能力响应的 cutFeedUnits 无效。')
  const seen = new Set<PrintJobFeedUnit>()
  const units = value.map((item, index) => {
    if (item !== 'Lines' && item !== 'Dots' && item !== 'MotionUnits') {
      throw new Error(`能力响应的 cutFeedUnits[${index}] 无效。`)
    }
    if (seen.has(item)) throw new Error(`能力响应的 cutFeedUnits[${index}] 不能重复。`)
    seen.add(item)
    return item
  })
  if (units.includes('Lines')) throw new Error('能力响应的 cutFeedUnits 不支持 Lines。')
  if (protocol === 'Tspl' && units.some((unit) => unit !== 'Dots')) {
    throw new Error('能力响应的 TSPL cutFeedUnits 仅支持 Dots。')
  }
  if (protocol !== 'EscPos' && protocol !== 'Tspl' && units.length > 0) throw new Error('能力响应的 cutFeedUnits 仅能用于 ESC/POS 或 TSPL。')
  return units
}

function parseCutFeedMotionUnits(value: unknown, protocol: PrintProtocol, cutFeedUnits: PrintJobFeedUnit[]): CapabilityRange | null {
  if (value === undefined || value === null) return null
  const range = parseRange(value, 'cutFeedMotionUnits')
  if (range.minimum !== 0 || range.maximum !== 255) throw new Error('能力响应的 cutFeedMotionUnits 必须为 0–255。')
  if (protocol !== 'EscPos' || !cutFeedUnits.includes('MotionUnits')) {
    throw new Error('能力响应的 cutFeedMotionUnits 与切纸进纸单位不一致。')
  }
  return range
}

function parseCutFeedDots(value: unknown, protocol: PrintProtocol): CapabilityRange | null {
  if (value === undefined || value === null) return null
  if (protocol !== 'Tspl') throw new Error('能力响应的 cutFeedDots 仅能用于 TSPL。')
  const range = parseRange(value, 'cutFeedDots')
  if (range.minimum !== 0 || range.maximum === null || range.maximum > 9999) {
    throw new Error('能力响应的 cutFeedDots 必须为 0–9999 范围。')
  }
  return range
}

function parseZplCutter(value: unknown, protocol: PrintProtocol): ZplCutterCapability | null {
  if (value === undefined || value === null) return null
  if (protocol !== 'Zpl' || typeof value !== 'object' || Array.isArray(value)) throw new Error('能力响应的 zplCutter 仅能用于 ZPL。')
  const input = value as Record<string, unknown>
  const keys = ['strategy', 'supportedCutModes', 'supportsTearOff', 'kioskPartialUncutMillimeters', 'kioskCutMarginMillimeters']
  for (const key of Object.keys(input)) if (!keys.includes(key)) throw new Error(`能力响应的 zplCutter.${key} 无效。`)
  if (input.strategy !== 'Cutter' && input.strategy !== 'Delayed' && input.strategy !== 'Kiosk') throw new Error('能力响应的 zplCutter.strategy 无效。')
  if (!Array.isArray(input.supportedCutModes) || input.supportedCutModes.length === 0) throw new Error('能力响应的 zplCutter.supportedCutModes 必须非空。')
  const modes = parseCutModes(input.supportedCutModes, protocol)
  if (input.supportsTearOff !== true) throw new Error('能力响应的 zplCutter.supportsTearOff 必须为 true。')
  function millimeters(key: string, min: number, max: number): number | null {
    const amount = input[key]
    if (amount === null) return null
    if (!isFiniteNumber(amount) || !Number.isInteger(amount) || amount < min || amount > max) throw new Error(`能力响应的 zplCutter.${key} 无效。`)
    return amount
  }
  const partial = millimeters('kioskPartialUncutMillimeters', 10, 60)
  const margin = millimeters('kioskCutMarginMillimeters', 2, 9)
  if (input.strategy !== 'Kiosk' && (modes.includes('Partial') || partial !== null || margin !== null)) throw new Error('能力响应的 Cutter/Delayed 不能使用 Kiosk 参数或半切。')
  if (input.strategy === 'Kiosk' && (margin === null || modes.includes('Partial') !== (partial !== null))) throw new Error('能力响应的 Kiosk 尺寸与切法不一致。')
  return { strategy: input.strategy, supportedCutModes: modes, supportsTearOff: true,
    kioskPartialUncutMillimeters: partial, kioskCutMarginMillimeters: margin }
}

function parseTsplCutter(value: unknown, protocol: PrintProtocol): TsplCutterCapability | null {
  if (value === undefined || value === null) return null
  if (protocol !== 'Tspl' || typeof value !== 'object' || Array.isArray(value)) {
    throw new Error('能力响应的 tsplCutter 仅能用于 TSPL。')
  }
  const input = value as Record<string, unknown>
  const allowed = ['strategy', 'supportedCutModes', 'supportsEndOfJob', 'cutPositionFeedDots']
  for (const key of Object.keys(input)) if (!allowed.includes(key)) throw new Error(`能力响应的 tsplCutter.${key} 无效。`)
  if (input.strategy !== 'Immediate' && input.strategy !== 'Batch') throw new Error('能力响应的 tsplCutter.strategy 无效。')
  if (!Array.isArray(input.supportedCutModes) || input.supportedCutModes.length === 0) {
    throw new Error('能力响应的 tsplCutter.supportedCutModes 必须为非空数组。')
  }
  const modes = input.supportedCutModes.map((item, index) => {
    if (item !== 'Full' && item !== 'Partial') throw new Error(`能力响应的 tsplCutter.supportedCutModes[${index}] 无效。`)
    return item
  }) as PrintCutMode[]
  if (new Set(modes).size !== modes.length) throw new Error('能力响应的 tsplCutter.supportedCutModes 不能重复。')
  if (input.strategy === 'Immediate' && modes.length !== 1) {
    throw new Error('能力响应的 Immediate TSPL cutter 只能声明一种固定切法。')
  }
  if (input.supportsEndOfJob !== true) throw new Error('能力响应的 tsplCutter.supportsEndOfJob 必须为 true。')
  const feedDots = input.cutPositionFeedDots
  if (feedDots !== null && (!isFiniteNumber(feedDots) || !Number.isInteger(feedDots) || feedDots < 0 || feedDots > 9999)) {
    throw new Error('能力响应的 tsplCutter.cutPositionFeedDots 必须为 null 或 0–9999 整数。')
  }
  if (input.strategy === 'Batch' && feedDots !== null) {
    throw new Error('能力响应的 Batch TSPL cutter 不能声明切刀位置补偿。')
  }
  return {
    strategy: input.strategy as TsplCutStrategy,
    supportedCutModes: [...modes],
    supportsEndOfJob: true,
    cutPositionFeedDots: feedDots as number | null,
  }
}

function parseBarcodeTypeNames(value: unknown, path: string): string[] | null | undefined {
  if (value === undefined) return undefined
  if (value === null) return null
  if (!Array.isArray(value)) throw new Error(`能力响应的 ${path} 必须为数组或 null。`)
  const seen = new Set<string>()
  return value.map((item, index) => {
    if (typeof item !== 'string' || !(BARCODE_TYPE_NAMES as readonly string[]).includes(item)) {
      throw new Error(`能力响应的 ${path}[${index}] 无效。`)
    }
    if (seen.has(item)) throw new Error(`能力响应的 ${path} 不能包含重复制式。`)
    seen.add(item)
    return item
  })
}

function parseLabelProfile(value: unknown, protocol: PrintProtocol): LabelProfileCapability | null {
  if (value === undefined || value === null) return null
  if ((protocol !== 'Tspl' && protocol !== 'Zpl') || typeof value !== 'object' || Array.isArray(value)) {
    throw new Error('能力响应的 labelProfile 仅能用于 TSPL/ZPL。')
  }
  const input = value as Record<string, unknown>
  const allowed = ['name', 'firmwareVersion', 'printableWidthDots', 'maxLabelHeightDots', 'maxImageDataBytes', 'supportsNativeBarcodeHumanReadable', 'supportedNativeBarcodeHumanReadableTypes', 'supportedNativeQrCodeRotations', 'encodingCodePages', 'characterRanges']
  for (const key of Object.keys(input)) if (!allowed.includes(key)) throw new Error(`能力响应的 labelProfile.${key} 无效。`)
  if (typeof input.name !== 'string' || !input.name.trim() ||
    (input.firmwareVersion !== null && typeof input.firmwareVersion !== 'string')) {
    throw new Error('能力响应的 labelProfile 名称或固件记录无效。')
  }
  for (const field of ['printableWidthDots', 'maxLabelHeightDots', 'maxImageDataBytes']) {
    const limit = input[field]
    if (limit !== null && (!isFiniteNumber(limit) || !Number.isInteger(limit) || limit <= 0 || limit > 2147483647)) {
      throw new Error(`能力响应的 labelProfile.${field} 无效。`)
    }
  }
  if (input.supportsNativeBarcodeHumanReadable !== undefined && typeof input.supportsNativeBarcodeHumanReadable !== 'boolean') {
    throw new Error('能力响应的 labelProfile.supportsNativeBarcodeHumanReadable 无效。')
  }
  const supportedNativeBarcodeHumanReadableTypes = parseBarcodeTypeNames(
    input.supportedNativeBarcodeHumanReadableTypes, 'labelProfile.supportedNativeBarcodeHumanReadableTypes')
  let supportedNativeQrCodeRotations: RotationAngleName[] | null | undefined
  if (input.supportedNativeQrCodeRotations !== undefined) {
    if (input.supportedNativeQrCodeRotations === null) {
      supportedNativeQrCodeRotations = null
    } else if (!Array.isArray(input.supportedNativeQrCodeRotations)) {
      throw new Error('能力响应的 labelProfile.supportedNativeQrCodeRotations 无效。')
    } else {
      const seen = new Set<string>()
      supportedNativeQrCodeRotations = input.supportedNativeQrCodeRotations.map((item, index) => {
        if (typeof item !== 'string' || !(ROTATION_ANGLE_NAMES as readonly string[]).includes(item)) {
          throw new Error(`能力响应的 labelProfile.supportedNativeQrCodeRotations[${index}] 无效。`)
        }
        if (seen.has(item)) throw new Error('能力响应的 labelProfile.supportedNativeQrCodeRotations 不能重复。')
        seen.add(item)
        return item as RotationAngleName
      })
    }
  }
  const allowedEncodings = protocol === 'Tspl' ? [20127, 65001, 936] : [20127, 65001]
  const encodings = input.encodingCodePages
  if (!Array.isArray(encodings) || !encodings.length || new Set(encodings).size !== encodings.length ||
    encodings.some((code) => !allowedEncodings.includes(code))) throw new Error('能力响应的 labelProfile.encodingCodePages 无效。')
  let ranges: EscPosCharacterRange[] | null = null
  if (input.characterRanges !== null) {
    if (!Array.isArray(input.characterRanges) || !input.characterRanges.length) throw new Error('能力响应的 labelProfile.characterRanges 无效。')
    ranges = input.characterRanges.map((range, index) => parseCharacterRange(range, `labelProfile.characterRanges[${index}]`))
    const sorted = [...ranges].sort((a, b) => a.first - b.first)
    if (sorted.some((range, index) => index > 0 && range.first <= sorted[index - 1].last)) {
      throw new Error('能力响应的 labelProfile.characterRanges 区间重叠。')
    }
  }
  return {
    name: input.name, firmwareVersion: input.firmwareVersion as string | null,
    printableWidthDots: input.printableWidthDots as number | null,
    maxLabelHeightDots: input.maxLabelHeightDots as number | null,
    maxImageDataBytes: input.maxImageDataBytes as number | null,
    ...(input.supportsNativeBarcodeHumanReadable === undefined ? {} : {
      supportsNativeBarcodeHumanReadable: input.supportsNativeBarcodeHumanReadable as boolean,
    }),
    ...(supportedNativeBarcodeHumanReadableTypes === undefined ? {} : { supportedNativeBarcodeHumanReadableTypes }),
    ...(supportedNativeQrCodeRotations === undefined ? {} : { supportedNativeQrCodeRotations }),
    encodingCodePages: [...encodings], characterRanges: ranges,
  }
}

function parseDeviceFonts(value: unknown): DeviceFontCapability[] {
  if (!Array.isArray(value)) throw new Error('能力响应的 deviceFonts 无效。')
  return value.map((item, index) => {
    if (!item || typeof item !== 'object' || Array.isArray(item)) throw new Error(`能力响应的 deviceFonts[${index}] 无效。`)
    const entry = item as Record<string, unknown>
    if (typeof entry.name !== 'string' || !entry.name.trim()) throw new Error(`能力响应的 deviceFonts[${index}].name 无效。`)
    const sizes = entry.sizes === null ? null : parseNumberArray(entry.sizes, `deviceFonts[${index}].sizes`)
    return { name: entry.name, size: parseRange(entry.size, `deviceFonts[${index}].size`), sizes }
  })
}

/** 将 Host 返回的能力对象严格转换为 Designer 可消费的能力快照。 */
export function normalizeDesignerCapabilities(value: unknown): DesignerCapabilities {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error('能力服务返回的数据格式无效。')
  const input = value as Record<string, unknown>
  if (!isFiniteNumber(input.dpi) || input.dpi <= 0) throw new Error('能力响应的 dpi 无效。')
  const protocol = parseProtocol(input.protocol)
  const escPosLayoutMode = parseLayoutMode(input.escPosLayoutMode)
  const nativeFlow = parseNativeFlow(input.nativeFlow)
  const nativeText = parseNativeText(input.nativeText)
  const rasterFonts = parseRasterFonts(input.rasterFonts)
  const rasterBarcodeHumanReadableFontSize = parseNullablePositiveInteger(
    input.rasterBarcodeHumanReadableFontSize, 'rasterBarcodeHumanReadableFontSize')
  const supportedNativeBarcodeHumanReadableTypes = parseBarcodeTypeNames(
    input.supportedNativeBarcodeHumanReadableTypes, 'supportedNativeBarcodeHumanReadableTypes')
  const supportedNativeBarcodeHumanReadablePositions = parseBarcodeTextPositions(
    input.supportedNativeBarcodeHumanReadablePositions, 'supportedNativeBarcodeHumanReadablePositions')
  if (supportedNativeBarcodeHumanReadablePositions !== undefined &&
    (protocol !== 'EscPos' || escPosLayoutMode !== 'Native')) {
    throw new Error('能力响应的 supportedNativeBarcodeHumanReadablePositions 仅能用于 ESC/POS Native 模式。')
  }
  const fontKinds = parseNumberArray(input.fontKinds, 'fontKinds')
  if (input.textSize === null && fontKinds.includes(0)) throw new Error('能力响应的 textSize 与默认字体能力不一致。')
  if (protocol === 'EscPos' && escPosLayoutMode === 'Native' && nativeFlow === null) {
    throw new Error('能力响应的 nativeFlow 在 ESC/POS Native 模式下不能为 null。')
  }
  if ((protocol !== 'EscPos' || escPosLayoutMode !== 'Native') && nativeFlow !== null) {
    throw new Error('能力响应的 nativeFlow 仅能用于 ESC/POS Native 模式。')
  }
  if ((protocol !== 'EscPos' || escPosLayoutMode !== 'Native') && nativeText !== null) {
    throw new Error('能力响应的 nativeText 仅能用于 ESC/POS Native 模式。')
  }
  if ((protocol !== 'EscPos' || escPosLayoutMode !== 'Raster') && rasterFonts !== null) {
    throw new Error('能力响应的 rasterFonts 仅能用于 ESC/POS Raster 模式。')
  }
  if ((protocol !== 'EscPos' || escPosLayoutMode !== 'Raster') && rasterBarcodeHumanReadableFontSize !== null) {
    throw new Error('能力响应的 rasterBarcodeHumanReadableFontSize 仅能用于 ESC/POS Raster 模式。')
  }
  const supportsCut = parseBoolean(input.supportsCut, 'supportsCut')
  const cutModes = parseCutModes(input.cutModes, protocol)
  const cutFeedUnits = parseCutFeedUnits(input.cutFeedUnits, protocol)
  const cutFeedMotionUnits = parseCutFeedMotionUnits(input.cutFeedMotionUnits, protocol, cutFeedUnits)
  const cutFeedDots = parseCutFeedDots(input.cutFeedDots, protocol)
  const tsplCutter = parseTsplCutter(input.tsplCutter, protocol)
  const zplCutter = parseZplCutter(input.zplCutter, protocol)
  const supportsIndependentOutputSegments = input.supportsIndependentOutputSegments === undefined ? false
    : parseBoolean(input.supportsIndependentOutputSegments, 'supportsIndependentOutputSegments')
  if (supportsIndependentOutputSegments !== (zplCutter?.strategy === 'Delayed')) throw new Error('能力响应的独立输出能力与 ZPL 切刀策略不一致。')
  if (zplCutter !== null && (!supportsCut || cutModes.length !== zplCutter.supportedCutModes.length ||
    cutModes.some((mode, index) => mode !== zplCutter.supportedCutModes[index]))) throw new Error('能力响应的 ZPL 切刀与切法能力不一致。')
  if (protocol === 'Zpl' && zplCutter === null && cutModes.length > 0) throw new Error('能力响应的 ZPL 显式切法须声明 zplCutter。')
  if (!supportsCut && cutModes.length > 0) throw new Error('能力响应的 cutModes 与 supportsCut 不一致。')
  if (protocol === 'Tspl' && tsplCutter !== null) {
    if (!supportsCut) throw new Error('能力响应的 tsplCutter 与 supportsCut 不一致。')
    if (cutModes.length !== tsplCutter.supportedCutModes.length ||
      cutModes.some((mode, index) => mode !== tsplCutter.supportedCutModes[index])) {
      throw new Error('能力响应的 cutModes 与 tsplCutter.supportedCutModes 不一致。')
    }
    if (tsplCutter.cutPositionFeedDots === null) {
      if (cutFeedDots !== null || cutFeedUnits.length > 0) {
        throw new Error('未声明 TSPL 切刀位置补偿时，cutFeedDots 与 cutFeedUnits 必须为空。')
      }
    } else {
      if (!cutFeedUnits.includes('Dots') || cutFeedDots === null) {
        throw new Error('声明 TSPL 切刀位置补偿时必须提供 Dots 切刀补偿范围。')
      }
      const base = tsplCutter.cutPositionFeedDots
      if (cutFeedDots.maximum !== 9999 - base) {
        throw new Error('能力响应的 cutFeedDots 最大值必须为 9999 减去切刀位置基准。')
      }
    }
  }
  const imageRotations = parseRotationArray(input.imageRotations, 'imageRotations')
  if (protocol === 'EscPos' && escPosLayoutMode === 'Native' && imageRotations.some((rotation) => rotation !== 0)) {
    throw new Error('能力响应的 ESC/POS Native 图片旋转只能包含 0 度。')
  }
  const qrCodeRotations = parseRotationArray(input.qrCodeRotations, 'qrCodeRotations')
  const result: DesignerCapabilities = {
    protocol,
    escPosLayoutMode,
    nativeFlow,
    nativeText,
    rasterFonts,
    rasterBarcodeHumanReadableFontSize,
    ...(supportedNativeBarcodeHumanReadableTypes === undefined ? {} : { supportedNativeBarcodeHumanReadableTypes }),
    ...(supportedNativeBarcodeHumanReadablePositions === undefined ? {} : { supportedNativeBarcodeHumanReadablePositions }),
    labelProfile: parseLabelProfile(input.labelProfile, protocol),
    jobFeedUnits: parseJobFeedUnits(input.jobFeedUnits),
    cutModes,
    cutFeedUnits,
    cutFeedMotionUnits,
    cutFeedDots,
    tsplCutter,
    zplCutter,
    supportsIndependentOutputSegments,
    dpi: input.dpi,
    elementTypes: parseNumberArray(input.elementTypes, 'elementTypes'),
    fontKinds,
    deviceFonts: parseDeviceFonts(input.deviceFonts),
    textSizes: parseNullableNumberArray(input.textSizes, 'textSizes'),
    textSize: input.textSize === null ? null : parseRange(input.textSize, 'textSize'),
    textRotations: parseNumberArray(input.textRotations, 'textRotations'),
    imageRotations,
    barcodeTypes: parseNumberArray(input.barcodeTypes, 'barcodeTypes'),
    barcodeRotations: parseNumberArray(input.barcodeRotations, 'barcodeRotations'),
    qrCodeRotations,
    qrModuleSize: input.qrModuleSize === null ? null : parseRange(input.qrModuleSize, 'qrModuleSize'),
    barcodeModuleWidth: input.barcodeModuleWidth === null ? null : parseRange(input.barcodeModuleWidth, 'barcodeModuleWidth'),
    barcodeHeight: input.barcodeHeight === null ? null : parseRange(input.barcodeHeight, 'barcodeHeight'),
    supportsExplicitBarcodeModuleWidth: parseBoolean(input.supportsExplicitBarcodeModuleWidth, 'supportsExplicitBarcodeModuleWidth'),
    supportsWideModuleWidth: parseBoolean(input.supportsWideModuleWidth, 'supportsWideModuleWidth'),
    supportsBoldText: parseBoolean(input.supportsBoldText, 'supportsBoldText'),
    supportsUnderlineText: parseBoolean(input.supportsUnderlineText, 'supportsUnderlineText'),
    supportsReverseText: input.supportsReverseText === undefined ? false : parseBoolean(input.supportsReverseText, 'supportsReverseText'),
    supportsRotatedUnderlineText: parseBoolean(input.supportsRotatedUnderlineText, 'supportsRotatedUnderlineText'),
    supportsHostFontFamily: parseBoolean(input.supportsHostFontFamily, 'supportsHostFontFamily'),
    supportsLegacyDeviceFont: parseBoolean(input.supportsLegacyDeviceFont, 'supportsLegacyDeviceFont'),
    supportsBarcodeHumanReadable: parseBoolean(input.supportsBarcodeHumanReadable, 'supportsBarcodeHumanReadable'),
    supportsCut,
    supportsExtraFeed: parseBoolean(input.supportsExtraFeed, 'supportsExtraFeed'),
    supportsFilledRectangles: parseBoolean(input.supportsFilledRectangles, 'supportsFilledRectangles'),
    supportsCoordinates: parseBoolean(input.supportsCoordinates, 'supportsCoordinates'),
    supportsExplicitQuietZone: parseBoolean(input.supportsExplicitQuietZone, 'supportsExplicitQuietZone'),
  }
  const hasEscPosCutFields = Object.prototype.hasOwnProperty.call(input, 'cutModes') &&
    Object.prototype.hasOwnProperty.call(input, 'cutFeedUnits') &&
    Object.prototype.hasOwnProperty.call(input, 'cutFeedMotionUnits')
  const hasTsplCutFields = protocol === 'Tspl' &&
    Object.prototype.hasOwnProperty.call(input, 'cutModes') &&
    Object.prototype.hasOwnProperty.call(input, 'cutFeedUnits') &&
    Object.prototype.hasOwnProperty.call(input, 'tsplCutter') &&
    Object.prototype.hasOwnProperty.call(input, 'cutFeedDots')
  const hasZplCutFields = protocol === 'Zpl' && Object.prototype.hasOwnProperty.call(input, 'zplCutter') &&
    Object.prototype.hasOwnProperty.call(input, 'cutModes') && Object.prototype.hasOwnProperty.call(input, 'supportsIndependentOutputSegments')
  if ((protocol === 'EscPos' && hasEscPosCutFields) || (protocol === 'Tspl' && hasTsplCutFields) || hasZplCutFields) {
    Object.defineProperty(result, knownCutCapabilities, { value: true })
  }
  return result
}

/** 旧 Host 未返回裁切能力字段时为 false，调用方应要求更新 Host 后再提交自定义裁切。 */
export function cutCapabilitiesKnown(caps: DesignerCapabilities | undefined): boolean {
  return caps !== undefined &&
    (caps as DesignerCapabilities & { [knownCutCapabilities]?: boolean })[knownCutCapabilities] === true
}

export function protocolLabel(value: string): string {
  return protocolLabels[value] ?? value
}

export function capabilityFeatureReason(caps: DesignerCapabilities | undefined, feature: string): string {
  if (!caps) return '当前协议能力尚未查询，暂不能确认支持情况。'
  const layout = caps.escPosLayoutMode ? ` ${caps.escPosLayoutMode}` : ''
  return `${protocolLabel(caps.protocol)}${layout} 不支持${feature}。`
}

export function elementTypeSupported(caps: DesignerCapabilities | undefined, type: TemplateElement['Type']): boolean {
  if (type === 6) return true
  // ESC/POS Native 只能按流式原生命令输出，图片必须显式切换到 Raster。
  if (type === 2 && caps?.protocol === 'EscPos' && caps.escPosLayoutMode === 'Native') return false
  return Boolean(caps?.elementTypes.includes(type))
}

/** 矩形轮廓与填充是两个独立能力；旧 Host 仍可创建轮廓矩形。 */
export function filledRectangleSupported(caps: DesignerCapabilities | undefined): boolean {
  return caps?.supportsFilledRectangles === true
}

export function canCreateElement(caps: DesignerCapabilities | undefined, state: CapabilitiesState, type: TemplateElement['Type']): boolean {
  return state === 'ready' && elementTypeSupported(caps, type)
}

export function elementTypeReason(caps: DesignerCapabilities | undefined, type: TemplateElement['Type']): string {
  if (!caps) return '能力查询完成前不能新增元素。'
  if (type === 6) return ''
  return elementTypeSupported(caps, type) ? '' : capabilityFeatureReason(caps, `${type} 类元素`)
}

export function valueInRange(value: number | undefined, range: CapabilityRange | null | undefined): boolean {
  if (value === undefined || !range || !Number.isInteger(value)) return false
  return value >= range.minimum && (range.maximum === null || value <= range.maximum)
}

export function rotationSupported(caps: DesignerCapabilities | undefined, type: 1 | 2 | 3 | 4, value: number | undefined): boolean {
  if (!caps || value === undefined) return false
  return (type === 1 ? caps.textRotations : type === 2 ? caps.imageRotations : type === 3 ? caps.qrCodeRotations ?? [0] : caps.barcodeRotations).includes(value)
}

export function barcodeTypeSupported(caps: DesignerCapabilities | undefined, value: number | undefined): boolean {
  return Boolean(caps && value !== undefined && caps.barcodeTypes.includes(value))
}

export function fontSourceSupported(caps: DesignerCapabilities | undefined, source: CapabilityFontSource): boolean {
  if (source === 'legacy') return true
  const kind = source === 'default' ? 0 : source === 'device' ? 1 : 2
  return Boolean(caps?.fontKinds.includes(kind))
}

export function deviceFontCapability(caps: DesignerCapabilities | undefined, name: string | undefined): DeviceFontCapability | undefined {
  return name ? caps?.deviceFonts.find((font) => font.name === name) : undefined
}

export function fontSizeRule(caps: DesignerCapabilities | undefined, element: FontElement): { range: CapabilityRange | null; sizes: number[] | null } {
  if (!caps) return { range: null, sizes: null }
  const legacyDeviceName = element.DeviceFontName ??
    (caps.protocol === 'Cpcl' && element.FontName !== undefined && element.FontName !== 0 ? String(element.FontName) : undefined)
  if (element.Font?.Kind === 1) {
    const device = deviceFontCapability(caps, element.Font.Name)
    return device ? { range: device.size, sizes: device.sizes } : { range: null, sizes: [] }
  }
  const device = deviceFontCapability(caps, legacyDeviceName)
  if (legacyDeviceName && !device) return { range: null, sizes: [] }
  return { range: device?.size ?? caps.textSize, sizes: device?.sizes ?? caps.textSizes }
}

export function textSizeSupported(caps: DesignerCapabilities | undefined, element: FontElement, value: number | undefined): boolean {
  if (value === undefined || !caps) return false
  const rule = fontSizeRule(caps, element)
  if (rule.sizes) return rule.sizes.includes(value)
  return valueInRange(value, rule.range)
}

export function legacyDeviceFontSupported(caps: DesignerCapabilities | undefined, name: number | undefined, fontSize?: number): boolean {
  if (name === undefined || name === 0) return true
  if (caps?.protocol !== 'Cpcl') return false
  const font = caps.deviceFonts.find((item) => item.name === String(name))
  const effectiveSize = fontSize === undefined || fontSize === 0 ? 24 : fontSize
  return Boolean(font && (font.sizes === null || font.sizes.includes(effectiveSize)))
}

export function hostFontFamilySupported(caps: DesignerCapabilities | undefined): boolean {
  return Boolean(caps?.supportsHostFontFamily)
}

export function barcodeModuleWidthSupported(caps: DesignerCapabilities | undefined, value: number | undefined): boolean {
  return Boolean(caps?.supportsExplicitBarcodeModuleWidth && valueInRange(value, caps.barcodeModuleWidth))
}

export function wideModuleWidthSupported(caps: DesignerCapabilities | undefined, value: number | undefined): boolean {
  return Boolean(caps?.supportsWideModuleWidth && valueInRange(value, caps.barcodeModuleWidth))
}

export function qrModuleSizeSupported(caps: DesignerCapabilities | undefined, value: number | undefined): boolean {
  return Boolean(caps?.qrModuleSize && valueInRange(value, caps.qrModuleSize))
}

export function currentFontSource(element: FontElement): CapabilityFontSource {
  if (!element.Font) return 'legacy'
  if (element.Font.Kind === 0) return 'default'
  if (element.Font.Kind === 1) return 'device'
  return 'host'
}

export function currentFontSizeRule(caps: DesignerCapabilities | undefined, element: TemplateElement): { range: CapabilityRange | null; sizes: number[] | null } {
  return element.Type === 1 ? fontSizeRule(caps, element) : { range: null, sizes: null }
}

export type CapabilityRequestRunner<TRequest, TResponse> = {
  run: (request: TRequest) => Promise<{ status: 'ready'; value: TResponse } | { status: 'error'; error: unknown } | { status: 'stale' }>
  cancel: () => void
}

/** 为配置切换提供单写者竞态保护；旧请求即使未响应 Abort 也不会提交结果。 */
export function createCapabilityRequestRunner<TRequest, TResponse>(
  load: (request: TRequest, signal: AbortSignal) => Promise<TResponse>,
): CapabilityRequestRunner<TRequest, TResponse> {
  let revision = 0
  let controller: AbortController | undefined
  return {
    async run(request) {
      controller?.abort()
      const nextController = new AbortController()
      controller = nextController
      const requestRevision = ++revision
      try {
        const value = await load(request, nextController.signal)
        return requestRevision === revision ? { status: 'ready', value } : { status: 'stale' }
      } catch (error) {
        if (requestRevision !== revision || (error instanceof DOMException && error.name === 'AbortError')) return { status: 'stale' }
        return { status: 'error', error }
      }
    },
    cancel() {
      revision += 1
      controller?.abort()
      controller = undefined
    },
  }
}

export type CapabilityRequestShape = {
  protocol?: PrintProtocol
  escPosLayoutMode?: EscPosLayoutMode
  encoding?: PrintEncoding
  escPosDevice?: EscPosDeviceRequest
  tsplMedia?: TsplMediaRequest
  zplMedia?: ZplMediaRequest
  allowRasterizedLines?: boolean
  requireCutContract?: boolean
}
