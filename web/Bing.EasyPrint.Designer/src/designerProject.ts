import type {
  DeviceFontCalibrationMap,
  DeviceFontCalibrationSets,
  EscPosCharacterRange,
  EscPosDeviceRequest,
  EscPosNativeFlowMode,
  EscPosNativeTextProfile,
  EscPosPrinterProfile,
  EscPosRasterFontResource,
  EscPosRasterFontsRequest,
  EscPosTextCharacterMode,
  EscPosBarcodeTextPositionName,
  LabelPrinterCharacterRange,
  LabelPrinterProfile,
  PrintJobCopyOrder,
  PrintJobFeedUnit,
  PrintJobOptions,
  PrintJobScope,
  PrintCutMode,
  PrintCutOptions,
  PrintEncoding,
  TsplCutterProfile,
  ZplCutterProfile,
  ZplCutStrategy,
  TsplCutStrategy,
  TsplPrinterProfile,
  TsplMediaRequest,
  ZplPrinterProfile,
  ZplMediaRequest,
  RotationAngleName,
} from './api'
import { BARCODE_TYPE_NAMES, defaultEscPosPrinterProfile, ESC_POS_BARCODE_TEXT_POSITION_NAMES, ROTATION_ANGLE_NAMES } from './api'
import { assertTemplate, cloneTemplate, type BpTemplate } from './model'

export { defaultEscPosPrinterProfile } from './api'

/** 设计器界面可选择的最终输出目标。 */
export type DesignerOutputTarget = 'Cpcl' | 'Tspl' | 'Zpl' | 'EscPosRaster' | 'EscPosNative'

/** 设计器需要随模板保存的输出设置。 */
export type DesignerOutputSettings = {
  outputTarget: DesignerOutputTarget
  allowRasterizedLines: boolean
  imageThreshold: number
  deviceHalfCellWidthAt24?: number
  deviceFullCellWidthAt24?: number
  deviceLineHeightAt24?: number
  deviceFontCalibrations: DeviceFontCalibrationSets
  requireDeclaredNativeBounds: boolean
  copies?: number
  feedLines?: number
  cutAfterPrint?: boolean
  cutOptions?: PrintCutOptions
  job?: PrintJobOptions
  encoding?: PrintEncoding
  escPosDevice: EscPosDeviceRequest
  tsplMedia: TsplMediaRequest
  zplMedia: ZplMediaRequest
}

export type DesignerProject = {
  format: 'Bing.EasyPrint.DesignerProject'
  version: 1
  template: BpTemplate
  outputSettings: DesignerOutputSettings
}

export class DesignerProjectValidationError extends Error {}

const DESIGNER_PROJECT_FORMAT = 'Bing.EasyPrint.DesignerProject' as const
const OUTPUT_TARGETS = ['Cpcl', 'Tspl', 'Zpl', 'EscPosRaster', 'EscPosNative'] as const
const ENCODINGS = ['Gbk', 'Utf8', 'Ascii', 'Device'] as const
const ZPL_ORIENTATIONS = ['None', 'Down', 'Left', 'Right'] as const
const ZPL_TRACKING = ['N', 'Y', 'W', 'M', 'A', 'V'] as const
const ESC_POS_IMAGE_COMMANDS = ['RasterGsV0', 'BitImageEscStar'] as const
const ESC_POS_BIT_IMAGE_MODES = ['SingleDensity8', 'DoubleDensity8', 'SingleDensity24', 'DoubleDensity24'] as const
const ESC_POS_TEXT_MODES = ['DeviceDefault', 'SingleByte', 'FsKanji'] as const
const ESC_POS_FS_KANJI_CODE_PAGES = [936, 54936, 950, 949] as const
const MAX_RASTER_FONT_RESOURCES = 64
const MAX_RASTER_FONT_BYTES = 32 * 1024 * 1024
const JOB_COPY_ORDERS = ['Collated', 'Uncollated'] as const
const JOB_SCOPES = ['None', 'EachPage', 'EachCopy', 'JobEnd'] as const
const JOB_FEED_UNITS = ['Lines', 'Dots', 'MotionUnits'] as const
const CUT_MODES = ['Full', 'Partial'] as const
const TSPL_DEVICE_FONT_NAME = /^[0-8]$/
const ZPL_DEVICE_FONT_NAME = /^[0-9A-Z]$/
const TSPL_ENCODING_CODE_PAGES = [20127, 65001, 936] as const
const ZPL_ENCODING_CODE_PAGES = [20127, 65001] as const
const TSPL_CUT_STRATEGIES = ['Immediate', 'Batch'] as const
const LABEL_PROFILE_MAX_INT = 2147483647

type RecordValue = Record<string, unknown>

function isRecord(value: unknown): value is RecordValue {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return false
  const prototype = Object.getPrototypeOf(value)
  return prototype === Object.prototype || prototype === null
}

function fail(path: string, message: string): never {
  throw new DesignerProjectValidationError(`${path}${message}`)
}

function assertKnownKeys(value: RecordValue, keys: readonly string[], path: string): void {
  const allowed = new Set(keys)
  for (const key of Object.keys(value)) if (!allowed.has(key)) fail(`${path}.${key}`, '包含不支持的字段。')
}

function optionalNumber(value: unknown, path: string, predicate?: (number: number) => boolean): number | undefined {
  if (value === undefined) return undefined
  if (typeof value !== 'number' || !Number.isFinite(value) || (predicate && !predicate(value))) fail(path, '必须为有效数字。')
  return value
}

function optionalInteger(value: unknown, path: string, predicate?: (number: number) => boolean): number | undefined {
  if (value === undefined) return undefined
  if (typeof value !== 'number' || !Number.isInteger(value) || !Number.isFinite(value) || (predicate && !predicate(value))) {
    fail(path, '必须为有效整数。')
  }
  return value
}

function optionalBoolean(value: unknown, path: string): boolean | undefined {
  if (value === undefined) return undefined
  if (typeof value !== 'boolean') fail(path, '必须为布尔值。')
  return value
}

function optionalString(value: unknown, path: string): string | undefined {
  if (value === undefined) return undefined
  if (typeof value !== 'string' || !value.trim()) fail(path, '必须为非空字符串。')
  return value
}

function optionalStringValue(value: unknown, path: string): string | undefined {
  if (value === undefined) return undefined
  if (typeof value !== 'string') fail(path, '必须为字符串。')
  return value
}

function optionalEnum<T extends string>(value: unknown, path: string, choices: readonly T[]): T | undefined {
  if (value === undefined) return undefined
  if (typeof value !== 'string' || !choices.includes(value as T)) fail(path, '包含无效枚举值。')
  return value as T
}

function base64ByteLength(value: unknown, path: string): number {
  if (typeof value !== 'string' || value.length === 0 || value.length % 4 !== 0 ||
    !/^[A-Za-z0-9+/]*={0,2}$/.test(value) ||
    (value.includes('=') && !value.endsWith('=') && !value.endsWith('=='))) {
    fail(path, '必须为有效且非空的 Base64 字符串。')
  }
  const padding = value.endsWith('==') ? 2 : value.endsWith('=') ? 1 : 0
  const bytes = value.length / 4 * 3 - padding
  if (!Number.isInteger(bytes) || bytes <= 0) fail(path, '必须为有效且非空的 Base64 字符串。')
  return bytes
}

