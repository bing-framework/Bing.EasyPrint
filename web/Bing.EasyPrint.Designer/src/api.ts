import JSZip from 'jszip'
import { previewElementKey, type BpTemplate, type DesignerDiagnostic, type Gs1CompositeElement,
  type PreviewPage, type PreviewResponse, type TemplateResource } from './model'
import { normalizeDesignerCapabilities, type DesignerCapabilities } from './capabilities'
import { formatDesignerDiagnostic, normalizeDesignerDiagnostics } from './diagnostics'
import { stringifyJsonPreservingPrecision } from './losslessJson'
import {
  GS1_COMPOSITE_EAN_UPC_ADDONS_FEATURE, GS1_COMPOSITE_GS1_128_CC_AB_FEATURE, isGs1CompositeCcCCombination,
  isGs1CompositeExpandedCombination, isGs1CompositeExpandedStackedCombination, isGs1CompositeGs1128CcAbCombination,
  isGs1CompositeEanUpcCombination, isGs1CompositeRetailLinearType, isGs1CompositeStackedCombination,
  GS1_DATABAR_EXPANDED_STACKED_COLUMNS_FEATURE,
  DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE,
  GS1_COMPOSITE_STACKED_OMNIDIRECTIONAL_FEATURE, GS1_COMPOSITE_TRUNCATED_FEATURE,
  isGs1CompositeStackedOmnidirectionalCombination, isGs1CompositeTruncatedCombination,
  isValidGs1CompositeCombination, gs1CompositeRetailHasAddon, gs1CompositeRetailPrimaryError,
} from './gs1Composite'
import { walkTemplateNodes } from './templateTraversal'

export type DesignerSample = { id: string; name: string; template: BpTemplate; data: unknown }
export type PrintProtocol = 'Cpcl' | 'EscPos' | 'Tspl' | 'Zpl'
export type EscPosLayoutMode = 'Native' | 'Raster'
/** ESC/POS Native 的排版流模式；Rows 保持旧版按行输出行为。 */
export type EscPosNativeFlowMode = 'Rows' | 'Sequential'
export type PrintEncoding = 'Gbk' | 'Utf8' | 'Ascii' | 'Device'
export type DeviceFontCalibration = {
  halfCellWidthAt24: number; fullCellWidthAt24: number; lineHeightAt24: number
}
export type DeviceFontCalibrationMap = Record<string, DeviceFontCalibration>
export type DeviceFontCalibrationSets = {
  Tspl: DeviceFontCalibrationMap; Zpl: DeviceFontCalibrationMap
}
export type EscPosImageCommand = 'RasterGsV0' | 'BitImageEscStar'
export type EscPosBitImageMode = 'SingleDensity8' | 'DoubleDensity8' | 'SingleDensity24' | 'DoubleDensity24'
export type EscPosTextCharacterMode = 'DeviceDefault' | 'SingleByte' | 'FsKanji'
export const ESC_POS_BARCODE_TEXT_POSITION_NAMES = ['Above', 'Below', 'Both'] as const
export type EscPosBarcodeTextPositionName = typeof ESC_POS_BARCODE_TEXT_POSITION_NAMES[number]
/** 标签协议 profile 可声明的原生二维码非零旋转角度。 */
export const ROTATION_ANGLE_NAMES = ['Rotate90', 'Rotate180', 'Rotate270'] as const
export type RotationAngleName = typeof ROTATION_ANGLE_NAMES[number]
export const BARCODE_TYPE_NAMES = [
  'Code128', 'Code39', 'Code93', 'Codabar', 'Ean8', 'Ean13', 'UpcA', 'UpcE',
  'I2OF5', 'I2OF5C', 'I2OF5G', 'UccEan128', 'Msi', 'Postnet', 'Fim', 'Planet',
  'IntelligentMail', 'Plessey', 'Itf14', 'Ean14', 'Code11', 'ChinaPost', 'Telepen',
  'Telepenn', 'Code49', 'Gs1DataBarOmnidirectional', 'Gs1DataBarTruncated',
  'Gs1DataBarLimited', 'Gs1DataBarExpanded', 'Code128Auto', 'Gs1DataBarStacked', 'Gs1DataBarExpandedStacked',
  'Gs1DataBarStackedOmnidirectional',
] as const
export type BarcodeTypeName = typeof BARCODE_TYPE_NAMES[number]
export type EscPosCharacterRange = { first: number; last: number }
export type EscPosNativeTextProfile = {
  encodingCodePage: number
  codePageIndex?: number
  characterMode?: EscPosTextCharacterMode
  characterRanges: readonly EscPosCharacterRange[]
  halfCellWidthDots?: number
  fullCellWidthDots?: number
  lineHeightDots?: 24 | 26
}
export type EscPosRasterFontResource = {
  name: string
  version: string
  regularData: string
  boldData?: string
}
export type EscPosRasterFontsRequest = {
  defaultFont: string
  fallbackFonts?: readonly string[]
  fonts: readonly EscPosRasterFontResource[]
}
export type EscPosRasterFontCapability = {
  name: string
  version: string
  hasBold: boolean
}
export type EscPosRasterFontsCapability = {
  defaultFont: string
  fallbackFonts: readonly string[]
  fonts: readonly EscPosRasterFontCapability[]
}
export type EscPosPrinterProfile = {
  name: string
  imageCommand: EscPosImageCommand
  bitImageMode: EscPosBitImageMode
  horizontalDpi: number
  verticalDpi: number
  motionUnitsPerInch: number
  printableWidthDots?: number
  maxImageBlockBytes?: number
  supportsCut: boolean
  supportedCutModes?: readonly PrintCutMode[] | null
  supportsFeedAndCut?: boolean
  supportsNativeBarcode: boolean
  supportsNativeBarcodeHumanReadable?: boolean
  supportedNativeBarcodeHumanReadableTypes?: readonly BarcodeTypeName[] | null
  supportedNativeBarcodeHumanReadablePositions?: readonly EscPosBarcodeTextPositionName[] | null
  supportsNativeQrCode: boolean
  supportsPrintAreaWidth: boolean
  supportsTextSize: boolean
  unidirectionalBitImage: boolean
  text?: EscPosNativeTextProfile
}
export type EscPosDeviceRequest = {
  dpi?: number; codePageIndex?: number; printAreaWidthUnits?: number; nativeTabWidthDots?: number;
  rasterBarcodeHumanReadableFontSize?: number;
  nativeFlow?: EscPosNativeFlowMode;
  rasterFonts?: EscPosRasterFontsRequest;
  profile?: EscPosPrinterProfile
}

export type PrintJobCopyOrder = 'Collated' | 'Uncollated'
export type PrintJobScope = 'None' | 'EachPage' | 'EachCopy' | 'JobEnd'
export type PrintJobFeedUnit = 'Lines' | 'Dots' | 'MotionUnits'
export type PrintCutMode = 'Full' | 'Partial'
export type PrintCutOptions = {
  mode?: PrintCutMode
  feedToCutPosition?: boolean
  feedBeforeCutAmount?: number
  feedBeforeCutUnit?: PrintJobFeedUnit
}
/** 任务级份数、进纸和切纸策略；省略 job 时继续使用旧页面字段。 */
export type PrintJobOptions = {
  copies?: number
  copyOrder?: PrintJobCopyOrder
  feedScope?: PrintJobScope
  feedUnit?: PrintJobFeedUnit
  feedAmount?: number
  cutScope?: PrintJobScope
  cutOptions?: PrintCutOptions
}

