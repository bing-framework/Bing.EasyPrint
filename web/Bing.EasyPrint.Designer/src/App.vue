<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import {
  AlertTriangle, ArrowDownToLine, Barcode, Check, ChevronDown, ChevronRight, CircleHelp, CodeXml,
  Download, FilePlus2, FileText, ImagePlus, Layers3, Minus, PanelLeft, PanelRight, Play, Printer, Save,
  QrCode, Redo2, RotateCcw, Ruler, Search, Type, Upload, X, Undo2, ZoomIn, ZoomOut,
  Circle, Hand, MousePointer2, Shapes, Square, Table2, Layers, Ungroup, Unlink, Pencil, Trash2, Plus, ArrowUp, ArrowDown, Copy,
} from '@lucide/vue'
import CanvasWorkspace from './components/CanvasWorkspace.vue'
import PropertyInspector from './components/PropertyInspector.vue'
import TemplateTree from './components/TemplateTree.vue'
import LabelPrinterProfileEditor from './components/LabelPrinterProfileEditor.vue'
import { DesignerValidationError, loadDesignerSamples, previewTemplate, renderTemplate, type DesignerSample,
  loadDesignerCapabilities, type PrintProtocol, type EscPosLayoutMode, type TsplMediaRequest, type ZplMediaRequest,
  type ZplMediaOrientation, type ZplMediaTracking, type PrintEncoding, type EscPosDeviceRequest,
  type EscPosNativeFlowMode,
  type PrintJobCopyOrder, type PrintJobFeedUnit, type PrintJobOptions, type PrintJobScope,
  type PrintCutMode, type PrintCutOptions,
  type DeviceFontCalibrationMap, type DeviceFontCalibrationSets, type EscPosCharacterRange,
  type EscPosNativeTextProfile, type EscPosPrinterProfile, type EscPosTextCharacterMode,
  type BarcodeTypeName, type EscPosBarcodeTextPositionName,
  type EscPosRasterFontResource, type EscPosRasterFontsRequest,
  defaultEscPosPrinterProfile } from './api'
import { canCreateElement, createCapabilityRequestRunner, cutCapabilitiesKnown, elementTypeReason,
  type CapabilitiesState, type CapabilityQuery, type DesignerCapabilities } from './capabilities'
import { collectResourceKeys, createComponentTemplate, exportTemplateBundle, importTemplateBundle, MAX_TEMPLATE_JSON_BYTES, resourceForImage } from './archive'
import { buildLocalLayout } from './layout'
import { bindImageResource, collectArrayPaths, collectRootFieldPaths, resolvePath, setPathValue } from './binding'
import { assertTemplate, cloneTemplate, type BpTemplate, type DesignerDiagnostic, type PreviewElement, type PreviewPage, type TemplateElement, type TemplateResource } from './model'
import { findDiagnosticPage, formatDesignerDiagnostic, normalizeDesignerDiagnostics, resolveDiagnosticTarget } from './diagnostics'
import { makeSample, sampleData, type SampleKey } from './samples'
import { findCell, findComponent, findElement, findRow, listTemplateTree } from './tree'
import { SnapshotHistory } from './history'
import { barcodeOccupiedSize, barcodeRawSizeFromOccupied, dotsToMm, getNudgeDelta, materializeElementPosition, PRINT_DPI, rotatedSize, translateElement } from './coordinates'
import { addNestedRow as appendNestedRow } from './structure'
import { appendComponent, appendDataTable, appendStaticTable, componentLibraryTargetVersion, detachComponent, enableV2, groupCellElements, removeComponent, renameComponent, saveGroupComponent, ungroupElement, type ElementBounds } from './componentsModel'
import { parseTextTable } from './textTable'
import { duplicateElement, groupRowsAtPaths, offsetFlowGroup, reorderFlowGroup, setFlowGroupPosition } from './editorOperations'
import { defaultDesignerOutputSettings, parseDesignerProject, serializeDesignerProject, type DesignerOutputSettings } from './designerProject'

const localSampleLabels: Record<string, string> = {
  blank: '空白标签 · 576 × 360 点',
  product: '商品标签 · 五类元素',
  receipt: '动态小票 · 重复明细',
  'receipt-v2': 'V2 分页小票 · 页眉 / 表格 / 页脚',
}

const template = ref<BpTemplate>(makeSample('blank'))
const templateName = ref('空白标签 · 576 × 360 点')
const selectedPath = ref('Rows[0].Cells[0].Elements[0]')
const selectedPaths = ref<string[]>([])
const selectedOccurrencePath = ref('')
const sampleId = ref<string>('blank')
const remoteSamples = ref<DesignerSample[]>([])
const serviceSampleError = ref('')
const unit = ref<'pt' | 'mm'>('pt')
const outputTarget = ref<'Cpcl' | 'Tspl' | 'Zpl' | 'EscPosRaster' | 'EscPosNative'>('Cpcl')
const printProtocol = computed<PrintProtocol>(() => outputTarget.value.startsWith('EscPos') ? 'EscPos' : outputTarget.value as PrintProtocol)
const escPosLayoutMode = computed<EscPosLayoutMode>(() => outputTarget.value === 'EscPosNative' ? 'Native' : 'Raster')
const targetDpi = computed(() => {
  if (printProtocol.value === 'Cpcl') return PRINT_DPI
  const configuredDpi = printProtocol.value === 'EscPos'
    ? escPosDevice.value.dpi
    : printProtocol.value === 'Tspl' ? tsplMedia.value.dpi : zplMedia.value.dpi
  const profileDpi = printProtocol.value === 'EscPos' ? escPosDevice.value.profile?.horizontalDpi : undefined
  if (configuredDpi !== undefined && Number.isFinite(configuredDpi) && configuredDpi > 0) return configuredDpi
  return profileDpi !== undefined && Number.isFinite(profileDpi) && profileDpi > 0 ? profileDpi : 203
})
const allowRasterizedLines = ref(false)
const showOutputSettings = ref(false)
const imageThreshold = ref(128)
const deviceHalfCellWidthAt24 = ref<number | undefined>()
const deviceFullCellWidthAt24 = ref<number | undefined>()
const deviceLineHeightAt24 = ref<number | undefined>()
const deviceFontCalibrations = ref<DeviceFontCalibrationSets>({ Tspl: {}, Zpl: {} })
const deviceFontCalibrationName = ref('0')
const deviceFontCalibrationHalfCellWidthAt24 = ref<number | undefined>()
const deviceFontCalibrationFullCellWidthAt24 = ref<number | undefined>()
const deviceFontCalibrationLineHeightAt24 = ref<number | undefined>()
const requireDeclaredNativeBounds = ref(false)
const copies = ref<number | undefined>()
const feedLines = ref<number | undefined>()
const cutAfterPrint = ref<boolean | undefined>()
const cutOptions = ref<PrintCutOptions | undefined>()
const job = ref<PrintJobOptions | undefined>()
const encoding = ref<PrintEncoding | undefined>()
const escPosDevice = ref<EscPosDeviceRequest>({})
const tsplMedia = ref<TsplMediaRequest>({})
const zplMedia = ref<ZplMediaRequest>({})
const tsplProfileError = ref('')
const zplProfileError = ref('')
const labelProfileError = computed(() => printProtocol.value === 'Tspl' ? tsplProfileError.value :
  printProtocol.value === 'Zpl' ? zplProfileError.value : '')
const escPosProfileExpanded = ref(false)
const usesDeviceFontCalibration = computed(() => printProtocol.value === 'Tspl' || printProtocol.value === 'Zpl')
const usesImageThreshold = computed(() => outputTarget.value === 'EscPosRaster' || usesDeviceFontCalibration.value)
const usesFeedLines = computed(() => printProtocol.value === 'EscPos')
const printDataLabel = computed(() => `生成 ${printProtocol.value === 'EscPos' ? 'ESC/POS' : printProtocol.value.toUpperCase()}`)
const usesCutAfterPrint = computed(() => printProtocol.value === 'EscPos' || printProtocol.value === 'Tspl' || printProtocol.value === 'Zpl')
const usesEscPosDevice = computed(() => printProtocol.value === 'EscPos')
function cloneEscPosProfileState(profile: EscPosPrinterProfile): EscPosPrinterProfile {
  return {
    ...profile,
    ...(profile.supportedCutModes === undefined ? {} : {
      supportedCutModes: profile.supportedCutModes === null ? null : [...profile.supportedCutModes],
    }),
    ...(profile.supportedNativeBarcodeHumanReadableTypes === undefined ? {} : {
      supportedNativeBarcodeHumanReadableTypes: profile.supportedNativeBarcodeHumanReadableTypes === null
        ? null : [...profile.supportedNativeBarcodeHumanReadableTypes],
    }),
    ...(profile.supportedNativeBarcodeHumanReadablePositions === undefined ? {} : {
      supportedNativeBarcodeHumanReadablePositions: profile.supportedNativeBarcodeHumanReadablePositions === null
        ? null : [...profile.supportedNativeBarcodeHumanReadablePositions],
    }),
    ...(profile.text === undefined ? {} : {
      text: {
        ...profile.text,
        characterRanges: profile.text.characterRanges.map((range) => ({ ...range })),
      },
    }),
  }
}
function clonePrintCutOptionsState(options: PrintCutOptions | undefined): PrintCutOptions | undefined {
  return options === undefined ? undefined : { ...options }
}
function cloneEscPosRasterFontsState(rasterFonts: EscPosRasterFontsRequest): EscPosRasterFontsRequest {
  return {
    defaultFont: rasterFonts.defaultFont,
    ...(rasterFonts.fallbackFonts === undefined ? {} : { fallbackFonts: [...rasterFonts.fallbackFonts] }),
    fonts: rasterFonts.fonts.map((font) => ({ ...font })),
  }
}
function cloneEscPosDeviceState(device: EscPosDeviceRequest): EscPosDeviceRequest {
  return {
    ...device,
    ...(device.profile === undefined ? {} : { profile: cloneEscPosProfileState(device.profile) }),
    ...(device.rasterFonts === undefined ? {} : { rasterFonts: cloneEscPosRasterFontsState(device.rasterFonts) }),
  }
}
const effectiveEncoding = computed<PrintEncoding | undefined>(() =>
  printProtocol.value === 'Zpl' && encoding.value === 'Gbk' ? undefined :
    encoding.value === 'Device' && !(outputTarget.value === 'EscPosNative' && escPosDevice.value.profile?.text) ? undefined : encoding.value)
const effectiveEscPosDevice = computed<EscPosDeviceRequest | undefined>(() => {
  if (!usesEscPosDevice.value) return undefined
  if (escPosLayoutMode.value !== 'Raster') {
    const { rasterFonts: _rasterFonts, rasterBarcodeHumanReadableFontSize: _rasterHriFontSize, ...nativeDevice } = escPosDevice.value
    return nativeDevice
  }
  const { nativeTabWidthDots: _nativeTabWidthDots, nativeFlow: _nativeFlow, ...rasterDevice } = escPosDevice.value
  if (!rasterDevice.profile?.text) return rasterDevice
  const { text: _text, ...rasterProfile } = rasterDevice.profile
  return { ...rasterDevice, profile: rasterProfile }
})
const usesTsplMedia = computed(() => printProtocol.value === 'Tspl')
const usesZplMedia = computed(() => printProtocol.value === 'Zpl')
function labelEncodingDisabled(codePage: number): boolean {
  const declared = usesTsplMedia.value ? tsplMedia.value.profile?.encodingCodePages :
    usesZplMedia.value ? zplMedia.value.profile?.encodingCodePages : undefined
  return declared !== undefined && !declared.includes(codePage)
}
const labelEncodingWarning = computed(() => {
  const codePage = effectiveEncoding.value === 'Ascii' ? 20127 : effectiveEncoding.value === 'Utf8' ? 65001 :
    effectiveEncoding.value === 'Gbk' ? 936 : usesZplMedia.value ? 65001 : 936
  return labelEncodingDisabled(codePage) ? '当前输出编码未在机型配置中声明，请选择已确认的编码。' : ''
})
const jobEnabled = computed(() => job.value !== undefined)
const jobFeedUnitOptions: PrintJobFeedUnit[] = ['Lines', 'Dots', 'MotionUnits']
const cutModeOptions: PrintCutMode[] = ['Full', 'Partial']
const cutFeedUnitOptions: PrintJobFeedUnit[] = ['MotionUnits', 'Dots', 'Lines']
const jobScopeOptions: PrintJobScope[] = ['None', 'EachPage', 'EachCopy', 'JobEnd']
const jobCopyOrderOptions: PrintJobCopyOrder[] = ['Collated', 'Uncollated']
const jobScopeLabels: Record<PrintJobScope, string> = { None: '不额外处理', EachPage: '每页', EachCopy: '每份', JobEnd: '任务结束' }
const jobFeedUnitLabels: Record<PrintJobFeedUnit, string> = { Lines: '行', Dots: '点', MotionUnits: 'MotionUnits' }
const jobUncollatedWarning = computed(() => Boolean(job.value && job.value.copyOrder === 'Uncollated' &&
  (job.value.copies ?? 1) > 1 && (job.value.feedScope === 'EachCopy' || job.value.cutScope === 'EachCopy')))
const pageCutOptionsEnabled = computed(() => cutOptions.value !== undefined)
const jobCutOptionsEnabled = computed(() => job.value?.cutOptions !== undefined)
const customCutOptionsConfigured = computed(() => cutOptions.value !== undefined || job.value?.cutOptions !== undefined)
const currentPageContext = computed(() => {
  const page = previewPages.value[previewIndex.value]
  if (!page) return ''
  const source = page.sourcePageIndex === undefined || page.sourcePageIndex === null ? '' : `原页 ${page.sourcePageIndex + 1}`
  const copy = page.copyIndex === undefined || page.copyIndex === null ? '' : `第 ${page.copyIndex + 1} 份`
  return [source, copy].filter(Boolean).join(' · ')
})
const capabilities = ref<DesignerCapabilities>()
const capabilitiesState = ref<CapabilitiesState>('idle')
const capabilitiesError = ref('')
const capabilityQuery = computed<CapabilityQuery>(() => ({
  protocol: printProtocol.value,
  escPosLayoutMode: escPosLayoutMode.value,
  encoding: effectiveEncoding.value,
  escPosDevice: effectiveEscPosDevice.value ? cloneEscPosDeviceState(effectiveEscPosDevice.value) : undefined,
  tsplMedia: usesTsplMedia.value ? cloneTemplate(tsplMedia.value) : undefined,
  zplMedia: usesZplMedia.value ? cloneTemplate(zplMedia.value) : undefined,
  allowRasterizedLines: allowRasterizedLines.value,
  requireCutContract: usesCutAfterPrint.value && customCutOptionsConfigured.value,
}))
const capabilityRequestRunner = createCapabilityRequestRunner(loadDesignerCapabilities)
const capabilityNotice = computed(() => {
  if (capabilitiesState.value === 'loading') return '正在读取当前协议能力，暂不能新增元素。'
  if (capabilitiesState.value === 'error') return '当前协议能力不可用，已禁用新增元素。'
  if (capabilitiesState.value === 'ready') {
    const protocol = capabilities.value?.protocol
    return `${protocol === 'EscPos' ? 'ESC/POS' : protocol?.toUpperCase() ?? '当前协议'} 能力已更新`
  }
  return '等待读取当前协议能力。'
})
const capabilityQueryReady = computed(() => capabilitiesState.value === 'ready' && Boolean(capabilities.value))
const customCutCapabilitiesUnknown = computed(() => usesCutAfterPrint.value && customCutOptionsConfigured.value &&
  (!capabilityQueryReady.value || !cutCapabilitiesKnown(capabilities.value)))
function cutModeSupported(mode: PrintCutMode): boolean {
  return Boolean(capabilityQueryReady.value && capabilities.value?.cutModes.includes(mode))
}
function cutFeedUnitSupported(unit: PrintJobFeedUnit): boolean {
  return Boolean(capabilityQueryReady.value && capabilities.value?.cutFeedUnits.includes(unit))
}
function cutFeedAmountWithinCapability(options: PrintCutOptions | undefined): boolean {
  if (!options || options.feedBeforeCutAmount === undefined) return true
  const range = options.feedBeforeCutUnit === 'Dots' ? capabilities.value?.cutFeedDots :
    options.feedBeforeCutUnit === 'MotionUnits' ? capabilities.value?.cutFeedMotionUnits : undefined
  return !range || valueWithinRange(options.feedBeforeCutAmount, range)
}
function valueWithinRange(value: number, range: { minimum: number; maximum: number | null }): boolean {
  return value >= range.minimum && (range.maximum === null || value <= range.maximum)
}
const deviceFontCalibrationsForRequest = computed<DeviceFontCalibrationMap | undefined>(() => {
  if (!usesDeviceFontCalibration.value) return undefined
  const pattern = printProtocol.value === 'Tspl' ? /^[0-8]$/ : /^[0-9A-Z]$/
  const source = printProtocol.value === 'Tspl' ? deviceFontCalibrations.value.Tspl : deviceFontCalibrations.value.Zpl
  const compatible = Object.fromEntries(Object.entries(source)
    .filter(([name]) => pattern.test(name))) as DeviceFontCalibrationMap
  return Object.keys(compatible).length ? compatible : undefined
})
const zoom = ref(1)
const canvasMode = ref<'select' | 'pan'>('select')
const componentName = ref('')
const componentEditId = ref('')
const componentNewId = ref('')
const pastedTable = ref('')
const showTableImport = ref(false)
const showTableWizard = ref(false)
const tableSource = ref('Items')
const tableRepeatHeader = ref(true)
const tableColumns = ref<Array<{ title: string; path: string; width?: number; alignment: 0 | 1 | 2 }>>([
  { title: '名称', path: 'Name', alignment: 0 }, { title: '数量', path: 'Quantity', alignment: 2 },
])
const showAdvancedTools = ref(false)
const resources = ref<TemplateResource[]>([])
const dataText = ref('{}')
const previewPages = ref<PreviewPage[]>([])
const previewIndex = ref(0)
const previewSource = ref<'service' | 'local'>('local')
const previewBusy = ref(false)
const previewError = ref('')
const previewDiagnostics = ref<DesignerDiagnostic[]>([])
const previewErrorPath = computed(() => previewDiagnostics.value
  .map((item) => resolveDiagnosticTarget(template.value, item)?.path).find(Boolean) ?? '')
const statusMessage = ref('本地预览 · 近似布局')
const statusTone = ref<'neutral' | 'success' | 'warning' | 'error'>('neutral')
const showDataPanel = ref(true)
const mobileLayersOpen = ref(false)
const mobileInspectorOpen = ref(false)
const activeDataTab = ref<'business' | 'fields' | 'json'>('business')
const toast = ref('')
const toastTone = ref<'success' | 'warning' | 'error'>('success')
const jsonInput = ref<HTMLInputElement>()
const imageInput = ref<HTMLInputElement>()
const componentInput = ref<HTMLInputElement>()
const rasterFontRegularInput = ref<HTMLInputElement>()
const rasterFontBoldInput = ref<HTMLInputElement>()
const rasterFontEditorOpen = ref(false)
const rasterFontNameDraft = ref('')
const rasterFontVersionDraft = ref('')
const rasterFontRegularDataDraft = ref<string>()
const rasterFontRegularFileName = ref('')
const rasterFontBoldDataDraft = ref<string>()
const rasterFontBoldFileName = ref('')
const rasterFontFallbackDraft = ref('')
const dataTextarea = ref<HTMLTextAreaElement>()
const treeCollapsed = ref(false)
const searchText = ref('')

const outputSettingsSnapshot = (): DesignerOutputSettings => ({
  outputTarget: outputTarget.value,
  allowRasterizedLines: allowRasterizedLines.value,
  imageThreshold: imageThreshold.value,
  deviceHalfCellWidthAt24: deviceHalfCellWidthAt24.value,
  deviceFullCellWidthAt24: deviceFullCellWidthAt24.value,
  deviceLineHeightAt24: deviceLineHeightAt24.value,
  deviceFontCalibrations: {
    Tspl: Object.fromEntries(Object.entries(deviceFontCalibrations.value.Tspl)
      .map(([name, calibration]) => [name, { ...calibration }])),
    Zpl: Object.fromEntries(Object.entries(deviceFontCalibrations.value.Zpl)
      .map(([name, calibration]) => [name, { ...calibration }])),
  },
  requireDeclaredNativeBounds: requireDeclaredNativeBounds.value,
  copies: copies.value,
  feedLines: feedLines.value,
  cutAfterPrint: cutAfterPrint.value,
  ...(cutOptions.value === undefined ? {} : { cutOptions: clonePrintCutOptionsState(cutOptions.value) }),
  ...(job.value === undefined ? {} : {
    job: {
      ...job.value,
      ...(job.value.cutOptions === undefined ? {} : { cutOptions: clonePrintCutOptionsState(job.value.cutOptions) }),
    },
  }),
  encoding: encoding.value,
  escPosDevice: cloneEscPosDeviceState(escPosDevice.value),
  tsplMedia: tsplMedia.value,
  zplMedia: zplMedia.value,
})