function cloneEscPosRasterFonts(value: unknown, path: string): EscPosRasterFontsRequest | undefined {
  if (value === undefined) return undefined
  if (!isRecord(value)) fail(path, '必须为对象。')
  assertKnownKeys(value, ['defaultFont', 'fallbackFonts', 'fonts'], path)
  const defaultFont = optionalString(value.defaultFont, `${path}.defaultFont`)
  if (defaultFont === undefined) fail(`${path}.defaultFont`, '必须为非空字符串。')
  if (!Array.isArray(value.fonts) || value.fonts.length === 0 || value.fonts.length > MAX_RASTER_FONT_RESOURCES) {
    fail(`${path}.fonts`, `必须为 1–${MAX_RASTER_FONT_RESOURCES} 个资源。`)
  }
  const fallbackFonts = value.fallbackFonts === undefined ? undefined : value.fallbackFonts
  if (fallbackFonts !== undefined && (!Array.isArray(fallbackFonts) || fallbackFonts.some((item) => typeof item !== 'string' || !item.trim()))) {
    fail(`${path}.fallbackFonts`, '必须为非空名称数组。')
  }
  const names = new Set<string>()
  let totalBytes = 0
  const fonts: EscPosRasterFontResource[] = value.fonts.map((item, index) => {
    const itemPath = `${path}.fonts[${index}]`
    if (!isRecord(item)) fail(itemPath, '必须为对象。')
    assertKnownKeys(item, ['name', 'version', 'regularData', 'boldData'], itemPath)
    const name = optionalString(item.name, `${itemPath}.name`)
    const version = optionalString(item.version, `${itemPath}.version`)
    if (name === undefined || version === undefined) fail(itemPath, '必须同时提供非空 name 和 version。')
    if (names.has(name)) fail(`${itemPath}.name`, '资源名称不能重复。')
    names.add(name)
    const regularData = item.regularData
    totalBytes += base64ByteLength(regularData, `${itemPath}.regularData`)
    const boldData = item.boldData === undefined ? undefined : item.boldData
    if (boldData !== undefined) totalBytes += base64ByteLength(boldData, `${itemPath}.boldData`)
    if (totalBytes > MAX_RASTER_FONT_BYTES) fail(path, '字体资源解码后的总字节数不能超过 32 MiB。')
    return {
      name,
      version,
      regularData: regularData as string,
      ...(boldData === undefined ? {} : { boldData: boldData as string }),
    }
  })
  if (!names.has(defaultFont)) fail(`${path}.defaultFont`, '必须引用已声明的字体资源。')
  const normalizedFallbacks = fallbackFonts === undefined ? undefined : [...fallbackFonts] as string[]
  if (normalizedFallbacks !== undefined) {
    if (new Set(normalizedFallbacks).size !== normalizedFallbacks.length || normalizedFallbacks.some((name) => !names.has(name))) {
      fail(`${path}.fallbackFonts`, '必须引用已声明且不重复的字体资源。')
    }
  }
  return {
    defaultFont,
    ...(normalizedFallbacks === undefined ? {} : { fallbackFonts: normalizedFallbacks }),
    fonts,
  }
}

function cloneEscPosNativeTextProfile(value: unknown, path: string): EscPosNativeTextProfile | undefined {
  if (value === undefined) return undefined
  if (!isRecord(value)) fail(path, '必须为对象。')
  assertKnownKeys(value, [
    'encodingCodePage', 'codePageIndex', 'characterMode', 'characterRanges',
    'halfCellWidthDots', 'fullCellWidthDots', 'lineHeightDots',
  ], path)
  const encodingCodePage = optionalInteger(value.encodingCodePage, `${path}.encodingCodePage`, (item) => item > 0)
  if (encodingCodePage === undefined) fail(`${path}.encodingCodePage`, '必须为正整数。')
  const codePageIndex = optionalInteger(value.codePageIndex, `${path}.codePageIndex`, (item) => item >= 0 && item <= 255)
  const characterMode = optionalEnum<EscPosTextCharacterMode>(value.characterMode, `${path}.characterMode`, ESC_POS_TEXT_MODES)
  if (!Array.isArray(value.characterRanges) || value.characterRanges.length === 0) {
    fail(`${path}.characterRanges`, '必须为非空数组。')
  }
  const characterRanges: EscPosCharacterRange[] = value.characterRanges.map((item, index) => {
    const itemPath = `${path}.characterRanges[${index}]`
    if (!isRecord(item)) fail(itemPath, '必须为对象。')
    assertKnownKeys(item, ['first', 'last'], itemPath)
    const first = optionalInteger(item.first, `${itemPath}.first`, (number) => number >= 0 && number <= 0x10FFFF)
    const last = optionalInteger(item.last, `${itemPath}.last`, (number) => number >= 0 && number <= 0x10FFFF)
    if (first === undefined || last === undefined) fail(itemPath, '必须同时提供有效的 Unicode 范围端点。')
    if (first > last) fail(itemPath, '范围起点不能大于终点。')
    if (first <= 0xDFFF && last >= 0xD800) fail(itemPath, 'Unicode 范围不能包含 D800-DFFF 代理项。')
    return { first, last }
  })
  if (characterMode === 'FsKanji' && !ESC_POS_FS_KANJI_CODE_PAGES.includes(encodingCodePage as typeof ESC_POS_FS_KANJI_CODE_PAGES[number])) {
    fail(`${path}.encodingCodePage`, 'FsKanji 仅支持代码页 936、54936、950 或 949。')
  }
  const halfCellWidthDots = optionalInteger(value.halfCellWidthDots, `${path}.halfCellWidthDots`, (item) => item > 0)
  const fullCellWidthDots = optionalInteger(value.fullCellWidthDots, `${path}.fullCellWidthDots`, (item) => item > 0)
  const lineHeightDots = optionalInteger(value.lineHeightDots, `${path}.lineHeightDots`, (item) => item === 24 || item === 26) as 24 | 26 | undefined
  return {
    encodingCodePage,
    ...(codePageIndex === undefined ? {} : { codePageIndex }),
    ...(characterMode === undefined ? {} : { characterMode }),
    characterRanges,
    ...(halfCellWidthDots === undefined ? {} : { halfCellWidthDots }),
    ...(fullCellWidthDots === undefined ? {} : { fullCellWidthDots }),
    ...(lineHeightDots === undefined ? {} : { lineHeightDots }),
  }
}