/** 返回与 Core ESC/POS 配置一致的默认机型描述。 */
export function defaultEscPosPrinterProfile(): EscPosPrinterProfile {
  return {
    name: 'Custom',
    imageCommand: 'RasterGsV0',
    bitImageMode: 'DoubleDensity24',
    horizontalDpi: 203,
    verticalDpi: 203,
    motionUnitsPerInch: 203,
    supportsCut: true,
    supportsNativeBarcode: true,
    supportsNativeBarcodeHumanReadable: true,
    supportsNativeQrCode: true,
    supportsPrintAreaWidth: true,
    supportsTextSize: true,
    unidirectionalBitImage: false,
  }
}
export type LabelPrinterCharacterRange = { first: number; last: number }
export type LabelPrinterProfile = {
  name?: string
  firmwareVersion?: string
  printableWidthDots?: number
  maxLabelHeightDots?: number
  maxImageDataBytes?: number
  supportsImages?: boolean
  supportsGraphics?: boolean
  supportsNativeBarcode?: boolean
  supportsNativeBarcodeHumanReadable?: boolean
  supportedNativeBarcodeHumanReadableTypes?: BarcodeTypeName[]
  supportedNativeQrCodeRotations?: RotationAngleName[]
  supportsNativeQrCode?: boolean
  supportsRotation?: boolean
  deviceFontNames?: string[]
  encodingCodePages?: number[]
  characterRanges?: LabelPrinterCharacterRange[]
}
export type TsplCutStrategy = 'Immediate' | 'Batch'
/** TSPL 机型按手册和实测声明的切刀契约。未声明 cutter 时保持旧无切刀行为。 */
export type TsplCutterProfile = {
  strategy?: TsplCutStrategy
  supportedCutModes?: readonly PrintCutMode[]
  supportsEndOfJob: boolean
  cutPositionFeedDots?: number
}
export type TsplPrinterProfile = LabelPrinterProfile & {
  cutter?: TsplCutterProfile
}
export type ZplPrinterProfile = LabelPrinterProfile & {
  supportsCut?: boolean
  cutter?: ZplCutterProfile
}
export type ZplCutStrategy = 'Cutter' | 'Delayed' | 'Kiosk'
/** 显式确认设备模式及 Kiosk 裁切尺寸，不自动推断固件能力。 */
export type ZplCutterProfile = {
  strategy?: ZplCutStrategy
  supportedCutModes?: readonly PrintCutMode[]
  supportsTearOff: boolean
  kioskPartialUncutMillimeters?: number
  kioskCutMarginMillimeters?: number
}
export type TsplMediaRequest = {
  allowRasterizedTextStyles?: boolean;
  dpi?: number; gapDistance?: number; gapOffset?: number; direction?: number;
  referenceX?: number; referenceY?: number; speed?: number; density?: number;
  profile?: TsplPrinterProfile
}
export type ZplMediaOrientation = 'None' | 'Down'
export type ZplMediaTracking = 'N' | 'Y' | 'W' | 'M' | 'A' | 'V'
export type ZplMediaRequest = {
  allowRasterizedTextStyles?: boolean;
  dpi?: number; printSpeed?: number; orientation?: ZplMediaOrientation;
  mediaTracking?: ZplMediaTracking; darkness?: number;
  profile?: ZplPrinterProfile
}
export type ProtocolFinishingRequest = {
  feedAmount?: number
  feedUnit?: 'Lines' | 'Dots' | 'MotionUnits'
  cut?: boolean
  placement?: 'JobEnd' | 'PageEnd'
}
export type CustomProtocolRequest = { id: string; config: unknown; finishing?: ProtocolFinishingRequest }
export type PreviewRequest = { template: BpTemplate; data: unknown; resources?: Record<string, string>;
  protocolId?: string; protocolConfig?: unknown;
  protocol?: PrintProtocol; escPosLayoutMode?: EscPosLayoutMode; allowRasterizedLines?: boolean;
  imageThreshold?: number; deviceHalfCellWidthAt24?: number; deviceFullCellWidthAt24?: number;
  deviceLineHeightAt24?: number; requireDeclaredNativeBounds?: boolean;
  deviceFontCalibrations?: DeviceFontCalibrationMap;
  copies?: number; feedLines?: number; cutAfterPrint?: boolean;
  tsplMedia?: TsplMediaRequest; zplMedia?: ZplMediaRequest;
  encoding?: PrintEncoding; escPosDevice?: EscPosDeviceRequest; cutOptions?: PrintCutOptions; job?: PrintJobOptions
  validateGs1Associations?: boolean
  pageIndex?: number; elementIndex?: number; sourcePath?: string }

/** 能力查询只接受输出配置，不携带模板、业务数据或图片资源。 */
export type DesignerCapabilitiesRequest = {
  protocol?: PrintProtocol
  escPosLayoutMode?: EscPosLayoutMode
  encoding?: PrintEncoding
  escPosDevice?: EscPosDeviceRequest
  tsplMedia?: TsplMediaRequest
  zplMedia?: ZplMediaRequest
  allowRasterizedLines?: boolean
  protocolId?: string
  protocolConfig?: unknown
  /** 仅供客户端要求新的 Host 能力字段，不会序列化到请求体。 */
  requireCutContract?: boolean
}

/** 保留服务端诊断，供预览与导出共用定位；message 继续支持现有字符串调用方。 */
export class DesignerValidationError extends Error {
  readonly diagnostics: DesignerDiagnostic[]
  readonly status?: number

  constructor(message: string, diagnostics: DesignerDiagnostic[] = [], status?: number) {
    super(message)
    this.name = 'DesignerValidationError'
    this.diagnostics = normalizeDesignerDiagnostics(diagnostics)
    this.status = status
  }
}

function resourcesAsBase64(resources: TemplateResource[]): Record<string, string> | undefined {
  if (!resources.length) return undefined
  return Object.fromEntries(resources.map((resource) => [resource.key, resource.dataUrl.split(',')[1] ?? '']))
}

function compactOptions<T extends object>(value: T | undefined): T | undefined {
  if (!value) return undefined
  const entries = Object.entries(value).filter(([, item]) => item !== undefined)
  return entries.length ? Object.fromEntries(entries) as T : undefined
}

function profileRequestsCutContract(profile: EscPosPrinterProfile | undefined): boolean {
  return profile !== undefined && (
    Object.prototype.hasOwnProperty.call(profile, 'supportedCutModes') ||
    Object.prototype.hasOwnProperty.call(profile, 'supportsFeedAndCut'))
}

function profileRequestsTsplCutter(profile: TsplPrinterProfile | undefined): boolean {
  return profile !== undefined && profile.cutter !== undefined
}

function sameCutModes(left: readonly PrintCutMode[], right: readonly PrintCutMode[]): boolean {
  return left.length === right.length && left.every((mode) => right.includes(mode))
}

function requiresCutContract(cutOptions: PrintCutOptions | undefined, job: PrintJobOptions | undefined,
  escPosDevice: EscPosDeviceRequest | undefined): boolean {
  return cutOptions !== undefined || job?.cutOptions !== undefined || profileRequestsCutContract(escPosDevice?.profile)
}

function requiresTsplCutterContract(protocol: PrintProtocol, tsplMedia: TsplMediaRequest | undefined): boolean {
  return protocol === 'Tspl' && profileRequestsTsplCutter(tsplMedia?.profile)
}

async function ensureCutContract(protocol: PrintProtocol, escPosLayoutMode: EscPosLayoutMode,
  allowRasterizedLines: boolean, tsplMedia: TsplMediaRequest | undefined, zplMedia: ZplMediaRequest | undefined,
  encoding: PrintEncoding | undefined, escPosDevice: EscPosDeviceRequest | undefined,
  cutOptions: PrintCutOptions | undefined, job: PrintJobOptions | undefined, signal?: AbortSignal): Promise<void> {
  if (!requiresCutContract(cutOptions, job, escPosDevice) && !requiresTsplCutterContract(protocol, tsplMedia) &&
    !(protocol === 'Zpl' && zplMedia?.profile?.cutter !== undefined)) return
  await loadDesignerCapabilities({
    protocol, escPosLayoutMode, allowRasterizedLines, tsplMedia, zplMedia, encoding, escPosDevice,
    requireCutContract: true,
  }, signal)
}

async function responseError(response: Response): Promise<DesignerValidationError> {
  try {
    const body = await response.json() as { errors?: unknown; message?: unknown; title?: unknown } | null
    const diagnostics = normalizeDesignerDiagnostics(body?.errors)
    const message = diagnostics.length ? diagnostics.map(formatDesignerDiagnostic).join('\n')
      : typeof body?.message === 'string' && body.message ? body.message
        : typeof body?.title === 'string' && body.title ? body.title : `服务返回 HTTP ${response.status}`
    return new DesignerValidationError(message, diagnostics, response.status)
  } catch {
    return new DesignerValidationError(`服务返回 HTTP ${response.status}`, [], response.status)
  }
}

const DESIGNER_CONTRACT_VERSION = '1'
const DESIGNER_CONTRACT_VERSION_HEADER = 'X-EasyPrint-Contract-Version'
const DESIGNER_FEATURES_HEADER = 'X-EasyPrint-Features'
const DESIGNER_REQUIRED_FEATURES_HEADER = 'X-EasyPrint-Required-Features'
const GS1_COMPOSITE_CC_C_FEATURE = 'gs1-composite-cc-c'
const GS1_COMPOSITE_GS1_128_AB_FEATURE = GS1_COMPOSITE_GS1_128_CC_AB_FEATURE
const GS1_COMPOSITE_EXPANDED_FEATURE = 'gs1-composite-expanded'
const GS1_COMPOSITE_STACKED_FEATURE = 'gs1-composite-stacked'
const GS1_COMPOSITE_EXPANDED_STACKED_FEATURE = 'gs1-composite-expanded-stacked'
const GS1_COMPOSITE_EAN_UPC_FEATURE = 'gs1-composite-ean-upc'
const GS1_COMPOSITE_EAN_UPC_ADDONS_FEATURE_NAME = GS1_COMPOSITE_EAN_UPC_ADDONS_FEATURE
const GS1_COMPOSITE_TRUNCATED_FEATURE_NAME = GS1_COMPOSITE_TRUNCATED_FEATURE
const GS1_COMPOSITE_STACKED_OMNIDIRECTIONAL_FEATURE_NAME = GS1_COMPOSITE_STACKED_OMNIDIRECTIONAL_FEATURE
const GS1_DATABAR_EXPANDED_STACKED_ROWS_FEATURE = 'gs1-databar-expanded-stacked-rows'
const RASTER_ELEMENT_PNG_FEATURE = 'raster-element-png'
const RASTER_PAGE_PNG_FEATURE = 'raster-page-png'
const PAGE_PDF_EXPORT_FEATURE = 'page-pdf-export'
const BARCODE_SVG_EXPORT_FEATURE = 'barcode-svg-export'
const PREVIEW_BUNDLE_FEATURE = 'preview-bundle'
const PROTOCOL_REGISTRY_FEATURE = 'protocol-registry'
const GS1_TWO_DIMENSIONAL_FEATURE = 'gs1-two-dimensional'
const GS1_ASSOCIATION_VALIDATION_FEATURE = 'gs1-association-validation'
const FIELD_FORMAT_FEATURE = 'field-format'
const BATCH_EXPORT_FEATURE = 'batch-export'
const GS1_AI_CATALOG_FEATURE = 'gs1-ai-catalog'
const GS1_STRUCTURED_INPUT_FEATURE = 'gs1-structured-input'
const GS1_DIGITAL_LINK_FEATURE = 'gs1-digital-link'
const DESIGNER_REQUIRED_FEATURES = ['structured-errors', 'strict-options', 'binding-diagnostics'] as const