function applyOutputSettings(settings: DesignerOutputSettings) {
  outputTarget.value = settings.outputTarget
  allowRasterizedLines.value = settings.allowRasterizedLines
  imageThreshold.value = settings.imageThreshold
  deviceHalfCellWidthAt24.value = settings.deviceHalfCellWidthAt24
  deviceFullCellWidthAt24.value = settings.deviceFullCellWidthAt24
  deviceLineHeightAt24.value = settings.deviceLineHeightAt24
  deviceFontCalibrations.value = {
    Tspl: Object.fromEntries(Object.entries(settings.deviceFontCalibrations.Tspl)
      .map(([name, calibration]) => [name, { ...calibration }])),
    Zpl: Object.fromEntries(Object.entries(settings.deviceFontCalibrations.Zpl)
      .map(([name, calibration]) => [name, { ...calibration }])),
  }
  requireDeclaredNativeBounds.value = settings.requireDeclaredNativeBounds
  copies.value = settings.copies
  feedLines.value = settings.feedLines
  cutAfterPrint.value = settings.cutAfterPrint
  cutOptions.value = clonePrintCutOptionsState(settings.cutOptions)
  job.value = settings.job === undefined ? undefined : {
    ...settings.job,
    ...(settings.job.cutOptions === undefined ? {} : { cutOptions: clonePrintCutOptionsState(settings.job.cutOptions) }),
  }
  if (job.value !== undefined) {
    copies.value = undefined
    feedLines.value = undefined
    cutAfterPrint.value = undefined
  }
  encoding.value = settings.encoding
  const importedEscPosDevice = cloneEscPosDeviceState(settings.escPosDevice)
  if (settings.outputTarget === 'EscPosRaster') delete importedEscPosDevice.nativeFlow
  if (settings.outputTarget !== 'EscPosRaster') {
    delete importedEscPosDevice.rasterFonts
    delete importedEscPosDevice.rasterBarcodeHumanReadableFontSize
  }
  escPosDevice.value = importedEscPosDevice
  rasterFontEditorOpen.value = settings.outputTarget === 'EscPosRaster' && importedEscPosDevice.rasterFonts !== undefined
  tsplMedia.value = cloneTemplate(settings.tsplMedia)
  zplMedia.value = cloneTemplate(settings.zplMedia)
}

type EditorSnapshot = { template: BpTemplate; resources: TemplateResource[]; outputSettings: DesignerOutputSettings }
const editorSnapshot = (): EditorSnapshot => ({
  template: template.value, resources: resources.value, outputSettings: outputSettingsSnapshot(),
})
let history = new SnapshotHistory<EditorSnapshot>(editorSnapshot())
const savedSnapshot = ref(JSON.stringify(editorSnapshot()))
const isDirty = computed(() => JSON.stringify(editorSnapshot()) !== savedSnapshot.value)
let historyTimer: ReturnType<typeof setTimeout> | undefined
let previewTimer: ReturnType<typeof setTimeout> | undefined
let toastTimer: ReturnType<typeof setTimeout> | undefined
let historyApplying = false
let previewRequest = 0
let previewAbort: AbortController | undefined

const parsedData = computed(() => {
  try { return { value: JSON.parse(dataText.value) as unknown, error: '' } }
  catch (error) { return { value: undefined, error: error instanceof Error ? error.message : 'JSON 格式无效。' } }
})
const dataByteSize = computed(() => new TextEncoder().encode(dataText.value).length)
const filteredTreeCount = computed(() => listTemplateTree(template.value).filter((entry) => entry.label.toLowerCase().includes(searchText.value.toLowerCase())).length)
const rootFieldPaths = computed(() => collectRootFieldPaths(parsedData.value.value))
const bindingFieldPaths = computed(() => {
  const root = parsedData.value.value
  const path = selectedPath.value.startsWith('Components[')
    ? selectedOccurrencePath.value || selectedPath.value : selectedPath.value
  let item = root
  for (const match of path.matchAll(/(?:^|\.)(?:Rows|Elements)\[\d+\]/g)) {
    const nodePath = path.slice(0, match.index + match[0].length)
    if (nodePath === path) break
    const row = findRow(template.value, nodePath)
    const group = findElement(template.value, nodePath)
    const source = row?.RepeatSource ?? (group?.Type === 6 ? group.DataPath : undefined)
    if (!source) continue
    try {
      const value = resolvePath(source, { root, item, index: -1, variables: template.value.Variables })
      item = row?.RepeatSource && Array.isArray(value) ? value[0] : value
    } catch { item = undefined }
  }
  const itemPaths = collectRootFieldPaths(item)
  const rootPaths = collectRootFieldPaths(root)
  const definitionPath = /^Components\[\d+\]/.exec(selectedPath.value)?.[0]
  const parameterNames = new Set(Object.keys(definitionPath ? findComponent(template.value, definitionPath)?.Parameters ?? {} : {}))
  for (const match of path.matchAll(/(?:^|\.)Elements\[\d+\]/g)) {
    const group = findElement(template.value, path.slice(0, match.index + match[0].length))
    if (group?.Type === 6 && group.Parameters) Object.keys(group.Parameters).forEach((name) => parameterNames.add(name))
  }
  return [...new Set([...itemPaths, ...itemPaths.map((name) => `$item.${name}`),
    ...rootPaths.map((name) => `$root.${name}`), '$index',
    ...[...parameterNames].map((name) => `$param.${name}`),
    ...Object.keys(template.value.Variables ?? {}).map((name) => `$var.${name}`)])]
})
const editableFields = computed(() => {
  const result: Array<{ path: string; value: string | number | boolean | null }> = []
  const visit = (value: unknown, prefix: string, depth: number): void => {
    if (!value || typeof value !== 'object' || Array.isArray(value) || depth > 6 || result.length >= 60) return
    for (const [key, child] of Object.entries(value)) {
      const path = prefix ? `${prefix}.${key}` : key
      if (child === null || ['string', 'number', 'boolean'].includes(typeof child)) result.push({ path, value: child as string | number | boolean | null })
      else visit(child, path, depth + 1)
    }
  }
  visit(parsedData.value.value, '', 0)
  return result
})
const tableSourceOptions = computed(() => {
  return collectArrayPaths(parsedData.value.value)
})
const tableItemFields = computed(() => {
  const root = parsedData.value.value
  let source: unknown
  try { source = resolvePath(tableSource.value, { root, item: root, index: -1 }) }
  catch { source = undefined }
  const first = Array.isArray(source) ? source.find((value) => value && typeof value === 'object') : undefined
  return collectRootFieldPaths(first)
})
const selectedElement = computed(() => findElement(template.value, selectedPath.value))
const selectedDefinition = computed(() => findComponent(template.value, selectedPath.value))
const selectedRow = computed(() => findRow(template.value, selectedPath.value))
const selectedCell = computed(() => findCell(template.value, selectedPath.value))
const currentPage = computed(() => previewPages.value[previewIndex.value])
const selectedDeviceFontName = computed(() => selectedElement.value?.Type === 1
  ? (selectedElement.value.Font?.Kind === 1
    ? selectedElement.value.Font.Name?.trim() : selectedElement.value.DeviceFontName?.trim()) || '0' : '0')
const sampleOptions = computed(() => [
  ...Object.entries(localSampleLabels).map(([id, name]) => ({ id, name, source: 'local' as const })),
  ...remoteSamples.value.map((sample) => ({ id: `service:${sample.id}`, name: `${sample.name} · 服务端`, source: 'service' as const })),
])

watch([template, resources, outputTarget, allowRasterizedLines, imageThreshold,
  deviceHalfCellWidthAt24, deviceFullCellWidthAt24, deviceLineHeightAt24,
  deviceFontCalibrations,
  requireDeclaredNativeBounds, copies, feedLines, cutAfterPrint, cutOptions, job, encoding,
  escPosDevice, tsplMedia, zplMedia], () => {
  if (historyApplying) return
  if (historyTimer) clearTimeout(historyTimer)
  historyTimer = setTimeout(() => { history.record(editorSnapshot()); syncHistoryState() }, 250)
  schedulePreview()
}, { deep: true, flush: 'sync' })

watch(dataText, () => schedulePreview(), { flush: 'post' })
watch(selectedPath, () => loadDeviceFontCalibrationDraft(), { immediate: true })
watch(capabilityQuery, () => { void refreshCapabilities() }, { deep: true, immediate: true })
watch(outputTarget, () => {
  if (encoding.value === 'Device' && outputTarget.value !== 'EscPosNative') encoding.value = undefined
  if (outputTarget.value === 'EscPosRaster' && escPosDevice.value.nativeFlow !== undefined) {
    const { nativeFlow: _nativeFlow, ...rasterDevice } = escPosDevice.value
    escPosDevice.value = rasterDevice
  }
  if (outputTarget.value !== 'EscPosRaster' &&
    (escPosDevice.value.rasterFonts !== undefined || escPosDevice.value.rasterBarcodeHumanReadableFontSize !== undefined)) {
    const { rasterFonts: _rasterFonts, rasterBarcodeHumanReadableFontSize: _rasterHriFontSize, ...device } = escPosDevice.value
    escPosDevice.value = device
    rasterFontEditorOpen.value = false
  }
  if (printProtocol.value === 'Zpl' && encoding.value === 'Gbk') encoding.value = undefined
  deviceFontCalibrationName.value = selectedDeviceFontName.value
  loadDeviceFontCalibrationDraft()
  schedulePreview()
})
onMounted(async () => {
  await refreshPreview()
  try {
    const samples = await loadDesignerSamples()
    remoteSamples.value = samples
    if (!samples.length) throw new Error('服务端没有返回可用样例。')
  } catch (error) {
    serviceSampleError.value = error instanceof Error ? error.message : '样例服务暂不可用。'
  }
})

onBeforeUnmount(() => {
  if (historyTimer) clearTimeout(historyTimer)
  if (previewTimer) clearTimeout(previewTimer)
  if (toastTimer) clearTimeout(toastTimer)
  previewAbort?.abort()
  capabilityRequestRunner.cancel()
})

async function refreshCapabilities() {
  if (customCutOptionsConfigured.value) invalidatePreview()
  capabilitiesState.value = 'loading'
  capabilitiesError.value = ''
  capabilities.value = undefined
  const result = await capabilityRequestRunner.run(capabilityQuery.value)
  if (result.status === 'stale') return
  if (result.status === 'error') {
    capabilitiesState.value = 'error'
    capabilitiesError.value = result.error instanceof Error ? result.error.message : '能力服务暂不可用。'
    if (customCutOptionsConfigured.value) schedulePreview()
    return
  }
  capabilities.value = result.value
  capabilitiesState.value = 'ready'
  if (customCutOptionsConfigured.value) schedulePreview()
}

function canAddElement(type: TemplateElement['Type']): boolean {
  return canCreateElement(capabilities.value, capabilitiesState.value, type)
}

function canDragElement(type: TemplateElement['Type']): boolean {
  return canAddElement(type) && capabilities.value?.supportsCoordinates === true
}

function elementToolTitle(type: TemplateElement['Type'], label: string, shortcut?: string): string {
  const suffix = shortcut ? ` (${shortcut})` : ''
  const reason = capabilitiesState.value === 'ready'
    ? elementTypeReason(capabilities.value, type)
    : capabilityNotice.value
  return reason ? `${label}${suffix}：${reason}` : `${label}，可拖入画布${suffix}`
}

function showCapabilityReason(type: TemplateElement['Type']) {
  const reason = capabilitiesState.value === 'ready'
    ? elementTypeReason(capabilities.value, type)
    : capabilityNotice.value
  if (reason) showToast(reason, 'warning')
}

function showToast(message: string, tone: 'success' | 'warning' | 'error' = 'success') {
  toast.value = message
  toastTone.value = tone
  if (toastTimer) clearTimeout(toastTimer)
  toastTimer = setTimeout(() => { toast.value = '' }, 3600)
}

function replaceTemplate(next: BpTemplate, path = '', settings = defaultDesignerOutputSettings()) {
  assertTemplate(next)
  historyApplying = true
  template.value = cloneTemplate(next)
  applyOutputSettings(settings)
  history = new SnapshotHistory<EditorSnapshot>(editorSnapshot())
  savedSnapshot.value = JSON.stringify(editorSnapshot())
  syncHistoryState()
  selectedPath.value = path
  selectedPaths.value = path ? [path] : []
  previewIndex.value = 0
  nextTick(() => { historyApplying = false })
  schedulePreview()
}

function useSample(event: Event) {
  const value = (event.target as HTMLSelectElement).value
  sampleId.value = value
  resources.value = []
  if (value.startsWith('service:')) {
    const found = remoteSamples.value.find((sample) => sample.id === value.slice(8))
    if (!found) return
    templateName.value = found.name
    replaceTemplate(found.template)
    dataText.value = JSON.stringify(found.data ?? {}, null, 2)
    showToast(`已载入服务端样例：${found.name}`)
    return
  }
  const key = value as SampleKey
  const next = makeSample(key)
  templateName.value = localSampleLabels[key] ?? '未命名模板'
  replaceTemplate(next, listTemplateTree(next).find((entry) => entry.kind === 'element')?.path ?? '')
  const data = key === 'blank' ? {} : key === 'product' ? sampleData.product : key === 'receipt-v2' ? sampleData.receiptV2 : sampleData.receipt
  dataText.value = JSON.stringify(data, null, 2)
  activeDataTab.value = key === 'product' ? 'business' : 'fields'
  showToast(`已载入${localSampleLabels[key] ?? '样例'}`)
}

function updateBusinessField(path: string, value: string) {
  updateBusinessValue(path, value)
}

function updateGenericField(path: string, value: string, previous: unknown) {
  if (typeof previous === 'number') {
    const number = Number(value)
    if (!value.trim() || !Number.isFinite(number)) { showToast('请输入有效数字。', 'error'); return }
    updateBusinessValue(path, number)
  } else updateBusinessValue(path, value)
}

function setImageThreshold(event: Event) {
  const value = Number((event.target as HTMLInputElement).value)
  if (Number.isInteger(value) && value >= 0 && value <= 255) imageThreshold.value = value
}

function setDeviceCellWidth(target: 'half' | 'full', event: Event) {
  const raw = (event.target as HTMLInputElement).value.trim()
  const value = raw === '' ? undefined : Number(raw)
  if (value !== undefined && (!Number.isFinite(value) || value <= 0)) return
  if (target === 'half') deviceHalfCellWidthAt24.value = value
  else deviceFullCellWidthAt24.value = value
}

function deviceCellWidthValue(value: number | undefined): string {
  return value === undefined ? '' : String(value)
}

function setDeviceLineHeight(event: Event) {
  const raw = (event.target as HTMLInputElement).value.trim()
  const value = raw === '' ? undefined : Number(raw)
  if (value !== undefined && (!Number.isFinite(value) || value <= 0)) return
  deviceLineHeightAt24.value = value
}

type DeviceFontCalibrationField = 'halfCellWidthAt24' | 'fullCellWidthAt24' | 'lineHeightAt24'

function loadDeviceFontCalibrationDraft() {
  const source = printProtocol.value === 'Tspl' ? deviceFontCalibrations.value.Tspl : deviceFontCalibrations.value.Zpl
  const calibration = source[deviceFontCalibrationName.value]
  deviceFontCalibrationHalfCellWidthAt24.value = calibration?.halfCellWidthAt24
  deviceFontCalibrationFullCellWidthAt24.value = calibration?.fullCellWidthAt24
  deviceFontCalibrationLineHeightAt24.value = calibration?.lineHeightAt24
}

function setDeviceFontCalibrationName(event: Event) {
  const raw = (event.target as HTMLInputElement).value.trim().toUpperCase()
  deviceFontCalibrationName.value = raw.slice(0, 1)
  loadDeviceFontCalibrationDraft()
}

function setDeviceFontCalibrationNumber(field: DeviceFontCalibrationField, event: Event) {
  const raw = (event.target as HTMLInputElement).value.trim()
  const value = raw === '' ? undefined : Number(raw)
  if (value !== undefined && (!Number.isFinite(value) || value <= 0)) return
  if (field === 'halfCellWidthAt24') deviceFontCalibrationHalfCellWidthAt24.value = value
  else if (field === 'fullCellWidthAt24') deviceFontCalibrationFullCellWidthAt24.value = value
  else deviceFontCalibrationLineHeightAt24.value = value
}

function deviceFontCalibrationNumberValue(value: number | undefined): string {
  return value === undefined ? '' : String(value)
}

function applyDeviceFontCalibration() {
  const name = deviceFontCalibrationName.value.trim().toUpperCase()
  const pattern = printProtocol.value === 'Tspl' ? /^[0-8]$/ : /^[0-9A-Z]$/
  if (!pattern.test(name)) {
    showToast(printProtocol.value === 'Tspl' ? 'TSPL 字体编号必须是 0–8 的单个数字。' : 'ZPL 字体编号必须是单个数字或大写字母。', 'error')
    return
  }
  const halfCellWidthAt24 = deviceFontCalibrationHalfCellWidthAt24.value
  const fullCellWidthAt24 = deviceFontCalibrationFullCellWidthAt24.value
  const lineHeightAt24 = deviceFontCalibrationLineHeightAt24.value
  if (halfCellWidthAt24 === undefined || fullCellWidthAt24 === undefined || lineHeightAt24 === undefined) {
    showToast('逐字体校准需要同时填写半角字宽、全角字宽和行高。', 'error')
    return
  }
  deviceFontCalibrationName.value = name
  const key = printProtocol.value === 'Tspl' ? 'Tspl' : 'Zpl'
  deviceFontCalibrations.value = {
    ...deviceFontCalibrations.value,
    [key]: {
      ...deviceFontCalibrations.value[key],
      [name]: { halfCellWidthAt24, fullCellWidthAt24, lineHeightAt24 },
    },
  }
  showToast(`已保存设备字体 ${name} 的校准值`)
}

function removeDeviceFontCalibration() {
  const name = deviceFontCalibrationName.value.trim().toUpperCase()
  const key = printProtocol.value === 'Tspl' ? 'Tspl' : 'Zpl'
  if (!deviceFontCalibrations.value[key][name]) return
  const next = { ...deviceFontCalibrations.value[key] }
  delete next[name]
  deviceFontCalibrations.value = { ...deviceFontCalibrations.value, [key]: next }
  loadDeviceFontCalibrationDraft()
  showToast(`已清除设备字体 ${name} 的校准值`)
}

function setPageNumber(target: 'copies' | 'feedLines', event: Event) {
  const raw = (event.target as HTMLInputElement).value.trim()
  const value = raw === '' ? undefined : Number(raw)
  if (value !== undefined && (!Number.isInteger(value) || (target === 'copies' ? value < 1 : value < 0))) return
  if (target === 'copies') copies.value = value
  else feedLines.value = value
}

function setCutAfterPrint(event: Event) {
  const value = (event.target as HTMLSelectElement).value
  cutAfterPrint.value = value === '' ? undefined : value === 'true'
}

function defaultPrintCutOptions(): PrintCutOptions {
  return { mode: capabilities.value?.cutModes[0] ?? 'Full', feedToCutPosition: false,
    feedBeforeCutAmount: 0, feedBeforeCutUnit: printProtocol.value === 'Tspl' ? 'Dots' : 'MotionUnits' }
}

function setPageCutOptionsEnabled(event: Event) {
  if ((event.target as HTMLInputElement).checked) {
    if (cutAfterPrint.value === false || printProtocol.value !== 'EscPos') cutAfterPrint.value = true
    cutOptions.value = cutOptions.value ?? defaultPrintCutOptions()
  } else {
    cutOptions.value = undefined
  }
}

function clearPageCutOptions() {
  cutOptions.value = undefined
}

function setJobCutOptionsEnabled(event: Event) {
  if (!job.value) return
  if ((event.target as HTMLInputElement).checked) {
    if (job.value.cutScope === 'None') {
      showToast('请先指定任务切纸范围，再启用自定义裁切参数。', 'warning')
      return
    }
    job.value = { ...job.value, cutOptions: job.value.cutOptions ?? defaultPrintCutOptions() }
  } else {
    clearJobCutOptions()
  }
}

function clearJobCutOptions() {
  if (!job.value) return
  const { cutOptions: _cutOptions, ...withoutCutOptions } = job.value
  job.value = withoutCutOptions
}

type CutOptionsTarget = 'page' | 'job'

function cutOptionsForTarget(target: CutOptionsTarget): PrintCutOptions | undefined {
  return target === 'page' ? cutOptions.value : job.value?.cutOptions
}

function setCutOptions(target: CutOptionsTarget, next: PrintCutOptions | undefined) {
  if (target === 'page') cutOptions.value = next
  else if (job.value) {
    if (next === undefined) clearJobCutOptions()
    else job.value = { ...job.value, cutOptions: next }
  }
}

function setCutOptionMode(target: CutOptionsTarget, event: Event) {
  const value = (event.target as HTMLSelectElement).value
  if (!cutModeOptions.includes(value as PrintCutMode)) return
  const current = cutOptionsForTarget(target)
  if (!current) return
  setCutOptions(target, { ...current, mode: value as PrintCutMode })
}

function setCutOptionFeedToCutPosition(target: CutOptionsTarget, event: Event) {
  const current = cutOptionsForTarget(target)
  if (!current) return
  setCutOptions(target, { ...current, feedToCutPosition: (event.target as HTMLInputElement).checked })
}

function setCutOptionFeedUnit(target: CutOptionsTarget, event: Event) {
  const value = (event.target as HTMLSelectElement).value
  if (!cutFeedUnitOptions.includes(value as PrintJobFeedUnit)) return
  const current = cutOptionsForTarget(target)
  if (!current) return
  setCutOptions(target, { ...current, feedBeforeCutUnit: value as PrintJobFeedUnit })
}

function setCutOptionAmount(target: CutOptionsTarget, event: Event) {
  const current = cutOptionsForTarget(target)
  if (!current) return
  const raw = (event.target as HTMLInputElement).value.trim()
  const value = raw === '' ? undefined : Number(raw)
  // 保留有限的非法编辑值，由保存/预览的严格校验拒绝，避免界面显示新值却输出旧值。
  if (value !== undefined && !Number.isFinite(value)) return
  setCutOptions(target, { ...current, feedBeforeCutAmount: value })
}

function enableJob() {
  job.value = {
  copies: 1, copyOrder: 'Collated', feedScope: 'None', feedUnit: 'Lines', feedAmount: 0, cutScope: 'None',
  }
  copies.value = undefined
  feedLines.value = undefined
  cutAfterPrint.value = undefined
  cutOptions.value = undefined
}