function cloneEscPosPrinterProfile(value: unknown, path: string): EscPosPrinterProfile | undefined {
  if (value === undefined) return undefined
  if (!isRecord(value)) fail(path, '必须为对象。')
  assertKnownKeys(value, [
    'name', 'imageCommand', 'bitImageMode', 'horizontalDpi', 'verticalDpi', 'motionUnitsPerInch',
    'printableWidthDots', 'maxImageBlockBytes', 'supportsCut', 'supportedCutModes', 'supportsFeedAndCut', 'supportsNativeBarcode',
    'supportsNativeBarcodeHumanReadable', 'supportedNativeBarcodeHumanReadableTypes', 'supportsNativeQrCode', 'supportsPrintAreaWidth', 'supportsTextSize', 'unidirectionalBitImage', 'text',
    'supportedNativeBarcodeHumanReadablePositions',
  ], path)
  const defaults = defaultEscPosPrinterProfile()
  const name = optionalString(value.name, `${path}.name`) ?? defaults.name
  const imageCommand = optionalEnum(value.imageCommand ?? defaults.imageCommand, `${path}.imageCommand`, ESC_POS_IMAGE_COMMANDS)!
  const bitImageMode = optionalEnum(value.bitImageMode ?? defaults.bitImageMode, `${path}.bitImageMode`, ESC_POS_BIT_IMAGE_MODES)!
  const horizontalDpi = optionalInteger(value.horizontalDpi ?? defaults.horizontalDpi, `${path}.horizontalDpi`, (item) => item > 0)!
  const verticalDpi = optionalInteger(value.verticalDpi ?? defaults.verticalDpi, `${path}.verticalDpi`, (item) => item > 0)!
  const motionUnitsPerInch = optionalInteger(value.motionUnitsPerInch ?? defaults.motionUnitsPerInch,
    `${path}.motionUnitsPerInch`, (item) => item > 0)!
  const printableWidthDots = optionalInteger(value.printableWidthDots, `${path}.printableWidthDots`, (item) => item > 0)
  const maxImageBlockBytes = optionalInteger(value.maxImageBlockBytes, `${path}.maxImageBlockBytes`, (item) => item > 0)
  const supportsCut = optionalBoolean(value.supportsCut ?? defaults.supportsCut, `${path}.supportsCut`)!
  const supportsFeedAndCut = value.supportsFeedAndCut === undefined
    ? undefined
    : optionalBoolean(value.supportsFeedAndCut, `${path}.supportsFeedAndCut`)
  let supportedCutModes: readonly PrintCutMode[] | null | undefined
  if (value.supportedCutModes !== undefined) {
    if (value.supportedCutModes === null) {
      supportedCutModes = null
    } else {
      if (!Array.isArray(value.supportedCutModes)) fail(`${path}.supportedCutModes`, '必须为数组或 null。')
      const seen = new Set<PrintCutMode>()
      supportedCutModes = value.supportedCutModes.map((item, index) => {
        const mode = optionalEnum<PrintCutMode>(item, `${path}.supportedCutModes[${index}]`, CUT_MODES)
        if (mode === undefined) fail(`${path}.supportedCutModes[${index}]`, '包含无效枚举值。')
        if (seen.has(mode)) fail(`${path}.supportedCutModes[${index}]`, '切法不能重复。')
        seen.add(mode)
        return mode
      })
    }
  }
  const supportsNativeBarcode = optionalBoolean(value.supportsNativeBarcode ?? defaults.supportsNativeBarcode,
    `${path}.supportsNativeBarcode`)!
  const supportsNativeBarcodeHumanReadable = optionalBoolean(
    value.supportsNativeBarcodeHumanReadable ?? defaults.supportsNativeBarcodeHumanReadable,
    `${path}.supportsNativeBarcodeHumanReadable`)!
  let supportedNativeBarcodeHumanReadableTypes: typeof BARCODE_TYPE_NAMES[number][] | null | undefined
  if (value.supportedNativeBarcodeHumanReadableTypes !== undefined) {
    if (value.supportedNativeBarcodeHumanReadableTypes === null) {
      supportedNativeBarcodeHumanReadableTypes = null
    } else {
      if (!Array.isArray(value.supportedNativeBarcodeHumanReadableTypes)) {
        fail(`${path}.supportedNativeBarcodeHumanReadableTypes`, '必须为数组或 null。')
      }
      const seen = new Set<string>()
      supportedNativeBarcodeHumanReadableTypes = value.supportedNativeBarcodeHumanReadableTypes.map((item, index) => {
        const itemPath = `${path}.supportedNativeBarcodeHumanReadableTypes[${index}]`
        if (typeof item !== 'string' || !(BARCODE_TYPE_NAMES as readonly string[]).includes(item)) {
          fail(itemPath, '包含无效的条码制式。')
        }
        if (seen.has(item)) fail(itemPath, '条码制式不能重复。')
        seen.add(item)
        return item as typeof BARCODE_TYPE_NAMES[number]
      })
    }
  }
  let supportedNativeBarcodeHumanReadablePositions: EscPosBarcodeTextPositionName[] | null | undefined
  if (value.supportedNativeBarcodeHumanReadablePositions !== undefined) {
    if (value.supportedNativeBarcodeHumanReadablePositions === null) {
      supportedNativeBarcodeHumanReadablePositions = null
    } else {
      if (!Array.isArray(value.supportedNativeBarcodeHumanReadablePositions)) {
        fail(`${path}.supportedNativeBarcodeHumanReadablePositions`, '必须为数组或 null。')
      }
      const seen = new Set<string>()
      supportedNativeBarcodeHumanReadablePositions = value.supportedNativeBarcodeHumanReadablePositions.map((item, index) => {
        const itemPath = `${path}.supportedNativeBarcodeHumanReadablePositions[${index}]`
        if (typeof item !== 'string' || !(ESC_POS_BARCODE_TEXT_POSITION_NAMES as readonly string[]).includes(item)) {
          fail(itemPath, '包含无效的 HRI 位置。')
        }
        if (seen.has(item)) fail(itemPath, 'HRI 位置不能重复。')
        seen.add(item)
        return item as EscPosBarcodeTextPositionName
      })
    }
  }
  const supportsNativeQrCode = optionalBoolean(value.supportsNativeQrCode ?? defaults.supportsNativeQrCode,
    `${path}.supportsNativeQrCode`)!
  const supportsPrintAreaWidth = optionalBoolean(value.supportsPrintAreaWidth ?? defaults.supportsPrintAreaWidth,
    `${path}.supportsPrintAreaWidth`)!
  const supportsTextSize = optionalBoolean(value.supportsTextSize ?? defaults.supportsTextSize, `${path}.supportsTextSize`)!
  const unidirectionalBitImage = optionalBoolean(value.unidirectionalBitImage ?? defaults.unidirectionalBitImage,
    `${path}.unidirectionalBitImage`)!
  const text = cloneEscPosNativeTextProfile(value.text, `${path}.text`)
  if (imageCommand === 'RasterGsV0' && horizontalDpi !== verticalDpi) {
    fail(path, 'GS v 0 必须使用相同的横纵分辨率。')
  }
  if (imageCommand === 'RasterGsV0' && (bitImageMode !== 'DoubleDensity24' || unidirectionalBitImage)) {
    fail(path, 'GS v 0 不接受 ESC * 点阵模式或单向打印设置。')
  }
  const minimumBlockBytes = imageCommand === 'BitImageEscStar' &&
    (bitImageMode === 'SingleDensity24' || bitImageMode === 'DoubleDensity24') ? 3 : 1
  if (maxImageBlockBytes !== undefined && maxImageBlockBytes < minimumBlockBytes) {
    fail(`${path}.maxImageBlockBytes`, `图片分块预算至少为 ${minimumBlockBytes} 字节。`)
  }
  return {
    name, imageCommand, bitImageMode, horizontalDpi, verticalDpi, motionUnitsPerInch,
    ...(printableWidthDots === undefined ? {} : { printableWidthDots }),
    ...(maxImageBlockBytes === undefined ? {} : { maxImageBlockBytes }),
    supportsCut,
    ...(supportedCutModes === undefined ? {} : { supportedCutModes }),
    ...(supportsFeedAndCut === undefined ? {} : { supportsFeedAndCut }),
    supportsNativeBarcode, supportsNativeBarcodeHumanReadable,
    ...(supportedNativeBarcodeHumanReadableTypes === undefined ? {} : { supportedNativeBarcodeHumanReadableTypes }),
    ...(supportedNativeBarcodeHumanReadablePositions === undefined ? {} : { supportedNativeBarcodeHumanReadablePositions }),
    supportsNativeQrCode, supportsPrintAreaWidth, supportsTextSize,
    unidirectionalBitImage,
    ...(text === undefined ? {} : { text }),
  }
}