function clientValidationError(message: string, code: string, phase: string): DesignerValidationError {
  return new DesignerValidationError(message, [{ message, code, phase }])
}

function designerPostHeaders(requiredFeatures: string[]): Record<string, string> {
  return {
    'Content-Type': 'application/json',
    [DESIGNER_CONTRACT_VERSION_HEADER]: DESIGNER_CONTRACT_VERSION,
    [DESIGNER_REQUIRED_FEATURES_HEADER]: requiredFeatures.join(','),
  }
}

function isAbortError(error: unknown): boolean {
  return typeof error === 'object' && error !== null && 'name' in error && error.name === 'AbortError'
}

function validateDesignerContract(response: Response, requiredFeatures: string[]): void {
  if (response.headers.get(DESIGNER_CONTRACT_VERSION_HEADER)?.trim() !== DESIGNER_CONTRACT_VERSION) {
    throw clientValidationError('打印服务版本不兼容，请更新 Host。', 'ContractVersionMismatch', 'Contract')
  }

  const availableFeatures = new Set((response.headers.get(DESIGNER_FEATURES_HEADER) ?? '')
    .split(',').map((feature) => feature.trim()).filter((feature) => feature.length > 0))
  const missingFeatures = requiredFeatures.filter((feature) => !availableFeatures.has(feature))
  if (missingFeatures.length > 0) {
    if (missingFeatures.includes(GS1_COMPOSITE_CC_C_FEATURE)) {
      const message = '打印服务缺少 GS1-128 + CC-C Composite 功能，请更新 Host。'
      throw clientValidationError(message, 'ContractFeatureUnavailable', 'Contract')
    }
    if (missingFeatures.includes(GS1_COMPOSITE_GS1_128_AB_FEATURE)) {
      const message = '打印服务缺少 GS1-128 + CC-A/CC-B Composite 功能，请更新 Host。'
      throw clientValidationError(message, 'ContractFeatureUnavailable', 'Contract')
    }
    if (missingFeatures.includes(GS1_COMPOSITE_EXPANDED_FEATURE)) {
      const message = '打印服务缺少 GS1 DataBar Expanded Composite 功能，请更新 Host。'
      throw clientValidationError(message, 'ContractFeatureUnavailable', 'Contract')
    }
    if (missingFeatures.includes(GS1_COMPOSITE_STACKED_FEATURE)) {
      const message = '打印服务缺少 GS1 DataBar Stacked Composite 功能，请更新 Host。'
      throw clientValidationError(message, 'ContractFeatureUnavailable', 'Contract')
    }
    if (missingFeatures.includes(GS1_COMPOSITE_EXPANDED_STACKED_FEATURE)) {
      const message = '打印服务缺少 GS1 DataBar Expanded Stacked Composite 功能，请更新 Host。'
      throw clientValidationError(message, 'ContractFeatureUnavailable', 'Contract')
    }
    if (missingFeatures.includes(GS1_COMPOSITE_EAN_UPC_ADDONS_FEATURE_NAME)) {
      const message = '打印服务缺少 EAN/UPC Composite 附加码功能，请更新 Host。'
      throw clientValidationError(message, 'ContractFeatureUnavailable', 'Contract')
    }
    if (missingFeatures.includes(GS1_COMPOSITE_EAN_UPC_FEATURE)) {
      const message = '打印服务缺少 EAN/UPC Composite 功能，请更新 Host。'
      throw clientValidationError(message, 'ContractFeatureUnavailable', 'Contract')
    }
    if (missingFeatures.includes(GS1_COMPOSITE_TRUNCATED_FEATURE_NAME)) {
      const message = '打印服务缺少 GS1 DataBar Truncated Composite 功能，请更新 Host。'
      throw clientValidationError(message, 'ContractFeatureUnavailable', 'Contract')
    }
    if (missingFeatures.includes(GS1_COMPOSITE_STACKED_OMNIDIRECTIONAL_FEATURE_NAME)) {
      const message = '打印服务缺少 GS1 DataBar Stacked Omnidirectional Composite 功能，请更新 Host。'
      throw clientValidationError(message, 'ContractFeatureUnavailable', 'Contract')
    }
    if (missingFeatures.includes(GS1_DATABAR_EXPANDED_STACKED_COLUMNS_FEATURE)) {
      const message = '打印服务缺少 GS1 DataBar Expanded Stacked 自定义列数功能，请更新 Host。'
      throw clientValidationError(message, 'ContractFeatureUnavailable', 'Contract')
    }
    if (missingFeatures.includes(GS1_DATABAR_EXPANDED_STACKED_ROWS_FEATURE)) {
      const message = '打印服务缺少 GS1 DataBar Expanded Stacked 最大行数功能，请更新 Host。'
      throw clientValidationError(message, 'ContractFeatureUnavailable', 'Contract')
    }
    if (missingFeatures.includes(RASTER_ELEMENT_PNG_FEATURE)) {
      throw clientValidationError('打印服务缺少元素码图 PNG 功能，请更新 Host。',
        'ContractFeatureUnavailable', 'Contract')
    }
    if (missingFeatures.includes(RASTER_PAGE_PNG_FEATURE)) {
      throw clientValidationError('打印服务缺少完整页面 PNG 功能，请更新 Host。',
        'ContractFeatureUnavailable', 'Contract')
    }
    if (missingFeatures.includes(PROTOCOL_REGISTRY_FEATURE)) {
      throw clientValidationError('打印服务缺少新协议注册目录功能，请更新 Host。',
        'ContractFeatureUnavailable', 'Contract')
    }
    if (missingFeatures.includes(PREVIEW_BUNDLE_FEATURE)) {
      throw clientValidationError('打印服务缺少合并预览包功能，请更新 Host。',
        'ContractFeatureUnavailable', 'Contract')
    }
    throw clientValidationError('打印服务缺少所需功能，请更新 Host。',
      'ContractFeatureUnavailable', 'Contract')
  }
}