function disableJob() {
  job.value = undefined
  copies.value = undefined
  feedLines.value = undefined
  cutAfterPrint.value = undefined
  cutOptions.value = undefined
}

function setJobEnabled(event: Event) {
  if ((event.target as HTMLInputElement).checked) enableJob()
  else disableJob()
}

type PrintJobNumberField = 'copies' | 'feedAmount'

function setJobNumber(field: PrintJobNumberField, event: Event) {
  if (!job.value) return
  const raw = (event.target as HTMLInputElement).value.trim()
  const value = raw === '' ? undefined : Number(raw)
  if (value !== undefined && (!Number.isInteger(value) || (field === 'copies' ? value < 1 : value < 0))) return
  job.value = { ...job.value, [field]: value }
}

function setJobCopyOrder(event: Event) {
  if (!job.value) return
  const value = (event.target as HTMLSelectElement).value
  if (!jobCopyOrderOptions.includes(value as PrintJobCopyOrder)) return
  job.value = { ...job.value, copyOrder: value as PrintJobCopyOrder }
}

function setJobFeedScope(event: Event) {
  if (!job.value) return
  const value = (event.target as HTMLSelectElement).value
  if (!jobScopeOptions.includes(value as PrintJobScope)) return
  job.value = { ...job.value, feedScope: value as PrintJobScope }
}

function setJobFeedUnit(event: Event) {
  if (!job.value) return
  const value = (event.target as HTMLSelectElement).value
  if (!jobFeedUnitOptions.includes(value as PrintJobFeedUnit)) return
  job.value = { ...job.value, feedUnit: value as PrintJobFeedUnit }
}

function setJobCutScope(event: Event) {
  if (!job.value) return
  const value = (event.target as HTMLSelectElement).value
  if (!jobScopeOptions.includes(value as PrintJobScope)) return
  job.value = { ...job.value, cutScope: value as PrintJobScope }
}

function setEncoding(event: Event) {
  const value = (event.target as HTMLSelectElement).value
  if (value === '') { encoding.value = undefined; return }
  if (value !== 'Gbk' && value !== 'Utf8' && value !== 'Ascii' && value !== 'Device') return
  if (value === 'Device' && !(outputTarget.value === 'EscPosNative' && escPosDevice.value.profile?.text)) {
    showToast('Device 编码仅适用于启用 Native 文字契约的 ESC/POS Native 输出。', 'warning')
    return
  }
  encoding.value = value
}

type EscPosDeviceNumberField = 'dpi' | 'codePageIndex' | 'printAreaWidthUnits' | 'nativeTabWidthDots' | 'rasterBarcodeHumanReadableFontSize'

function setEscPosNativeFlow(event: Event) {
  const value = (event.target as HTMLSelectElement).value
  if (value === 'Rows') {
    const { nativeFlow: _nativeFlow, ...device } = escPosDevice.value
    escPosDevice.value = device
    return
  }
  if (value !== 'Sequential') return
  escPosDevice.value = { ...escPosDevice.value, nativeFlow: value as EscPosNativeFlowMode }
}

const rasterFallbackChoices = computed<EscPosRasterFontResource[]>(() => {
  const catalog = escPosDevice.value.rasterFonts
  if (!catalog) return []
  const fallbackNames = new Set(catalog.fallbackFonts ?? [])
  return catalog.fonts.filter((font) => !fallbackNames.has(font.name))
})

function decodedRasterFontBytes(value: string): number | undefined {
  if (!value || value.length % 4 !== 0 || !/^[A-Za-z0-9+/]*={0,2}$/.test(value)) return undefined
  const padding = value.endsWith('==') ? 2 : value.endsWith('=') ? 1 : 0
  const bytes = value.length / 4 * 3 - padding
  return Number.isInteger(bytes) && bytes > 0 ? bytes : undefined
}

async function readRasterFontFile(file: File): Promise<string> {
  const bytes = new Uint8Array(await file.arrayBuffer())
  if (!bytes.length) throw new Error('字体文件不能为空。')
  let binary = ''
  const chunkSize = 0x8000
  for (let offset = 0; offset < bytes.length; offset += chunkSize) {
    binary += String.fromCharCode(...bytes.subarray(offset, Math.min(offset + chunkSize, bytes.length)))
  }
  return btoa(binary)
}

function rasterFontFileIsSupported(file: File): boolean {
  return /\.(ttf|otf)$/i.test(file.name)
}

function openRasterFontRegularPicker() { rasterFontRegularInput.value?.click() }
function openRasterFontBoldPicker() { rasterFontBoldInput.value?.click() }

async function selectRasterFontRegular(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  try {
    if (!rasterFontFileIsSupported(file)) throw new Error('固定字体只接受 TTF 或 OTF 文件。')
    rasterFontRegularDataDraft.value = await readRasterFontFile(file)
    rasterFontRegularFileName.value = file.name
  } catch (error) {
    showToast(error instanceof Error ? error.message : '读取常规字体失败。', 'error')
  } finally {
    input.value = ''
  }
}

async function selectRasterFontBold(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  try {
    if (!rasterFontFileIsSupported(file)) throw new Error('固定字体只接受 TTF 或 OTF 文件。')
    rasterFontBoldDataDraft.value = await readRasterFontFile(file)
    rasterFontBoldFileName.value = file.name
  } catch (error) {
    showToast(error instanceof Error ? error.message : '读取粗体字体失败。', 'error')
  } finally {
    input.value = ''
  }
}

function enableEscPosRasterFonts() {
  if (outputTarget.value !== 'EscPosRaster') return
  rasterFontEditorOpen.value = true
}

function clearEscPosRasterFonts() {
  const { rasterFonts: _rasterFonts, ...device } = escPosDevice.value
  escPosDevice.value = device
  rasterFontEditorOpen.value = false
  rasterFontNameDraft.value = ''
  rasterFontVersionDraft.value = ''
  rasterFontRegularDataDraft.value = undefined
  rasterFontRegularFileName.value = ''
  rasterFontBoldDataDraft.value = undefined
  rasterFontBoldFileName.value = ''
  rasterFontFallbackDraft.value = ''
}

function addRasterFontResource() {
  const name = rasterFontNameDraft.value.trim()
  const version = rasterFontVersionDraft.value.trim()
  const regularData = rasterFontRegularDataDraft.value
  const boldData = rasterFontBoldDataDraft.value
  if (!name || !version || !regularData) {
    showToast('请填写资源别名、版本并选择常规字体。', 'warning')
    return
  }
  const regularBytes = decodedRasterFontBytes(regularData)
  const boldBytes = boldData === undefined ? 0 : decodedRasterFontBytes(boldData)
  if (regularBytes === undefined || (boldData !== undefined && boldBytes === undefined)) {
    showToast('字体数据不是有效的 Base64。', 'error')
    return
  }
  const catalog = escPosDevice.value.rasterFonts
  const fonts = catalog?.fonts ?? []
  if (fonts.length >= 64) {
    showToast('固定字体资源最多 64 个。', 'warning')
    return
  }
  if (fonts.some((font) => font.name === name)) {
    showToast(`字体别名已存在：${name}`, 'warning')
    return
  }
  const totalBytes = fonts.reduce((total, font) => total + (decodedRasterFontBytes(font.regularData) ?? 0) + (font.boldData ? decodedRasterFontBytes(font.boldData) ?? 0 : 0), 0) + regularBytes + (boldBytes ?? 0)
  if (totalBytes > 32 * 1024 * 1024) {
    showToast('固定字体解码后的总字节数不能超过 32 MiB。', 'warning')
    return
  }
  const resource: EscPosRasterFontResource = { name, version, regularData, ...(boldData === undefined ? {} : { boldData }) }
  escPosDevice.value = {
    ...escPosDevice.value,
    rasterFonts: {
      defaultFont: catalog?.defaultFont ?? name,
      fallbackFonts: catalog?.fallbackFonts === undefined ? [] : [...catalog.fallbackFonts],
      fonts: [...fonts.map((font) => ({ ...font })), resource],
    },
  }
  rasterFontEditorOpen.value = true
  rasterFontNameDraft.value = ''
  rasterFontVersionDraft.value = ''
  rasterFontRegularDataDraft.value = undefined
  rasterFontRegularFileName.value = ''
  rasterFontBoldDataDraft.value = undefined
  rasterFontBoldFileName.value = ''
  showToast(`已添加固定字体 ${name}`)
}

function setRasterDefaultFont(event: Event) {
  const catalog = escPosDevice.value.rasterFonts
  if (!catalog) return
  const name = (event.target as HTMLSelectElement).value
  if (!catalog.fonts.some((font) => font.name === name)) return
  escPosDevice.value = { ...escPosDevice.value, rasterFonts: { ...cloneEscPosRasterFontsState(catalog), defaultFont: name } }
}

function addRasterFallback() {
  const catalog = escPosDevice.value.rasterFonts
  const name = rasterFontFallbackDraft.value
  if (!catalog || !name || !catalog.fonts.some((font) => font.name === name)) return
  const fallbackFonts = [...(catalog.fallbackFonts ?? [])]
  if (fallbackFonts.includes(name)) return
  escPosDevice.value = { ...escPosDevice.value, rasterFonts: { ...cloneEscPosRasterFontsState(catalog), fallbackFonts: [...fallbackFonts, name] } }
  rasterFontFallbackDraft.value = ''
}

function removeRasterFallback(name: string) {
  const catalog = escPosDevice.value.rasterFonts
  if (!catalog?.fallbackFonts?.includes(name)) return
  escPosDevice.value = { ...escPosDevice.value, rasterFonts: { ...cloneEscPosRasterFontsState(catalog), fallbackFonts: catalog.fallbackFonts.filter((item) => item !== name) } }
}

function moveRasterFallback(index: number, delta: -1 | 1) {
  const catalog = escPosDevice.value.rasterFonts
  const fallbackFonts = [...(catalog?.fallbackFonts ?? [])]
  const nextIndex = index + delta
  if (!catalog || index < 0 || nextIndex < 0 || nextIndex >= fallbackFonts.length) return
  const [item] = fallbackFonts.splice(index, 1)
  fallbackFonts.splice(nextIndex, 0, item)
  escPosDevice.value = { ...escPosDevice.value, rasterFonts: { ...cloneEscPosRasterFontsState(catalog), fallbackFonts } }
}

function removeRasterFontResource(name: string) {
  const catalog = escPosDevice.value.rasterFonts
  if (!catalog) return
  const fonts = catalog.fonts.filter((font) => font.name !== name)
  if (!fonts.length) {
    clearEscPosRasterFonts()
    return
  }
  const defaultFont = catalog.defaultFont === name ? fonts[0].name : catalog.defaultFont
  const fallbackFonts = (catalog.fallbackFonts ?? []).filter((item) => item !== name)
  escPosDevice.value = {
    ...escPosDevice.value,
    rasterFonts: {
      ...cloneEscPosRasterFontsState(catalog),
      defaultFont,
      fallbackFonts,
      fonts: fonts.map((font) => ({ ...font })),
    },
  }
  if (rasterFontFallbackDraft.value === name) rasterFontFallbackDraft.value = ''
}

function setEscPosDeviceNumber(field: EscPosDeviceNumberField, event: Event) {
  const raw = (event.target as HTMLInputElement).value.trim()
  const value = raw === '' ? undefined : Number(raw)
  if (value !== undefined && !Number.isInteger(value)) return
  if (value !== undefined && (
    (field === 'dpi' && value <= 0) ||
    (field === 'codePageIndex' && (value < 0 || value > 255)) ||
    (field === 'printAreaWidthUnits' && (value < 0 || value > 65535)) ||
    (field === 'nativeTabWidthDots' && (value < 8 || value % 8 !== 0)) ||
    (field === 'rasterBarcodeHumanReadableFontSize' && (value < 1 || value > 255)))) return
  escPosDevice.value = { ...escPosDevice.value, [field]: value }
}

type EscPosProfileNumberField = 'horizontalDpi' | 'verticalDpi' | 'motionUnitsPerInch' | 'printableWidthDots' | 'maxImageBlockBytes'

function ensureEscPosProfile(): EscPosPrinterProfile {
  if (!escPosDevice.value.profile) escPosDevice.value = { ...escPosDevice.value, profile: defaultEscPosPrinterProfile() }
  return escPosDevice.value.profile!
}

function createEscPosProfile() {
  if (encoding.value === 'Device') encoding.value = undefined
  escPosDevice.value = { ...escPosDevice.value, profile: defaultEscPosPrinterProfile() }
  escPosProfileExpanded.value = true
}

function createEightDotImpactProfile() {
  if (encoding.value === 'Device') encoding.value = undefined
  const { printAreaWidthUnits: _printAreaWidthUnits, ...device } = escPosDevice.value
  cutAfterPrint.value = false
  escPosDevice.value = {
    ...device,
    dpi: 180,
    profile: {
      ...defaultEscPosPrinterProfile(),
      name: '8-dot impact',
      imageCommand: 'BitImageEscStar',
      bitImageMode: 'DoubleDensity8',
      horizontalDpi: 180,
      verticalDpi: 60,
      motionUnitsPerInch: 144,
      supportsCut: false,
      supportsNativeBarcode: false,
      supportsNativeBarcodeHumanReadable: false,
      supportsNativeQrCode: false,
      supportsPrintAreaWidth: false,
      supportsTextSize: false,
      unidirectionalBitImage: true,
    },
  }
  escPosProfileExpanded.value = true
}

function clearEscPosProfile() {
  if (encoding.value === 'Device') encoding.value = undefined
  const { profile: _profile, ...device } = escPosDevice.value
  escPosDevice.value = device
  escPosProfileExpanded.value = false
}

function setEscPosProfileName(event: Event) {
  const profile = ensureEscPosProfile()
  profile.name = (event.target as HTMLInputElement).value
  escPosDevice.value = { ...escPosDevice.value, profile: { ...profile } }
}

function setEscPosProfileImageCommand(event: Event) {
  const value = (event.target as HTMLSelectElement).value
  if (value !== 'RasterGsV0' && value !== 'BitImageEscStar') return
  const profile = ensureEscPosProfile()
  escPosDevice.value = {
    ...escPosDevice.value,
    profile: {
      ...profile,
      imageCommand: value,
      ...(value === 'RasterGsV0' ? { bitImageMode: 'DoubleDensity24' as const, unidirectionalBitImage: false } : {}),
    },
  }
}

function setEscPosProfileBitImageMode(event: Event) {
  const value = (event.target as HTMLSelectElement).value
  if (!['SingleDensity8', 'DoubleDensity8', 'SingleDensity24', 'DoubleDensity24'].includes(value)) return
  const profile = ensureEscPosProfile()
  escPosDevice.value = { ...escPosDevice.value, profile: { ...profile, bitImageMode: value as EscPosPrinterProfile['bitImageMode'] } }
}

function setEscPosProfileNumber(field: EscPosProfileNumberField, event: Event) {
  const raw = (event.target as HTMLInputElement).value.trim()
  const profile = ensureEscPosProfile()
  const next = { ...profile } as EscPosPrinterProfile & Record<EscPosProfileNumberField, number | undefined>
  if (!raw) {
    if (field === 'printableWidthDots' || field === 'maxImageBlockBytes') delete next[field]
    else return
  } else {
    const value = Number(raw)
    if (!Number.isInteger(value) || value <= 0) return
    next[field] = value
  }
  escPosDevice.value = { ...escPosDevice.value, profile: next }
}

function setEscPosProfileBoolean(field: keyof Pick<EscPosPrinterProfile, 'supportsCut' | 'supportsFeedAndCut' | 'supportsNativeBarcode' | 'supportsNativeBarcodeHumanReadable' |
  'supportsNativeQrCode' | 'supportsPrintAreaWidth' | 'supportsTextSize' | 'unidirectionalBitImage'>, event: Event) {
  const profile = ensureEscPosProfile()
  escPosDevice.value = { ...escPosDevice.value, profile: { ...profile, [field]: (event.target as HTMLInputElement).checked } }
}

function setEscPosProfileHriTypes(event: Event) {
  const profile = ensureEscPosProfile()
  const raw = (event.target as HTMLInputElement).value.trim()
  if (!raw) {
    const { supportedNativeBarcodeHumanReadableTypes: _types, ...withoutTypes } = profile
    escPosDevice.value = { ...escPosDevice.value, profile: withoutTypes }
    return
  }
  const types = raw.split(/[,;\s]+/) as BarcodeTypeName[]
  escPosDevice.value = { ...escPosDevice.value, profile: {
    ...profile, supportedNativeBarcodeHumanReadableTypes: types,
  } }
}

function setEscPosProfileHriPositions(event: Event) {
  const profile = ensureEscPosProfile()
  const raw = (event.target as HTMLInputElement).value.trim()
  if (!raw) {
    const { supportedNativeBarcodeHumanReadablePositions: _positions, ...withoutPositions } = profile
    escPosDevice.value = { ...escPosDevice.value, profile: withoutPositions }
    return
  }
  const positions = raw.split(/[,;\s]+/) as EscPosBarcodeTextPositionName[]
  escPosDevice.value = { ...escPosDevice.value, profile: {
    ...profile, supportedNativeBarcodeHumanReadablePositions: positions,
  } }
}

function escPosProfileCutModesValue(modes: readonly PrintCutMode[] | null | undefined): string {
  if (modes === undefined || modes === null) return ''
  if (modes.length === 0) return 'None'
  return modes.join(',')
}

function setEscPosProfileCutModes(event: Event) {
  const value = (event.target as HTMLSelectElement).value
  const profile = ensureEscPosProfile()
  if (value === '') {
    const { supportedCutModes: _supportedCutModes, ...withoutCutModes } = profile
    escPosDevice.value = { ...escPosDevice.value, profile: withoutCutModes }
    return
  }
  const modes: readonly PrintCutMode[] = value === 'None' ? [] : value === 'Full' ? ['Full'] : value === 'Partial' ? ['Partial'] : ['Full', 'Partial']
  escPosDevice.value = { ...escPosDevice.value, profile: { ...profile, supportedCutModes: modes } }
}

const ESC_POS_FS_KANJI_CODE_PAGES = [936, 54936, 950, 949] as const

function defaultEscPosNativeTextProfile(): EscPosNativeTextProfile {
  return { encodingCodePage: 437, characterMode: 'DeviceDefault', characterRanges: [{ first: 0, last: 127 }] }
}

function enableEscPosNativeText() {
  const profile = ensureEscPosProfile()
  escPosDevice.value = {
    ...escPosDevice.value,
    profile: { ...profile, text: profile.text ?? defaultEscPosNativeTextProfile() },
  }
  escPosProfileExpanded.value = true
}

function clearEscPosNativeText() {
  const profile = escPosDevice.value.profile
  if (encoding.value === 'Device') encoding.value = undefined
  if (!profile?.text) return
  const { text: _text, ...withoutText } = profile
  escPosDevice.value = { ...escPosDevice.value, profile: withoutText }
}

type EscPosNativeTextNumberField = 'encodingCodePage' | 'codePageIndex' | 'halfCellWidthDots' | 'fullCellWidthDots'

function setEscPosNativeTextNumber(field: EscPosNativeTextNumberField, event: Event) {
  const profile = escPosDevice.value.profile
  if (!profile?.text) return
  const raw = (event.target as HTMLInputElement).value.trim()
  const nextText = { ...profile.text }
  if (!raw) {
    if (field === 'codePageIndex' || field === 'halfCellWidthDots' || field === 'fullCellWidthDots') {
      delete nextText[field]
    } else {
      return
    }
  } else {
    const value = Number(raw)
    const valid = Number.isInteger(value) && Number.isFinite(value) && (
      field === 'codePageIndex' ? value >= 0 && value <= 255 : value > 0)
    if (!valid) return
    nextText[field] = value
  }
  escPosDevice.value = { ...escPosDevice.value, profile: { ...profile, text: nextText } }
}

function setEscPosNativeTextMode(event: Event) {
  const profile = escPosDevice.value.profile
  if (!profile?.text) return
  const value = (event.target as HTMLSelectElement).value
  if (value !== 'DeviceDefault' && value !== 'SingleByte' && value !== 'FsKanji') return
  if (value === 'FsKanji' && !ESC_POS_FS_KANJI_CODE_PAGES.includes(profile.text.encodingCodePage as typeof ESC_POS_FS_KANJI_CODE_PAGES[number])) {
    showToast('FsKanji 仅支持代码页 936、54936、950 或 949。', 'warning')
    return
  }
  escPosDevice.value = { ...escPosDevice.value, profile: { ...profile, text: { ...profile.text, characterMode: value as EscPosTextCharacterMode } } }
}

function setEscPosNativeTextLineHeight(event: Event) {
  const profile = escPosDevice.value.profile
  if (!profile?.text) return
  const value = (event.target as HTMLSelectElement).value
  const text = { ...profile.text }
  if (value === '') delete text.lineHeightDots
  else if (value === '24' || value === '26') text.lineHeightDots = Number(value) as 24 | 26
  else return
  escPosDevice.value = { ...escPosDevice.value, profile: { ...profile, text } }
}

function nativeTextRangesValue(ranges: readonly EscPosCharacterRange[]): string {
  return ranges.map((range) => `${range.first}-${range.last}`).join(', ')
}