function cloneEscPosDevice(value: unknown, path: string): EscPosDeviceRequest {
  if (value === undefined) return {}
  if (!isRecord(value)) fail(path, '必须为对象。')
  assertKnownKeys(value, ['dpi', 'codePageIndex', 'printAreaWidthUnits', 'nativeTabWidthDots',
    'rasterBarcodeHumanReadableFontSize', 'nativeFlow', 'rasterFonts', 'profile'], path)
  const profile = cloneEscPosPrinterProfile(value.profile, `${path}.profile`)
  const nativeFlow = optionalEnum<EscPosNativeFlowMode>(value.nativeFlow, `${path}.nativeFlow`, ['Rows', 'Sequential'])
  const rasterFonts = cloneEscPosRasterFonts(value.rasterFonts, `${path}.rasterFonts`)
  return {
    dpi: optionalInteger(value.dpi, `${path}.dpi`, (item) => item > 0),
    codePageIndex: optionalInteger(value.codePageIndex, `${path}.codePageIndex`, (item) => item >= 0 && item <= 255),
    printAreaWidthUnits: optionalInteger(value.printAreaWidthUnits, `${path}.printAreaWidthUnits`, (item) => item >= 0 && item <= 65535),
    nativeTabWidthDots: optionalInteger(value.nativeTabWidthDots, `${path}.nativeTabWidthDots`, (item) => item >= 8 && item % 8 === 0),
    rasterBarcodeHumanReadableFontSize: optionalInteger(value.rasterBarcodeHumanReadableFontSize,
      `${path}.rasterBarcodeHumanReadableFontSize`, (item) => item >= 1 && item <= 255),
    ...(nativeFlow === undefined ? {} : { nativeFlow }),
    ...(rasterFonts === undefined ? {} : { rasterFonts }),
    ...(profile === undefined ? {} : { profile }),
  }
}