async function postDesignerJson(path: string, body: unknown, signal?: AbortSignal,
  additionalRequiredFeatures: string[] = []): Promise<Response> {
  const requiredFeatures: string[] = [...DESIGNER_REQUIRED_FEATURES]
  const template = (body as { template?: BpTemplate }).template
  const protocolId = (body as { protocolId?: string }).protocolId
  if (protocolId && !requiredFeatures.includes(PROTOCOL_REGISTRY_FEATURE))
    requiredFeatures.push(PROTOCOL_REGISTRY_FEATURE)
  if ((body as { validateGs1Associations?: boolean }).validateGs1Associations && !requiredFeatures.includes(GS1_ASSOCIATION_VALIDATION_FEATURE))
    requiredFeatures.push(GS1_ASSOCIATION_VALIDATION_FEATURE)
  if (template) walkTemplateNodes(template, (node) => {
    if (!node || typeof node !== 'object') return
    const source = node as { Field?: { Format?: unknown; Gs1Input?: { Mode?: number } };
      SecondaryField?: { Format?: unknown; Gs1Input?: { Mode?: number } } }
    if ((source.Field?.Format || source.SecondaryField?.Format) && !requiredFeatures.includes(FIELD_FORMAT_FEATURE))
      requiredFeatures.push(FIELD_FORMAT_FEATURE)
    const gs1Inputs = [source.Field?.Gs1Input, source.SecondaryField?.Gs1Input].filter(Boolean)
    if (gs1Inputs.length) {
      for (const feature of [GS1_AI_CATALOG_FEATURE, GS1_STRUCTURED_INPUT_FEATURE])
        if (!requiredFeatures.includes(feature)) requiredFeatures.push(feature)
      if (gs1Inputs.some(input => input?.Mode === 1) && !requiredFeatures.includes(GS1_DIGITAL_LINK_FEATURE))
        requiredFeatures.push(GS1_DIGITAL_LINK_FEATURE)
    }
  })
  if (template && !protocolId) walkTemplateNodes(template, (node, context) => {
    if (node && typeof node === 'object') {
      const source = node as { Field?: { Format?: unknown }; SecondaryField?: { Format?: unknown } }
      if (source.Field?.Format || source.SecondaryField?.Format) {
        if (!requiredFeatures.includes(FIELD_FORMAT_FEATURE)) requiredFeatures.push(FIELD_FORMAT_FEATURE)
      }
    }
    if (context.kind !== 'element' || !('Type' in node)) return
    const requestContract = body as { protocol?: PrintProtocol; escPosLayoutMode?: EscPosLayoutMode }
    const rawNode = node as Record<string, unknown>
    const columnAliases = Object.keys(rawNode).filter((key) =>
      key.toLowerCase() === 'expandedstackedcolumns' && key !== 'ExpandedStackedColumns')
    if (columnAliases.length) {
      const duplicate = Object.hasOwn(rawNode, 'ExpandedStackedColumns')
      const message = `${context.path}.ExpandedStackedColumns 属性名必须为 ExpandedStackedColumns${
        duplicate ? '，不能同时包含大小写不同的重复字段' : ''}。`
      throw clientValidationError(message, 'InvalidExpandedStackedColumns', 'Validation')
    }
    if ((node.Type === 3 || node.Type === 10) && (node as { Gs1?: boolean }).Gs1 === true) {
      if (node.Type === 10 && (node as { Format?: string }).Format !== 'DataMatrix')
        throw clientValidationError(`${context.path}.Gs1 仅支持 DataMatrix，PDF417 不能启用 Gs1。`, 'InvalidGs1TwoDimensional', 'Validation')
      if (!requiredFeatures.includes(GS1_TWO_DIMENSIONAL_FEATURE)) requiredFeatures.push(GS1_TWO_DIMENSIONAL_FEATURE)
    }
    if (node.Type === 4 || node.Type === 11) {
      const element = node as { Type: number; BarcodeType?: number; LinearType?: number; ExpandedStackedColumns?: number | null;
        ExpandedStackedRows?: number | null }
      if (element.ExpandedStackedColumns !== undefined && element.ExpandedStackedColumns !== null) {
        const minimum = element.Type === 4 && element.BarcodeType === 31 ? 1
          : element.Type === 11 && element.LinearType === 31 ? 2 : undefined
        if (minimum === undefined || !Number.isInteger(element.ExpandedStackedColumns) ||
          element.ExpandedStackedColumns < minimum || element.ExpandedStackedColumns > 11) {
          const message = `${context.path}.ExpandedStackedColumns 必须用于 Expanded Stacked 且为 ${minimum ?? 1}–11 的整数列数。`
          throw clientValidationError(message, 'InvalidExpandedStackedColumns', 'Validation')
        }
        if (element.Type === 4 && (requestContract.protocol !== 'EscPos' || requestContract.escPosLayoutMode !== 'Raster')) {
          const message = 'Expanded Stacked 普通条码列数配置仅支持 ESC/POS Raster。'
          throw clientValidationError(message, 'UnsupportedBarcodeType', 'Validation')
        }
        if (!requiredFeatures.includes(GS1_DATABAR_EXPANDED_STACKED_COLUMNS_FEATURE))
          requiredFeatures.push(GS1_DATABAR_EXPANDED_STACKED_COLUMNS_FEATURE)
      }
      const rowAliases = Object.keys(rawNode).filter((key) =>
        key.toLowerCase() === 'expandedstackedrows' && key !== 'ExpandedStackedRows')
      if (rowAliases.length) {
        const duplicate = Object.hasOwn(rawNode, 'ExpandedStackedRows')
        const message = `${context.path}.ExpandedStackedRows 属性名必须为 ExpandedStackedRows${
          duplicate ? '，不能同时包含大小写不同的重复字段' : ''}。`
        throw clientValidationError(message, 'InvalidExpandedStackedRows', 'Validation')
      }
      if (element.ExpandedStackedRows !== undefined && element.ExpandedStackedRows !== null) {
        const supported = element.Type === 4 && element.BarcodeType === 31 || element.Type === 11 && element.LinearType === 31
        if (!supported || !Number.isInteger(element.ExpandedStackedRows) ||
          element.ExpandedStackedRows < 2 || element.ExpandedStackedRows > 11) {
          throw clientValidationError(
            `${context.path}.ExpandedStackedRows 必须用于 Expanded Stacked 且为 2–11 的整数数据行上限。`,
            'InvalidExpandedStackedRows', 'Validation')
        }
        if (element.Type === 4 && (requestContract.protocol !== 'EscPos' || requestContract.escPosLayoutMode !== 'Raster')) {
          throw clientValidationError('Expanded Stacked 普通条码最大行数配置仅支持 ESC/POS Raster。',
            'UnsupportedBarcodeType', 'Validation')
        }
        if (!requiredFeatures.includes(GS1_DATABAR_EXPANDED_STACKED_ROWS_FEATURE))
          requiredFeatures.push(GS1_DATABAR_EXPANDED_STACKED_ROWS_FEATURE)
      }
      if (element.ExpandedStackedColumns != null && element.ExpandedStackedRows != null) {
        throw clientValidationError(
          `${context.path}.ExpandedStackedColumns 与 ExpandedStackedRows 互斥，不能同时指定。`,
          'InvalidExpandedStackedLayout', 'Validation')
      }
    }
    if (node.Type === 4 && (node as { BarcodeType?: number }).BarcodeType === DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE &&
      (requestContract.protocol !== 'EscPos' || requestContract.escPosLayoutMode !== 'Raster')) {
      const message = 'GS1 DataBar Stacked Omnidirectional 普通条码仅支持 ESC/POS Raster。'
      throw clientValidationError(message, 'UnsupportedBarcodeType', 'Validation')
    }
    if (node.Type !== 11) return
    const composite = node as Gs1CompositeElement
    if (!isValidGs1CompositeCombination(composite.LinearType, composite.Mode)) {
      const message = `${context.path}.LinearType 与 Mode 组合无效；EAN/UPC 与 DataBar 仅支持 Auto、CC-A 或 CC-B，GS1-128 支持 Auto、CC-A、CC-B 或 CC-C。`
      throw clientValidationError(message, 'InvalidGs1CompositeMode', 'Validation')
    }
    if (isGs1CompositeRetailLinearType(composite.LinearType) && !composite.Field?.Name && composite.Field?.Value !== undefined) {
      const error = gs1CompositeRetailPrimaryError(composite.LinearType, composite.Field.Value)
      if (error) throw clientValidationError(`${context.path}.Field.Value ${error}`,
        'InvalidGs1CompositePrimaryData', 'Validation')
    }
    if (!requiredFeatures.includes('gs1-composite')) requiredFeatures.push('gs1-composite')
    if (isGs1CompositeCcCCombination(composite.LinearType, composite.Mode) &&
      !requiredFeatures.includes(GS1_COMPOSITE_CC_C_FEATURE)) {
      requiredFeatures.push(GS1_COMPOSITE_CC_C_FEATURE)
    }
    if (isGs1CompositeGs1128CcAbCombination(composite.LinearType, composite.Mode) &&
      !requiredFeatures.includes(GS1_COMPOSITE_GS1_128_AB_FEATURE)) {
      requiredFeatures.push(GS1_COMPOSITE_GS1_128_AB_FEATURE)
    }
    if (isGs1CompositeExpandedCombination(composite.LinearType, composite.Mode) &&
      !requiredFeatures.includes(GS1_COMPOSITE_EXPANDED_FEATURE)) {
      requiredFeatures.push(GS1_COMPOSITE_EXPANDED_FEATURE)
    }
    if (isGs1CompositeStackedCombination(composite.LinearType, composite.Mode) &&
      !requiredFeatures.includes(GS1_COMPOSITE_STACKED_FEATURE)) {
      requiredFeatures.push(GS1_COMPOSITE_STACKED_FEATURE)
    }
    if (isGs1CompositeExpandedStackedCombination(composite.LinearType, composite.Mode) &&
      !requiredFeatures.includes(GS1_COMPOSITE_EXPANDED_STACKED_FEATURE)) {
      requiredFeatures.push(GS1_COMPOSITE_EXPANDED_STACKED_FEATURE)
    }
    if (isGs1CompositeEanUpcCombination(composite.LinearType, composite.Mode) &&
      !requiredFeatures.includes(GS1_COMPOSITE_EAN_UPC_FEATURE)) {
      requiredFeatures.push(GS1_COMPOSITE_EAN_UPC_FEATURE)
    }
    if (isGs1CompositeEanUpcCombination(composite.LinearType, composite.Mode) &&
      !composite.Field?.Name && gs1CompositeRetailHasAddon(composite.Field?.Value) &&
      !requiredFeatures.includes(GS1_COMPOSITE_EAN_UPC_ADDONS_FEATURE_NAME)) {
      requiredFeatures.push(GS1_COMPOSITE_EAN_UPC_ADDONS_FEATURE_NAME)
    }
    if (isGs1CompositeTruncatedCombination(composite.LinearType, composite.Mode) &&
      !requiredFeatures.includes(GS1_COMPOSITE_TRUNCATED_FEATURE_NAME)) {
      requiredFeatures.push(GS1_COMPOSITE_TRUNCATED_FEATURE_NAME)
    }
    if (isGs1CompositeStackedOmnidirectionalCombination(composite.LinearType, composite.Mode) &&
      !requiredFeatures.includes(GS1_COMPOSITE_STACKED_OMNIDIRECTIONAL_FEATURE_NAME)) {
      requiredFeatures.push(GS1_COMPOSITE_STACKED_OMNIDIRECTIONAL_FEATURE_NAME)
    }
  })
  for (const feature of additionalRequiredFeatures)
    if (!requiredFeatures.includes(feature)) requiredFeatures.push(feature)
  const serializedBody = stringifyJsonPreservingPrecision(body)
  let response: Response
  try {
    response = await fetch(path, {
      method: 'POST',
      headers: designerPostHeaders(requiredFeatures),
      body: serializedBody,
      signal,
    })
  } catch (error) {
    if (isAbortError(error)) throw error
    if (error instanceof TypeError) {
      throw clientValidationError('无法连接打印服务，请检查 Host 是否正在运行。', 'ServiceUnavailable', 'Transport')
    }
    throw error
  }

  if (!response.ok) throw await responseError(response)
  validateDesignerContract(response, requiredFeatures)
  return response
}