function parseEscPosNativeTextRanges(raw: string): EscPosCharacterRange[] {
  const input = raw.trim()
  if (!input) throw new Error('文字范围不能为空。')
  let ranges: unknown
  if (input.startsWith('[')) {
    ranges = JSON.parse(input) as unknown
  } else {
    ranges = input.split(/[,;\n]+/).map((part) => {
      const match = /^\s*(\d+)\s*-\s*(\d+)\s*$/.exec(part)
      if (!match) throw new Error('文字范围需使用 first-last 十进制格式。')
      return { first: Number(match[1]), last: Number(match[2]) }
    })
  }
  if (!Array.isArray(ranges) || ranges.length === 0) throw new Error('文字范围必须为非空数组。')
  return ranges.map((item, index) => {
    if (!item || typeof item !== 'object' || Array.isArray(item)) throw new Error(`文字范围 ${index + 1} 必须为对象。`)
    const range = item as Record<string, unknown>
    const first = range.first
    const last = range.last
    if (typeof first !== 'number' || !Number.isInteger(first) || !Number.isFinite(first) ||
      typeof last !== 'number' || !Number.isInteger(last) || !Number.isFinite(last) ||
      first < 0 || first > 0x10FFFF || last < 0 || last > 0x10FFFF ||
      first > last || (first <= 0xDFFF && last >= 0xD800)) {
      throw new Error(`文字范围 ${index + 1} 无效，不能包含 D800-DFFF。`)
    }
    return { first, last }
  })
}

function setEscPosNativeTextRanges(event: Event) {
  const profile = escPosDevice.value.profile
  if (!profile?.text) return
  try {
    const ranges = parseEscPosNativeTextRanges((event.target as HTMLInputElement).value)
    escPosDevice.value = { ...escPosDevice.value, profile: { ...profile, text: { ...profile.text, characterRanges: ranges } } }
  } catch (error) {
    showToast(error instanceof Error ? error.message : '文字范围无效。', 'warning')
  }
}

type TsplMediaNumberField = 'dpi' | 'gapDistance' | 'gapOffset' | 'direction' | 'referenceX' | 'referenceY' | 'speed' | 'density'
type ZplMediaNumberField = 'dpi' | 'printSpeed' | 'darkness'

function setTsplMediaNumber(field: TsplMediaNumberField, event: Event) {
  const raw = (event.target as HTMLInputElement).value.trim()
  const value = raw === '' ? undefined : Number(raw)
  if (value !== undefined && !Number.isInteger(value)) return
  if (value !== undefined && (
    (field === 'dpi' && value <= 0) ||
    (['gapDistance', 'gapOffset', 'referenceX', 'referenceY'].includes(field) && value < 0) ||
    (field === 'direction' && (value < 0 || value > 1)) ||
    (field === 'speed' && value <= 0) ||
    (field === 'density' && (value < 0 || value > 15)))) return
  tsplMedia.value = { ...tsplMedia.value, [field]: value }
}

function setZplMediaNumber(field: ZplMediaNumberField, event: Event) {
  const raw = (event.target as HTMLInputElement).value.trim()
  const value = raw === '' ? undefined : Number(raw)
  if (value !== undefined && !Number.isInteger(value)) return
  if (value !== undefined && (
    (field === 'dpi' && value <= 0) ||
    (field === 'printSpeed' && value <= 0) ||
    (field === 'darkness' && (value < -30 || value > 30)))) return
  zplMedia.value = { ...zplMedia.value, [field]: value }
}

function setTsplDirection(event: Event) {
  const value = (event.target as HTMLSelectElement).value
  tsplMedia.value = { ...tsplMedia.value, direction: value === '' ? undefined : Number(value) }
}

function setZplOrientation(event: Event) {
  const value = (event.target as HTMLSelectElement).value
  zplMedia.value = { ...zplMedia.value, orientation: value === '' ? undefined : value as ZplMediaOrientation }
}

function setZplMediaTracking(event: Event) {
  const value = (event.target as HTMLSelectElement).value
  zplMedia.value = { ...zplMedia.value, mediaTracking: value === '' ? undefined : value as ZplMediaTracking }
}

function optionalNumberValue(value: number | undefined): string {
  return value === undefined ? '' : String(value)
}

function locatePreviewError() {
  const diagnostic = previewDiagnostics.value.find((item) => resolveDiagnosticTarget(template.value, item))
  if (diagnostic) locateDiagnostic(diagnostic)
}

function setPreviewDiagnostics(diagnostics: DesignerDiagnostic[]) {
  previewDiagnostics.value = normalizeDesignerDiagnostics(diagnostics)
  previewError.value = previewDiagnostics.value.map(formatDesignerDiagnostic).join('\n')
}

function locateDiagnostic(diagnostic: DesignerDiagnostic, kind: 'source' | 'definition' | 'instance' = 'source') {
  const target = resolveDiagnosticTarget(template.value, diagnostic, kind)
  if (!target) return
  const page = findDiagnosticPage(previewPages.value, diagnostic)
  if (page !== undefined) previewIndex.value = page
  selectElement(target.path, false, target.occurrencePath)
  searchText.value = ''
  treeCollapsed.value = false
  mobileLayersOpen.value = true
}

async function exportComponents(rootId?: string) {
  if (!template.value.Components?.length) return
  try {
    const bundle = createComponentTemplate(template.value, rootId)
    const selection = collectResourceKeys(bundle)
    const hasResources = selection.dynamic ? resources.value.length > 0 : resources.value.some((resource) => selection.keys.has(resource.key))
    if (hasResources) downloadBlob(await exportTemplateBundle(bundle, resources.value), `${rootId ?? 'easyprint-components'}.zip`)
    else downloadBlob(new Blob([JSON.stringify(bundle, null, 2)], { type: 'application/json;charset=utf-8' }), `${rootId ?? 'easyprint-components'}.json`)
  } catch (error) { showToast(error instanceof Error ? error.message : '组件导出失败。', 'error') }
}

async function importComponents(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  try {
    let contents: { Version?: number; Components?: BpTemplate['Components'] }
    let importedResources: TemplateResource[] = []
    if (file.name.toLowerCase().endsWith('.zip')) {
      const imported = await importTemplateBundle(file)
      contents = imported.template
      importedResources = imported.resources
    } else {
      if (file.size > MAX_TEMPLATE_JSON_BYTES) throw new Error('组件库超过 1 MiB 上限。')
      contents = JSON.parse(await file.text()) as typeof contents
    }
    if (![1, 2, 3].includes(contents.Version ?? 0) || !Array.isArray(contents.Components) || !contents.Components.length)
      throw new Error('组件库格式无效。')
    const next = cloneTemplate(template.value)
    const existingIds = new Set((next.Components ?? []).map((component) => component.Id))
    const duplicates = contents.Components.map((component) => component.Id).filter((id) => existingIds.has(id))
    if (duplicates.length) throw new Error(`组件标识已存在：${duplicates.join(', ')}`)
    const keys = new Set(resources.value.map((resource) => resource.key))
    if (importedResources.some((resource) => keys.has(resource.key))) throw new Error('图片资源键已存在。')
    next.Version = componentLibraryTargetVersion(next.Version, contents.Version, contents.Components)
    next.Components = [...(next.Components ?? []), ...contents.Components]
    assertTemplate(next)
    template.value = next
    resources.value = [...resources.value, ...importedResources]
    showToast(`已导入 ${contents.Components.length} 个组件`)
  } catch (error) { showToast(error instanceof Error ? error.message : '导入组件失败。', 'error') }
  finally { input.value = '' }
}

function updateBusinessValue(path: string, value: unknown) {
  let root: Record<string, unknown>
  try { root = JSON.parse(dataText.value) as Record<string, unknown> }
  catch { root = {} }
  setPathValue(root, path, value)
  dataText.value = JSON.stringify(root, null, 2)
}

function updateSalePrice(value: string) {
  const root = parsedData.value.value as Record<string, unknown> | undefined
  if (root?.Product) { updateBusinessValue('Product.Price', value); return }
  if (!/^\d+(?:\.\d{0,2})?$/.test(value) || !Number.isFinite(Number(value))) {
    showToast('价格必须为非负数，最多两位小数。', 'error')
    return
  }
  const [integer, fraction = ''] = value.split('.')
  const normalized = `${integer}.${fraction.padEnd(2, '0')}`
  const next: Record<string, unknown> = { ...root, SalePriceInteger: integer, SalePriceFraction: `.${fraction.padEnd(2, '0')}` }
  if (next.OriginPriceText != null) next.IsPromotion = Number(next.OriginPriceText) !== Number(normalized)
  dataText.value = JSON.stringify(next, null, 2)
}

function invalidatePreview() {
  ++previewRequest
  previewAbort?.abort()
  previewBusy.value = false
}

function schedulePreview() {
  invalidatePreview()
  if (previewTimer) clearTimeout(previewTimer)
  previewTimer = setTimeout(() => { void refreshPreview() }, 400)
}

watch(labelProfileError, () => { void refreshPreview() })

async function refreshPreview() {
  invalidatePreview()
  if (labelProfileError.value) {
    previewError.value = labelProfileError.value
    previewDiagnostics.value = []
    previewPages.value = []
    statusMessage.value = '机型配置有误'
    statusTone.value = 'error'
    return
  }
  if (parsedData.value.error) {
    previewError.value = `业务数据 JSON 无效：${parsedData.value.error}`
    previewDiagnostics.value = []
    previewPages.value = []
    statusMessage.value = '业务数据有误'
    statusTone.value = 'error'
    return
  }
  if (customCutCapabilitiesUnknown.value) {
    const loading = capabilitiesState.value === 'loading' || capabilitiesState.value === 'idle'
    previewError.value = loading ? '' : capabilitiesError.value ||
      '当前 Host 未声明所需裁切能力，请更新 Host 后再预览。'
    previewDiagnostics.value = []
    previewPages.value = []
    statusMessage.value = loading ? '正在读取裁切能力' : '裁切能力不可用'
    statusTone.value = loading ? 'warning' : 'error'
    return
  }
  const request = previewRequest
  previewAbort = new AbortController()
  previewBusy.value = true
  try {
    const result = await previewTemplate(template.value, parsedData.value.value, resources.value,
      previewAbort.signal, printProtocol.value, escPosLayoutMode.value, allowRasterizedLines.value,
      imageThreshold.value, usesDeviceFontCalibration.value ? deviceHalfCellWidthAt24.value : undefined,
      usesDeviceFontCalibration.value ? deviceFullCellWidthAt24.value : undefined,
      jobEnabled.value ? undefined : copies.value,
      jobEnabled.value ? undefined : (usesFeedLines.value ? feedLines.value : undefined),
      jobEnabled.value ? undefined : (usesCutAfterPrint.value ? cutAfterPrint.value : undefined),
      usesTsplMedia.value ? tsplMedia.value : undefined,
      usesZplMedia.value ? zplMedia.value : undefined,
      effectiveEncoding.value,
      effectiveEscPosDevice.value,
      usesDeviceFontCalibration.value ? deviceLineHeightAt24.value : undefined,
      usesDeviceFontCalibration.value ? requireDeclaredNativeBounds.value : undefined,
       deviceFontCalibrationsForRequest.value, job.value,
       jobEnabled.value ? undefined : cutOptions.value)
    if (request !== previewRequest) return
    if (result.errors?.length) {
      setPreviewDiagnostics(result.errors)
      previewPages.value = []
      previewSource.value = 'service'
      statusMessage.value = '模板校验失败'
      statusTone.value = 'error'
      return
    }
    setPreviewDiagnostics([])
    previewPages.value = result.pages
    previewIndex.value = Math.min(previewIndex.value, Math.max(0, result.pages.length - 1))
    previewSource.value = 'service'
    statusMessage.value = `引擎预览 · ${result.pages.length} 页`
    statusTone.value = 'success'
  } catch (error) {
    if (request !== previewRequest || (error instanceof DOMException && error.name === 'AbortError')) return
    if (error instanceof DesignerValidationError) {
      setPreviewDiagnostics(error.diagnostics)
      if (!previewDiagnostics.value.length) previewError.value = error.message
      previewPages.value = []
      previewSource.value = 'service'
      statusMessage.value = '模板校验失败'
      statusTone.value = 'error'
      return
    }
    const local = buildLocalLayout(template.value, parsedData.value.value)
    const missingImages = local.pages.flatMap((page) => page.elements)
      .filter((element) => element.type === 2 && !resources.value.some((resource) => resource.key === element.resourceKey))
      .map((element) => ({ sourcePath: element.sourcePath, message: `图片资源尚未导入：${element.resourceKey ?? ''}` }))
    setPreviewDiagnostics([...local.errors, ...missingImages])
    previewPages.value = local.pages
    previewIndex.value = Math.min(previewIndex.value, Math.max(0, local.pages.length - 1))
    previewSource.value = 'local'
    statusMessage.value = '本地预览 · 近似布局'
    statusTone.value = 'warning'
  } finally {
    if (request === previewRequest) previewBusy.value = false
  }
}

function syncHistoryState() {
  // Referenced from template after asynchronous snapshot recording.
  historyState.value += 1
}
function flushHistorySnapshot() {
  if (historyTimer) clearTimeout(historyTimer)
  historyTimer = undefined
  history.record(editorSnapshot())
  syncHistoryState()
}
const historyState = ref(0)
const canUndo = computed(() => { historyState.value; return history.canUndo })
const canRedo = computed(() => { historyState.value; return history.canRedo })

function undo() {
  flushHistorySnapshot()
  const previous = history.undo()
  if (!previous) return
  historyApplying = true
  template.value = previous.template
  resources.value = previous.resources
  applyOutputSettings(previous.outputSettings)
  syncHistoryState()
  nextTick(() => { historyApplying = false })
  showToast('已撤销')
  schedulePreview()
}

function redo() {
  const next = history.redo()
  if (!next) return
  historyApplying = true
  template.value = next.template
  resources.value = next.resources
  applyOutputSettings(next.outputSettings)
  syncHistoryState()
  nextTick(() => { historyApplying = false })
  showToast('已重做')
  schedulePreview()
}

function defaultElement(type: TemplateElement['Type']): TemplateElement {
  const start = { X: 24, Y: 40 }
  const end = { X: 240, Y: 76 }
  const field = { Value: type === 1 ? '新建文字' : type === 3 ? 'QR 内容' : type === 4 ? '条码内容' : type === 2 ? 'image/resource-key' : undefined,
    NeedFeedLine: type !== 1 }
  if (type === 1) return { Type: 1, FontSize: 24, FontName: 0, FontStyle: 0, Bold: false, TextAlignment: 0, Field: field, StartCoordinate: start, EndCoordinate: end }
  if (type === 2) return { Type: 2, Width: 80, Height: 80, IsCompress: false, Field: { Name: 'image/resource-key' }, StartCoordinate: start, EndCoordinate: { X: 104, Y: 120 } }
  if (type === 3) return { Type: 3, UnitWidth: 3, ErrorLevel: 'M', Width: 100, Height: 100, Field: field, StartCoordinate: start, EndCoordinate: { X: 124, Y: 140 } }
  if (type === 4) return { Type: 4, BarcodeType: 0, LineWidth: 2, Ratio: 2, Width: 220, Height: 46, Field: field, StartCoordinate: start, EndCoordinate: { X: 244, Y: 86 } }
  if (type === 6) return { Type: 6, Width: 120, Height: 64, Elements: [{ Type: 1, FontSize: 24, Field: { Value: '组合文字' }, StartCoordinate: { X: 8, Y: 8 } }], StartCoordinate: start }
  if (type === 7) return { Type: 7, Width: 120, Height: 64, LineWidth: 1, Filled: false, StartCoordinate: start }
  if (type === 8) return { Type: 8, Width: 120, Height: 64, LineWidth: 1, StartCoordinate: start }
  if (type === 9) return { Type: 9, Icon: 'check', Size: 32, LineWidth: 2, StartCoordinate: start }
  if (type === 10) return { Type: 10, Format: 'DataMatrix', Width: 96, Height: 96, Field: { Value: 'DATA' }, StartCoordinate: start }
  return { Type: 5, LineWidth: 2, StartCoordinate: start, EndCoordinate: end, LineEndCoordinate: { X: end.X, Y: start.Y } }
}

function activeCell() {
  const cell = findCell(template.value, activeCellPath())
  if (cell) return cell
  const row = findRow(template.value, selectedPath.value) ?? template.value.Rows[0]
  if (!row) {
    template.value.Rows.push({ Height: 80, Cells: [{ Width: template.value.Width, Elements: [] }] })
    return template.value.Rows[0].Cells![0]
  }
  if (!row.Cells?.length) row.Cells = [{ Width: template.value.Width, Elements: [] }]
  return row.Cells[0]
}

function activeCellPath(): string {
  const segments = selectedPath.value.split('.')
  let lastCell = -1
  let lastRow = -1
  for (let index = 0; index < segments.length; index += 1) if (segments[index].startsWith('Cells[')) lastCell = index
  for (let index = 0; index < segments.length; index += 1) if (segments[index].startsWith('Rows[')) lastRow = index
  if (lastRow > lastCell) return `${selectedPath.value}.Cells[0]`
  if (lastCell >= 0) return segments.slice(0, lastCell + 1).join('.')
  if (selectedPath.value.startsWith('Rows[')) return `${selectedPath.value}.Cells[0]`
  return 'Rows[0].Cells[0]'
}

function addElement(type: TemplateElement['Type']): boolean {
  if (!canAddElement(type)) { showCapabilityReason(type); return false }
  if (type > 5) enableV2(template.value)
  if (selectedDefinition.value?.Rows) { showToast('请选择定义内的单元格再添加元素。', 'error'); return false }
  const cell = selectedDefinition.value ? undefined : activeCell()
  if (cell) cell.Elements ??= []
  const group = selectedElement.value?.Type === 6 ? selectedElement.value : undefined
  const groupElements = group?.ComponentId ? undefined : group?.Elements ?? selectedDefinition.value?.Elements
  if (selectedDefinition.value && !selectedDefinition.value.Elements) selectedDefinition.value.Elements = []
  const target = groupElements ?? selectedDefinition.value?.Elements ?? cell?.Elements
  if (!target) return false
  const element = defaultElement(type)
  if (capabilities.value?.labelProfile) {
    if (element.Type === 1 && !capabilities.value.fontKinds.includes(0) && capabilities.value.deviceFonts.length) {
      element.Font = { Kind: 1, Name: capabilities.value.deviceFonts[0].name }
    }
    if (element.Type === 9 && !capabilities.value.elementTypes.includes(2)) element.Icon = 'info'
  }
  if (capabilities.value?.supportsCoordinates === true) {
    element.StartCoordinate = { X: group ? 8 : 24, Y: (group ? 8 : 36) + target.length * 16 }
    if (element.Type === 5) element.LineEndCoordinate = { X: 224, Y: element.StartCoordinate.Y }
    else if (type <= 5) element.EndCoordinate = { X: element.StartCoordinate.X + (element.Type === 1 ? 220 : element.Type === 4 ? 220 : 100), Y: element.StartCoordinate.Y + (element.Type === 1 ? 36 : element.Type === 4 ? 46 : 100) }
    if (group && element.Type === 1) element.EndCoordinate = { X: Math.max(element.StartCoordinate.X + 16, (group.Width || 120) - 8), Y: element.StartCoordinate.Y + 36 }
  } else if (element.Type === 1) {
    // Native 文字按模板顺序流式输出，不能带入固定坐标。
    delete element.StartCoordinate
    delete element.EndCoordinate
  } else if (element.Type === 6) {
    // 组合自身及其默认文字子项也从原点开始按顺序布局。
    delete element.StartCoordinate
    for (const child of element.Elements ?? []) {
      delete child.StartCoordinate
      delete child.EndCoordinate
    }
  } else {
    element.StartCoordinate = { X: 0, Y: 0 }
    if (element.Type === 5) element.LineEndCoordinate = { X: 224, Y: 0 }
    else if (type <= 5) element.EndCoordinate = { X: element.Type === 4 ? 220 : 100, Y: element.Type === 4 ? 46 : 100 }
  }
  target.push(element)
  const parentPath = groupElements || selectedDefinition.value ? selectedPath.value : activeCellPath()
  selectedPath.value = `${parentPath}.Elements[${target.length - 1}]`
  showToast(`已添加${({ 1: '文字', 2: '图片', 3: '二维码', 4: '条码', 5: '线段', 6: '组合', 7: '矩形', 8: '椭圆', 9: '图标', 10: '二维点阵码' } as Record<number, string>)[type]}`)
  return true
}

function addMultilineText() {
  if (!addElement(1)) return
  const element = findElement(template.value, selectedPath.value)
  if (element?.Type === 1) {
    element.Field ??= {}
    element.Field.NeedFeedLine = true
  }
}

function dragTool(event: DragEvent, type: TemplateElement['Type'], multiline = false) {
  if (!canAddElement(type)) { event.preventDefault(); showCapabilityReason(type); return }
  if (capabilities.value?.supportsCoordinates !== true) {
    event.preventDefault()
    showToast('当前模式不支持拖入定位，请使用新增入口。', 'warning')
    return
  }
  event.dataTransfer?.setData('application/x-easyprint-element', multiline ? `${type}:multiline` : String(type))
  if (event.dataTransfer) event.dataTransfer.effectAllowed = 'copy'
}

function addElementAt(value: { type: TemplateElement['Type']; x: number; y: number; cellPath?: string; multiline?: boolean }) {
  if (capabilitiesState.value === 'ready' && capabilities.value?.supportsCoordinates === false) {
    showToast('当前模式不支持拖入定位，请使用新增入口。', 'warning')
    canvasMode.value = 'select'
    return
  }
  if (value.cellPath && findCell(template.value, value.cellPath)) selectedPath.value = value.cellPath
  if (!addElement(value.type)) { canvasMode.value = 'select'; return }
  const element = findElement(template.value, selectedPath.value)
  if (element) {
    translateElement(element, value.x - (element.StartCoordinate?.X ?? 0), value.y - (element.StartCoordinate?.Y ?? 0))
    if (element.Type === 1 && value.multiline) { element.Field ??= {}; element.Field.NeedFeedLine = true }
  }
  canvasMode.value = 'select'
}