function cloneLabelPrinterProfile(value: unknown, path: string, protocol: 'Tspl'): TsplPrinterProfile | undefined
function cloneLabelPrinterProfile(value: unknown, path: string, protocol: 'Zpl'): ZplPrinterProfile | undefined
function cloneLabelPrinterProfile(value: unknown, path: string, protocol: 'Tspl' | 'Zpl'): LabelPrinterProfile | undefined {
  if (value === undefined) return undefined
  if (!isRecord(value)) fail(path, '必须为对象。')
  const keys = [
    'name', 'firmwareVersion', 'printableWidthDots', 'maxLabelHeightDots', 'maxImageDataBytes',
    'supportsImages', 'supportsGraphics', 'supportsNativeBarcode', 'supportsNativeBarcodeHumanReadable', 'supportedNativeBarcodeHumanReadableTypes', 'supportedNativeQrCodeRotations', 'supportsNativeQrCode', 'supportsRotation',
    'deviceFontNames', 'encodingCodePages', 'characterRanges',
    'cutter',
    ...(protocol === 'Zpl' ? ['supportsCut'] : []),
  ]
  assertKnownKeys(value, keys, path)

  const name = optionalString(value.name, `${path}.name`) ?? 'Custom'
  const firmwareVersion = optionalStringValue(value.firmwareVersion, `${path}.firmwareVersion`)
  const printableWidthDots = optionalInteger(value.printableWidthDots, `${path}.printableWidthDots`,
    (item) => item > 0 && item <= LABEL_PROFILE_MAX_INT)
  const maxLabelHeightDots = optionalInteger(value.maxLabelHeightDots, `${path}.maxLabelHeightDots`,
    (item) => item > 0 && item <= LABEL_PROFILE_MAX_INT)
  const maxImageDataBytes = optionalInteger(value.maxImageDataBytes, `${path}.maxImageDataBytes`,
    (item) => item > 0 && item <= LABEL_PROFILE_MAX_INT)
  const supportsImages = optionalBoolean(value.supportsImages, `${path}.supportsImages`)
  const supportsGraphics = optionalBoolean(value.supportsGraphics, `${path}.supportsGraphics`)
  const supportsNativeBarcode = optionalBoolean(value.supportsNativeBarcode, `${path}.supportsNativeBarcode`)
  const supportsNativeBarcodeHumanReadable = optionalBoolean(value.supportsNativeBarcodeHumanReadable, `${path}.supportsNativeBarcodeHumanReadable`)
  let supportedNativeBarcodeHumanReadableTypes: typeof BARCODE_TYPE_NAMES[number][] | undefined
  if (value.supportedNativeBarcodeHumanReadableTypes !== undefined) {
    if (!Array.isArray(value.supportedNativeBarcodeHumanReadableTypes)) {
      fail(`${path}.supportedNativeBarcodeHumanReadableTypes`, '必须为数组。')
    }
    const seen = new Set<string>()
    supportedNativeBarcodeHumanReadableTypes = value.supportedNativeBarcodeHumanReadableTypes.map((item, index) => {
      const itemPath = `${path}.supportedNativeBarcodeHumanReadableTypes[${index}]`
      if (typeof item !== 'string' || !(BARCODE_TYPE_NAMES as readonly string[]).includes(item)) {
        fail(itemPath, '包含无效的条码制式。')
      }
      if (seen.has(item)) fail(itemPath, '条码制式不能重复。')
      seen.add(item)
      return item as typeof BARCODE_TYPE_NAMES[number]
    })
  }
  let supportedNativeQrCodeRotations: RotationAngleName[] | undefined
  if (value.supportedNativeQrCodeRotations !== undefined) {
    if (!Array.isArray(value.supportedNativeQrCodeRotations)) {
      fail(`${path}.supportedNativeQrCodeRotations`, '必须为数组。')
    }
    const seen = new Set<string>()
    supportedNativeQrCodeRotations = value.supportedNativeQrCodeRotations.map((item, index) => {
      const itemPath = `${path}.supportedNativeQrCodeRotations[${index}]`
      if (typeof item !== 'string' || !(ROTATION_ANGLE_NAMES as readonly string[]).includes(item)) {
        fail(itemPath, '包含无效的二维码旋转角度。')
      }
      if (seen.has(item)) fail(itemPath, '二维码旋转角度不能重复。')
      seen.add(item)
      return item as RotationAngleName
    })
  }
  const supportsNativeQrCode = optionalBoolean(value.supportsNativeQrCode, `${path}.supportsNativeQrCode`)
  const supportsRotation = optionalBoolean(value.supportsRotation, `${path}.supportsRotation`)
  const supportsCut = protocol === 'Zpl' ? optionalBoolean(value.supportsCut, `${path}.supportsCut`) : undefined
  const cutter = protocol === 'Tspl' ? cloneTsplCutter(value.cutter, `${path}.cutter`) : cloneZplCutter(value.cutter, `${path}.cutter`)
  if (protocol === 'Zpl' && cutter !== undefined && supportsCut === false) fail(`${path}.cutter`, '禁用切刀时不能配置 Cutter。')

  let deviceFontNames: string[] | undefined
  if (value.deviceFontNames !== undefined) {
    if (!Array.isArray(value.deviceFontNames)) fail(`${path}.deviceFontNames`, '必须为数组。')
    const pattern = protocol === 'Tspl' ? TSPL_DEVICE_FONT_NAME : ZPL_DEVICE_FONT_NAME
    const seen = new Set<string>()
    deviceFontNames = value.deviceFontNames.map((item, index) => {
      if (typeof item !== 'string' || !pattern.test(item)) {
        fail(`${path}.deviceFontNames[${index}]`, '包含当前协议不支持的内置字体标识。')
      }
      if (seen.has(item)) fail(`${path}.deviceFontNames[${index}]`, '字体标识不能重复。')
      seen.add(item)
      return item
    })
  }

  let encodingCodePages: number[] | undefined
  if (value.encodingCodePages !== undefined) {
    if (!Array.isArray(value.encodingCodePages) || value.encodingCodePages.length === 0) {
      fail(`${path}.encodingCodePages`, '必须为非空数组。')
    }
    const choices = protocol === 'Tspl' ? TSPL_ENCODING_CODE_PAGES : ZPL_ENCODING_CODE_PAGES
    const seen = new Set<number>()
    encodingCodePages = value.encodingCodePages.map((item, index) => {
      const itemPath = `${path}.encodingCodePages[${index}]`
      const codePage = optionalInteger(item, itemPath)
      if (codePage === undefined || !choices.some((choice) => choice === codePage)) {
        fail(itemPath, '必须为当前协议支持的代码页。')
      }
      if (seen.has(codePage)) fail(`${path}.encodingCodePages[${index}]`, '代码页不能重复。')
      seen.add(codePage)
      return codePage
    })
  }

  let characterRanges: LabelPrinterCharacterRange[] | undefined
  if (value.characterRanges !== undefined) {
    if (!Array.isArray(value.characterRanges) || value.characterRanges.length === 0) {
      fail(`${path}.characterRanges`, '必须为非空数组。')
    }
    characterRanges = value.characterRanges.map((item, index) => {
      const itemPath = `${path}.characterRanges[${index}]`
      if (!isRecord(item)) fail(itemPath, '必须为对象。')
      assertKnownKeys(item, ['first', 'last'], itemPath)
      const first = optionalInteger(item.first, `${itemPath}.first`, (number) => number >= 0 && number <= 0x10FFFF)
      const last = optionalInteger(item.last, `${itemPath}.last`, (number) => number >= 0 && number <= 0x10FFFF)
      if (first === undefined || last === undefined) fail(itemPath, '必须同时提供有效的 Unicode 范围端点。')
      if (first > last) fail(itemPath, '范围起点不能大于终点。')
      if (first <= 0xDFFF && last >= 0xD800) fail(itemPath, 'Unicode 范围不能包含 D800-DFFF 代理项。')
      return { first, last }
    })
    const sortedRanges = characterRanges
      .map((range, index) => ({ range, index }))
      .sort((left, right) => left.range.first - right.range.first)
    for (let index = 1; index < sortedRanges.length; index += 1) {
      const prior = sortedRanges[index - 1]
      const current = sortedRanges[index]
      if (current.range.first <= prior.range.last) {
        fail(`${path}.characterRanges[${current.index}]`, 'Unicode 范围不能重叠。')
      }
    }
  }

  return {
    name,
    ...(firmwareVersion === undefined ? {} : { firmwareVersion }),
    ...(printableWidthDots === undefined ? {} : { printableWidthDots }),
    ...(maxLabelHeightDots === undefined ? {} : { maxLabelHeightDots }),
    ...(maxImageDataBytes === undefined ? {} : { maxImageDataBytes }),
    ...(supportsImages === undefined ? {} : { supportsImages }),
    ...(supportsGraphics === undefined ? {} : { supportsGraphics }),
    ...(supportsNativeBarcode === undefined ? {} : { supportsNativeBarcode }),
    ...(supportsNativeBarcodeHumanReadable === undefined ? {} : { supportsNativeBarcodeHumanReadable }),
    ...(supportedNativeBarcodeHumanReadableTypes === undefined ? {} : { supportedNativeBarcodeHumanReadableTypes }),
    ...(supportedNativeQrCodeRotations === undefined ? {} : { supportedNativeQrCodeRotations }),
    ...(supportsNativeQrCode === undefined ? {} : { supportsNativeQrCode }),
    ...(supportsRotation === undefined ? {} : { supportsRotation }),
    ...(cutter === undefined ? {} : { cutter }),
    ...(supportsCut === undefined ? {} : { supportsCut }),
    ...(deviceFontNames === undefined ? {} : { deviceFontNames }),
    ...(encodingCodePages === undefined ? {} : { encodingCodePages }),
    ...(characterRanges === undefined ? {} : { characterRanges }),
  }
}