export type DesignerProtocolDescriptor = {
  id: string
  displayName: string
  isBuiltIn: boolean
  configSchema?: unknown
  constraints?: unknown
  capabilities: Record<string, unknown>
}

export type Gs1AiCatalogEntry = {
  ai: string
  title: string
  specification: string
  attributes: string
  predefinedLength: boolean
  requiresSeparator: boolean
  minimumLength: number
  maximumLength: number
  role: 'PrimaryKey' | 'KeyQualifier' | 'DataAttribute' | 'Unsupported'
  branches: string[][]
  components: Array<{ type: string; minimumLength: number; maximumLength: number; optional: boolean; linters: string[] }>
  requirementRules: Array<{ kind: string; raw: string; alternatives: string[][] }>
  exclusionRules: Array<{ kind: string; raw: string; alternatives: string[][] }>
}

export type Gs1AiCatalog = { dictionaryCommit: string; entries: Gs1AiCatalogEntry[] }
export type Gs1ValidationItem = { path: string; mode: string; status: 'valid' | 'pending-data' | 'invalid';
  output?: string; dynamic: boolean; instanceIndices?: number[] }
export type Gs1ValidationResponse = {
  status: 'valid' | 'pending' | 'invalid'
  dataProvided: boolean
  items: Gs1ValidationItem[]
  errors: Array<DesignerDiagnostic & { ai?: string | null; itemIndex?: number | null }>
}
export type Gs1ConversionRequest = {
  input: string
  targetFormat: 'elements' | 'digital-link'
  baseUri?: string
  fragment?: string
  validateAssociations?: boolean
  queryParameters?: Array<{ key: string; value?: string }>
}
export type Gs1ConversionResponse = {
  status: 'valid'
  sourceFormat: 'elements' | 'digital-link'
  targetFormat: 'elements' | 'digital-link'
  output: string
  structuredInput: {
    mode: 0 | 1
    baseUri?: string
    fragment?: string
    validateAssociations: boolean
    items: Array<{ ai: string; value: string; placement: 0 | 1 | 2 }>
    queryParameters: Array<{ key: string; value: string }>
  }
}

/** 读取 Host 启动时冻结的协议目录；旧 Host 不含该端点时会明确失败。 */
export async function loadProtocolRegistry(signal?: AbortSignal): Promise<DesignerProtocolDescriptor[]> {
  const response = await fetch('/api/designer/protocol-registry', {
    signal,
    headers: { [DESIGNER_CONTRACT_VERSION_HEADER]: DESIGNER_CONTRACT_VERSION },
  })
  validateDesignerContract(response, [PROTOCOL_REGISTRY_FEATURE])
  const result = await response.json() as { protocols?: unknown }
  if (!Array.isArray(result.protocols))
    throw new Error('协议目录服务返回的数据格式无效。')
  return result.protocols as DesignerProtocolDescriptor[]
}

/** 读取 Host 固定版本的完整 GS1 AI 目录；旧 Host 会因缺少特性而明确失败。 */
export async function loadGs1AiCatalog(signal?: AbortSignal): Promise<Gs1AiCatalog> {
  const requiredFeatures = [GS1_AI_CATALOG_FEATURE, GS1_STRUCTURED_INPUT_FEATURE, GS1_DIGITAL_LINK_FEATURE]
  const response = await fetch('/api/designer/gs1-catalog', {
    signal,
    headers: {
      [DESIGNER_CONTRACT_VERSION_HEADER]: DESIGNER_CONTRACT_VERSION,
      'X-EasyPrint-Required-Features': requiredFeatures.join(','),
    },
  })
  if (!response.ok) throw await responseError(response)
  validateDesignerContract(response, requiredFeatures)
  const result = await response.json() as Partial<Gs1AiCatalog>
  if (typeof result.dictionaryCommit !== 'string' || !Array.isArray(result.entries))
    throw new Error('GS1 AI 目录服务返回的数据格式无效。')
  return result as Gs1AiCatalog
}

/** 使用 Host 权威校验规则检查结构化 GS1 字段和当前绑定数据。 */
export async function validateGs1Input(template: BpTemplate, data: unknown,
  signal?: AbortSignal): Promise<Gs1ValidationResponse> {
  const response = await postDesignerJson('/api/designer/gs1-validate', { template, data }, signal,
    [GS1_AI_CATALOG_FEATURE, GS1_STRUCTURED_INPUT_FEATURE, GS1_DIGITAL_LINK_FEATURE])
  return await response.json() as Gs1ValidationResponse
}

/** 在括号 AI 元素串与 Digital Link 之间转换并取得可写回模板的结构数据。 */
export async function convertGs1Input(request: Gs1ConversionRequest,
  signal?: AbortSignal): Promise<Gs1ConversionResponse> {
  const response = await postDesignerJson('/api/designer/gs1-convert', { gs1Conversion: request }, signal,
    [GS1_AI_CATALOG_FEATURE, GS1_STRUCTURED_INPUT_FEATURE, GS1_DIGITAL_LINK_FEATURE])
  return await response.json() as Gs1ConversionResponse
}