async function groupCurrentCell() {
  try {
    await refreshPreview()
    const cellPath = activeCellPath()
    const cell = findCell(template.value, cellPath)
    const indices = selectedPaths.value.length > 1 ? selectedPaths.value.map((path) => {
      const match = new RegExp(`^${cellPath.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}\\.Elements\\[(\\d+)\\]$`).exec(path)
      if (!match) throw new Error('请选择同一单元格中的元素。')
      return Number(match[1])
    }) : undefined
    const selectedIndices = indices ?? cell?.Elements?.map((_, index) => index) ?? []
    const selectedElementPaths = selectedIndices.map((index) => `${cellPath}.Elements[${index}]`)
    const page = previewSource.value === 'service' && !previewBusy.value
      ? [currentPage.value, ...previewPages.value].find((candidate) => candidate &&
        selectedElementPaths.every((path) => candidate.elements.some((item) => item.sourcePath === path)))
      : undefined
    const measured = new Map<number, ElementBounds>()
    let cellWidth: number | undefined
    if (page) for (const index of selectedIndices) {
      const path = `${cellPath}.Elements[${index}]`
      const parts = page.elements.filter((item) => item.sourcePath === path)
      const instance = parts[0]?.instanceIndices?.join(',') ?? ''
      const frame = page.elements.find((item) => item.kind === 'cell' && item.sourcePath === cellPath &&
        (item.instanceIndices?.join(',') ?? '') === instance)
      if (!frame || !parts.length) continue
      if (previewPages.value.some((other) => other !== page && other.elements.some((item) =>
        item.sourcePath === path && (item.instanceIndices?.join(',') ?? '') === instance)))
        throw new Error('选中元素跨页，请调整位置后再编组。')
      const visibleParts = parts.filter((item) => (item.instanceIndices?.join(',') ?? '') === instance)
      measured.set(index, { left: Math.min(...visibleParts.map((item) => item.x)) - frame.x,
        top: Math.min(...visibleParts.map((item) => item.y)) - frame.y,
        right: Math.max(...visibleParts.map((item) => item.x + item.width)) - frame.x,
        bottom: Math.max(...visibleParts.map((item) => item.y + item.height)) - frame.y })
      cellWidth = frame.width
    }
    selectedPath.value = groupCellElements(template.value, cellPath, indices, measured, cellWidth)
    selectedPaths.value = [selectedPath.value]
    showToast(indices ? '已组合选中元素' : '已组合当前单元格的元素')
  }
  catch (error) { showToast(error instanceof Error ? error.message : '组合失败。', 'error') }
}

function groupSelectedRows() {
  try {
    const paths = selectedPaths.value.length ? selectedPaths.value : [selectedPath.value]
    selectedPath.value = groupRowsAtPaths(template.value, paths)
    selectedPaths.value = [selectedPath.value]
    showToast('已组合流式行')
  } catch (error) { showToast(error instanceof Error ? error.message : '流式组合失败。', 'error') }
}

async function ungroupSelected() {
  try {
    const path = selectedPath.value
    const group = findElement(template.value, path)
    let measured: Map<number, ElementBounds> | undefined
    let renderedGroup: { width: number; height: number; position: { X: number; Y: number } } | undefined
    const children = group?.Type === 6 ? group.ComponentId
      ? template.value.Components?.find((item) => item.Id === group.ComponentId)?.Elements : group.Elements : undefined
    if (children?.length) {
      await refreshPreview()
      if (previewSource.value !== 'service' || previewError.value) throw new Error('拆组需要成功的引擎预览。')
      const page = [currentPage.value, ...previewPages.value].find((candidate) => candidate?.elements.some((item) =>
        item.sourcePath === path && item.type === 6))
      const groupPart = page?.elements.find((item) => item.sourcePath === path && item.type === 6)
      if (!page || !groupPart) throw new Error('组合不在当前预览中，请检查显示条件。')
      const parentPath = path.slice(0, path.lastIndexOf('.Elements['))
      const container = page.elements.find((item) => item.sourcePath === parentPath &&
        (item.kind === 'cell' || item.type === 6) && item.instanceIndices?.join(',') === groupPart.instanceIndices?.join(','))
      if (!container) throw new Error('无法定位组合所在的单元格。')
      renderedGroup = { width: groupPart.width, height: groupPart.height,
        position: { X: groupPart.x - container.x - (group?.Margin?.Left ?? 0),
          Y: groupPart.y - container.y - (group?.Margin?.Top ?? 0) } }
      measured = new Map()
      children.forEach((_, index) => {
        const childPath = `${path}.Elements[${index}]`
        const parts = page.elements.filter((item) => item.sourcePath === childPath &&
          item.instanceIndices?.join(',') === groupPart.instanceIndices?.join(','))
        if (!parts.length) return
        measured!.set(index, { left: Math.min(...parts.map((item) => item.x)) - groupPart.x,
          top: Math.min(...parts.map((item) => item.y)) - groupPart.y,
          right: Math.max(...parts.map((item) => item.x + item.width)) - groupPart.x,
          bottom: Math.max(...parts.map((item) => item.y + item.height)) - groupPart.y })
      })
    }
    selectedPath.value = ungroupElement(template.value, path, measured, renderedGroup)
    selectedPaths.value = [selectedPath.value]
    showToast('组合已拆开')
  }
  catch (error) { showToast(error instanceof Error ? error.message : '拆组失败。', 'error') }
}

function detachSelected() {
  try { detachComponent(template.value, selectedPath.value); showToast('该实例已转为独立组合') }
  catch (error) { showToast(error instanceof Error ? error.message : '解除引用失败。', 'error') }
}

function renameDefinition() {
  try {
    renameComponent(template.value, componentEditId.value, componentNewId.value.trim())
    showToast('组件已重命名'); componentEditId.value = ''; componentNewId.value = ''
  } catch (error) { showToast(error instanceof Error ? error.message : '重命名失败。', 'error') }
}

function deleteDefinition(id: string) {
  try { removeComponent(template.value, id); showToast(`组件 ${id} 已删除`) }
  catch (error) { showToast(error instanceof Error ? error.message : '删除失败。', 'error') }
}

function selectElement(path: string, additive = false, occurrencePath = '') {
  selectedPaths.value = additive ? selectedPaths.value.includes(path)
    ? selectedPaths.value.filter((item) => item !== path) : [...selectedPaths.value, path] : path ? [path] : []
  selectedPath.value = selectedPaths.value.at(-1) ?? ''
  selectedOccurrencePath.value = occurrencePath
  mobileLayersOpen.value = false
}

function duplicateSelected() {
  try {
    selectedPath.value = duplicateElement(template.value, selectedPath.value)
    selectedPaths.value = [selectedPath.value]
    showToast('元素已复制')
  } catch (error) { showToast(error instanceof Error ? error.message : '复制失败。', 'error') }
}

function resizeElement(value: { path: string; width: number; height: number }) {
  const element = findElement(template.value, value.path)
  if (!element) return
  const start = element.StartCoordinate ?? { X: 0, Y: 0 }
  if (element.Type === 9) element.Size = Math.max(8, Math.min(value.width, value.height))
  else if (element.Type === 4) {
    const raw = barcodeRawSizeFromOccupied(Math.max(8, value.width), Math.max(8, value.height),
      element.HumanReadableReserveHeight ?? 0, element.Rotation ?? 0, element.QuietZone)
    element.Width = Math.max(8, raw.width)
    element.Height = Math.max(8, raw.height)
    const occupied = barcodeOccupiedSize(element.Width, element.Height,
      element.HumanReadableReserveHeight ?? 0, element.Rotation ?? 0, element.QuietZone)
    if (element.EndCoordinate) element.EndCoordinate = { X: start.X + occupied.width, Y: start.Y + occupied.height }
  }
  else if ('Width' in element && 'Height' in element) {
    const rawSize = element.Type === 2 && (element.Rotation === 90 || element.Rotation === 270)
      ? { width: value.height, height: value.width }
      : { width: value.width, height: value.height }
    element.Width = Math.max(8, rawSize.width)
    element.Height = Math.max(8, rawSize.height)
    if (element.EndCoordinate) {
      const occupied = element.Type === 2
        ? rotatedSize(element.Width, element.Height, element.Rotation ?? 0)
        : { width: element.Width ?? value.width, height: element.Height ?? value.height }
      element.EndCoordinate = { X: start.X + occupied.width, Y: start.Y + occupied.height }
    }
  }
  selectedPath.value = value.path
}

function saveSelectedGroup() {
  try {
    saveGroupComponent(template.value, selectedPath.value, componentName.value.trim())
    showToast(`组件 ${componentName.value.trim()} 已保存到模板`)
    componentName.value = ''
  } catch (error) { showToast(error instanceof Error ? error.message : '组件保存失败。', 'error') }
}

function insertComponent(id: string) {
  try { selectedPath.value = appendComponent(template.value, activeCellPath(), id); showToast(`已插入组件 ${id}`) }
  catch (error) { showToast(error instanceof Error ? error.message : '组件插入失败。', 'error') }
}

function addTable() {
  try {
    appendDataTable(template.value, tableSource.value, tableColumns.value, tableRepeatHeader.value)
    selectedPath.value = `Rows[${template.value.Rows.length - 2}]`
    showTableWizard.value = false
    showToast('已添加动态表格')
  } catch (error) { showToast(error instanceof Error ? error.message : '表格创建失败。', 'error') }
}

function openTableWizard() {
  const source = tableSourceOptions.value[0]
  if (source) tableSource.value = source
  if (tableItemFields.value.length) tableColumns.value = tableItemFields.value.slice(0, 3)
    .map((path) => ({ title: path, path, alignment: 0 }))
  showTableWizard.value = !showTableWizard.value
}

function moveTableColumn(index: number, direction: -1 | 1) {
  const next = index + direction
  if (next < 0 || next >= tableColumns.value.length) return
  ;[tableColumns.value[index], tableColumns.value[next]] = [tableColumns.value[next], tableColumns.value[index]]
}

function insertPastedTable() {
  try {
    const parsed = parseTextTable(pastedTable.value, { firstRowHeader: true })
    appendStaticTable(template.value, parsed.headers, parsed.rows)
    selectedPath.value = `Rows[${template.value.Rows.length - parsed.rows.length - 1}]`
    showTableImport.value = false
    pastedTable.value = ''
    showToast('文字表格已插入')
  } catch (error) { showToast(error instanceof Error ? error.message : '文字表格解析失败。', 'error') }
}

function addRow() {
  const index = template.value.Rows.length
  template.value.Rows.push({ Height: 64, Cells: [{ Width: template.value.Width, Elements: [] }] })
  selectedPath.value = `Rows[${index}]`
}

function addColumn() {
  const selectedRow = findRow(template.value, selectedRowPath()) ?? template.value.Rows[0]
  if (!selectedRow) { addRow(); return }
  selectedRow.Cells ??= []
  selectedRow.Cells.push({ Width: 0, Elements: [] })
  const rowPath = selectedRowPath()
  selectedPath.value = `${rowPath}.Cells[${selectedRow.Cells.length - 1}]`
}

function addNestedRow() {
  const path = appendNestedRow(template.value, activeCellPath())
  if (!path) return
  selectedPath.value = path
  showToast('已添加嵌套行')
}

function selectedRowPath(): string {
  if (findRow(template.value, selectedPath.value)) return selectedPath.value
  const segments = selectedPath.value.split('.')
  let lastCell = -1
  for (let index = 0; index < segments.length; index += 1) if (segments[index].startsWith('Cells[')) lastCell = index
  return lastCell > 0 ? segments.slice(0, lastCell).join('.') : 'Rows[0]'
}

function moveElement(value: { path: string; dx: number; dy: number; rendered?: PreviewElement }) {
  const element = findElement(template.value, value.path)
  if (!element) return
  if (element.Type === 6 && !element.StartCoordinate) {
    const definition = template.value.Components?.find((item) => item.Id === element.ComponentId)
    if (element.Rows || definition?.Rows) {
      if (value.dy !== 0) selectedPath.value = reorderFlowGroup(template.value, value.path, Math.sign(value.dy) as -1 | 1) ?? value.path
      offsetFlowGroup(element, value.dx, 0)
    } else {
      offsetFlowGroup(element, value.dx, value.dy)
    }
    return
  }
  if (!element.StartCoordinate) {
    const pages = [currentPage.value, ...previewPages.value].filter((page): page is PreviewPage => Boolean(page))
    const page = pages.find((candidate) => candidate.elements.some((part) => part === value.rendered))
      ?? pages.find((candidate) => candidate.elements.some((part) => part.sourcePath === value.path || part.definitionPath === value.path))
    const rendered = value.rendered ?? page?.elements.find((part) => (part.sourcePath === value.path || part.definitionPath === value.path) && part.kind === 'element')
    const renderedPath = rendered?.sourcePath ?? value.path
    const parentPath = renderedPath.slice(0, renderedPath.lastIndexOf('.Elements['))
    const container = page?.elements.find((part) => part.sourcePath === parentPath &&
      (part.kind === 'cell' || part.type === 6) && part.instanceIndices?.join(',') === rendered?.instanceIndices?.join(','))
    if (!rendered || !container || previewSource.value !== 'service' ||
      !materializeElementPosition(element, rendered, container, value.dx !== 0)) {
      showToast('自动排列的元素需要先完成引擎预览再移动。', 'error')
      return
    }
  }
  translateElement(element, value.dx, value.dy)
  selectedPath.value = value.path
}

function setElementPosition(axis: 'X' | 'Y', target: number) {
  const element = selectedElement.value
  if (!element) return
  if (element.Type === 6 && !element.StartCoordinate && setFlowGroupPosition(element, axis, target)) {
    return
  }
  if (!element.StartCoordinate) {
    const page = [currentPage.value, ...previewPages.value].find((candidate) => candidate?.elements.some((part) =>
      part.sourcePath === selectedPath.value && part.kind === 'element'))
    const rendered = page?.elements.find((part) => part.sourcePath === selectedPath.value && part.kind === 'element')
    const parentPath = selectedPath.value.slice(0, selectedPath.value.lastIndexOf('.Elements['))
    const container = page?.elements.find((part) => part.sourcePath === parentPath &&
      (part.kind === 'cell' || part.type === 6) && part.instanceIndices?.join(',') === rendered?.instanceIndices?.join(','))
    if (!rendered || !container || previewSource.value !== 'service' ||
      !materializeElementPosition(element, rendered, container, axis === 'X')) {
      showToast('自动排列的元素需要先完成引擎预览再编辑坐标。', 'error')
      return
    }
  }
  const delta = target - element.StartCoordinate![axis]
  translateElement(element, axis === 'X' ? delta : 0, axis === 'Y' ? delta : 0)
}

function removeSelected() {
  const elementMatch = /^(.*)\.Elements\[(\d+)\]$/.exec(selectedPath.value)
  if (elementMatch) {
    const cell = findCell(template.value, elementMatch[1])
    const group = findElement(template.value, elementMatch[1])
    const definition = findComponent(template.value, elementMatch[1])
    const target = cell?.Elements ?? (group?.Type === 6 ? group.ComponentId
      ? template.value.Components?.find((item) => item.Id === group.ComponentId)?.Elements : group.Elements : undefined) ?? definition?.Elements
    target?.splice(Number(elementMatch[2]), 1)
    pruneUnusedResources()
    selectedPath.value = elementMatch[1]
    return
  }
  const cellMatch = /^(.*)\.Cells\[(\d+)\]$/.exec(selectedPath.value)
  if (cellMatch) {
    const row = findRow(template.value, cellMatch[1])
    row?.Cells?.splice(Number(cellMatch[2]), 1)
    pruneUnusedResources()
    selectedPath.value = cellMatch[1]
    return
  }
  const nestedRowMatch = /^(.*)\.Rows\[(\d+)\]$/.exec(selectedPath.value)
  if (nestedRowMatch) {
    const parent = findCell(template.value, nestedRowMatch[1]) ?? findElement(template.value, nestedRowMatch[1]) ?? findComponent(template.value, nestedRowMatch[1])
    if (parent && 'Rows' in parent) parent.Rows?.splice(Number(nestedRowMatch[2]), 1)
    pruneUnusedResources()
    selectedPath.value = nestedRowMatch[1]
    return
  }
  if (selectedDefinition.value) {
    try { removeComponent(template.value, selectedDefinition.value.Id); pruneUnusedResources(); selectedPath.value = '' }
    catch (error) { showToast(error instanceof Error ? error.message : '删除失败。', 'error') }
    return
  }
  const rowMatch = /^Rows\[(\d+)\]$/.exec(selectedPath.value)
  if (rowMatch) {
    template.value.Rows.splice(Number(rowMatch[1]), 1)
    pruneUnusedResources()
    selectedPath.value = ''
  }
}

function pruneUnusedResources() {
  const selection = collectResourceKeys(template.value)
  if (selection.dynamic) return
  resources.value = resources.value.filter((resource) => selection.keys.has(resource.key))
}

function openTemplatePicker() { jsonInput.value?.click() }
function openImagePicker() {
  if (selectedElement.value?.Type !== 2 && !canAddElement(2)) { showCapabilityReason(2); return }
  imageInput.value?.click()
}

async function importFile(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0]
  if (!file) return
  try {
    if (file.name.toLowerCase().endsWith('.zip')) {
      const imported = await importTemplateBundle(file)
      resources.value = imported.resources
      templateName.value = file.name.replace(/\.zip$/i, '')
      replaceTemplate(imported.template, '', imported.outputSettings ?? defaultDesignerOutputSettings())
      showToast(`已导入模板包 · ${imported.resources.length} 个资源`)
    } else {
      if (file.size > MAX_TEMPLATE_JSON_BYTES) throw new Error('JSON 模板超过 1 MiB 上限。')
      const text = await file.text()
      const imported = parseDesignerProject(JSON.parse(text) as unknown)
      resources.value = []
      templateName.value = file.name.replace(/\.json$/i, '')
      replaceTemplate(imported.template, '', imported.outputSettings ?? defaultDesignerOutputSettings())
      showToast('模板 JSON 已导入')
    }
    sampleId.value = ''
  } catch (error) {
    showToast(error instanceof Error ? error.message : '导入失败。', 'error')
  } finally {
    ;(event.target as HTMLInputElement).value = ''
  }
}

function downloadBlob(blob: Blob, name: string) {
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = name
  anchor.click()
  URL.revokeObjectURL(url)
}

function exportJson() {
  try {
    assertTemplate(template.value)
    const json = JSON.stringify(template.value, null, 2)
    if (new TextEncoder().encode(json).length > MAX_TEMPLATE_JSON_BYTES) throw new Error('模板 JSON 超过 1 MiB 上限。')
    downloadBlob(new Blob([json], { type: 'application/json;charset=utf-8' }), 'easyprint-template.json')
    showToast(resources.value.length ? 'JSON 已导出，图片资源需使用模板包导出' : '模板 JSON 已导出', resources.value.length ? 'warning' : 'success')
  } catch (error) { showToast(error instanceof Error ? error.message : '导出失败。', 'error') }
}

async function exportBundle() {
  try {
    if (labelProfileError.value) throw new Error(labelProfileError.value)
    flushHistorySnapshot()
    const snapshot = cloneTemplate(editorSnapshot())
    const selection = collectResourceKeys(snapshot.template)
    const blob = await exportTemplateBundle(snapshot.template, snapshot.resources, snapshot.outputSettings)
    downloadBlob(blob, 'easyprint-template-package.zip')
    if (selection.dynamic || snapshot.resources.every((resource) => selection.keys.has(resource.key)))
      savedSnapshot.value = JSON.stringify(snapshot)
    showToast(`模板包已导出 · ${snapshot.resources.filter((resource) => selection.dynamic || selection.keys.has(resource.key)).length} 个资源`)
  } catch (error) { showToast(error instanceof Error ? error.message : '模板包导出失败。', 'error') }
}

async function saveProject() {
  if (labelProfileError.value) { showToast(labelProfileError.value, 'error'); return }
  if (resources.value.length || escPosDevice.value.rasterFonts !== undefined) { await exportBundle(); return }
  try {
    flushHistorySnapshot()
    const json = serializeDesignerProject(template.value, outputSettingsSnapshot())
    if (new TextEncoder().encode(json).length > MAX_TEMPLATE_JSON_BYTES) throw new Error('设计器项目 JSON 超过 1 MiB 上限。')
    downloadBlob(new Blob([json], { type: 'application/json;charset=utf-8' }), 'easyprint-designer-project.json')
    savedSnapshot.value = JSON.stringify(editorSnapshot())
    showToast('设计器项目已保存')
  } catch (error) { showToast(error instanceof Error ? error.message : '保存项目失败。', 'error') }
}

async function importImage(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  try {
    const key = `resources/${Date.now()}-${file.name.replace(/[\\/:*?"<>|]/g, '_')}`
    const resource = await resourceForImage(key, file)
    resources.value = [...resources.value.filter((item) => item.key !== key), resource]
    if (selectedElement.value?.Type === 2) {
      selectedElement.value.Field = bindImageResource(selectedElement.value.Field, key)
    } else {
      if (!addElement(2)) return
      const added = findElement(template.value, selectedPath.value)
      if (added?.Type === 2) added.Field = bindImageResource(added.Field, key)
    }
    showToast(`图片资源已导入：${file.name}`)
  } catch (error) { showToast(error instanceof Error ? error.message : '图片导入失败。', 'error') }
  finally { input.value = '' }
}

