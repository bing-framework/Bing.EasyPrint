import type { BpTemplate, DesignerDiagnostic, PreviewResponse, TemplateResource } from './model'
import { normalizeDesignerCapabilities, type DesignerCapabilities } from './capabilities'
import { formatDesignerDiagnostic, normalizeDesignerDiagnostics } from './diagnostics'

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
  'Gs1DataBarLimited', 'Gs1DataBarExpanded', 'Code128Auto',
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
export type ZplMediaOrientation = 'None' | 'Down' | 'Left' | 'Right'
export type ZplMediaTracking = 'N' | 'Y' | 'W' | 'M' | 'A' | 'V'
export type ZplMediaRequest = {
  allowRasterizedTextStyles?: boolean;
  dpi?: number; printSpeed?: number; orientation?: ZplMediaOrientation;
  mediaTracking?: ZplMediaTracking; darkness?: number;
  profile?: ZplPrinterProfile
}
export type PreviewRequest = { template: BpTemplate; data: unknown; resources?: Record<string, string>;
  protocol?: PrintProtocol; escPosLayoutMode?: EscPosLayoutMode; allowRasterizedLines?: boolean;
  imageThreshold?: number; deviceHalfCellWidthAt24?: number; deviceFullCellWidthAt24?: number;
  deviceLineHeightAt24?: number; requireDeclaredNativeBounds?: boolean;
  deviceFontCalibrations?: DeviceFontCalibrationMap;
  copies?: number; feedLines?: number; cutAfterPrint?: boolean;
  tsplMedia?: TsplMediaRequest; zplMedia?: ZplMediaRequest;
  encoding?: PrintEncoding; escPosDevice?: EscPosDeviceRequest; cutOptions?: PrintCutOptions; job?: PrintJobOptions }

/** 能力查询只接受输出配置，不携带模板、业务数据或图片资源。 */
export type DesignerCapabilitiesRequest = {
  protocol?: PrintProtocol
  escPosLayoutMode?: EscPosLayoutMode
  encoding?: PrintEncoding
  escPosDevice?: EscPosDeviceRequest
  tsplMedia?: TsplMediaRequest
  zplMedia?: ZplMediaRequest
  allowRasterizedLines?: boolean
  /** 仅供客户端要求新版 Host 能力字段，不会序列化到请求体。 */
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
const DESIGNER_REQUIRED_FEATURES = ['structured-errors', 'strict-options', 'binding-diagnostics'] as const

function clientValidationError(message: string, code: string, phase: string): DesignerValidationError {
  return new DesignerValidationError(message, [{ message, code, phase }])
}

function designerPostHeaders(): Record<string, string> {
  return {
    'Content-Type': 'application/json',
    [DESIGNER_CONTRACT_VERSION_HEADER]: DESIGNER_CONTRACT_VERSION,
    [DESIGNER_REQUIRED_FEATURES_HEADER]: DESIGNER_REQUIRED_FEATURES.join(','),
  }
}

function isAbortError(error: unknown): boolean {
  return typeof error === 'object' && error !== null && 'name' in error && error.name === 'AbortError'
}

function validateDesignerContract(response: Response): void {
  if (response.headers.get(DESIGNER_CONTRACT_VERSION_HEADER)?.trim() !== DESIGNER_CONTRACT_VERSION) {
    throw clientValidationError('打印服务版本不兼容，请更新 Host。', 'ContractVersionMismatch', 'Contract')
  }

  const availableFeatures = new Set((response.headers.get(DESIGNER_FEATURES_HEADER) ?? '')
    .split(',').map((feature) => feature.trim()).filter((feature) => feature.length > 0))
  const missingFeatures = DESIGNER_REQUIRED_FEATURES.filter((feature) => !availableFeatures.has(feature))
  if (missingFeatures.length > 0) {
    throw clientValidationError('打印服务缺少所需功能，请更新 Host。',
      'ContractFeatureUnavailable', 'Contract')
  }
}

async function postDesignerJson(path: string, body: unknown, signal?: AbortSignal): Promise<Response> {
  const serializedBody = JSON.stringify(body)
  let response: Response
  try {
    response = await fetch(path, {
      method: 'POST',
      headers: designerPostHeaders(),
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
  validateDesignerContract(response)
  return response
}

export async function loadDesignerCapabilities(request: DesignerCapabilitiesRequest, signal?: AbortSignal): Promise<DesignerCapabilities> {
  const body: DesignerCapabilitiesRequest = {
    ...(request.protocol === undefined ? {} : { protocol: request.protocol }),
    ...(request.escPosLayoutMode === undefined ? {} : { escPosLayoutMode: request.escPosLayoutMode }),
    ...(request.encoding === undefined ? {} : { encoding: request.encoding }),
    ...(request.allowRasterizedLines === undefined ? {} : { allowRasterizedLines: request.allowRasterizedLines }),
    ...(compactOptions(request.escPosDevice) === undefined ? {} : { escPosDevice: compactOptions(request.escPosDevice) }),
    ...(compactOptions(request.tsplMedia) === undefined ? {} : { tsplMedia: compactOptions(request.tsplMedia) }),
    ...(compactOptions(request.zplMedia) === undefined ? {} : { zplMedia: compactOptions(request.zplMedia) }),
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
  cutOptions?: PrintCutOptions): Promise<PreviewResponse> {
  await ensureCutContract(protocol, escPosLayoutMode, allowRasterizedLines, tsplMedia, zplMedia, encoding, escPosDevice,
    cutOptions, job, signal)
  const compactTsplMedia = compactOptions(tsplMedia)
  const compactZplMedia = compactOptions(zplMedia)
  const compactEscPosDevice = compactOptions(escPosDevice)
  const compactJob = job === undefined ? undefined : compactOptions(job) ?? {}
  const body: PreviewRequest = {
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
  }
  const response = await postDesignerJson('/api/designer/preview', body, signal)
  const result = await response.json() as PreviewResponse
  if (!Array.isArray(result.pages)) throw new Error('预览服务返回的数据格式无效。')
  return { ...result, errors: normalizeDesignerDiagnostics(result.errors) }
}

export async function renderTemplate(template: BpTemplate, data: unknown, resources: TemplateResource[],
  protocol: PrintProtocol = 'Cpcl', escPosLayoutMode: EscPosLayoutMode = 'Native',
  allowRasterizedLines = false, imageThreshold?: number, deviceHalfCellWidthAt24?: number,
  deviceFullCellWidthAt24?: number, copies?: number, feedLines?: number, cutAfterPrint?: boolean,
  tsplMedia?: TsplMediaRequest, zplMedia?: ZplMediaRequest,
  encoding?: PrintEncoding, escPosDevice?: EscPosDeviceRequest,
  deviceLineHeightAt24?: number, requireDeclaredNativeBounds?: boolean,
  deviceFontCalibrations?: DeviceFontCalibrationMap, job?: PrintJobOptions,
  cutOptions?: PrintCutOptions): Promise<Blob> {
  await ensureCutContract(protocol, escPosLayoutMode, allowRasterizedLines, tsplMedia, zplMedia, encoding, escPosDevice,
    cutOptions, job)
  const compactTsplMedia = compactOptions(tsplMedia)
  const compactZplMedia = compactOptions(zplMedia)
  const compactEscPosDevice = compactOptions(escPosDevice)
  const compactJob = job === undefined ? undefined : compactOptions(job) ?? {}
  const body: PreviewRequest = {
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
  }
  const response = await postDesignerJson('/api/designer/render', body)
  return response.blob()
}