export async function loadDesignerCapabilities(request: DesignerCapabilitiesRequest, signal?: AbortSignal): Promise<DesignerCapabilities> {
  if (request.protocolId) {
    const response = await postDesignerJson('/api/designer/capabilities', {
      protocolId: request.protocolId,
      ...(request.protocolConfig === undefined ? {} : { protocolConfig: request.protocolConfig }),
    } as DesignerCapabilitiesRequest, signal)
    const customCapabilities = await response.json() as unknown
    if (!customCapabilities || typeof customCapabilities !== 'object' || Array.isArray(customCapabilities))
      throw new Error('能力服务返回的数据格式无效。')
    const capabilities = normalizeDesignerCapabilities({
      ...(customCapabilities as Record<string, unknown>), protocol: 'Cpcl',
    })
    capabilities.protocol = null
    capabilities.protocolId = request.protocolId
    capabilities.supportsFinishing = (customCapabilities as { supportsFinishing?: unknown }).supportsFinishing === true
    const availableFeatures = new Set((response.headers.get(DESIGNER_FEATURES_HEADER) ?? '')
      .split(',').map(feature => feature.trim()))
    capabilities.rasterElementPngFeature = availableFeatures.has(RASTER_ELEMENT_PNG_FEATURE)
    capabilities.rasterPagePngFeature = availableFeatures.has(RASTER_PAGE_PNG_FEATURE)
    capabilities.pagePdfExportFeature = availableFeatures.has(PAGE_PDF_EXPORT_FEATURE)
    capabilities.barcodeSvgExportFeature = availableFeatures.has(BARCODE_SVG_EXPORT_FEATURE)
    capabilities.previewBundleFeature = availableFeatures.has(PREVIEW_BUNDLE_FEATURE)
    return capabilities
  }
  const body: DesignerCapabilitiesRequest = {
    ...(request.protocol === undefined ? {} : { protocol: request.protocol }),
    ...(request.escPosLayoutMode === undefined ? {} : { escPosLayoutMode: request.escPosLayoutMode }),
    ...(request.encoding === undefined ? {} : { encoding: request.encoding }),
    ...(request.allowRasterizedLines === undefined ? {} : { allowRasterizedLines: request.allowRasterizedLines }),
    ...(compactOptions(request.escPosDevice) === undefined ? {} : { escPosDevice: compactOptions(request.escPosDevice) }),
    ...(compactOptions(request.tsplMedia) === undefined ? {} : { tsplMedia: compactOptions(request.tsplMedia) }),
    ...(compactOptions(request.zplMedia) === undefined ? {} : { zplMedia: compactOptions(request.zplMedia) }),
    ...(request.protocolId === undefined ? {} : { protocolId: request.protocolId }),
    ...(request.protocolConfig === undefined ? {} : { protocolConfig: request.protocolConfig }),
  }
  const response = await postDesignerJson('/api/designer/capabilities', body, signal)
  const rawCapabilities = await response.json() as unknown
  const tsplCutterProfileRequested = request.protocol === 'Tspl' && profileRequestsTsplCutter(request.tsplMedia?.profile)
  const tsplCutterRequested = tsplCutterProfileRequested || (request.protocol === 'Tspl' && request.requireCutContract === true)
  const zplCutterProfileRequested = request.protocol === 'Zpl' && request.zplMedia?.profile?.cutter !== undefined
  const zplCutterRequested = zplCutterProfileRequested || (request.protocol === 'Zpl' && request.requireCutContract === true)
  const escPosCutRequested = request.protocol === 'EscPos' &&
    (profileRequestsCutContract(request.escPosDevice?.profile) || request.requireCutContract === true)
  if ((escPosCutRequested) &&
    (!rawCapabilities || typeof rawCapabilities !== 'object' || Array.isArray(rawCapabilities) ||
    !Object.prototype.hasOwnProperty.call(rawCapabilities, 'cutModes') ||
    !Object.prototype.hasOwnProperty.call(rawCapabilities, 'cutFeedUnits') ||
    !Object.prototype.hasOwnProperty.call(rawCapabilities, 'cutFeedMotionUnits'))) {
    throw new Error('能力服务未返回 ESC/POS 裁切能力字段，请更新 Host。')
  }
  if (tsplCutterRequested &&
    (!rawCapabilities || typeof rawCapabilities !== 'object' || Array.isArray(rawCapabilities) ||
    !Object.prototype.hasOwnProperty.call(rawCapabilities, 'tsplCutter') ||
    !Object.prototype.hasOwnProperty.call(rawCapabilities, 'cutModes') ||
    !Object.prototype.hasOwnProperty.call(rawCapabilities, 'cutFeedUnits') ||
    !Object.prototype.hasOwnProperty.call(rawCapabilities, 'cutFeedDots'))) {
    throw new Error('能力服务未返回 TSPL 裁切能力字段，请更新 Host。')
  }
  if (zplCutterRequested &&
    (!rawCapabilities || typeof rawCapabilities !== 'object' || Array.isArray(rawCapabilities) ||
    !Object.prototype.hasOwnProperty.call(rawCapabilities, 'zplCutter') ||
    !Object.prototype.hasOwnProperty.call(rawCapabilities, 'cutModes') ||
    !Object.prototype.hasOwnProperty.call(rawCapabilities, 'supportsIndependentOutputSegments'))) {
    throw new Error('能力服务未返回 ZPL 裁切及独立输出能力字段，请更新 Host。')
  }
  const capabilities = normalizeDesignerCapabilities(rawCapabilities)
  if (zplCutterProfileRequested) {
    const requested = request.zplMedia?.profile?.cutter
    const actual = capabilities.zplCutter
    if (!actual || actual.strategy !== (requested?.strategy ?? 'Cutter') ||
      !sameCutModes(actual.supportedCutModes, requested?.supportedCutModes ?? ['Full']) ||
      actual.supportsTearOff !== true || actual.kioskPartialUncutMillimeters !== (requested?.kioskPartialUncutMillimeters ?? null) ||
      actual.kioskCutMarginMillimeters !== (requested?.kioskCutMarginMillimeters ?? null)) {
      throw new Error('能力服务返回的 ZPL cutter 与请求配置不一致，请更新 Host。')
    }
  }
  if (tsplCutterProfileRequested) {
    const requested = request.tsplMedia?.profile?.cutter
    const actual = capabilities.tsplCutter
    const requestedStrategy = requested?.strategy ?? 'Batch'
    const requestedModes: PrintCutMode[] = requested?.supportedCutModes === undefined ? ['Full'] : [...requested.supportedCutModes]
    const requestedFeedDots = requested?.cutPositionFeedDots ?? null
    if (!actual || actual.strategy !== requestedStrategy || !sameCutModes(actual.supportedCutModes, requestedModes) ||
      actual.supportsEndOfJob !== true || actual.cutPositionFeedDots !== requestedFeedDots) {
      throw new Error('能力服务返回的 TSPL cutter 与请求配置不一致，请更新 Host。')
    }
  }
  const labelProfileRequested = request.tsplMedia?.profile !== undefined || request.zplMedia?.profile !== undefined
  if (labelProfileRequested && capabilities.labelProfile == null) {
    throw new Error('能力服务未返回标签机型配置，请更新 Host。')
  }
  const features = new Set((response.headers.get(DESIGNER_FEATURES_HEADER) ?? '').split(',')
    .map((feature) => feature.trim()).filter((feature) => feature.length > 0))
  capabilities.gs1CompositeFeature = features.has('gs1-composite')
  capabilities.gs1CompositeCcCFeature = features.has(GS1_COMPOSITE_CC_C_FEATURE)
  capabilities.gs1CompositeExpandedFeature = features.has(GS1_COMPOSITE_EXPANDED_FEATURE)
  capabilities.gs1CompositeStackedFeature = features.has(GS1_COMPOSITE_STACKED_FEATURE)
  capabilities.gs1CompositeExpandedStackedFeature = features.has(GS1_COMPOSITE_EXPANDED_STACKED_FEATURE)
  capabilities.gs1CompositeGs1128CcAbFeature = features.has(GS1_COMPOSITE_GS1_128_AB_FEATURE)
  capabilities.gs1CompositeEanUpcFeature = features.has(GS1_COMPOSITE_EAN_UPC_FEATURE)
  capabilities.gs1CompositeEanUpcAddonsFeature = features.has(GS1_COMPOSITE_EAN_UPC_ADDONS_FEATURE_NAME)
  capabilities.gs1CompositeTruncatedFeature = features.has(GS1_COMPOSITE_TRUNCATED_FEATURE_NAME)
  capabilities.gs1CompositeStackedOmnidirectionalFeature = features.has(GS1_COMPOSITE_STACKED_OMNIDIRECTIONAL_FEATURE_NAME)
  capabilities.expandedStackedColumnsFeature = features.has(GS1_DATABAR_EXPANDED_STACKED_COLUMNS_FEATURE)
  capabilities.expandedStackedRowsFeature = features.has(GS1_DATABAR_EXPANDED_STACKED_ROWS_FEATURE)
  capabilities.rasterElementPngFeature = features.has(RASTER_ELEMENT_PNG_FEATURE)
  capabilities.rasterPagePngFeature = features.has(RASTER_PAGE_PNG_FEATURE)
  capabilities.pagePdfExportFeature = features.has(PAGE_PDF_EXPORT_FEATURE)
  capabilities.barcodeSvgExportFeature = features.has(BARCODE_SVG_EXPORT_FEATURE)
  capabilities.previewBundleFeature = features.has(PREVIEW_BUNDLE_FEATURE)
  capabilities.gs1TwoDimensionalFeature = features.has(GS1_TWO_DIMENSIONAL_FEATURE)
  capabilities.gs1AssociationValidationFeature = features.has(GS1_ASSOCIATION_VALIDATION_FEATURE)
  return capabilities
}

export async function loadDesignerSamples(signal?: AbortSignal): Promise<DesignerSample[]> {
  const response = await fetch('/api/designer/samples', { signal })
  if (!response.ok) throw await responseError(response)
  const body = await response.json() as { samples?: DesignerSample[] }
  if (!Array.isArray(body.samples)) throw new Error('样例服务返回的数据格式无效。')
  return body.samples
}

export async function previewTemplate(template: BpTemplate, data: unknown, resources: TemplateResource[], signal?: AbortSignal,
  protocol: PrintProtocol = 'Cpcl', escPosLayoutMode: EscPosLayoutMode = 'Native',
  allowRasterizedLines = false, imageThreshold?: number, deviceHalfCellWidthAt24?: number,
  deviceFullCellWidthAt24?: number, copies?: number, feedLines?: number, cutAfterPrint?: boolean,
  tsplMedia?: TsplMediaRequest, zplMedia?: ZplMediaRequest,
  encoding?: PrintEncoding, escPosDevice?: EscPosDeviceRequest,
  deviceLineHeightAt24?: number, requireDeclaredNativeBounds?: boolean,
  deviceFontCalibrations?: DeviceFontCalibrationMap, job?: PrintJobOptions,
  cutOptions?: PrintCutOptions, validateGs1Associations = false,
  customProtocol?: CustomProtocolRequest): Promise<PreviewResponse> {
  if (!customProtocol) await ensureCutContract(protocol, escPosLayoutMode, allowRasterizedLines, tsplMedia, zplMedia,
    encoding, escPosDevice, cutOptions, job, signal)
  const body = buildPreviewBody(template, data, resources, protocol, escPosLayoutMode, allowRasterizedLines,
    imageThreshold, deviceHalfCellWidthAt24, deviceFullCellWidthAt24, copies, feedLines, cutAfterPrint,
    tsplMedia, zplMedia, encoding, escPosDevice, deviceLineHeightAt24, requireDeclaredNativeBounds,
    deviceFontCalibrations, job, cutOptions, validateGs1Associations, customProtocol)
  const response = await postDesignerJson('/api/designer/preview', body, signal)
  const result = await response.json() as PreviewResponse
  if (!Array.isArray(result.pages)) throw new Error('预览服务返回的数据格式无效。')
  return { ...result, errors: normalizeDesignerDiagnostics(result.errors) }
}