export function cloneZplCutter(value: unknown, path: string): ZplCutterProfile | undefined {
  if (value === undefined) return undefined
  if (!isRecord(value)) fail(path, '必须为对象。')
  assertKnownKeys(value, ['strategy', 'supportedCutModes', 'supportsTearOff', 'kioskPartialUncutMillimeters', 'kioskCutMarginMillimeters'], path)
  const strategy = optionalEnum<ZplCutStrategy>(value.strategy, `${path}.strategy`, ['Cutter', 'Delayed', 'Kiosk']) ?? 'Cutter'
  const supportsTearOff = optionalBoolean(value.supportsTearOff, `${path}.supportsTearOff`)
  if (supportsTearOff !== true) fail(`${path}.supportsTearOff`, '必须明确确认 ^MMT 非裁切模式。')
  const rawModes = value.supportedCutModes ?? ['Full']
  if (!Array.isArray(rawModes) || rawModes.length === 0) fail(`${path}.supportedCutModes`, '必须为非空数组。')
  const supportedCutModes: PrintCutMode[] = rawModes.map((mode, index) => {
    if (mode !== 'Full' && mode !== 'Partial') fail(`${path}.supportedCutModes[${index}]`, '包含无效切法。')
    return mode
  })
  if (new Set(supportedCutModes).size !== supportedCutModes.length) fail(`${path}.supportedCutModes`, '切法不能重复。')
  const kioskPartialUncutMillimeters = optionalInteger(value.kioskPartialUncutMillimeters, `${path}.kioskPartialUncutMillimeters`, (item) => item >= 10 && item <= 60)
  const kioskCutMarginMillimeters = optionalInteger(value.kioskCutMarginMillimeters, `${path}.kioskCutMarginMillimeters`, (item) => item >= 2 && item <= 9)
  if (strategy !== 'Kiosk') {
    if (supportedCutModes.includes('Partial')) fail(`${path}.supportedCutModes`, 'Cutter/Delayed 只映射全切。')
    if (kioskPartialUncutMillimeters !== undefined || kioskCutMarginMillimeters !== undefined) fail(path, 'Kiosk 参数只能用于 Kiosk 策略。')
  } else {
    if (kioskCutMarginMillimeters === undefined) fail(`${path}.kioskCutMarginMillimeters`, 'Kiosk 须声明裁切边距。')
    if (supportedCutModes.includes('Partial') !== (kioskPartialUncutMillimeters !== undefined)) {
      fail(`${path}.kioskPartialUncutMillimeters`, '半切距离须与 Partial 切法声明同时提供。')
    }
  }
  return { strategy, supportsTearOff, supportedCutModes,
    ...(kioskPartialUncutMillimeters === undefined ? {} : { kioskPartialUncutMillimeters }),
    ...(kioskCutMarginMillimeters === undefined ? {} : { kioskCutMarginMillimeters }) }
}

function cloneTsplCutter(value: unknown, path: string): TsplCutterProfile | undefined {
  if (value === undefined) return undefined
  if (!isRecord(value)) fail(path, '必须为对象。')
  assertKnownKeys(value, ['strategy', 'supportedCutModes', 'supportsEndOfJob', 'cutPositionFeedDots'], path)

  const strategy = optionalEnum<TsplCutStrategy>(value.strategy, `${path}.strategy`, TSPL_CUT_STRATEGIES) ?? 'Batch'
  const supportsEndOfJob = optionalBoolean(value.supportsEndOfJob, `${path}.supportsEndOfJob`)
  if (supportsEndOfJob !== true) fail(`${path}.supportsEndOfJob`, '必须明确为 true，以确认设备支持 EOJ 完成同步。')

  let supportedCutModes: PrintCutMode[]
  if (value.supportedCutModes === undefined) {
    supportedCutModes = ['Full']
  } else {
    if (!Array.isArray(value.supportedCutModes) || value.supportedCutModes.length === 0) {
      fail(`${path}.supportedCutModes`, '必须为非空数组。')
    }
    const seen = new Set<PrintCutMode>()
    supportedCutModes = value.supportedCutModes.map((item, index) => {
      if (item !== 'Full' && item !== 'Partial') fail(`${path}.supportedCutModes[${index}]`, '包含无效切法。')
      if (seen.has(item)) fail(`${path}.supportedCutModes[${index}]`, '切法不能重复。')
      seen.add(item)
      return item
    })
  }
  if (strategy === 'Immediate' && supportedCutModes.length !== 1) {
    fail(`${path}.supportedCutModes`, 'Immediate 没有切法参数，只能声明设备的一种固定切法。')
  }

  const cutPositionFeedDots = optionalInteger(value.cutPositionFeedDots, `${path}.cutPositionFeedDots`,
    (item) => item >= 0 && item <= 9999)
  if (strategy === 'Batch' && cutPositionFeedDots !== undefined) {
    fail(`${path}.cutPositionFeedDots`, 'Batch 自动裁切不能配置切刀位置补偿点数。')
  }

  return {
    strategy,
    supportedCutModes,
    supportsEndOfJob,
    ...(cutPositionFeedDots === undefined ? {} : { cutPositionFeedDots }),
  }
}

export function normalizeLabelPrinterProfile(value: unknown, protocol: 'Tspl'): TsplPrinterProfile | undefined
export function normalizeLabelPrinterProfile(value: unknown, protocol: 'Zpl'): ZplPrinterProfile | undefined
export function normalizeLabelPrinterProfile(value: unknown, protocol: 'Tspl' | 'Zpl'): LabelPrinterProfile | undefined
export function normalizeLabelPrinterProfile(value: unknown, protocol: 'Tspl' | 'Zpl'): LabelPrinterProfile | undefined {
  return protocol === 'Tspl'
    ? cloneLabelPrinterProfile(value, 'profile', 'Tspl')
    : cloneLabelPrinterProfile(value, 'profile', 'Zpl')
}

function cloneTsplMedia(value: unknown, path: string): TsplMediaRequest {
  if (value === undefined) return {}
  if (!isRecord(value)) fail(path, '必须为对象。')
  assertKnownKeys(value, ['allowRasterizedTextStyles', 'dpi', 'gapDistance', 'gapOffset', 'direction', 'referenceX', 'referenceY', 'speed', 'density', 'profile'], path)
  const profile = cloneLabelPrinterProfile(value.profile, `${path}.profile`, 'Tspl')
  const allowRasterizedTextStyles = optionalBoolean(value.allowRasterizedTextStyles, `${path}.allowRasterizedTextStyles`)
  return {
    ...(allowRasterizedTextStyles === undefined ? {} : { allowRasterizedTextStyles }),
    dpi: optionalInteger(value.dpi, `${path}.dpi`, (item) => item > 0),
    gapDistance: optionalInteger(value.gapDistance, `${path}.gapDistance`, (item) => item >= 0),
    gapOffset: optionalInteger(value.gapOffset, `${path}.gapOffset`, (item) => item >= 0),
    direction: optionalInteger(value.direction, `${path}.direction`, (item) => item >= 0 && item <= 1),
    referenceX: optionalInteger(value.referenceX, `${path}.referenceX`, (item) => item >= 0),
    referenceY: optionalInteger(value.referenceY, `${path}.referenceY`, (item) => item >= 0),
    speed: optionalInteger(value.speed, `${path}.speed`, (item) => item > 0),
    density: optionalInteger(value.density, `${path}.density`, (item) => item >= 0 && item <= 15),
    ...(profile === undefined ? {} : { profile }),
  }
}

function cloneZplMedia(value: unknown, path: string): ZplMediaRequest {
  if (value === undefined) return {}
  if (!isRecord(value)) fail(path, '必须为对象。')
  assertKnownKeys(value, ['allowRasterizedTextStyles', 'dpi', 'printSpeed', 'orientation', 'mediaTracking', 'darkness', 'profile'], path)
  const profile = cloneLabelPrinterProfile(value.profile, `${path}.profile`, 'Zpl')
  const allowRasterizedTextStyles = optionalBoolean(value.allowRasterizedTextStyles, `${path}.allowRasterizedTextStyles`)
  return {
    ...(allowRasterizedTextStyles === undefined ? {} : { allowRasterizedTextStyles }),
    dpi: optionalInteger(value.dpi, `${path}.dpi`, (item) => item > 0),
    printSpeed: optionalInteger(value.printSpeed, `${path}.printSpeed`, (item) => item > 0),
    orientation: optionalEnum(value.orientation, `${path}.orientation`, ZPL_ORIENTATIONS),
    mediaTracking: optionalEnum(value.mediaTracking, `${path}.mediaTracking`, ZPL_TRACKING),
    darkness: optionalInteger(value.darkness, `${path}.darkness`, (item) => item >= -30 && item <= 30),
    ...(profile === undefined ? {} : { profile }),
  }
}