async function exportPrintData() {
  if (labelProfileError.value) { showToast(labelProfileError.value, 'error'); return }
  if (parsedData.value.error) { showToast('请先修正业务数据 JSON。', 'error'); return }
  if (customCutCapabilitiesUnknown.value) { showToast('当前 Host 未声明所需裁切能力，请更新 Host 后再导出。', 'error'); return }
  try {
    const blob = await renderTemplate(template.value, parsedData.value.value, resources.value,
      printProtocol.value, escPosLayoutMode.value, allowRasterizedLines.value,
      imageThreshold.value, usesDeviceFontCalibration.value ? deviceHalfCellWidthAt24.value : undefined,
      usesDeviceFontCalibration.value ? deviceFullCellWidthAt24.value : undefined,
      jobEnabled.value ? undefined : copies.value,
      jobEnabled.value ? undefined : (usesFeedLines.value ? feedLines.value : undefined),
      jobEnabled.value ? undefined : (usesCutAfterPrint.value ? cutAfterPrint.value : undefined),
      usesTsplMedia.value ? tsplMedia.value : undefined,
      usesZplMedia.value ? zplMedia.value : undefined,
      effectiveEncoding.value,
      effectiveEscPosDevice.value,
      usesDeviceFontCalibration.value ? deviceLineHeightAt24.value : undefined,
      usesDeviceFontCalibration.value ? requireDeclaredNativeBounds.value : undefined,
       deviceFontCalibrationsForRequest.value, job.value,
       jobEnabled.value ? undefined : cutOptions.value)
    downloadBlob(blob, `easyprint-${printProtocol.value.toLowerCase()}-pages.zip`)
    showToast(`${printProtocol.value.toUpperCase()} 页面包已导出`)
  } catch (error) {
    if (error instanceof DesignerValidationError) {
      setPreviewDiagnostics(error.diagnostics)
      if (!previewDiagnostics.value.length) previewError.value = error.message
      previewPages.value = []
      previewSource.value = 'service'
      statusMessage.value = '打印指令导出失败'
      statusTone.value = 'error'
    }
    showToast(error instanceof Error ? error.message : '打印指令导出失败。', 'error')
  }
}

function onKeyDown(event: KeyboardEvent) {
  const target = event.target as HTMLElement
  const editing = target.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName)
  if (!editing && !event.ctrlKey && !event.metaKey && !event.altKey) {
    const shortcuts: Record<string, TemplateElement['Type']> = { t: 1, q: 3, b: 4, l: 5 }
    const type = shortcuts[event.key.toLowerCase()]
    if (type) { event.preventDefault(); addElement(type); return }
    if (event.key.toLowerCase() === 'i') { event.preventDefault(); openImagePicker(); return }
  }
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'z') {
    event.preventDefault()
    if (event.shiftKey) redo(); else undo()
  } else if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'y') {
    event.preventDefault(); redo()
  } else if (!editing && (event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'd') {
    event.preventDefault(); duplicateSelected()
  } else if (!editing && ['Delete', 'Backspace'].includes(event.key)) {
    event.preventDefault(); removeSelected()
  } else if (!editing && event.key === 'Escape') {
    mobileLayersOpen.value = false; mobileInspectorOpen.value = false
  } else if (!editing) {
    const delta = getNudgeDelta(event.key, event.shiftKey ? 10 : 1)
    if (!delta || !selectedElement.value) return
    event.preventDefault()
    moveElement({ path: selectedPath.value, ...delta })
  }
}

function focusDataJson() { activeDataTab.value = 'json'; dataTextarea.value?.focus() }
</script>