export function buildPreviewBody(template: BpTemplate, data: unknown, resources: TemplateResource[],
  protocol: PrintProtocol, escPosLayoutMode: EscPosLayoutMode, allowRasterizedLines: boolean,
  imageThreshold?: number, deviceHalfCellWidthAt24?: number, deviceFullCellWidthAt24?: number,
  copies?: number, feedLines?: number, cutAfterPrint?: boolean, tsplMedia?: TsplMediaRequest,
  zplMedia?: ZplMediaRequest, encoding?: PrintEncoding, escPosDevice?: EscPosDeviceRequest,
  deviceLineHeightAt24?: number, requireDeclaredNativeBounds?: boolean,
  deviceFontCalibrations?: DeviceFontCalibrationMap, job?: PrintJobOptions,
  cutOptions?: PrintCutOptions, validateGs1Associations = false,
  customProtocol?: CustomProtocolRequest): PreviewRequest {
  if (customProtocol) {
    return {
      template, data, resources: resourcesAsBase64(resources), protocolId: customProtocol.id,
      protocolConfig: customProtocol.config,
      ...(customProtocol.finishing === undefined ? {} : { finishing: customProtocol.finishing }),
      ...(imageThreshold === undefined ? {} : { imageThreshold }),
      ...(copies === undefined ? {} : { copies }),
      ...(feedLines === undefined ? {} : { feedLines }),
      ...(cutAfterPrint === undefined ? {} : { cutAfterPrint }),
    }
  }
  const compactTsplMedia = compactOptions(tsplMedia)
  const compactZplMedia = compactOptions(zplMedia)
  const compactEscPosDevice = compactOptions(escPosDevice)
  const compactJob = job === undefined ? undefined : compactOptions(job) ?? {}
  return {
    template, data, resources: resourcesAsBase64(resources), protocol, escPosLayoutMode, allowRasterizedLines,
    ...(imageThreshold === undefined ? {} : { imageThreshold }),
    ...(deviceHalfCellWidthAt24 === undefined ? {} : { deviceHalfCellWidthAt24 }),
    ...(deviceFullCellWidthAt24 === undefined ? {} : { deviceFullCellWidthAt24 }),
    ...(deviceLineHeightAt24 === undefined ? {} : { deviceLineHeightAt24 }),
    ...(requireDeclaredNativeBounds === undefined ? {} : { requireDeclaredNativeBounds }),
    ...(deviceFontCalibrations === undefined ? {} : { deviceFontCalibrations }),
    ...(copies === undefined ? {} : { copies }),
    ...(feedLines === undefined ? {} : { feedLines }),
    ...(cutAfterPrint === undefined ? {} : { cutAfterPrint }),
    ...(compactTsplMedia === undefined ? {} : { tsplMedia: compactTsplMedia }),
    ...(compactZplMedia === undefined ? {} : { zplMedia: compactZplMedia }),
    ...(encoding === undefined ? {} : { encoding }),
    ...(compactEscPosDevice === undefined ? {} : { escPosDevice: compactEscPosDevice }),
    ...(compactOptions(cutOptions) === undefined ? {} : { cutOptions: compactOptions(cutOptions) }),
    ...(compactJob === undefined ? {} : { job: compactJob }),
    ...(validateGs1Associations ? { validateGs1Associations: true } : {}),
  }
}

export async function renderTemplate(template: BpTemplate, data: unknown, resources: TemplateResource[],
  protocol: PrintProtocol = 'Cpcl', escPosLayoutMode: EscPosLayoutMode = 'Native',
  allowRasterizedLines = false, imageThreshold?: number, deviceHalfCellWidthAt24?: number,
  deviceFullCellWidthAt24?: number, copies?: number, feedLines?: number, cutAfterPrint?: boolean,
  tsplMedia?: TsplMediaRequest, zplMedia?: ZplMediaRequest,
  encoding?: PrintEncoding, escPosDevice?: EscPosDeviceRequest,
  deviceLineHeightAt24?: number, requireDeclaredNativeBounds?: boolean,
  deviceFontCalibrations?: DeviceFontCalibrationMap, job?: PrintJobOptions,
  cutOptions?: PrintCutOptions, validateGs1Associations = false,
  customProtocol?: CustomProtocolRequest): Promise<Blob> {
  if (!customProtocol) await ensureCutContract(protocol, escPosLayoutMode, allowRasterizedLines, tsplMedia, zplMedia,
    encoding, escPosDevice, cutOptions, job)
  const body = buildPreviewBody(template, data, resources, protocol, escPosLayoutMode, allowRasterizedLines,
    imageThreshold, deviceHalfCellWidthAt24, deviceFullCellWidthAt24, copies, feedLines, cutAfterPrint,
    tsplMedia, zplMedia, encoding, escPosDevice, deviceLineHeightAt24, requireDeclaredNativeBounds,
    deviceFontCalibrations, job, cutOptions, validateGs1Associations, customProtocol)
  const response = await postDesignerJson('/api/designer/render', body)
  return response.blob()
}

type PreviewBundleImage = {
  key: string
  file: string
  pageIndex: number
  elementIndex: number
  type: number
  sourcePath: string
  width: number
  height: number
}

type PreviewBundleManifest = {
  layout: { pageIndex: number; pageCount: number; pages: PreviewPage[] }
  images: PreviewBundleImage[]
  diagnostics?: unknown
}

function throwIfAborted(signal?: AbortSignal): void {
  if (signal?.aborted) throw new DOMException('Aborted', 'AbortError')
}

function isPreviewBundleManifest(value: unknown): value is PreviewBundleManifest {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return false
  const manifest = value as Record<string, unknown>
  if (!manifest.layout || typeof manifest.layout !== 'object' || Array.isArray(manifest.layout)) return false
  const layout = manifest.layout as Record<string, unknown>
  return Number.isInteger(layout.pageIndex) && Number.isInteger(layout.pageCount) &&
    Array.isArray(layout.pages) && Array.isArray(manifest.images)
}

async function readPreviewBundle(response: Response, signal?: AbortSignal):
  Promise<PreviewResponse & { images: Record<string, string>; imageDiagnostics: DesignerDiagnostic[] }> {
  throwIfAborted(signal)
  let archive: JSZip
  try {
    archive = await JSZip.loadAsync(await response.arrayBuffer(), { checkCRC32: false, createFolders: false })
  } catch {
    throw new Error('预览包不是有效的 ZIP 数据。')
  }
  throwIfAborted(signal)
  const manifestEntry = archive.file('manifest.json')
  if (!manifestEntry) throw new Error('预览包缺少 manifest.json。')
  let manifest: unknown
  try {
    manifest = JSON.parse(await manifestEntry.async('string')) as unknown
  } catch {
    throw new Error('预览包 manifest.json 不是有效 JSON。')
  }
  if (!isPreviewBundleManifest(manifest)) throw new Error('预览包清单格式无效。')
  const images: Record<string, string> = {}
  try {
    for (const image of manifest.images) {
      throwIfAborted(signal)
      if (!image || typeof image.key !== 'string' || typeof image.file !== 'string')
        throw new Error('预览包图片清单格式无效。')
      const entry = archive.file(image.file)
      if (!entry) throw new Error(`预览包缺少图片 ${image.file}。`)
      const bytes = await entry.async('uint8array')
      throwIfAborted(signal)
      const pngBytes = bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength) as ArrayBuffer
      images[image.key] = URL.createObjectURL(new Blob([pngBytes], { type: 'image/png' }))
    }
    throwIfAborted(signal)
    return {
      pages: manifest.layout.pages,
      errors: [],
      imageDiagnostics: normalizeDesignerDiagnostics(manifest.diagnostics),
      images,
    }
  } catch (error) {
    for (const url of Object.values(images)) URL.revokeObjectURL(url)
    throw error
  }
}