function cloneDeviceFontCalibrationMap(value: unknown, path: string, pattern: RegExp): DeviceFontCalibrationMap {
  if (value === undefined) return {}
  if (!isRecord(value)) fail(path, '必须为对象。')
  const result: DeviceFontCalibrationMap = {}
  for (const [fontName, rawCalibration] of Object.entries(value)) {
    if (!pattern.test(fontName)) fail(`${path}.${fontName}`, '字体编号不符合当前协议格式。')
    if (!isRecord(rawCalibration)) fail(`${path}.${fontName}`, '必须为对象。')
    assertKnownKeys(rawCalibration, ['halfCellWidthAt24', 'fullCellWidthAt24', 'lineHeightAt24'], `${path}.${fontName}`)
    const halfCellWidthAt24 = optionalNumber(rawCalibration.halfCellWidthAt24,
      `${path}.${fontName}.halfCellWidthAt24`, (item) => item > 0)
    const fullCellWidthAt24 = optionalNumber(rawCalibration.fullCellWidthAt24,
      `${path}.${fontName}.fullCellWidthAt24`, (item) => item > 0)
    const lineHeightAt24 = optionalNumber(rawCalibration.lineHeightAt24,
      `${path}.${fontName}.lineHeightAt24`, (item) => item > 0)
    if (halfCellWidthAt24 === undefined || fullCellWidthAt24 === undefined || lineHeightAt24 === undefined) {
      fail(`${path}.${fontName}`, '必须同时提供半角字宽、全角字宽和行高。')
    }
    result[fontName] = { halfCellWidthAt24, fullCellWidthAt24, lineHeightAt24 }
  }
  return result
}

function cloneDeviceFontCalibrations(value: unknown, path: string): DeviceFontCalibrationSets {
  if (value === undefined) return { Tspl: {}, Zpl: {} }
  if (!isRecord(value)) fail(path, '必须为对象。')

  assertKnownKeys(value, ['Tspl', 'Zpl'], path)
  return {
    Tspl: cloneDeviceFontCalibrationMap(value.Tspl, `${path}.Tspl`, /^[0-8]$/),
    Zpl: cloneDeviceFontCalibrationMap(value.Zpl, `${path}.Zpl`, /^[0-9A-Z]$/),
  }
}

function clonePrintCutOptions(value: unknown, path: string): PrintCutOptions | undefined {
  if (value === undefined) return undefined
  if (!isRecord(value)) fail(path, '必须为对象。')
  assertKnownKeys(value, ['mode', 'feedToCutPosition', 'feedBeforeCutAmount', 'feedBeforeCutUnit'], path)
  const mode = optionalEnum<PrintCutMode>(value.mode, `${path}.mode`, CUT_MODES) ?? 'Full'
  const feedToCutPosition = optionalBoolean(value.feedToCutPosition, `${path}.feedToCutPosition`) ?? false
  const feedBeforeCutAmount = optionalInteger(value.feedBeforeCutAmount, `${path}.feedBeforeCutAmount`,
    (item) => item >= 0 && item <= 2147483647) ?? 0
  const feedBeforeCutUnit = optionalEnum<PrintJobFeedUnit>(value.feedBeforeCutUnit, `${path}.feedBeforeCutUnit`, JOB_FEED_UNITS)
    ?? 'MotionUnits'
  if (feedBeforeCutAmount > 0 && !feedToCutPosition) {
    fail(`${path}.feedToCutPosition`, '补偿量为正数时必须启用 feedToCutPosition。')
  }
  return { mode, feedToCutPosition, feedBeforeCutAmount, feedBeforeCutUnit }
}

function clonePrintJob(value: unknown, path: string): PrintJobOptions | undefined {
  if (value === undefined) return undefined
  if (!isRecord(value)) fail(path, '必须为对象。')
  assertKnownKeys(value, ['copies', 'copyOrder', 'feedScope', 'feedUnit', 'feedAmount', 'cutScope', 'cutOptions'], path)
  const copies = optionalInteger(value.copies, `${path}.copies`, (item) => item >= 1) ?? 1
  const copyOrder = optionalEnum<PrintJobCopyOrder>(value.copyOrder, `${path}.copyOrder`, JOB_COPY_ORDERS) ?? 'Collated'
  const feedScope = optionalEnum<PrintJobScope>(value.feedScope, `${path}.feedScope`, JOB_SCOPES) ?? 'None'
  const feedUnit = optionalEnum<PrintJobFeedUnit>(value.feedUnit, `${path}.feedUnit`, JOB_FEED_UNITS) ?? 'Lines'
  const feedAmount = optionalInteger(value.feedAmount, `${path}.feedAmount`, (item) => item >= 0) ?? 0
  const cutScope = optionalEnum<PrintJobScope>(value.cutScope, `${path}.cutScope`, JOB_SCOPES) ?? 'None'
  const cutOptions = clonePrintCutOptions(value.cutOptions, `${path}.cutOptions`)
  if (feedAmount > 0 && feedScope === 'None') fail(`${path}.feedScope`, '进纸数量为正数时必须指定进纸范围。')
  if (cutOptions !== undefined && cutScope === 'None') fail(`${path}.cutScope`, '启用 cutOptions 时必须指定切纸范围。')
  return { copies, copyOrder, feedScope, feedUnit, feedAmount, cutScope,
    ...(cutOptions === undefined ? {} : { cutOptions }) }
}

/** 返回新的默认设置对象；每次调用都包含新的嵌套选项对象。 */
export function defaultDesignerOutputSettings(): DesignerOutputSettings {
  return {
    outputTarget: 'Cpcl',
    allowRasterizedLines: false,
    imageThreshold: 128,
    deviceFontCalibrations: { Tspl: {}, Zpl: {} },
    requireDeclaredNativeBounds: false,
    escPosDevice: {},
    tsplMedia: {},
    zplMedia: {},
  }
}