<template>
  <main class="designer-app" @keydown="onKeyDown" tabindex="-1">
    <header class="app-header">
      <div class="brand-lockup">
        <div class="brand-mark"><span>B</span><i></i></div>
        <div class="brand-copy"><strong>Bing.EasyPrint</strong><span>模板设计器</span></div>
      </div>
      <div class="header-divider"></div>
      <label class="sample-picker"><span>模板</span><select v-model="sampleId" @change="useSample"><option disabled value="">选择模板</option><option v-for="option in sampleOptions" :key="option.id" :value="option.id">{{ option.name }}</option></select><ChevronDown :size="14" /></label>
      <div class="file-state"><FileText :size="14" /><span>{{ templateName }}</span><span v-if="isDirty" class="dirty-dot"></span></div>
      <div class="header-actions">
        <button class="icon-button header-quiet" type="button" title="撤销 Ctrl+Z" :disabled="!canUndo" @click="undo"><Undo2 :size="16" /></button>
        <button class="icon-button header-quiet" type="button" title="重做 Ctrl+Y" :disabled="!canRedo" @click="redo"><Redo2 :size="16" /></button>
        <span class="header-divider small"></span>
        <label class="unit-selector" title="坐标单位"><Ruler :size="14" /><select v-model="unit"><option value="pt">点</option><option value="mm">mm</option></select><ChevronDown :size="12" /></label>
        <label class="protocol-selector" title="输出协议"><select v-model="outputTarget" aria-label="输出协议"><option value="Cpcl">CPCL</option><option value="EscPosRaster">ESC Raster</option><option value="EscPosNative">ESC Native</option><option value="Tspl">TSPL</option><option value="Zpl">ZPL</option></select><ChevronDown :size="12" /></label>
        <label v-if="printProtocol === 'Tspl' || printProtocol === 'Zpl'" class="raster-line-option" title="允许斜线转换为位图"><input v-model="allowRasterizedLines" type="checkbox" aria-label="允许斜线转换为位图" /><span>位图斜线</span></label>
        <div class="output-settings-wrap">
          <button class="icon-button output-settings-button" type="button" title="输出校准设置" :aria-expanded="showOutputSettings" @click="showOutputSettings = !showOutputSettings"><Ruler :size="15" /></button>
          <div v-if="showOutputSettings" class="output-settings-panel">
            <div class="output-settings-title">输出设置</div>
            <label class="toggle-row"><span>任务级输出</span><input type="checkbox" :checked="jobEnabled" aria-label="启用任务级输出" @change="setJobEnabled" /></label>
            <p v-if="jobEnabled" class="field-note">任务模式统一管理份数、进纸和切纸；旧页面字段已清除。</p>
            <template v-if="jobEnabled">
              <div class="output-settings-title">任务顺序与收尾</div>
              <label class="field-label">任务份数 <span>默认 1</span><input class="field-input" type="number" min="1" step="1" :value="optionalNumberValue(job?.copies)" aria-label="任务打印份数" @input="setJobNumber('copies', $event)" /></label>
              <label class="field-label">份数顺序
                <select class="field-input" :value="job?.copyOrder ?? 'Collated'" aria-label="任务份数顺序" @change="setJobCopyOrder">
                  <option v-for="order in jobCopyOrderOptions" :key="order" :value="order">{{ order === 'Collated' ? '整份连续' : '同页连续' }}</option>
                </select>
              </label>
              <label class="field-label">进纸范围
                <select class="field-input" :value="job?.feedScope ?? 'None'" aria-label="任务进纸范围" @change="setJobFeedScope">
                  <option v-for="scope in jobScopeOptions" :key="scope" :value="scope">{{ jobScopeLabels[scope] }}</option>
                </select>
              </label>
              <label class="field-label">进纸单位
                <select class="field-input" :value="job?.feedUnit ?? 'Lines'" aria-label="任务进纸单位" @change="setJobFeedUnit">
                  <option v-for="unitOption in jobFeedUnitOptions" :key="unitOption" :value="unitOption" :disabled="capabilityQueryReady && !capabilities?.jobFeedUnits.includes(unitOption)">{{ jobFeedUnitLabels[unitOption] }}</option>
                </select>
              </label>
              <label class="field-label">进纸数量 <span>非负整数</span><input class="field-input" type="number" min="0" step="1" :value="optionalNumberValue(job?.feedAmount)" aria-label="任务进纸数量" @input="setJobNumber('feedAmount', $event)" /></label>
               <label class="field-label">切纸范围
                 <select class="field-input" :value="job?.cutScope ?? 'None'" aria-label="任务切纸范围" @change="setJobCutScope">
                   <option v-for="scope in jobScopeOptions" :key="scope" :value="scope" :disabled="scope !== 'None' && capabilityQueryReady && !capabilities?.supportsCut">{{ jobScopeLabels[scope] }}</option>
                 </select>
               </label>
               <label class="toggle-row"><span>自定义裁切参数</span><input type="checkbox" :checked="jobCutOptionsEnabled" :disabled="!job || job.cutScope === 'None'" aria-label="启用任务自定义裁切参数" @change="setJobCutOptionsEnabled" /></label>
               <template v-if="job?.cutOptions">
                 <label class="field-label">切法
                   <select class="field-input" :value="job.cutOptions.mode ?? 'Full'" aria-label="任务切法" @change="setCutOptionMode('job', $event)">
                     <option v-for="mode in cutModeOptions" :key="mode" :value="mode" :disabled="!capabilityQueryReady || !cutModeSupported(mode)">{{ mode === 'Full' ? '完全切断' : '部分切断' }}</option>
                   </select>
                 </label>
                 <label class="toggle-row"><span>进纸到切刀位置</span><input type="checkbox" :checked="job.cutOptions.feedToCutPosition ?? false" :disabled="!capabilityQueryReady || (!job.cutOptions.feedToCutPosition && !capabilities?.cutFeedUnits.length)" aria-label="任务进纸到切刀位置" @change="setCutOptionFeedToCutPosition('job', $event)" /></label>
                 <label class="field-label">切前补偿量 <span>0–Int32</span><input class="field-input" type="number" min="0" max="2147483647" step="1" :disabled="!(job.cutOptions.feedToCutPosition ?? false) && !(job.cutOptions.feedBeforeCutAmount ?? 0)" :value="optionalNumberValue(job.cutOptions.feedBeforeCutAmount)" aria-label="任务切前补偿量" @input="setCutOptionAmount('job', $event)" /></label>
                 <label class="field-label">补偿单位
                   <select class="field-input" :value="job.cutOptions.feedBeforeCutUnit ?? 'MotionUnits'" aria-label="任务切前补偿单位" @change="setCutOptionFeedUnit('job', $event)">
                     <option v-for="unitOption in cutFeedUnitOptions" :key="unitOption" :value="unitOption" :disabled="!capabilityQueryReady || !cutFeedUnitSupported(unitOption)">{{ unitOption === 'MotionUnits' ? 'MotionUnits' : unitOption === 'Dots' ? '点' : '行' }}</option>
                   </select>
                 </label>
                 <button class="field-clear-action" type="button" @click="clearJobCutOptions">清除自定义裁切参数</button>
                 <p v-if="capabilityQueryReady && job.cutOptions && !cutModeSupported(job.cutOptions.mode ?? 'Full')" class="field-note capability-warning">当前机型不支持所选切法，已保留该值供纠正。</p>
                 <p v-if="capabilityQueryReady && job.cutOptions && (job.cutOptions.feedBeforeCutAmount ?? 0) > 0 && !cutFeedUnitSupported(job.cutOptions.feedBeforeCutUnit ?? 'MotionUnits')" class="field-note capability-warning">当前机型不支持所选切前补偿单位，已保留该值供纠正。</p>
                 <p v-if="capabilityQueryReady && job.cutOptions && !cutFeedAmountWithinCapability(job.cutOptions)" class="field-note capability-warning">切前补偿量超出当前机型范围，已保留当前值供纠正。</p>
               </template>
               <p v-if="capabilityQueryReady && job && (job.feedAmount ?? 0) > 0 && !capabilities?.jobFeedUnits.includes(job.feedUnit ?? 'Lines')" class="field-note capability-warning">当前协议不支持所选进纸单位，保留导入值，提交时由服务严格校验。</p>
              <p v-if="capabilityQueryReady && job && job.cutScope !== 'None' && !capabilities?.supportsCut" class="field-note capability-warning">当前机型不支持切纸，保留导入值，提交时由服务严格校验。</p>
              <p v-if="jobUncollatedWarning" class="field-note capability-warning">同页连续的多页多份任务需要服务端知道页数；不支持时会严格拒绝，界面保留当前值。</p>
            </template>
            <template v-else>
              <label class="field-label">打印份数 <span>协议默认</span><input class="field-input" type="number" min="1" step="1" :value="optionalNumberValue(copies)" aria-label="打印份数" placeholder="协议默认" @input="setPageNumber('copies', $event)" /></label>
              <label v-if="usesFeedLines" class="field-label">额外进纸 <span>行</span><input class="field-input" type="number" min="0" step="1" :disabled="!capabilityQueryReady || !capabilities?.supportsExtraFeed" :value="optionalNumberValue(feedLines)" aria-label="额外进纸行数" placeholder="协议默认" @input="setPageNumber('feedLines', $event)" /></label>
               <label v-if="usesCutAfterPrint" class="field-label">打印后切纸 <span>协议默认</span><select class="field-input" :value="cutAfterPrint === undefined ? '' : String(cutAfterPrint)" aria-label="打印后切纸" @change="setCutAfterPrint"><option value="">协议默认</option><option value="true" :disabled="!capabilityQueryReady || !capabilities?.supportsCut">启用</option><option value="false">禁用</option></select></label>
               <button v-if="usesCutAfterPrint && cutAfterPrint === true && capabilityQueryReady && capabilities && !capabilities.supportsCut" class="field-clear-action" type="button" @click="cutAfterPrint = undefined">清除不支持的切刀设置</button>
               <p v-if="usesCutAfterPrint && capabilityQueryReady && capabilities && !capabilities.supportsCut" class="field-note">当前机型不支持切刀；显式启用仍保留，选择协议默认或点击清除即可。</p>
               <template v-if="usesCutAfterPrint">
                 <label class="toggle-row"><span>自定义裁切参数</span><input type="checkbox" :checked="pageCutOptionsEnabled" :disabled="!pageCutOptionsEnabled && (!capabilityQueryReady || !capabilities?.supportsCut)" aria-label="启用页面自定义裁切参数" @change="setPageCutOptionsEnabled" /></label>
                 <template v-if="cutOptions">
                   <label class="field-label">切法
                     <select class="field-input" :value="cutOptions.mode ?? 'Full'" aria-label="页面切法" @change="setCutOptionMode('page', $event)">
                       <option v-for="mode in cutModeOptions" :key="mode" :value="mode" :disabled="!capabilityQueryReady || !cutModeSupported(mode)">{{ mode === 'Full' ? '完全切断' : '部分切断' }}</option>
                     </select>
                   </label>
                   <label class="toggle-row"><span>进纸到切刀位置</span><input type="checkbox" :checked="cutOptions.feedToCutPosition ?? false" :disabled="!capabilityQueryReady || (!cutOptions.feedToCutPosition && !capabilities?.cutFeedUnits.length)" aria-label="页面进纸到切刀位置" @change="setCutOptionFeedToCutPosition('page', $event)" /></label>
                   <label class="field-label">切前补偿量 <span>0–Int32</span><input class="field-input" type="number" min="0" max="2147483647" step="1" :disabled="!(cutOptions.feedToCutPosition ?? false) && !(cutOptions.feedBeforeCutAmount ?? 0)" :value="optionalNumberValue(cutOptions.feedBeforeCutAmount)" aria-label="页面切前补偿量" @input="setCutOptionAmount('page', $event)" /></label>
                   <label class="field-label">补偿单位
                     <select class="field-input" :value="cutOptions.feedBeforeCutUnit ?? 'MotionUnits'" aria-label="页面切前补偿单位" @change="setCutOptionFeedUnit('page', $event)">
                       <option v-for="unitOption in cutFeedUnitOptions" :key="unitOption" :value="unitOption" :disabled="!capabilityQueryReady || !cutFeedUnitSupported(unitOption)">{{ unitOption === 'MotionUnits' ? 'MotionUnits' : unitOption === 'Dots' ? '点' : '行' }}</option>
                     </select>
                   </label>
                   <button class="field-clear-action" type="button" @click="clearPageCutOptions">清除自定义裁切参数</button>
                   <p v-if="capabilityQueryReady && !cutModeSupported(cutOptions.mode ?? 'Full')" class="field-note capability-warning">当前机型不支持所选切法，已保留该值供纠正。</p>
                   <p v-if="capabilityQueryReady && (cutOptions.feedBeforeCutAmount ?? 0) > 0 && !cutFeedUnitSupported(cutOptions.feedBeforeCutUnit ?? 'MotionUnits')" class="field-note capability-warning">当前机型不支持所选切前补偿单位，已保留该值供纠正。</p>
                   <p v-if="capabilityQueryReady && !cutFeedAmountWithinCapability(cutOptions)" class="field-note capability-warning">切前补偿量超出当前机型范围，已保留当前值供纠正。</p>
                 </template>
               </template>
             </template>
            <label class="field-label">输出编码 <span>协议默认</span><select class="field-input" :value="effectiveEncoding ?? ''" aria-label="输出编码" @change="setEncoding"><option value="">协议默认</option><option v-if="printProtocol !== 'Zpl'" value="Gbk" :disabled="labelEncodingDisabled(936)">GBK</option><option value="Utf8" :disabled="labelEncodingDisabled(65001)">UTF-8</option><option value="Ascii" :disabled="labelEncodingDisabled(20127)">ASCII</option><option v-if="escPosLayoutMode === 'Native' && escPosDevice.profile?.text" value="Device">Device · 机型代码页</option></select></label>
            <p v-if="labelEncodingWarning" class="field-note capability-warning">{{ labelEncodingWarning }}</p>
            <template v-if="usesEscPosDevice">
              <div class="output-settings-title">ESC/POS 设备</div>
              <label class="field-label">设备 DPI <span>正整数</span><input class="field-input" type="number" min="1" step="1" :value="optionalNumberValue(escPosDevice.dpi)" aria-label="ESC/POS 设备 DPI" placeholder="协议默认" @input="setEscPosDeviceNumber('dpi', $event)" /></label>
              <label class="field-label">代码页索引 <span>0–255</span><input class="field-input" type="number" min="0" max="255" step="1" :value="optionalNumberValue(escPosDevice.codePageIndex)" aria-label="ESC/POS 代码页索引" placeholder="协议默认" @input="setEscPosDeviceNumber('codePageIndex', $event)" /></label>
              <label class="field-label">打印区域宽度 <span>0–65535 单位</span><input class="field-input" type="number" min="0" max="65535" step="1" :value="optionalNumberValue(escPosDevice.printAreaWidthUnits)" aria-label="ESC/POS 打印区域宽度" placeholder="协议默认" @input="setEscPosDeviceNumber('printAreaWidthUnits', $event)" /></label>
              <label v-if="escPosLayoutMode === 'Native'" class="field-label">Native 排版流
                <select class="field-input" :value="escPosDevice.nativeFlow ?? 'Rows'" :disabled="!capabilityQueryReady" aria-label="ESC/POS Native 排版流" @change="setEscPosNativeFlow">
                  <option value="Rows">Rows · 兼容默认</option>
                  <option value="Sequential">Sequential · 混合收据</option>
                </select>
              </label>
              <p v-if="escPosLayoutMode === 'Native'" class="field-note">Sequential 按元素逻辑 Y 顺序输出文字、ASCII Tab 列、Code128 和二维码；设备按原生进纸推进，不承诺点对点坐标。</p>
              <label v-if="escPosLayoutMode === 'Native'" class="field-label">原生 Tab 宽度 <span>8 的倍数</span><input class="field-input" type="number" min="8" step="8" :value="optionalNumberValue(escPosDevice.nativeTabWidthDots)" aria-label="ESC/POS 原生 Tab 宽度" placeholder="协议默认" @input="setEscPosDeviceNumber('nativeTabWidthDots', $event)" /></label>
              <div class="output-settings-title">ESC/POS 机型配置</div>
              <div class="output-settings-actions">
                <button class="button-secondary" type="button" @click="createEscPosProfile">新建设备配置</button>
                <button class="button-secondary" type="button" @click="createEightDotImpactProfile">8-dot impact 预设</button>
                <button v-if="escPosDevice.profile" class="button-secondary" type="button" @click="clearEscPosProfile">清除机型配置</button>
              </div>
              <template v-if="escPosDevice.profile">
                <button class="wide-action" type="button" @click="escPosProfileExpanded = !escPosProfileExpanded">{{ escPosProfileExpanded ? '收起机型参数' : '编辑机型参数' }}</button>
                <div v-if="escPosProfileExpanded" class="profile-settings">
                  <label class="field-label">配置名称<input class="field-input" type="text" :value="escPosDevice.profile.name" aria-label="ESC/POS 配置名称" @input="setEscPosProfileName" /></label>
                  <label class="field-label">图片指令<select class="field-input" :value="escPosDevice.profile.imageCommand" aria-label="ESC/POS 图片指令" @change="setEscPosProfileImageCommand"><option value="RasterGsV0">GS v 0 光栅</option><option value="BitImageEscStar">ESC * 点阵</option></select></label>
                  <label v-if="escPosDevice.profile.imageCommand === 'BitImageEscStar'" class="field-label">ESC * 点阵模式<select class="field-input" :value="escPosDevice.profile.bitImageMode" aria-label="ESC/POS ESC 星点阵模式" @change="setEscPosProfileBitImageMode"><option value="SingleDensity8">8 点单密度</option><option value="DoubleDensity8">8 点双密度</option><option value="SingleDensity24">24 点单密度</option><option value="DoubleDensity24">24 点双密度</option></select></label>
                  <div class="numeric-grid">
                    <label class="field-label">横向 DPI<input class="field-input" type="number" min="1" step="1" :value="escPosDevice.profile.horizontalDpi" aria-label="ESC/POS 横向 DPI" @input="setEscPosProfileNumber('horizontalDpi', $event)" /></label>
                    <label class="field-label">纵向 DPI<input class="field-input" type="number" min="1" step="1" :value="escPosDevice.profile.verticalDpi" aria-label="ESC/POS 纵向 DPI" @input="setEscPosProfileNumber('verticalDpi', $event)" /></label>
                    <label class="field-label">进纸单位 / 英寸<input class="field-input" type="number" min="1" step="1" :value="escPosDevice.profile.motionUnitsPerInch" aria-label="ESC/POS 进纸单位" @input="setEscPosProfileNumber('motionUnitsPerInch', $event)" /></label>
                    <label class="field-label">可打印宽度 <span>点，可选</span><input class="field-input" type="number" min="1" step="1" :value="optionalNumberValue(escPosDevice.profile.printableWidthDots)" aria-label="ESC/POS 可打印宽度" placeholder="填写设备纸宽" @input="setEscPosProfileNumber('printableWidthDots', $event)" /></label>
                    <label class="field-label">图片分块预算 <span>字节，可选</span><input class="field-input" type="number" min="1" step="1" :value="optionalNumberValue(escPosDevice.profile.maxImageBlockBytes)" aria-label="ESC/POS 图片分块预算" placeholder="不分块" @input="setEscPosProfileNumber('maxImageBlockBytes', $event)" /></label>
                  </div>
                   <div class="style-controls">
                     <label class="toggle-row"><span>支持切刀</span><input type="checkbox" :checked="escPosDevice.profile.supportsCut" aria-label="ESC/POS 支持切刀" @change="setEscPosProfileBoolean('supportsCut', $event)" /></label>
                     <label class="field-label">切法白名单 <span>默认跟随旧能力</span>
                       <select class="field-input" :value="escPosProfileCutModesValue(escPosDevice.profile.supportedCutModes)" aria-label="ESC/POS 切法白名单" @change="setEscPosProfileCutModes">
                         <option value="">默认（Full / Partial）</option>
                         <option value="Full">仅 Full</option>
                         <option value="Partial">仅 Partial</option>
                         <option value="Full,Partial">Full + Partial</option>
                         <option value="None">禁用切刀</option>
                       </select>
                     </label>
                     <label class="toggle-row"><span>支持进纸到切刀</span><input type="checkbox" :checked="escPosDevice.profile.supportsFeedAndCut ?? false" aria-label="ESC/POS 支持进纸到切刀" @change="setEscPosProfileBoolean('supportsFeedAndCut', $event)" /></label>
                     <label class="toggle-row"><span>支持原生条码</span><input type="checkbox" :checked="escPosDevice.profile.supportsNativeBarcode" aria-label="ESC/POS 支持原生条码" @change="setEscPosProfileBoolean('supportsNativeBarcode', $event)" /></label>
                    <label class="toggle-row"><span>支持原生条码可读文字</span><input type="checkbox" :checked="escPosDevice.profile.supportsNativeBarcodeHumanReadable ?? true" aria-label="ESC/POS 支持原生条码可读文字" @change="setEscPosProfileBoolean('supportsNativeBarcodeHumanReadable', $event)" /></label>
                    <label class="field-label">原生 HRI 条码制式 <span>留空不按制式收窄</span><input class="field-input" :value="escPosDevice.profile.supportedNativeBarcodeHumanReadableTypes?.join(', ') ?? ''" aria-label="ESC/POS 原生 HRI 条码制式" placeholder="Code128, Ean13" @change="setEscPosProfileHriTypes" /></label>
                    <label class="field-label">原生 HRI 位置 <span>Above, Below, Both；留空不收窄</span><input class="field-input" :value="escPosDevice.profile.supportedNativeBarcodeHumanReadablePositions?.join(', ') ?? ''" aria-label="ESC/POS 原生 HRI 位置" placeholder="Below" @change="setEscPosProfileHriPositions" /></label>
                    <label class="toggle-row"><span>支持原生二维码</span><input type="checkbox" :checked="escPosDevice.profile.supportsNativeQrCode" aria-label="ESC/POS 支持原生二维码" @change="setEscPosProfileBoolean('supportsNativeQrCode', $event)" /></label>
                    <label class="toggle-row"><span>支持 GSW 打印区域</span><input type="checkbox" :checked="escPosDevice.profile.supportsPrintAreaWidth" aria-label="ESC/POS 支持 GSW 打印区域" @change="setEscPosProfileBoolean('supportsPrintAreaWidth', $event)" /></label>
                    <label class="toggle-row"><span>支持 GS! 文字倍数</span><input type="checkbox" :checked="escPosDevice.profile.supportsTextSize" aria-label="ESC/POS 支持 GS 感叹号文字倍数" @change="setEscPosProfileBoolean('supportsTextSize', $event)" /></label>
                    <label v-if="escPosDevice.profile.imageCommand === 'BitImageEscStar'" class="toggle-row"><span>ESC * 单向打印</span><input type="checkbox" :checked="escPosDevice.profile.unidirectionalBitImage" aria-label="ESC/POS ESC 星单向打印" @change="setEscPosProfileBoolean('unidirectionalBitImage', $event)" /></label>
                  </div>
                  <div class="output-settings-title">Native 文字契约</div>
                  <p class="field-note">仅 ESC/POS Native 使用；Raster 继续使用主机字体。启用后由服务严格检查编码和字库覆盖范围。</p>
                  <div class="output-settings-actions">
                    <button v-if="!escPosDevice.profile.text" class="button-secondary" type="button" @click="enableEscPosNativeText">启用文字契约</button>
                    <button v-else class="button-secondary" type="button" @click="clearEscPosNativeText">清除文字契约</button>
                  </div>
                  <template v-if="escPosDevice.profile.text">
                    <div class="numeric-grid">
                      <label class="field-label">编码代码页 <span>正整数</span><input class="field-input" type="number" min="1" step="1" :value="escPosDevice.profile.text.encodingCodePage" aria-label="ESC/POS Native 文字编码代码页" @change="setEscPosNativeTextNumber('encodingCodePage', $event)" /></label>
                      <label class="field-label">代码页索引 <span>0–255，可选</span><input class="field-input" type="number" min="0" max="255" step="1" :value="optionalNumberValue(escPosDevice.profile.text.codePageIndex)" aria-label="ESC/POS Native 文字代码页索引" placeholder="协议默认" @change="setEscPosNativeTextNumber('codePageIndex', $event)" /></label>
                      <label class="field-label">半角宽度 <span>点，可选</span><input class="field-input" type="number" min="1" step="1" :value="optionalNumberValue(escPosDevice.profile.text.halfCellWidthDots)" aria-label="ESC/POS Native 半角宽度" placeholder="主机默认" @change="setEscPosNativeTextNumber('halfCellWidthDots', $event)" /></label>
                      <label class="field-label">全角宽度 <span>点，可选</span><input class="field-input" type="number" min="1" step="1" :value="optionalNumberValue(escPosDevice.profile.text.fullCellWidthDots)" aria-label="ESC/POS Native 全角宽度" placeholder="主机默认" @change="setEscPosNativeTextNumber('fullCellWidthDots', $event)" /></label>
                    </div>
                    <label class="field-label">字符模式 <span>FsKanji 仅适用于 936 / 54936 / 950 / 949</span><select class="field-input" :value="escPosDevice.profile.text.characterMode ?? 'DeviceDefault'" aria-label="ESC/POS Native 文字字符模式" @change="setEscPosNativeTextMode"><option value="DeviceDefault">DeviceDefault</option><option value="SingleByte">SingleByte</option><option value="FsKanji">FsKanji</option></select></label>
                    <label class="field-label">Unicode 范围 <span>first-last 十进制，或 JSON 数组</span><input class="field-input" type="text" :value="nativeTextRangesValue(escPosDevice.profile.text.characterRanges)" aria-label="ESC/POS Native Unicode 文字范围" @change="setEscPosNativeTextRanges" /></label>
                    <label class="field-label">行高 <span>可选</span><select class="field-input" :value="escPosDevice.profile.text.lineHeightDots === undefined ? '' : String(escPosDevice.profile.text.lineHeightDots)" aria-label="ESC/POS Native 文字行高" @change="setEscPosNativeTextLineHeight"><option value="">主机默认</option><option value="24">24 点</option><option value="26">26 点</option></select></label>
                    <p v-if="escPosLayoutMode === 'Native' && capabilityQueryReady && !capabilities?.nativeText" class="field-note capability-warning">当前机型未声明 Native 文字契约，提交时由服务严格校验。</p>
                  </template>
                </div>
              </template>
              <template v-if="escPosLayoutMode === 'Raster'">
                <div class="output-settings-title">固定 Raster 字体</div>
                <p class="field-note">仅 Raster 使用；文字属性中的主机字体只能引用已声明别名。</p>
                <label class="field-label">条码可读文字字号 <span>1–255 点，默认 12</span><input class="field-input" type="number" min="1" max="255" step="1" :value="optionalNumberValue(escPosDevice.rasterBarcodeHumanReadableFontSize)" aria-label="ESC/POS Raster 条码可读文字字号" placeholder="默认 12" @input="setEscPosDeviceNumber('rasterBarcodeHumanReadableFontSize', $event)" /></label>
                <div class="output-settings-actions">
                  <button v-if="!escPosDevice.rasterFonts" class="button-secondary" type="button" @click="enableEscPosRasterFonts">启用固定字体</button>
                  <button v-else class="button-secondary" type="button" @click="clearEscPosRasterFonts">清除固定字体</button>
                </div>
                <template v-if="rasterFontEditorOpen || escPosDevice.rasterFonts">
                  <div class="numeric-grid">
                    <label class="field-label">资源别名<input v-model="rasterFontNameDraft" class="field-input" type="text" aria-label="Raster 字体资源别名" placeholder="例如 NotoSans" /></label>
                    <label class="field-label">版本<input v-model="rasterFontVersionDraft" class="field-input" type="text" aria-label="Raster 字体资源版本" placeholder="例如 1.0" /></label>
                  </div>
                  <div class="output-settings-actions">
                    <button class="button-secondary" type="button" @click="openRasterFontRegularPicker">选择常规字体</button>
                    <span v-if="rasterFontRegularFileName" class="field-note">{{ rasterFontRegularFileName }}</span>
                    <button class="button-secondary" type="button" @click="openRasterFontBoldPicker">选择粗体字体</button>
                    <span v-if="rasterFontBoldFileName" class="field-note">{{ rasterFontBoldFileName }}</span>
                    <button class="button-secondary" type="button" @click="addRasterFontResource">添加字体资源</button>
                  </div>
                  <template v-if="escPosDevice.rasterFonts">
                    <label class="field-label">默认字体<select class="field-input" aria-label="Raster 默认字体" :value="escPosDevice.rasterFonts.defaultFont" @change="setRasterDefaultFont"><option v-for="font in escPosDevice.rasterFonts.fonts" :key="font.name" :value="font.name">{{ font.name }} · {{ font.version }}</option></select></label>
                    <div class="field-label">替代字体顺序</div>
                    <div class="output-settings-actions">
                      <select v-model="rasterFontFallbackDraft" class="field-input" aria-label="添加 Raster 替代字体"><option value="">选择资源</option><option v-for="font in rasterFallbackChoices" :key="font.name" :value="font.name">{{ font.name }}</option></select>
                      <button class="button-secondary" type="button" :disabled="!rasterFontFallbackDraft" @click="addRasterFallback">添加替代</button>
                    </div>
                    <div v-for="(name, index) in escPosDevice.rasterFonts.fallbackFonts ?? []" :key="name" class="output-settings-actions">
                      <span class="field-note">{{ index + 1 }}. {{ name }}</span>
                      <button class="button-secondary" type="button" title="上移替代字体" :disabled="index === 0" @click="moveRasterFallback(index, -1)"><ArrowUp :size="13" /></button>
                      <button class="button-secondary" type="button" title="下移替代字体" :disabled="index === (escPosDevice.rasterFonts?.fallbackFonts?.length ?? 0) - 1" @click="moveRasterFallback(index, 1)"><ArrowDown :size="13" /></button>
                      <button class="button-secondary" type="button" title="删除替代字体" @click="removeRasterFallback(name)"><X :size="13" /></button>
                    </div>
                    <div v-for="font in escPosDevice.rasterFonts.fonts" :key="font.name" class="output-settings-actions">
                      <span class="field-note">{{ font.name }} · {{ font.version }} · 已就绪{{ font.boldData ? ' · 粗体已就绪' : ' · 仅常规' }}</span>
                      <button class="button-secondary" type="button" title="删除字体资源" @click="removeRasterFontResource(font.name)"><Trash2 :size="13" /></button>
                    </div>
                  </template>
                </template>
              </template>
             </template>
            <label v-if="usesImageThreshold" class="field-label">图片黑白阈值 <span>0–255</span><input class="field-input" type="number" min="0" max="255" :value="imageThreshold" aria-label="图片黑白阈值" @input="setImageThreshold" /></label>
            <template v-if="printProtocol === 'Tspl' || printProtocol === 'Zpl'">
              <label class="field-label">半角字宽 <span>24 点</span><input class="field-input" type="number" min="0.1" step="0.1" :value="deviceCellWidthValue(deviceHalfCellWidthAt24)" aria-label="半角字宽" placeholder="自动估算" @input="setDeviceCellWidth('half', $event)" /></label>
              <label class="field-label">全角字宽 <span>24 点</span><input class="field-input" type="number" min="0.1" step="0.1" :value="deviceCellWidthValue(deviceFullCellWidthAt24)" aria-label="全角字宽" placeholder="半角 × 2" @input="setDeviceCellWidth('full', $event)" /></label>
              <label class="field-label">设备行高 <span>24 点</span><input class="field-input" type="number" min="0.1" step="0.1" :value="deviceCellWidthValue(deviceLineHeightAt24)" aria-label="设备行高" placeholder="自动估算" @input="setDeviceLineHeight" /></label>
              <div class="output-settings-title">逐字体校准</div>
              <label class="field-label">设备字体 <span>{{ printProtocol === 'Tspl' ? '0–8' : '0–9 / A–Z' }}</span><input class="field-input" type="text" maxlength="1" :value="deviceFontCalibrationName" aria-label="逐字体校准字体编号" placeholder="选中文字自动带入" @input="setDeviceFontCalibrationName" /></label>
              <label class="field-label">半角字宽 <span>24 点</span><input class="field-input" type="number" min="0.1" step="0.1" :value="deviceFontCalibrationNumberValue(deviceFontCalibrationHalfCellWidthAt24)" aria-label="逐字体半角字宽" placeholder="必填" @input="setDeviceFontCalibrationNumber('halfCellWidthAt24', $event)" /></label>
              <label class="field-label">全角字宽 <span>24 点</span><input class="field-input" type="number" min="0.1" step="0.1" :value="deviceFontCalibrationNumberValue(deviceFontCalibrationFullCellWidthAt24)" aria-label="逐字体全角字宽" placeholder="必填" @input="setDeviceFontCalibrationNumber('fullCellWidthAt24', $event)" /></label>
              <label class="field-label">设备行高 <span>24 点</span><input class="field-input" type="number" min="0.1" step="0.1" :value="deviceFontCalibrationNumberValue(deviceFontCalibrationLineHeightAt24)" aria-label="逐字体设备行高" placeholder="必填" @input="setDeviceFontCalibrationNumber('lineHeightAt24', $event)" /></label>
              <div class="output-settings-actions">
                <button class="button-secondary" type="button" @click="applyDeviceFontCalibration">保存当前字体</button>
                <button class="button-secondary" type="button" :disabled="!(printProtocol === 'Tspl' ? deviceFontCalibrations.Tspl[deviceFontCalibrationName] : deviceFontCalibrations.Zpl[deviceFontCalibrationName])" @click="removeDeviceFontCalibration">清除当前字体</button>
              </div>
              <label class="raster-line-option" title="要求文字和条码声明设备实际占位尺寸"><input v-model="requireDeclaredNativeBounds" type="checkbox" aria-label="严格设备边界" /><span>严格设备边界</span></label>
            </template>
            <LabelPrinterProfileEditor v-if="usesTsplMedia" v-model="tsplMedia.profile" protocol="Tspl" @validity="tsplProfileError = $event" />
            <LabelPrinterProfileEditor v-else-if="usesZplMedia" v-model="zplMedia.profile" protocol="Zpl" @validity="zplProfileError = $event" />
            <p v-if="usesDeviceFontCalibration && capabilityQueryReady && capabilities?.labelProfile" class="field-note">
              已加载机型 {{ capabilities.labelProfile.name }}<span v-if="capabilities.labelProfile.firmwareVersion"> · {{ capabilities.labelProfile.firmwareVersion }}</span>
            </p>
            <template v-if="usesTsplMedia">
              <div class="output-settings-title">TSPL 介质</div>
              <label class="raster-line-option" title="允许将粗体、下划线和反白文字转换为主机位图"><input v-model="tsplMedia.allowRasterizedTextStyles" type="checkbox" aria-label="允许 TSPL 文字样式位图" /><span>文字样式位图</span></label>
              <label class="field-label">DPI <span>主机元数据</span><input class="field-input" type="number" min="1" step="1" :value="optionalNumberValue(tsplMedia.dpi)" aria-label="TSPL DPI" placeholder="默认 203" @input="setTsplMediaNumber('dpi', $event)" /></label>
              <label class="field-label">标签间隙距离 <span>协议单位</span><input class="field-input" type="number" min="0" step="1" :value="optionalNumberValue(tsplMedia.gapDistance)" aria-label="TSPL 标签间隙距离" placeholder="协议默认" @input="setTsplMediaNumber('gapDistance', $event)" /></label>
              <label class="field-label">标签间隙偏移 <span>协议单位</span><input class="field-input" type="number" min="0" step="1" :value="optionalNumberValue(tsplMedia.gapOffset)" aria-label="TSPL 标签间隙偏移" placeholder="协议默认" @input="setTsplMediaNumber('gapOffset', $event)" /></label>
              <label class="field-label">打印方向 <span>0 / 1</span><select class="field-input" :value="tsplMedia.direction === undefined ? '' : String(tsplMedia.direction)" aria-label="TSPL 打印方向" @change="setTsplDirection"><option value="">协议默认</option><option value="0">0 · 正向</option><option value="1">1 · 反向</option></select></label>
              <label class="field-label">参考点 X <span>协议单位</span><input class="field-input" type="number" min="0" step="1" :value="optionalNumberValue(tsplMedia.referenceX)" aria-label="TSPL 参考点 X" placeholder="协议默认" @input="setTsplMediaNumber('referenceX', $event)" /></label>
              <label class="field-label">参考点 Y <span>协议单位</span><input class="field-input" type="number" min="0" step="1" :value="optionalNumberValue(tsplMedia.referenceY)" aria-label="TSPL 参考点 Y" placeholder="协议默认" @input="setTsplMediaNumber('referenceY', $event)" /></label>
              <label class="field-label">打印速度 <span>正整数</span><input class="field-input" type="number" min="1" step="1" :value="optionalNumberValue(tsplMedia.speed)" aria-label="TSPL 打印速度" placeholder="协议默认" @input="setTsplMediaNumber('speed', $event)" /></label>
              <label class="field-label">打印浓度 <span>0–15</span><input class="field-input" type="number" min="0" max="15" step="1" :value="optionalNumberValue(tsplMedia.density)" aria-label="TSPL 打印浓度" placeholder="协议默认" @input="setTsplMediaNumber('density', $event)" /></label>
            </template>
            <template v-else-if="usesZplMedia">
              <div class="output-settings-title">ZPL 介质</div>
              <label class="raster-line-option" title="允许将粗体、下划线和反白文字转换为主机位图"><input v-model="zplMedia.allowRasterizedTextStyles" type="checkbox" aria-label="允许 ZPL 文字样式位图" /><span>文字样式位图</span></label>
              <label class="field-label">DPI <span>主机元数据</span><input class="field-input" type="number" min="1" step="1" :value="optionalNumberValue(zplMedia.dpi)" aria-label="ZPL DPI" placeholder="默认 203" @input="setZplMediaNumber('dpi', $event)" /></label>
              <label class="field-label">打印速度 <span>正整数</span><input class="field-input" type="number" min="1" step="1" :value="optionalNumberValue(zplMedia.printSpeed)" aria-label="ZPL 打印速度" placeholder="协议默认" @input="setZplMediaNumber('printSpeed', $event)" /></label>
              <label class="field-label">方向 <span>打印方向</span><select class="field-input" :value="zplMedia.orientation ?? ''" aria-label="ZPL 方向" @change="setZplOrientation"><option value="">协议默认</option><option value="None">默认方向</option><option value="Down">向下</option><option value="Left">向左</option><option value="Right">向右</option></select></label>
              <label class="field-label">介质跟踪 <span>N / Y / W / M / A / V</span><select class="field-input" :value="zplMedia.mediaTracking ?? ''" aria-label="ZPL 介质跟踪" @change="setZplMediaTracking"><option value="">协议默认</option><option value="N">N · 连续</option><option value="Y">Y · 间隙</option><option value="W">W · 间隙</option><option value="M">M · 黑标</option><option value="A">A · 自动</option><option value="V">V · 可变长度</option></select></label>
              <label class="field-label">打印浓度 <span>-30–30</span><input class="field-input" type="number" min="-30" max="30" step="1" :value="optionalNumberValue(zplMedia.darkness)" aria-label="ZPL 打印浓度" placeholder="协议默认" @input="setZplMediaNumber('darkness', $event)" /></label>
            </template>
          </div>
        </div>
        <button class="button-secondary" type="button" @click="openTemplatePicker"><Upload :size="15" /><span>导入</span></button>
        <button class="button-secondary" type="button" @click="exportJson"><Download :size="15" /><span>导出 JSON</span></button>
        <button class="button-secondary" type="button" @click="saveProject"><Save :size="15" /><span>保存项目</span></button>
        <button v-if="resources.length || escPosDevice.rasterFonts" class="button-secondary has-resource" type="button" @click="exportBundle"><ArrowDownToLine :size="15" /><span>模板包</span></button>
        <button class="button-primary" type="button" :disabled="previewBusy" :aria-label="printDataLabel" :title="printDataLabel" @click="exportPrintData"><Printer :size="15" /><span>{{ printDataLabel }}</span></button>
      </div>
      <button class="mobile-panel-button" type="button" title="打开结构树" @click="mobileLayersOpen = !mobileLayersOpen; mobileInspectorOpen = false"><PanelLeft :size="17" /></button>
      <button class="mobile-panel-button" type="button" title="打开属性" @click="mobileInspectorOpen = !mobileInspectorOpen; mobileLayersOpen = false"><PanelRight :size="17" /></button>
      <input ref="jsonInput" data-testid="template-import" type="file" accept=".json,.zip,application/json,application/zip" hidden @change="importFile" />
      <input ref="imageInput" type="file" accept="image/*" hidden @change="importImage" />
      <input ref="componentInput" data-testid="component-import" type="file" accept=".json,.zip,application/json,application/zip" hidden @change="importComponents" />
      <input ref="rasterFontRegularInput" data-testid="raster-font-regular-input" aria-label="上传 Raster 常规字体" type="file" accept=".ttf,.otf,font/ttf,font/otf" hidden @change="selectRasterFontRegular" />
      <input ref="rasterFontBoldInput" data-testid="raster-font-bold-input" aria-label="上传 Raster 粗体字体" type="file" accept=".ttf,.otf,font/ttf,font/otf" hidden @change="selectRasterFontBold" />
    </header>

    <div class="workbench">
      <div class="layers-shell" :class="{ 'mobile-open': mobileLayersOpen }">
        <TemplateTree :template="template" :selected-path="selectedPath" :selected-paths="selectedPaths" :collapsed="treeCollapsed" :query="searchText" @select="selectElement" @toggle="treeCollapsed = !treeCollapsed" />
        <div class="tree-tools">
          <label class="search-control"><Search :size="14" /><input v-model="searchText" placeholder="搜索元素" /><span v-if="searchText">{{ filteredTreeCount }}</span></label>
          <div class="tools-heading"><span>添加元素</span><span>{{ printProtocol === 'EscPos' ? 'ESC/POS' : printProtocol.toUpperCase() }}</span></div>
          <div class="capability-note" :class="capabilitiesState" :title="capabilitiesError || capabilityNotice"><AlertTriangle v-if="capabilitiesState !== 'ready'" :size="13" /><Check v-else :size="13" />{{ capabilityNotice }}</div>
          <div class="element-tools">
            <button type="button" :draggable="canDragElement(1)" :disabled="!canAddElement(1)" :title="elementToolTitle(1, '单行文字', 'T')" @click="addElement(1)" @dragstart="dragTool($event, 1)"><Type :size="15" /><span>单行文字</span><kbd>T</kbd></button>
            <button type="button" :draggable="canDragElement(2)" :disabled="!canAddElement(2)" :title="elementToolTitle(2, '图片', 'I')" @click="openImagePicker" @dragstart="dragTool($event, 2)"><ImagePlus :size="15" /><span>图片</span><kbd>I</kbd></button>
            <button type="button" :draggable="canDragElement(3)" :disabled="!canAddElement(3)" :title="elementToolTitle(3, '二维码', 'Q')" @click="addElement(3)" @dragstart="dragTool($event, 3)"><QrCode :size="15" /><span>二维码</span><kbd>Q</kbd></button>
            <button type="button" :draggable="canDragElement(4)" :disabled="!canAddElement(4)" :title="elementToolTitle(4, '一维条码', 'B')" @click="addElement(4)" @dragstart="dragTool($event, 4)"><Barcode :size="15" /><span>一维条码</span><kbd>B</kbd></button>
            <button type="button" :draggable="canDragElement(5)" :disabled="!canAddElement(5)" :title="elementToolTitle(5, '线段', 'L')" @click="addElement(5)" @dragstart="dragTool($event, 5)"><Minus :size="15" /><span>线段</span><kbd>L</kbd></button>
          </div>
          <button class="advanced-toggle" type="button" :aria-expanded="showAdvancedTools" @click="showAdvancedTools = !showAdvancedTools"><Shapes :size="14" />组件与表格<ChevronDown :size="13" /></button>
          <template v-if="showAdvancedTools">
            <div class="element-tools advanced-tools">
              <button type="button" :draggable="canDragElement(1)" :disabled="!canAddElement(1)" :title="elementToolTitle(1, '多行文字')" @click="addMultilineText" @dragstart="dragTool($event, 1, true)"><Type :size="15" /><span>多行文字</span></button>
              <button type="button" :draggable="canDragElement(7)" :disabled="!canAddElement(7)" :title="elementToolTitle(7, '矩形')" @click="addElement(7)" @dragstart="dragTool($event, 7)"><Square :size="15" /><span>矩形</span></button>
              <button type="button" :draggable="canDragElement(8)" :disabled="!canAddElement(8)" :title="elementToolTitle(8, '椭圆')" @click="addElement(8)" @dragstart="dragTool($event, 8)"><Circle :size="15" /><span>椭圆</span></button>
              <button type="button" :draggable="canDragElement(9)" :disabled="!canAddElement(9)" :title="elementToolTitle(9, '图标')" @click="addElement(9)" @dragstart="dragTool($event, 9)"><Shapes :size="15" /><span>图标</span></button>
              <button type="button" :draggable="canDragElement(10)" :disabled="!canAddElement(10)" :title="elementToolTitle(10, '二维点阵码')" @click="addElement(10)" @dragstart="dragTool($event, 10)"><QrCode :size="15" /><span>PDF417 / DM</span></button>
              <button type="button" title="配置动态表格" @click="openTableWizard"><Table2 :size="15" /><span>动态表格</span></button>
              <button type="button" title="将所选元素编组；未多选时组合当前单元格" @click="groupCurrentCell"><Layers :size="15" /><span>组合元素</span></button>
              <button type="button" title="将选中的连续行保存为流式组合" @click="groupSelectedRows"><Layers :size="15" /><span>组合行</span></button>
              <button type="button" title="复制所选元素" :disabled="!selectedElement" @click="duplicateSelected"><Copy :size="15" /><span>复制元素</span></button>
              <button type="button" title="拆开选中的组合" :disabled="selectedElement?.Type !== 6" @click="ungroupSelected"><Ungroup :size="15" /><span>拆开组合</span></button>
              <button type="button" title="将选中的共享组件转为独立组合" :disabled="selectedElement?.Type !== 6 || !selectedElement.ComponentId" @click="detachSelected"><Unlink :size="15" /><span>独立编辑</span></button>
              <button type="button" title="粘贴 CSV 或制表符文字生成表格" @click="showTableImport = !showTableImport"><Table2 :size="15" /><span>文字转表格</span></button>
            </div>
            <div v-if="showTableWizard" class="table-wizard">
              <label>数据源<input v-model="tableSource" list="table-sources" placeholder="Items" /><datalist id="table-sources"><option v-for="source in tableSourceOptions" :key="source" :value="source" /></datalist></label>
              <label class="table-repeat"><input v-model="tableRepeatHeader" type="checkbox" />每页重复表头</label>
              <div v-for="(column, index) in tableColumns" :key="index" class="table-column-editor">
                <input v-model="column.title" :aria-label="`第 ${index + 1} 列标题`" placeholder="标题" />
                <input v-model="column.path" :aria-label="`第 ${index + 1} 列字段`" list="table-item-fields" placeholder="字段路径" />
                <input type="number" min="1" :aria-label="`第 ${index + 1} 列宽`" :value="column.width ?? ''" placeholder="自动宽度" @input="column.width = Number(($event.target as HTMLInputElement).value) || undefined" />
                <select v-model.number="column.alignment" :aria-label="`第 ${index + 1} 列对齐`"><option :value="0">左</option><option :value="1">中</option><option :value="2">右</option></select>
                <div class="table-column-actions">
                  <button type="button" title="列左移" :disabled="index === 0" @click="moveTableColumn(index, -1)"><ArrowUp :size="13" /></button>
                  <button type="button" title="列右移" :disabled="index === tableColumns.length - 1" @click="moveTableColumn(index, 1)"><ArrowDown :size="13" /></button>
                  <button type="button" title="删除列" :disabled="tableColumns.length === 1" @click="tableColumns.splice(index, 1)"><X :size="13" /></button>
                </div>
              </div>
              <datalist id="table-item-fields"><option v-for="path in tableItemFields" :key="path" :value="path" /></datalist>
              <div class="table-wizard-actions"><button type="button" title="添加列" @click="tableColumns.push({ title: '', path: '', alignment: 0 })"><Plus :size="14" /></button><button class="button-secondary" type="button" @click="addTable">创建表格</button></div>
            </div>
            <div v-if="showTableImport" class="table-import">
              <textarea v-model="pastedTable" aria-label="粘贴表格文字" placeholder="名称,数量&#10;商品 A,2"></textarea>
              <button class="button-secondary" type="button" @click="insertPastedTable">插入表格</button>
            </div>
            <div v-if="selectedElement?.Type === 6 && (selectedElement.Elements?.length || selectedElement.Rows?.length)" class="component-save">
              <input v-model="componentName" aria-label="组件标识" placeholder="组件标识，如 orderBadge" />
              <button class="button-secondary" type="button" @click="saveSelectedGroup">保存组件</button>
            </div>
            <div v-if="template.Components?.length" class="component-library">
              <div class="tools-heading"><span>当前模板组件</span><span>{{ template.Components.length }}</span></div>
              <div v-for="(item, index) in template.Components" :key="item.Id" class="component-entry">
                <button type="button" :title="`插入组件 ${item.Id}`" @click="insertComponent(item.Id)"><Layers :size="14" />{{ item.Id }}</button>
                <button type="button" :title="`导出 ${item.Id} 及依赖`" @click="exportComponents(item.Id)"><Download :size="13" /></button>
                <button type="button" :title="`编辑定义 ${item.Id}`" @click="selectElement(`Components[${index}]`)"><Pencil :size="13" /></button>
                <button type="button" :title="`重命名组件 ${item.Id}`" @click="componentEditId = item.Id; componentNewId = item.Id"><Pencil :size="13" /></button>
                <button type="button" :title="`删除组件 ${item.Id}`" @click="deleteDefinition(item.Id)"><Trash2 :size="13" /></button>
              </div>
              <div v-if="componentEditId" class="component-save">
                <input v-model="componentNewId" aria-label="新组件标识" @keydown.enter="renameDefinition" />
                <button class="button-secondary" type="button" @click="renameDefinition">保存名称</button>
              </div>
            </div>
            <div class="component-library-actions">
              <button type="button" title="导入组件定义" @click="componentInput?.click()"><Upload :size="13" />导入组件</button>
              <button type="button" title="导出当前模板组件定义" :disabled="!template.Components?.length" @click="exportComponents()"><Download :size="13" />导出组件</button>
            </div>
          </template>
          <div class="tree-footer">
            <button class="icon-button quiet" type="button" title="添加行" @click="addRow"><FilePlus2 :size="15" /></button>
            <button class="icon-button quiet" type="button" title="删除所选项" @click="removeSelected"><X :size="15" /></button>
            <button class="icon-button quiet" type="button" title="编辑业务 JSON" @click="focusDataJson"><CodeXml :size="15" /></button>
            <span class="tree-footer-spacer"></span>
            <span class="element-count">{{ listTemplateTree(template).filter((entry) => entry.kind === 'element').length }} 个元素</span>
          </div>
        </div>
      </div>

      <section class="editor-column">
        <div class="editor-toolbar">
          <div class="element-toolbar-group">
            <span class="toolbar-caption">元素</span>
            <button class="tool-button" type="button" :disabled="!canAddElement(1)" :title="elementToolTitle(1, '文字')" @click="addElement(1)"><Type :size="16" /></button>
            <button class="tool-button" type="button" :disabled="!canAddElement(2)" :title="elementToolTitle(2, '图片')" @click="openImagePicker"><ImagePlus :size="16" /></button>
            <button class="tool-button" type="button" :disabled="!canAddElement(3)" :title="elementToolTitle(3, '二维码')" @click="addElement(3)"><QrCode :size="16" /></button>
            <button class="tool-button" type="button" :disabled="!canAddElement(4)" :title="elementToolTitle(4, '条码')" @click="addElement(4)"><Barcode :size="16" /></button>
            <button class="tool-button" type="button" :disabled="!canAddElement(5)" :title="elementToolTitle(5, '线段')" @click="addElement(5)"><Minus :size="16" /></button>
          </div>
          <span class="toolbar-divider"></span>
          <div class="element-toolbar-group">
            <button class="tool-button" type="button" title="添加行" @click="addRow"><FilePlus2 :size="15" /></button>
            <button class="tool-button" type="button" title="添加列" @click="addColumn"><Layers3 :size="15" /></button>
            <button class="tool-button tool-delete" type="button" title="删除所选项" :disabled="!selectedPath" @click="removeSelected"><X :size="15" /></button>
            <button class="tool-button" type="button" title="复制所选元素" :disabled="!selectedElement" @click="duplicateSelected"><Copy :size="15" /></button>
          </div>
          <span class="toolbar-spacer"></span>
          <div class="canvas-modes" role="group" aria-label="画布模式">
            <button class="tool-button" type="button" title="选择模式" :class="{ 'tool-active': canvasMode === 'select' }" @click="canvasMode = 'select'"><MousePointer2 :size="15" /></button>
            <button class="tool-button" type="button" title="拖拽画布模式" :class="{ 'tool-active': canvasMode === 'pan' }" @click="canvasMode = 'pan'"><Hand :size="15" /></button>
          </div>
          <span class="toolbar-divider"></span>
          <button class="icon-button" type="button" title="缩小画布" @click="zoom = Math.max(0.5, Math.round((zoom - 0.1) * 100) / 100)"><ZoomOut :size="16" /></button>
          <span class="toolbar-zoom">{{ Math.round(zoom * 100) }}%</span>
          <button class="icon-button" type="button" title="放大画布" @click="zoom = Math.min(1.5, Math.round((zoom + 0.1) * 100) / 100)"><ZoomIn :size="16" /></button>
          <span class="toolbar-divider"></span>
          <button class="icon-button" type="button" title="切换业务数据面板" :class="{ 'tool-active': showDataPanel }" @click="showDataPanel = !showDataPanel"><CodeXml :size="16" /></button>
          <button class="icon-button" type="button" title="刷新预览" @click="refreshPreview"><RotateCcw :size="15" /></button>
        </div>

        <section v-if="previewDiagnostics.length" class="diagnostic-panel" aria-label="打印诊断" aria-live="polite">
          <article v-for="(diagnostic, index) in previewDiagnostics" :key="index">
            <p>{{ formatDesignerDiagnostic(diagnostic) }}</p>
            <div class="diagnostic-actions">
              <button type="button" :disabled="!resolveDiagnosticTarget(template, diagnostic)" @click="locateDiagnostic(diagnostic)">定位节点</button>
              <button v-if="diagnostic.definitionPath" type="button" :disabled="!resolveDiagnosticTarget(template, diagnostic, 'definition')" @click="locateDiagnostic(diagnostic, 'definition')">定位组件定义</button>
              <button v-if="diagnostic.componentInstancePath" type="button" :disabled="!resolveDiagnosticTarget(template, diagnostic, 'instance')" @click="locateDiagnostic(diagnostic, 'instance')">定位组件实例</button>
            </div>
          </article>
        </section>

        <CanvasWorkspace
          :template="template"
          :page="currentPage"
          :selected-path="selectedPath"
          :selected-paths="selectedPaths"
          :zoom="zoom"
          :unit="unit"
          :target-dpi="targetDpi"
          :source="previewSource"
          :page-context="currentPageContext"
          :page-index="previewIndex"
          :page-count="previewPages.length"
          :resources="resources"
          :mode="canvasMode"
          @select="selectElement"
          @move="moveElement"
          @resize="resizeElement"
          @drop-element="addElementAt"
          @update:zoom="zoom = $event"
          @page-change="previewIndex = $event"
        />

        <section v-if="showDataPanel" class="data-panel">
          <header class="data-panel-header">
            <div class="data-panel-tabs"><button type="button" :class="{ active: activeDataTab === 'business' }" @click="activeDataTab = 'business'">价签</button><button type="button" :class="{ active: activeDataTab === 'fields' }" @click="activeDataTab = 'fields'">样例字段</button><button type="button" :class="{ active: activeDataTab === 'json' }" @click="activeDataTab = 'json'">JSON</button></div>
            <div class="data-panel-meta"><span v-if="parsedData.error" class="inline-error"><AlertTriangle :size="13" /> JSON 语法错误</span><span v-else class="inline-good"><Check :size="13" /> 数据有效</span><span class="meta-divider"></span><button class="text-button" type="button" @click="refreshPreview"><Play :size="13" />重新预览</button><button class="icon-button quiet" type="button" title="收起数据面板" @click="showDataPanel = false"><ChevronDown :size="15" /></button></div>
          </header>
          <div v-if="activeDataTab === 'business'" class="business-data-view">
            <div class="data-field-block"><label>商品名称 <span>Product.Name</span></label><input class="field-input" :value="(parsedData.value as any)?.Product?.Name ?? (parsedData.value as any)?.Name ?? ''" @input="updateBusinessField((parsedData.value as any)?.Product ? 'Product.Name' : 'Name', ($event.target as HTMLInputElement).value)" /></div>
            <div class="data-field-block"><label>商品编码 <span>Product.Code</span></label><input class="field-input" :value="(parsedData.value as any)?.Product?.Code ?? (parsedData.value as any)?.Barcode ?? ''" @input="updateBusinessField((parsedData.value as any)?.Product ? 'Product.Code' : 'Barcode', ($event.target as HTMLInputElement).value)" /></div>
            <div class="data-field-block"><label>销售价格 <span>Price</span></label><input class="field-input" :value="(parsedData.value as any)?.Product?.Price ?? ((parsedData.value as any)?.SalePriceInteger != null ? `${(parsedData.value as any).SalePriceInteger}${(parsedData.value as any).SalePriceFraction ?? '.00'}` : '')" @input="updateSalePrice(($event.target as HTMLInputElement).value)" /></div>
            <div class="data-field-block data-field-toggle"><label>促销状态 <span>IsPromotion</span></label><label class="switch"><input type="checkbox" :checked="Boolean((parsedData.value as any)?.IsPromotion)" @change="updateBusinessValue('IsPromotion', ($event.target as HTMLInputElement).checked)" /><i></i></label></div>
            <div class="data-field-block data-field-toggle"><label>会员价格 <span>HasMemberPrice</span></label><label class="switch"><input type="checkbox" :checked="Boolean((parsedData.value as any)?.HasMemberPrice)" @change="updateBusinessValue('HasMemberPrice', ($event.target as HTMLInputElement).checked)" /><i></i></label></div>
            <button class="data-json-link" type="button" @click="activeDataTab = 'json'">打开完整数据 <ChevronRight :size="14" /></button>
            <div v-if="serviceSampleError" class="sample-note" :title="serviceSampleError"><CircleHelp :size="13" /> 正在使用内置样例</div>
          </div>
          <div v-else-if="activeDataTab === 'fields'" class="business-data-view generic-fields">
            <div v-for="field in editableFields" :key="field.path" class="data-field-block">
              <label>{{ field.path }}</label>
              <input v-if="typeof field.value === 'boolean'" type="checkbox" :checked="field.value" @change="updateBusinessValue(field.path, ($event.target as HTMLInputElement).checked)" />
              <input v-else class="field-input" :type="typeof field.value === 'number' ? 'number' : 'text'" :value="field.value ?? ''" @change="updateGenericField(field.path, ($event.target as HTMLInputElement).value, field.value)" />
            </div>
            <button class="data-json-link" type="button" @click="focusDataJson">编辑明细数组和完整 JSON <ChevronRight :size="14" /></button>
          </div>
          <div v-else class="json-data-view">
            <textarea ref="dataTextarea" v-model="dataText" spellcheck="false" aria-label="业务数据 JSON"></textarea>
            <div class="json-data-status"><span>{{ dataByteSize.toLocaleString() }} bytes</span><span v-if="parsedData.error" class="inline-error">{{ parsedData.error }}</span><span v-else class="inline-good">JSON 格式正确</span></div>
          </div>
        </section>
      </section>

      <div class="inspector-shell" :class="{ 'mobile-open': mobileInspectorOpen }">
        <PropertyInspector
          :template="template"
          :selected-path="selectedPath"
          :element="selectedElement"
          :definition="selectedDefinition"
          :row="selectedRow"
          :cell="selectedCell"
          :unit="unit"
          :target-dpi="targetDpi"
          :capabilities="capabilities"
          :capabilities-state="capabilitiesState"
          :resources="resources"
          :field-paths="selectedPath ? bindingFieldPaths : rootFieldPaths"
          @set-position="setElementPosition"
          @update-condition="selectedElement ? selectedElement.Condition = $event : selectedRow && (selectedRow.Condition = $event)"
          @add-row="addRow"
          @add-column="addColumn"
          @add-nested-row="addNestedRow"
          @remove="removeSelected"
        />
      </div>
    </div>

    <footer class="status-bar">
      <div class="status-left"><span class="status-indicator" :class="statusTone"></span><span>{{ statusMessage }}</span><span v-if="previewBusy" class="status-spinner"></span><button v-if="previewError" class="status-error" type="button" :disabled="!previewErrorPath" :title="previewError" @click="locatePreviewError"><AlertTriangle :size="13" />{{ previewError.split('\n')[0] }}</button></div>
      <div class="status-right"><span>{{ template.Width }} × {{ template.Height }} pt</span><span class="status-dot"></span><span>{{ dotsToMm(template.Width, targetDpi) }} × {{ dotsToMm(template.Height, targetDpi) }} mm</span><span class="status-dot"></span><span>{{ targetDpi }} DPI</span><button class="icon-button quiet" type="button" title="帮助" @click="showToast('页面尺寸以打印点保存；本地预览仅用于布局参考。', 'warning')"><CircleHelp :size="14" /></button></div>
    </footer>

    <Transition name="toast"><div v-if="toast" class="toast-message" :class="`toast-${toastTone}`"><Check v-if="toastTone === 'success'" :size="15" /><AlertTriangle v-else :size="15" />{{ toast }}<button type="button" @click="toast = ''"><X :size="14" /></button></div></Transition>
    <div v-if="mobileLayersOpen || mobileInspectorOpen" class="mobile-scrim" @click="mobileLayersOpen = false; mobileInspectorOpen = false"></div>
  </main>
</template>