export async function previewWithImages(template: BpTemplate, data: unknown, resources: TemplateResource[],
  signal?: AbortSignal, protocol: PrintProtocol = 'Cpcl', escPosLayoutMode: EscPosLayoutMode = 'Native',
  allowRasterizedLines = false, imageThreshold?: number, deviceHalfCellWidthAt24?: number,
  deviceFullCellWidthAt24?: number, copies?: number, feedLines?: number, cutAfterPrint?: boolean,
  tsplMedia?: TsplMediaRequest, zplMedia?: ZplMediaRequest, encoding?: PrintEncoding,
  escPosDevice?: EscPosDeviceRequest, deviceLineHeightAt24?: number,
  requireDeclaredNativeBounds?: boolean, deviceFontCalibrations?: DeviceFontCalibrationMap,
  job?: PrintJobOptions, cutOptions?: PrintCutOptions, imageElementTypes: number[] = [],
  validateGs1Associations = false, pageIndex?: number, previewBundleFeature = false,
  customProtocol?: CustomProtocolRequest):
  Promise<PreviewResponse & { images: Record<string, string>; imageDiagnostics?: DesignerDiagnostic[] }> {
  if (!customProtocol) await ensureCutContract(protocol, escPosLayoutMode, allowRasterizedLines, tsplMedia, zplMedia,
    encoding, escPosDevice, cutOptions, job, signal)
  const body = buildPreviewBody(template, data, resources, protocol, escPosLayoutMode, allowRasterizedLines,
    imageThreshold, deviceHalfCellWidthAt24, deviceFullCellWidthAt24, copies, feedLines, cutAfterPrint,
    tsplMedia, zplMedia, encoding, escPosDevice, deviceLineHeightAt24, requireDeclaredNativeBounds,
    deviceFontCalibrations, job, cutOptions, validateGs1Associations, customProtocol)
  if (previewBundleFeature) {
    const response = await postDesignerJson('/api/designer/preview-bundle',
      { ...body, pageIndex: pageIndex ?? 0, imageTypes: imageElementTypes }, signal, [PREVIEW_BUNDLE_FEATURE])
    return readPreviewBundle(response, signal)
  }
  const response = await postDesignerJson('/api/designer/preview', body, signal)
  const result = await response.json() as PreviewResponse
  if (!Array.isArray(result.pages)) throw new Error('预览服务返回的数据格式无效。')
  if (pageIndex !== undefined && (pageIndex < 0 || pageIndex >= result.pages.length)) {
    const message = '预览服务返回的页面索引无效。'
    throw new DesignerValidationError(message, [{ message, code: 'InvalidPageIndex', phase: 'Validation' }])
  }
  const images: Record<string, string> = {}
  try {
    const pageIndexes = pageIndex === undefined ? result.pages.map((_, index) => index) : [pageIndex]
    for (const targetPageIndex of pageIndexes) {
      const page = result.pages[targetPageIndex]!
      for (let elementIndex = 0; elementIndex < page.elements.length; elementIndex++) {
        const element = page.elements[elementIndex]!
        if (!imageElementTypes.includes(element.type)) continue
        try {
          const imageResponse = await postDesignerJson('/api/designer/element-png',
            { ...body, pageIndex: targetPageIndex, elementIndex, sourcePath: element.sourcePath }, signal,
            [RASTER_ELEMENT_PNG_FEATURE])
          const blob = await imageResponse.blob()
          images[previewElementKey(targetPageIndex, element)] = URL.createObjectURL(blob)
        } catch (error) {
          if (signal?.aborted || (error instanceof DOMException && error.name === 'AbortError')) throw error
          // A single unsupported element must not blank the whole layout preview.
        }
      }
    }
    if (signal?.aborted) throw new DOMException('Aborted', 'AbortError')
    return { ...result, errors: normalizeDesignerDiagnostics(result.errors), images, imageDiagnostics: [] }
  } catch (error) {
    for (const url of Object.values(images)) URL.revokeObjectURL(url)
    throw error
  }
}

export async function elementPng(template: BpTemplate, data: unknown, resources: TemplateResource[],
  signal: AbortSignal | undefined, protocol: PrintProtocol, escPosLayoutMode: EscPosLayoutMode,
  allowRasterizedLines: boolean, imageThreshold: number | undefined,
  deviceHalfCellWidthAt24: number | undefined, deviceFullCellWidthAt24: number | undefined,
  copies: number | undefined, feedLines: number | undefined, cutAfterPrint: boolean | undefined,
  tsplMedia: TsplMediaRequest | undefined, zplMedia: ZplMediaRequest | undefined,
  encoding: PrintEncoding | undefined, escPosDevice: EscPosDeviceRequest | undefined,
  deviceLineHeightAt24: number | undefined, requireDeclaredNativeBounds: boolean | undefined,
  deviceFontCalibrations: DeviceFontCalibrationMap | undefined, job: PrintJobOptions | undefined,
  cutOptions: PrintCutOptions | undefined, pageIndex: number, elementIndex: number,
  sourcePath?: string, validateGs1Associations = false,
  customProtocol?: CustomProtocolRequest): Promise<Blob> {
  if (!customProtocol) await ensureCutContract(protocol, escPosLayoutMode, allowRasterizedLines, tsplMedia, zplMedia,
    encoding, escPosDevice, cutOptions, job, signal)
  const body = buildPreviewBody(template, data, resources, protocol, escPosLayoutMode, allowRasterizedLines,
    imageThreshold, deviceHalfCellWidthAt24, deviceFullCellWidthAt24, copies, feedLines, cutAfterPrint,
    tsplMedia, zplMedia, encoding, escPosDevice, deviceLineHeightAt24, requireDeclaredNativeBounds,
    deviceFontCalibrations, job, cutOptions, validateGs1Associations, customProtocol)
  const response = await postDesignerJson('/api/designer/element-png',
    { ...body, pageIndex, elementIndex, ...(sourcePath === undefined ? {} : { sourcePath }) }, signal,
    [RASTER_ELEMENT_PNG_FEATURE])
  return response.blob()
}

export async function pagePng(template: BpTemplate, data: unknown, resources: TemplateResource[],
  signal: AbortSignal | undefined, protocol: PrintProtocol, escPosLayoutMode: EscPosLayoutMode,
  allowRasterizedLines: boolean, imageThreshold: number | undefined,
  deviceHalfCellWidthAt24: number | undefined, deviceFullCellWidthAt24: number | undefined,
  copies: number | undefined, feedLines: number | undefined, cutAfterPrint: boolean | undefined,
  tsplMedia: TsplMediaRequest | undefined, zplMedia: ZplMediaRequest | undefined,
  encoding: PrintEncoding | undefined, escPosDevice: EscPosDeviceRequest | undefined,
  deviceLineHeightAt24: number | undefined, requireDeclaredNativeBounds: boolean | undefined,
  deviceFontCalibrations: DeviceFontCalibrationMap | undefined, job: PrintJobOptions | undefined,
  cutOptions: PrintCutOptions | undefined, pageIndex?: number, validateGs1Associations = false,
  customProtocol?: CustomProtocolRequest): Promise<Blob> {
  if (!customProtocol) await ensureCutContract(protocol, escPosLayoutMode, allowRasterizedLines, tsplMedia, zplMedia,
    encoding, escPosDevice, cutOptions, job, signal)
  const body = buildPreviewBody(template, data, resources, protocol, escPosLayoutMode, allowRasterizedLines,
    imageThreshold, deviceHalfCellWidthAt24, deviceFullCellWidthAt24, copies, feedLines, cutAfterPrint,
    tsplMedia, zplMedia, encoding, escPosDevice, deviceLineHeightAt24, requireDeclaredNativeBounds,
    deviceFontCalibrations, job, cutOptions, validateGs1Associations, customProtocol)
  const response = await postDesignerJson('/api/designer/page-png',
    { ...body, ...(pageIndex === undefined ? {} : { pageIndex }) }, signal, [RASTER_PAGE_PNG_FEATURE])
  return response.blob()
}

export async function pagePdf(baseBody: Record<string, unknown>, pageIndex?: number,
  signal?: AbortSignal): Promise<Blob> {
  const response = await postDesignerJson('/api/designer/page-pdf',
    { ...baseBody, ...(pageIndex === undefined ? {} : { pageIndex }) }, signal, [PAGE_PDF_EXPORT_FEATURE])
  return response.blob()
}

export async function elementSvg(baseBody: Record<string, unknown>, pageIndex: number, elementIndex: number,
  sourcePath: string, signal?: AbortSignal): Promise<Blob> {
  const response = await postDesignerJson('/api/designer/element-svg',
    { ...baseBody, pageIndex, elementIndex, sourcePath }, signal, [BARCODE_SVG_EXPORT_FEATURE])
  return response.blob()
}

export type BatchChunkRequest = {
  records: import('./batch').BatchDataRecord[]
  recordOffset: number
  totalRecords: number
  copiesField?: string
  exportFormats?: readonly ('pdf' | 'svg')[]
  remainingPages: number
  remainingBytes: number
}

export async function renderBatchChunk(baseBody: Record<string, unknown>, request: BatchChunkRequest,
  signal?: AbortSignal): Promise<{ blob: Blob; records: number; pages: number; bytes: number; failed: number
    remainingPages: number; remainingBytes: number }> {
  const body = {
    ...baseBody,
    data: {},
    records: request.records,
    recordOffset: request.recordOffset,
    totalRecords: request.totalRecords,
    ...(request.copiesField ? { copiesField: request.copiesField } : {}),
    ...(request.exportFormats?.length ? { exportFormats: [...request.exportFormats] } : {}),
    remainingPages: request.remainingPages,
    remainingBytes: request.remainingBytes,
  }
  const exportFeatures = new Set<string>()
  for (const format of request.exportFormats ?? []) {
    if (format === 'pdf') exportFeatures.add(PAGE_PDF_EXPORT_FEATURE)
    else if (format === 'svg') exportFeatures.add(BARCODE_SVG_EXPORT_FEATURE)
    else throw new Error(`不支持批量导出格式：${String(format)}`)
  }
  const response = await postDesignerJson('/api/designer/batch', body, signal,
    [BATCH_EXPORT_FEATURE, ...exportFeatures])
  const number = (name: string) => Number(response.headers.get(name) ?? 0)
  return {
    blob: await response.blob(),
    records: number('X-EasyPrint-Batch-Records'),
    pages: number('X-EasyPrint-Batch-Pages'),
    bytes: number('X-EasyPrint-Batch-Bytes'),
    failed: number('X-EasyPrint-Batch-Failed'),
    remainingPages: number('X-EasyPrint-Batch-Remaining-Pages'),
    remainingBytes: number('X-EasyPrint-Batch-Remaining-Bytes'),
  }
}