/** 校验并复制设计器输出设置，避免导入的对象继续被界面状态共享。 */
export function validateDesignerOutputSettings(value: unknown): DesignerOutputSettings {
  if (!isRecord(value)) fail('outputSettings', '必须为对象。')
  assertKnownKeys(value, [
    'outputTarget', 'allowRasterizedLines', 'imageThreshold', 'deviceHalfCellWidthAt24',
    'deviceFullCellWidthAt24', 'deviceLineHeightAt24', 'requireDeclaredNativeBounds',
    'deviceFontCalibrations',
    'copies', 'feedLines', 'cutAfterPrint', 'cutOptions', 'job', 'encoding', 'escPosDevice', 'tsplMedia', 'zplMedia',
  ], 'outputSettings')

  const defaults = defaultDesignerOutputSettings()
  const outputTarget = optionalEnum(value.outputTarget ?? defaults.outputTarget, 'outputSettings.outputTarget', OUTPUT_TARGETS)!
  const encoding = optionalEnum(value.encoding, 'outputSettings.encoding', ENCODINGS)
  if (outputTarget === 'Zpl' && encoding === 'Gbk') fail('outputSettings.encoding', 'ZPL 不支持 Gbk 编码。')

  const allowRasterizedLines = optionalBoolean(value.allowRasterizedLines ?? defaults.allowRasterizedLines, 'outputSettings.allowRasterizedLines')!
  const imageThreshold = optionalInteger(value.imageThreshold ?? defaults.imageThreshold, 'outputSettings.imageThreshold', (item) => item >= 0 && item <= 255)!
  const deviceHalfCellWidthAt24 = optionalNumber(value.deviceHalfCellWidthAt24, 'outputSettings.deviceHalfCellWidthAt24', (item) => item > 0)
  const deviceFullCellWidthAt24 = optionalNumber(value.deviceFullCellWidthAt24, 'outputSettings.deviceFullCellWidthAt24', (item) => item > 0)
  const deviceLineHeightAt24 = optionalNumber(value.deviceLineHeightAt24, 'outputSettings.deviceLineHeightAt24', (item) => item > 0)
  const deviceFontCalibrations = cloneDeviceFontCalibrations(value.deviceFontCalibrations, 'outputSettings.deviceFontCalibrations')
  const requireDeclaredNativeBounds = optionalBoolean(value.requireDeclaredNativeBounds ?? defaults.requireDeclaredNativeBounds,
    'outputSettings.requireDeclaredNativeBounds')!
  const copies = optionalInteger(value.copies, 'outputSettings.copies', (item) => item >= 1)
  const feedLines = optionalInteger(value.feedLines, 'outputSettings.feedLines', (item) => item >= 0)
  const cutAfterPrint = optionalBoolean(value.cutAfterPrint, 'outputSettings.cutAfterPrint')
  const cutOptions = clonePrintCutOptions(value.cutOptions, 'outputSettings.cutOptions')
  const job = clonePrintJob(value.job, 'outputSettings.job')
  if (cutOptions !== undefined && cutAfterPrint === false) {
    fail('outputSettings.cutAfterPrint', '启用 cutOptions 时旧 cutAfterPrint 不能为 false。')
  }
  if (job !== undefined) {
    if (copies !== undefined && copies !== 1) fail('outputSettings.copies', '启用 job 后旧 copies 只能省略或为 1。')
    if (feedLines !== undefined && feedLines > 0) fail('outputSettings.feedLines', '启用 job 后旧 feedLines 只能省略或为 0。')
    if (cutAfterPrint === true) fail('outputSettings.cutAfterPrint', '启用 job 后旧 cutAfterPrint 不能为 true。')
    if (cutOptions !== undefined) fail('outputSettings.cutOptions', '启用 job 后不能同时启用页面 cutOptions。')
  }
  const escPosDevice = cloneEscPosDevice(value.escPosDevice, 'outputSettings.escPosDevice')
  if (escPosDevice.nativeFlow === 'Sequential' && outputTarget !== 'EscPosNative') {
    fail('outputSettings.escPosDevice.nativeFlow', 'Sequential 仅适用于 ESC/POS Native 输出。')
  }
  if (encoding === 'Device' && (outputTarget !== 'EscPosNative' || escPosDevice.profile?.text === undefined)) {
    fail('outputSettings.encoding', 'Device 编码仅适用于启用 Native 文字契约的 ESC/POS Native 输出。')
  }
  if (escPosDevice.rasterFonts !== undefined && outputTarget !== 'EscPosRaster') {
    fail('outputSettings.escPosDevice.rasterFonts', '固定主机字体配置仅适用于 ESC/POS Raster 输出。')
  }
  if (escPosDevice.rasterBarcodeHumanReadableFontSize !== undefined && outputTarget !== 'EscPosRaster') {
    fail('outputSettings.escPosDevice.rasterBarcodeHumanReadableFontSize', 'Raster 条码可读文字字号仅适用于 ESC/POS Raster 输出。')
  }

  return {
    outputTarget,
    allowRasterizedLines,
    imageThreshold,
    ...(deviceHalfCellWidthAt24 === undefined ? {} : { deviceHalfCellWidthAt24 }),
    ...(deviceFullCellWidthAt24 === undefined ? {} : { deviceFullCellWidthAt24 }),
    ...(deviceLineHeightAt24 === undefined ? {} : { deviceLineHeightAt24 }),
    deviceFontCalibrations,
    requireDeclaredNativeBounds,
    ...(copies === undefined ? {} : { copies }),
    ...(feedLines === undefined ? {} : { feedLines }),
    ...(cutAfterPrint === undefined ? {} : { cutAfterPrint }),
    ...(cutOptions === undefined ? {} : { cutOptions }),
    ...(job === undefined ? {} : { job }),
    ...(encoding === undefined ? {} : { encoding }),
    escPosDevice,
    tsplMedia: cloneTsplMedia(value.tsplMedia, 'outputSettings.tsplMedia'),
    zplMedia: cloneZplMedia(value.zplMedia, 'outputSettings.zplMedia'),
  }
}

/** 将模板和输出设置序列化为带版本的设计器项目 envelope。 */
export function serializeDesignerProject(template: BpTemplate, outputSettings?: DesignerOutputSettings): string {
  assertTemplate(template)
  const project: DesignerProject = {
    format: DESIGNER_PROJECT_FORMAT,
    version: 1,
    template: cloneTemplate(template),
    outputSettings: validateDesignerOutputSettings(outputSettings ?? defaultDesignerOutputSettings()),
  }
  return JSON.stringify(project, null, 2)
}

/** 解析新版项目 envelope，也兼容历史上直接保存的模板 JSON。 */
export function parseDesignerProject(value: unknown): { template: BpTemplate; outputSettings?: DesignerOutputSettings } {
  if (!isRecord(value)) {
    assertTemplate(value)
    return { template: cloneTemplate(value) }
  }

  const looksLikeEnvelope = 'format' in value || 'version' in value || 'template' in value || 'outputSettings' in value
  if (!looksLikeEnvelope) {
    assertTemplate(value)
    return { template: cloneTemplate(value) }
  }

  assertKnownKeys(value, ['format', 'version', 'template', 'outputSettings'], '设计器项目')
  if (value.format !== DESIGNER_PROJECT_FORMAT || value.version !== 1) throw new DesignerProjectValidationError('设计器项目版本不受支持。')
  if (!Object.prototype.hasOwnProperty.call(value, 'template')) throw new DesignerProjectValidationError('设计器项目缺少模板。')
  assertTemplate(value.template)
  return {
    template: cloneTemplate(value.template),
    ...(value.outputSettings === undefined ? {} : { outputSettings: validateDesignerOutputSettings(value.outputSettings) }),
  }
}
