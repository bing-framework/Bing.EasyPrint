<script setup lang="ts">
import { computed, ref } from 'vue'
import { AlignCenter, AlignLeft, AlignRight, Braces, ChevronDown, FileText, Layers3, Ruler, Type } from '@lucide/vue'
import type { BarcodeElement, BpTemplate, Cell, ComponentDefinition, Condition, FieldFormat, FontElement, Gs1CompositeElement, Gs1Input, Gs1InputMode, ImageCrop, ImageElement, QuietZone, QrElement, RotationAngle, Row, TemplateElement } from '../model'
import { MAX_QUIET_ZONE_DOTS } from '../model'
import {
  applyDeclaredBarcodeEndCoordinate, applyEndCoordinateDelta, barcodeOccupiedSize, barcodeQuietZoneOccupiedDelta, barcodeRawSizeFromOccupied,
  dotsToMm, mmToDots, quietZoneExtentDelta, qrDefaultQuietZone, rotatedSize, type BarcodeQuietZoneContext,
} from '../coordinates'
import { bindImageResource } from '../binding'
import { defaultGroupPagination } from '../componentsModel'
import {
  barcodeModuleWidthSupported, barcodeTypeSupported, capabilityFeatureReason, currentFontSource, elementTypeSupported, filledRectangleSupported,
  deviceFontCapability, expandedStackedColumnsIssue, expandedStackedColumnsRange, expandedStackedColumnsSupported,
  expandedStackedRowsIssue, expandedStackedRowsRange, expandedStackedRowsSupported,
  fontSizeRule, fontSourceSupported, hostFontFamilySupported, legacyDeviceFontSupported,
  qrModuleSizeSupported, rotationSupported, textSizeSupported, valueInRange, wideModuleWidthSupported, gs1TwoDimensionalSupported,
  type CapabilitiesState, type DesignerCapabilities,
} from '../capabilities'
import {
  EAN_8_LINEAR_TYPE, EAN_13_LINEAR_TYPE, UPC_A_LINEAR_TYPE, UPC_E_LINEAR_TYPE,
  DATABAR_TRUNCATED_LINEAR_TYPE, DATABAR_EXPANDED_LINEAR_TYPE, DATABAR_STACKED_LINEAR_TYPE,
  DATABAR_EXPANDED_STACKED_LINEAR_TYPE, DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE,
  GS1_128_LINEAR_TYPE, GS1_COMPOSITE_CC_C_MODE,
  gs1CompositeModeChoices, minimumGs1DataBarExpandedStackedLinearHeight, minimumGs1DataBarStackedLinearHeight,
  minimumGs1DataBarTruncatedLinearHeight, minimumGs1DataBarStackedOmnidirectionalLinearHeight,
  isGs1CompositeRetailLinearType, gs1CompositeRetailHasAddon, setGs1CompositeLinearType, setGs1CompositeMode,
  supportsGs1CompositeCcC, supportsGs1CompositeEanUpc, supportsGs1CompositeEanUpcAddons, supportsGs1CompositeExpanded,
  supportsGs1CompositeExpandedStacked, supportsGs1CompositeGs1128, supportsGs1CompositeGs1128CcAb,
  supportsGs1CompositeStacked, supportsGs1CompositeTruncated, supportsGs1CompositeStackedOmnidirectional,
  type Gs1CompositeLinearType, type Gs1CompositeMode,
} from '../gs1Composite'
import {
  barcodeOptions, fixedBarcodeWideWidth, isTsplVariableBarcodeType, legacyRatioForTsplBarcodeRatio,
  tsplBarcodeModulePairForRatio, tsplBarcodeRatioForModules, tsplBarcodeRatioFromLegacyRatio,
  tsplVariableBarcodeRatios, type TsplBarcodeRatioKey,
} from '../barcodeOptions'
import ConditionEditor from './ConditionEditor.vue'
import FieldFormatEditor from './FieldFormatEditor.vue'
import Gs1AiEditor from './Gs1AiEditor.vue'

const props = defineProps<{
  template: BpTemplate
  selectedPath: string
  element?: TemplateElement
  definition?: ComponentDefinition
  row?: Row
  cell?: Cell
  unit: 'pt' | 'mm'
  targetDpi: number
  capabilities?: DesignerCapabilities
  capabilitiesState: CapabilitiesState
  resources: Array<{ key: string; fileName: string }>
  fieldPaths: string[]
  data: unknown
  locked?: boolean
}>()
const emit = defineEmits<{ 'toggle-condition': [value: boolean]; 'update-condition': [value: Condition | undefined]; 'set-position': [axis: 'X' | 'Y', value: number]; 'add-row': []; 'add-column': []; 'add-nested-row': []; remove: [] }>()
const typeName: Record<number, string> = { 1: '文字', 2: '图片', 3: '二维码', 4: '一维条码', 5: '线段', 6: '组合', 7: '矩形', 8: '椭圆', 9: '图标', 10: '二维点阵码', 11: 'GS1 Composite' }
const variableName = ref('')
const variablePath = ref('')
const parameterName = ref('')
const parameterError = ref('')
const isPage = computed(() => !props.selectedPath)
const isTopLevelRow = computed(() => /^Rows\[\d+\]$/.test(props.selectedPath))
const title = computed(() => isPage.value ? '页面属性' : props.definition ? `组件 · ${props.definition.Id}` : props.element ? `${typeName[props.element.Type]}属性` : props.row ? '行属性' : props.cell ? '列属性' : '属性')
const capabilitiesReady = computed(() => props.capabilitiesState === 'ready' && Boolean(props.capabilities))
const gs1CompositeModeOptions = computed(() => gs1CompositeModeChoices(
  props.element?.Type === 11 ? props.element.LinearType : undefined, props.capabilities))
const ordinaryExpandedStackedColumnOptions = Array.from({ length: 11 }, (_, index) => index + 1)
const compositeExpandedStackedColumnOptions = Array.from({ length: 10 }, (_, index) => index + 2)
const expandedStackedRowOptions = Array.from({ length: 10 }, (_, index) => index + 2)
const capabilityNotice = computed(() => props.capabilitiesState === 'error'
  ? '当前协议能力不可用，保留现有值，待能力恢复后再调整。'
  : props.capabilitiesState === 'loading' ? '正在读取当前协议能力，暂不能确认支持情况。' : '')
const fontSizes = [16, 24, 32, 48, 64, 72, 80, 96, 112, 120, 128, 144, 160, 168, 176, 192, 208, 216, 224, 240, 256, 264, 288, 312, 336, 360, 384]
const barcodeRatios = [
  ...[0, 1, 2, 3, 4].map((value) => ({ value, label: `${value} · ${[1.5, 2, 2.5, 3, 3.5][value]}:1` })),
  ...Array.from({ length: 11 }, (_, index) => ({ value: index + 20, label: `${index + 20} · ${(2 + index / 10).toFixed(1)}:1` })),
]
const rotationOptions: Array<{ value: RotationAngle; label: string }> = [
  { value: 0, label: '0°' },
  { value: 90, label: '90° 顺时针' },
  { value: 180, label: '180°' },
  { value: 270, label: '270° 顺时针' },
]
type FontSource = 'legacy' | 'default' | 'device' | 'host'

function fontSource(element: FontElement): FontSource {
  return currentFontSource(element)
}

function capabilityReason(feature: string): string {
  return props.capabilities ? capabilityFeatureReason(props.capabilities, feature) : capabilityNotice.value || `当前协议暂不能确认${feature}。`
}

function sourceDisabled(source: FontSource): boolean {
  return !fontSourceSupported(props.capabilities, source)
}

function sourceTitle(source: FontSource): string {
  return sourceDisabled(source) ? capabilityReason(source === 'host' ? '主机字体' : `${source} 字体`) : ''
}

function legacyFontChoices(element: FontElement): number[] {
  const current = element.FontName
  const names = props.capabilities?.protocol === 'Cpcl'
    ? props.capabilities.deviceFonts.map((font) => Number(font.name)).filter((name) => Number.isInteger(name))
    : []
  return [...new Set([0, ...names, ...(current === undefined ? [] : [current])])]
}

function deviceFontChoices(element: FontElement): string[] {
  const current = element.Font?.Kind === 1 && typeof element.Font.Name === 'string' ? element.Font.Name : undefined
  return [...new Set([
    ...(props.capabilities?.deviceFonts.map((font) => font.name) ?? []),
    ...(current ? [current] : []),
  ])]
}

function deviceFontDisabled(name: string): boolean {
  return !props.capabilities || !deviceFontCapability(props.capabilities, name)
}

function rasterFontChoices(current?: string): string[] {
  const declared = props.capabilities?.rasterFonts?.fonts.map((font) => font.name) ?? []
  return [...new Set([...declared, ...(current && !declared.includes(current) ? [current] : [])])]
}

function rasterFontAliasSupported(name: string | undefined): boolean {
  return Boolean(name && props.capabilities?.rasterFonts?.fonts.some((font) => font.name === name))
}

function setRasterFontAlias(element: FontElement, event: Event) {
  const name = (event.target as HTMLSelectElement).value.trim()
  if (!name) {
    delete element.FontFamily
    if (element.Font) delete element.Font.Name
    return
  }
  if (!rasterFontAliasSupported(name)) return
  if (element.Font?.Kind === 2) element.Font.Name = name
  else element.FontFamily = name
}

function textSizeUsesSelect(element: FontElement): boolean {
  return Boolean(capabilitiesReady.value && fontSizeRule(props.capabilities, element).sizes)
}

function textSizeChoices(element: FontElement): number[] {
  const rule = fontSizeRule(props.capabilities, element)
  const current = element.FontSize || 24
  return [...new Set([...(rule.sizes ?? fontSizes), current])].sort((left, right) => left - right)
}

function textSizeMinimum(element: FontElement): number | undefined {
  return fontSizeRule(props.capabilities, element).range?.minimum
}

function textSizeMaximum(element: FontElement): number | undefined {
  return fontSizeRule(props.capabilities, element).range?.maximum ?? undefined
}

function rotationDisabled(element: FontElement | ImageElement | QrElement | BarcodeElement | Gs1CompositeElement, value: RotationAngle): boolean {
  return !rotationSupported(props.capabilities, element.Type, value)
}

function elementRotationSupported(element: TemplateElement): boolean {
  return (element.Type === 1 || element.Type === 2 || element.Type === 3 || element.Type === 4 || element.Type === 11) && rotationSupported(props.capabilities, element.Type, element.Rotation ?? 0)
}

function barcodeTypeDisabled(value: number): boolean {
  return !barcodeTypeSupported(props.capabilities, value)
}

function setLegacyFontName(element: FontElement, event: Event) {
  const value = Number((event.target as HTMLSelectElement).value)
  if (!legacyDeviceFontSupported(props.capabilities, value, element.FontSize)) return
  element.FontName = value
}

function setDeviceFontName(element: FontElement, event: Event) {
  const name = (event.target as HTMLSelectElement).value
  if (!deviceFontCapability(props.capabilities, name)) return
  element.Font ??= { Kind: 1 }
  element.Font.Name = name
}

function setFontSource(element: FontElement, event: Event) {
  const source = (event.target as HTMLSelectElement).value as FontSource
  if (source !== 'legacy' && !fontSourceSupported(props.capabilities, source)) return
  if (source === 'legacy') {
    delete element.Font
    return
  }

  const kind = source === 'default' ? 0 : source === 'device' ? 1 : 2
  const current = element.Font
  const name = current?.Kind === kind && typeof current.Name === 'string' && current.Name.trim()
    ? current.Name.trim()
    : kind === 2 && props.capabilities?.rasterFonts && element.FontFamily?.trim()
      ? element.FontFamily.trim()
    : undefined
  delete element.FontName
  delete element.FontFamily
  delete element.DeviceFontName
  element.Font = name ? { Kind: kind, Name: name } : { Kind: kind }
}

function setFontName(element: FontElement, event: Event) {
  if (!element.Font || (element.Font.Kind !== 1 && element.Font.Kind !== 2)) return
  const name = (event.target as HTMLInputElement).value.trim()
  if (element.Font.Kind === 1 && !deviceFontCapability(props.capabilities, name)) return
  if (element.Font.Kind === 2 && props.capabilities?.rasterFonts && !rasterFontAliasSupported(name)) return
  if (name) element.Font.Name = name
  else delete element.Font.Name
}

function setFontSize(element: FontElement, event: Event) {
  const size = Number((event.target as HTMLSelectElement).value)
  if (!Number.isInteger(size) || !textSizeSupported(props.capabilities, element, size)) return
  element.FontSize = size
}

function dimensionValue(value: number | undefined): string {
  const actual = value ?? 0
  return props.unit === 'mm' ? String(dotsToMm(actual, props.targetDpi)) : String(actual)
}

function setDimension(target: Record<string, unknown>, key: string, event: Event) {
  const value = Number((event.target as HTMLInputElement).value)
  if (!Number.isFinite(value)) return
  target[key] = props.unit === 'mm' ? mmToDots(value, props.targetDpi) : Math.round(value)
}

function setElementPosition(axis: 'X' | 'Y', event: Event) {
  const value = Number((event.target as HTMLInputElement).value)
  if (!Number.isFinite(value)) return
  const target = props.unit === 'mm' ? mmToDots(value, props.targetDpi) : Math.round(value)
  emit('set-position', axis, target)
}

function setNumber(target: Record<string, unknown>, key: string, event: Event) {
  const value = Number((event.target as HTMLInputElement).value)
  if (Number.isFinite(value)) target[key] = Math.round(value)
}

function setTextWidth(element: FontElement, event: Event) {
  const value = Number((event.target as HTMLInputElement).value)
  if (!Number.isFinite(value)) return
  element.Width = Math.max(0, mmOrDots(value))
}

function updateTextEndCoordinate(element: FontElement, previousRotation = element.Rotation ?? 0) {
  if (!element.EndCoordinate) return
  const start = element.StartCoordinate ?? { X: 0, Y: 0 }
  const width = Math.max(1, element.EndCoordinate.X - start.X)
  const height = Math.max(1, element.EndCoordinate.Y - start.Y)
  const wasQuarterTurn = previousRotation === 90 || previousRotation === 270
  const isQuarterTurn = (element.Rotation ?? 0) === 90 || (element.Rotation ?? 0) === 270
  const occupied = wasQuarterTurn !== isQuarterTurn ? { width: height, height: width } : { width, height }
  element.EndCoordinate = { X: start.X + occupied.width, Y: start.Y + occupied.height }
}

function setRotation(element: FontElement | ImageElement | QrElement | BarcodeElement | Gs1CompositeElement, event: Event) {
  const value = Number((event.target as HTMLSelectElement).value)
  if (![0, 90, 180, 270].includes(value)) return
  if (!rotationSupported(props.capabilities, element.Type, value)) return
  const previousRotation = element.Rotation ?? 0
  element.Rotation = value as RotationAngle
  if (element.Type === 1) updateTextEndCoordinate(element, previousRotation)
  else if (element.Type === 4) updateBarcodeEndCoordinate(element, undefined, previousRotation)
  else if (element.Type === 2) updateImageEndCoordinate(element, previousRotation)
  else if (element.Type === 3) updateQrEndCoordinate(element, previousRotation)
}

function mmOrDots(value: number) {
  return props.unit === 'mm' ? mmToDots(value, props.targetDpi) : Math.round(value)
}

type QuietZoneElement = QrElement | BarcodeElement | Gs1CompositeElement
type QuietZoneSide = keyof QuietZone

function quietZoneMode(element: QuietZoneElement): 'default' | 'explicit' {
  return element.QuietZone !== undefined && element.QuietZone !== null ? 'explicit' : 'default'
}

function quietZoneSideValue(element: QuietZoneElement, side: QuietZoneSide): number {
  return element.QuietZone?.[side] ?? 0
}

function quietZoneEditorDisabled(): boolean {
  return !capabilitiesReady.value || props.capabilities?.supportsExplicitQuietZone !== true
}

function quietZoneUnsupported(element: QuietZoneElement): boolean {
  return quietZoneMode(element) === 'explicit' && props.capabilities?.supportsExplicitQuietZone !== true
}

function setQuietZoneMode(element: QuietZoneElement, event: Event) {
  const mode = (event.target as HTMLSelectElement).value
  const previousQuietZone = element.QuietZone
  if (mode === 'default') {
    delete element.QuietZone
    updateBarcodeEndCoordinate(element, previousQuietZone ?? null)
    return
  }
  if (mode !== 'explicit' || quietZoneEditorDisabled()) return
  const current = element.QuietZone && typeof element.QuietZone === 'object' ? element.QuietZone : {}
  element.QuietZone = {
    Left: current.Left ?? 0,
    Top: current.Top ?? 0,
    Right: current.Right ?? 0,
    Bottom: current.Bottom ?? 0,
  }
  updateBarcodeEndCoordinate(element, previousQuietZone ?? null)
}

function setQuietZoneSide(element: QuietZoneElement, side: QuietZoneSide, event: Event) {
  if (quietZoneEditorDisabled()) return
  const value = Number((event.target as HTMLInputElement).value)
  if (!Number.isInteger(value) || value < 0 || value > MAX_QUIET_ZONE_DOTS) return
  const previousQuietZone = element.QuietZone
  const current = element.QuietZone && typeof element.QuietZone === 'object' ? element.QuietZone : {}
  element.QuietZone = { ...current, [side]: value }
  updateBarcodeEndCoordinate(element, previousQuietZone ?? null)
}

function clearQuietZone(element: QuietZoneElement) {
  const previousQuietZone = element.QuietZone
  delete element.QuietZone
  updateBarcodeEndCoordinate(element, previousQuietZone ?? null)
}

function barcodeQuietZoneContext(element: BarcodeElement, preserveExistingHri = false): BarcodeQuietZoneContext {
  return {
    barcodeType: element.BarcodeType,
    lineWidth: element.LineWidth,
    narrowModuleWidth: element.NarrowModuleWidth,
    protocol: props.capabilities?.protocol ?? undefined,
    reserveHeight: element.HumanReadableReserveHeight ?? 0,
    preserveExistingHri,
  }
}

function barcodeRawDimensions(element: BarcodeElement, rotation = element.Rotation ?? 0): { width: number; height: number } {
  const start = element.StartCoordinate ?? { X: 0, Y: 0 }
  const end = element.EndCoordinate
  const occupiedWidth = end ? Math.max(1, end.X - start.X) : Math.max(1, element.Width ?? 1)
  const occupiedHeight = end ? Math.max(1, end.Y - start.Y) : Math.max(1, element.Height ?? 1)
  const raw = barcodeRawSizeFromOccupied(occupiedWidth, occupiedHeight,
    element.HumanReadableReserveHeight ?? 0, rotation, element.QuietZone)
  return {
    width: element.Width !== undefined && element.Width > 0 ? element.Width : Math.max(1, raw.width),
    height: element.Height !== undefined && element.Height > 0 ? element.Height : Math.max(1, raw.height),
  }
}

function updateBarcodeEndCoordinate(element: FontElement | QuietZoneElement, previousQuietZone?: QuietZone | null,
  previousRotation = element.Rotation ?? 0) {
  if (element.Type !== 3 && element.Type !== 4 || !element.EndCoordinate) return
  if (element.Type === 4 && (element.BarcodeType === DATABAR_EXPANDED_STACKED_LINEAR_TYPE ||
    element.BarcodeType === DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE)) {
    delete element.EndCoordinate
    return
  }
  const start = element.StartCoordinate ?? { X: 0, Y: 0 }
  const oldQuietZone = previousQuietZone === undefined ? element.QuietZone : previousQuietZone
  const widthAuto = element.Width === undefined || element.Width <= 0
  const heightAuto = element.Height === undefined || element.Height <= 0
  if (element.Type === 4 && previousRotation !== (element.Rotation ?? 0)) {
    const raw = barcodeRawDimensions(element, previousRotation)
    const occupied = barcodeOccupiedSize(raw.width, raw.height,
      element.HumanReadableReserveHeight ?? 0, element.Rotation ?? 0, element.QuietZone)
    element.EndCoordinate = { X: start.X + occupied.width, Y: start.Y + occupied.height }
    return
  }
  if (previousQuietZone !== undefined) {
    if (element.Type === 3) {
      const delta = quietZoneExtentDelta(oldQuietZone, element.QuietZone, qrDefaultQuietZone(element.UnitWidth ?? 4))
      applyEndCoordinateDelta(element.EndCoordinate, delta, widthAuto, heightAuto)
      return
    }
    const delta = barcodeQuietZoneOccupiedDelta(element.Rotation ?? 0, oldQuietZone, element.QuietZone, widthAuto,
      barcodeQuietZoneContext(element, true))
    // 条码 Height 是条高；即使 Height 已声明，上下静区仍属于外占位框。
    const quarterTurn = element.Rotation === 90 || element.Rotation === 270
    applyEndCoordinateDelta(element.EndCoordinate, delta, widthAuto || quarterTurn, true)
    return
  }
  if (element.Type !== 4) return
  const occupied = barcodeOccupiedSize(element.Width ?? 0, element.Height ?? 0,
    element.HumanReadableReserveHeight ?? 0, element.Rotation ?? 0, element.QuietZone)
  applyDeclaredBarcodeEndCoordinate(start, element.EndCoordinate, occupied, widthAuto, heightAuto, element.Rotation ?? 0)
}

function imageRawDimensions(element: ImageElement, rotation = element.Rotation ?? 0): { width: number; height: number } {
  const start = element.StartCoordinate ?? { X: 0, Y: 0 }
  const end = element.EndCoordinate
  const quarterTurn = rotation === 90 || rotation === 270
  const occupiedWidth = end ? Math.max(1, end.X - start.X) : 112
  const occupiedHeight = end ? Math.max(1, end.Y - start.Y) : 112
  return {
    width: element.Width !== undefined && element.Width > 0 ? element.Width : quarterTurn ? occupiedHeight : occupiedWidth,
    height: element.Height !== undefined && element.Height > 0 ? element.Height : quarterTurn ? occupiedWidth : occupiedHeight,
  }
}

function updateImageEndCoordinate(element: ImageElement, previousRotation = element.Rotation ?? 0) {
  if (!element.EndCoordinate) return
  const start = element.StartCoordinate ?? { X: 0, Y: 0 }
  const raw = imageRawDimensions(element, previousRotation)
  const occupied = rotatedSize(raw.width, raw.height, element.Rotation ?? 0)
  element.EndCoordinate = { X: start.X + occupied.width, Y: start.Y + occupied.height }
}

function qrRawDimensions(element: QrElement, rotation = element.Rotation ?? 0): { width: number; height: number } {
  const start = element.StartCoordinate ?? { X: 0, Y: 0 }
  const end = element.EndCoordinate
  const quarterTurn = rotation === 90 || rotation === 270
  const occupiedWidth = end ? Math.max(1, end.X - start.X) : 112
  const occupiedHeight = end ? Math.max(1, end.Y - start.Y) : 112
  return {
    width: element.Width !== undefined && element.Width > 0 ? element.Width : quarterTurn ? occupiedHeight : occupiedWidth,
    height: element.Height !== undefined && element.Height > 0 ? element.Height : quarterTurn ? occupiedWidth : occupiedHeight,
  }
}

function updateQrEndCoordinate(element: QrElement, previousRotation = element.Rotation ?? 0) {
  if (!element.EndCoordinate) return
  const start = element.StartCoordinate ?? { X: 0, Y: 0 }
  const raw = qrRawDimensions(element, previousRotation)
  const occupied = rotatedSize(raw.width, raw.height, element.Rotation ?? 0)
  element.EndCoordinate = { X: start.X + occupied.width, Y: start.Y + occupied.height }
}

function elementDimensionValue(element: TemplateElement, axis: 'X' | 'Y'): string {
  const start = axis === 'X' ? element.StartCoordinate?.X ?? 0 : element.StartCoordinate?.Y ?? 0
  const end = element.EndCoordinate
  if (element.Type === 9) return dimensionValue(element.Size)
  if (element.Type === 6 || element.Type === 7 || element.Type === 8 || element.Type === 10)
    return dimensionValue(axis === 'X' ? element.Width : element.Height)
  if (element.Type === 2) return dimensionValue(axis === 'X' ? imageRawDimensions(element).width : imageRawDimensions(element).height)
  if (element.Type === 3) {
    const occupied = rotatedSize(qrRawDimensions(element).width, qrRawDimensions(element).height, element.Rotation ?? 0)
    return dimensionValue(axis === 'X' ? occupied.width : occupied.height)
  }
  if (element.Type === 4) {
    if (element.BarcodeType === DATABAR_EXPANDED_STACKED_LINEAR_TYPE ||
      element.BarcodeType === DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE)
      return dimensionValue(axis === 'X' ? element.Width : element.Height)
    const widthAuto = element.Width === undefined || element.Width <= 0
    const heightAuto = element.Height === undefined || element.Height <= 0
    if (end && (widthAuto || heightAuto)) return dimensionValue(Math.max(0, (axis === 'X' ? end.X : end.Y) - start))
    const occupied = barcodeOccupiedSize(element.Width ?? 0, element.Height ?? 0,
      element.HumanReadableReserveHeight ?? 0, element.Rotation ?? 0, element.QuietZone)
    return dimensionValue(axis === 'X' ? occupied.width : occupied.height)
  }
  if (end) return dimensionValue(Math.max(0, (axis === 'X' ? end.X : end.Y) - start))
  return dimensionValue(axis === 'X' ? props.template.Width : props.template.Height)
}

function setElementDimension(element: TemplateElement, axis: 'X' | 'Y', event: Event) {
  if (element.Type === 11) return
  const input = event.target as HTMLInputElement
  const value = Number(input.value)
  if (!Number.isFinite(value)) return
  const size = Math.max(element.Type === 6 ? 0 : 1, mmOrDots(value))
  if (element.Type === 9) { element.Size = size; return }
  if (element.Type === 6 || element.Type === 7 || element.Type === 8 || element.Type === 10) {
    if (axis === 'X') element.Width = size
    else element.Height = size
    return
  }
  const start = element.StartCoordinate ?? { X: 0, Y: 0 }
  element.StartCoordinate ??= start
  if (element.Type === 2) {
    const raw = imageRawDimensions(element)
    if (axis === 'X') raw.width = size
    else raw.height = size
    element.Width = raw.width
    element.Height = raw.height
    if (element.EndCoordinate) {
      const occupied = rotatedSize(raw.width, raw.height, element.Rotation ?? 0)
      element.EndCoordinate = { X: start.X + occupied.width, Y: start.Y + occupied.height }
    }
    return
  }
  if (element.Type === 3) {
    const occupied = rotatedSize(qrRawDimensions(element).width, qrRawDimensions(element).height, element.Rotation ?? 0)
    if (axis === 'X') occupied.width = size
    else occupied.height = size
    const raw = rotatedSize(occupied.width, occupied.height, element.Rotation ?? 0)
    element.Width = raw.width
    element.Height = raw.height
    if (element.EndCoordinate) {
      element.EndCoordinate = { X: start.X + occupied.width, Y: start.Y + occupied.height }
    }
    return
  }
  if (element.Type === 4) {
    if (element.BarcodeType === DATABAR_EXPANDED_STACKED_LINEAR_TYPE ||
      element.BarcodeType === DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE) {
      if (axis === 'Y') {
        if (size < multirowBarcodeHeightMinimum(element) ||
          props.capabilities && !valueInRange(size, props.capabilities.barcodeHeight)) return
        element.Height = size
      } else element.Width = size
      delete element.EndCoordinate
      return
    }
    const widthAuto = element.Width === undefined || element.Width <= 0
    const heightAuto = element.Height === undefined || element.Height <= 0
    const occupied = element.EndCoordinate && (widthAuto || heightAuto)
      ? { width: Math.max(0, element.EndCoordinate.X - start.X), height: Math.max(0, element.EndCoordinate.Y - start.Y) }
      : barcodeOccupiedSize(element.Width ?? 0, element.Height ?? 0,
        element.HumanReadableReserveHeight ?? 0, element.Rotation ?? 0, element.QuietZone)
    if (axis === 'X') occupied.width = size
    else occupied.height = size
    const raw = barcodeRawSizeFromOccupied(occupied.width, occupied.height,
      element.HumanReadableReserveHeight ?? 0, element.Rotation ?? 0, element.QuietZone)
    if (props.capabilities && !valueInRange(Math.max(1, raw.height), props.capabilities.barcodeHeight)) return
    element.Width = Math.max(1, raw.width)
    element.Height = Math.max(1, raw.height)
    const actual = barcodeOccupiedSize(element.Width, element.Height,
      element.HumanReadableReserveHeight ?? 0, element.Rotation ?? 0, element.QuietZone)
    element.EndCoordinate ??= { X: start.X + actual.width, Y: start.Y + actual.height }
    element.EndCoordinate.X = start.X + actual.width
    element.EndCoordinate.Y = start.Y + actual.height
    return
  }
  element.EndCoordinate ??= { X: start.X + size, Y: start.Y + size }
  element.EndCoordinate[axis] = (axis === 'X' ? start.X : start.Y) + size
}

function addVariable() {
  const name = variableName.value.trim()
  const path = variablePath.value.trim()
  if (!/^[a-zA-Z][a-zA-Z0-9_-]{0,63}$/.test(name) || !path || path.startsWith('$')) return
  props.template.Version = 2
  props.template.Variables ??= {}
  props.template.Variables[name] = path
  variableName.value = ''
  variablePath.value = ''
}

type Gs1TwoDimensionalElement = QrElement | Extract<TemplateElement, { Type: 10 }>

function gs1TwoDimensionalDisabled(element: Gs1TwoDimensionalElement): boolean {
  if (!capabilitiesReady.value) return true
  if (element.Type === 10 && element.Format !== 'DataMatrix') return true
  return !gs1TwoDimensionalSupported(props.capabilities, element.Type === 3 ? 'QR' : 'DataMatrix')
}

function setGs1(element: Gs1TwoDimensionalElement, event: Event) {
  const checked = (event.target as HTMLInputElement).checked
  if (checked && gs1TwoDimensionalDisabled(element)) return
  if (checked) element.Gs1 = true
  else delete element.Gs1
}

function setMatrixFormat(element: Extract<TemplateElement, { Type: 10 }>, event: Event) {
  element.Format = (event.target as HTMLSelectElement).value as 'DataMatrix' | 'PDF417'
  if (element.Format === 'PDF417') {
    element.Width = Math.max(element.Width, 256)
    element.Height = Math.max(element.Height, 80)
  }
}

function setRectangleFilled(element: Extract<TemplateElement, { Type: 7 }>, event: Event) {
  const checked = (event.target as HTMLInputElement).checked
  if (checked && (!capabilitiesReady.value || !filledRectangleSupported(props.capabilities))) return
  element.Filled = checked
}

function filledRectangleUnsupported(element: Extract<TemplateElement, { Type: 7 }>): boolean {
  return capabilitiesReady.value && element.Filled === true && !filledRectangleSupported(props.capabilities)
}

function clearUnsupportedFilled(element: Extract<TemplateElement, { Type: 7 }>) {
  element.Filled = false
}

function barcodeRatioIsValid(value: number | undefined): boolean {
  return value !== undefined && (value >= 0 && value <= 4 || value >= 20 && value <= 30)
}

function barcodeHumanReadableMode(value: boolean | undefined): 'default' | 'on' | 'off' {
  return value === undefined ? 'default' : value ? 'on' : 'off'
}

function setBarcodeHumanReadable(element: Extract<TemplateElement, { Type: 4 }>, event: Event) {
  const value = (event.target as HTMLSelectElement).value
  if (value === 'on' && props.capabilities && !props.capabilities.supportsBarcodeHumanReadable) return
  if (value === 'default') delete element.HumanReadable
  else if (value === 'off') {
    element.HumanReadable = false
    // 预留高度只有在 HRI 开启时才有语义；显式关闭时清理无效组合。
    delete element.HumanReadableReserveHeight
  } else if (value === 'on') element.HumanReadable = true
  else return
  updateBarcodeEndCoordinate(element)
}

function effectiveBarcodeNarrowWidth(element: Extract<TemplateElement, { Type: 4 }>): number {
  return element.NarrowModuleWidth ?? Math.max(1, (element.LineWidth ?? 2) + 1)
}

function fixedWideWidthForElement(element: Extract<TemplateElement, { Type: 4 }>): number | undefined {
  return props.capabilities?.protocol === 'Tspl'
    ? fixedBarcodeWideWidth(element.BarcodeType ?? 0, effectiveBarcodeNarrowWidth(element))
    : undefined
}

function tsplVariableBarcodeRatioEditor(element: Extract<TemplateElement, { Type: 4 }>): boolean {
  return props.capabilities?.protocol === 'Tspl' && isTsplVariableBarcodeType(element.BarcodeType)
}

function currentTsplBarcodeRatio(element: Extract<TemplateElement, { Type: 4 }>): TsplBarcodeRatioKey | undefined {
  if (!isTsplVariableBarcodeType(element.BarcodeType)) return undefined
  return tsplBarcodeRatioForModules(element.NarrowModuleWidth, element.WideModuleWidth) ??
    tsplBarcodeRatioFromLegacyRatio(element.Ratio)
}

function tsplBarcodeRatioPair(element: Extract<TemplateElement, { Type: 4 }>, value: TsplBarcodeRatioKey) {
  return tsplBarcodeModulePairForRatio(value, effectiveBarcodeNarrowWidth(element), props.capabilities?.barcodeModuleWidth)
}

function tsplBarcodeRatioDisabled(element: Extract<TemplateElement, { Type: 4 }>, value: TsplBarcodeRatioKey): boolean {
  const pair = tsplBarcodeRatioPair(element, value)
  return !pair || !barcodeModuleWidthSupported(props.capabilities, pair.narrow) ||
    !wideModuleWidthSupported(props.capabilities, pair.wide)
}

function tsplBarcodeRatioIsValid(element: Extract<TemplateElement, { Type: 4 }>): boolean {
  const value = currentTsplBarcodeRatio(element)
  return value !== undefined && !tsplBarcodeRatioDisabled(element, value)
}

function syncTsplVariableBarcodeRatio(element: Extract<TemplateElement, { Type: 4 }>) {
  if (!isTsplVariableBarcodeType(element.BarcodeType)) return
  const value = currentTsplBarcodeRatio(element) ?? '1:2'
  const pair = tsplBarcodeRatioPair(element, value)
  if (!pair || tsplBarcodeRatioDisabled(element, value)) return
  element.Ratio = legacyRatioForTsplBarcodeRatio(value)
  element.NarrowModuleWidth = pair.narrow
  element.WideModuleWidth = pair.wide
}

function setTsplBarcodeRatio(element: Extract<TemplateElement, { Type: 4 }>, event: Event) {
  const value = (event.target as HTMLSelectElement).value as TsplBarcodeRatioKey
  if (!tsplVariableBarcodeRatios.some((ratio) => ratio.value === value)) return
  const pair = tsplBarcodeRatioPair(element, value)
  if (!pair || tsplBarcodeRatioDisabled(element, value)) return
  element.Ratio = legacyRatioForTsplBarcodeRatio(value)
  element.NarrowModuleWidth = pair.narrow
  element.WideModuleWidth = pair.wide
  updateBarcodeEndCoordinate(element)
}

function syncFixedBarcodeWideWidth(element: Extract<TemplateElement, { Type: 4 }>) {
  const wide = fixedWideWidthForElement(element)
  if (wide !== undefined) element.WideModuleWidth = wide
}

function setBarcodeType(element: Extract<TemplateElement, { Type: 4 }>, event: Event) {
  const value = Number((event.target as HTMLSelectElement).value)
  if (!Number.isInteger(value) || !barcodeTypeSupported(props.capabilities, value)) return
  if (value !== DATABAR_EXPANDED_STACKED_LINEAR_TYPE) {
    delete element.ExpandedStackedColumns
    delete element.ExpandedStackedRows
  }
  element.BarcodeType = value
  if (value === DATABAR_EXPANDED_STACKED_LINEAR_TYPE || value === DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE)
    delete element.EndCoordinate
  if (value === DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE) {
    const minimumHeight = minimumGs1DataBarStackedOmnidirectionalLinearHeight(effectiveBarcodeNarrowWidth(element))
    if ((element.Height ?? 0) < minimumHeight) element.Height = minimumHeight
  }
  syncTsplVariableBarcodeRatio(element)
  syncFixedBarcodeWideWidth(element)
  updateBarcodeEndCoordinate(element)
}

function expandedStackedColumnsDisabled(kind: 'ordinary' | 'composite', value: number): boolean {
  const range = expandedStackedColumnsRange(props.capabilities, kind)
  return !expandedStackedColumnsSupported(props.capabilities, kind) || !range || value < range.minimum || value > range.maximum
}

function expandedStackedColumnsWarning(kind: 'ordinary' | 'composite', value: number | null | undefined): string | undefined {
  return expandedStackedColumnsIssue(props.capabilities, kind, value)
}

function setExpandedStackedColumns(element: BarcodeElement | Gs1CompositeElement,
  kind: 'ordinary' | 'composite', event: Event) {
  const rawValue = (event.target as HTMLSelectElement).value
  if (rawValue === 'default') {
    delete element.ExpandedStackedColumns
    return
  }
  const value = Number(rawValue)
  const range = expandedStackedColumnsRange(props.capabilities, kind)
  if (Number.isInteger(value) && range && expandedStackedColumnsSupported(props.capabilities, kind) &&
    value >= range.minimum && value <= range.maximum) {
    element.ExpandedStackedColumns = value
    delete element.ExpandedStackedRows
  }
}

function expandedStackedRowsDisabled(kind: 'ordinary' | 'composite', value: number): boolean {
  const range = expandedStackedRowsRange(props.capabilities, kind)
  return !expandedStackedRowsSupported(props.capabilities, kind) || !range || value < range.minimum || value > range.maximum
}

function expandedStackedRowsWarning(kind: 'ordinary' | 'composite', value: number | null | undefined): string | undefined {
  return expandedStackedRowsIssue(props.capabilities, kind, value)
}

function setExpandedStackedRows(element: BarcodeElement | Gs1CompositeElement,
  kind: 'ordinary' | 'composite', event: Event) {
  const rawValue = (event.target as HTMLSelectElement).value
  if (rawValue === 'default') {
    delete element.ExpandedStackedRows
    return
  }
  const value = Number(rawValue)
  const range = expandedStackedRowsRange(props.capabilities, kind)
  if (Number.isInteger(value) && range && expandedStackedRowsSupported(props.capabilities, kind) &&
    value >= range.minimum && value <= range.maximum) {
    element.ExpandedStackedRows = value
    delete element.ExpandedStackedColumns
  }
}

function clearUnsupportedHumanReadable(element: Extract<TemplateElement, { Type: 4 }>) {
  delete element.HumanReadable
  delete element.HumanReadableReserveHeight
  updateBarcodeEndCoordinate(element)
}

function barcodeHumanReadableReserveInvalid(element: Extract<TemplateElement, { Type: 4 }>): boolean {
  return element.HumanReadable === false && (element.HumanReadableReserveHeight ?? 0) > 0
}

function clearBarcodeReserveHeight(element: Extract<TemplateElement, { Type: 4 }>) {
  delete element.HumanReadableReserveHeight
  updateBarcodeEndCoordinate(element)
}

function setBarcodeReserveHeight(element: Extract<TemplateElement, { Type: 4 }>, event: Event) {
  const value = Number((event.target as HTMLInputElement).value)
  if (!Number.isFinite(value)) return
  element.HumanReadableReserveHeight = Math.max(0, mmOrDots(value))
  updateBarcodeEndCoordinate(element)
}

function setFieldMode(mode: 'bind' | 'static') {
  if (!props.element) return
  props.element.Field ??= {}
  if (mode === 'bind') {
    props.element.Field.Name = props.element.Field.Name || ''
    delete props.element.Field.Value
  } else {
    props.element.Field.Value = props.element.Field.Value || ''
    delete props.element.Field.Name
  }
}

function structuredGs1Modes(element: TemplateElement): Gs1InputMode[] {
  if (element.Type === 3 || element.Type === 10) return [0, 1]
  if (element.Type === 11) return [0]
  const gs1BarcodeTypes: number[] = [GS1_128_LINEAR_TYPE, DATABAR_TRUNCATED_LINEAR_TYPE,
    DATABAR_EXPANDED_LINEAR_TYPE, DATABAR_STACKED_LINEAR_TYPE,
    DATABAR_EXPANDED_STACKED_LINEAR_TYPE, DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE]
  if (element.Type === 4 && gs1BarcodeTypes.includes(element.BarcodeType ?? -1))
    return [0]
  return []
}

function setPrimaryGs1Input(value: Gs1Input | undefined) {
  const element = props.element
  if (!element) return
  element.Field ??= {}
  if (!value) {
    delete element.Field.Gs1Input
    return
  }
  delete element.Field.Name
  delete element.Field.Value
  element.Field.Gs1Input = value
  if (element.Type === 3 || element.Type === 10) element.Gs1 = value.Mode === 0
}

function setSecondaryGs1Input(value: Gs1Input | undefined) {
  if (props.element?.Type !== 11) return
  if (!value) {
    delete props.element.SecondaryField.Gs1Input
    return
  }
  delete props.element.SecondaryField.Name
  delete props.element.SecondaryField.Value
  props.element.SecondaryField.Gs1Input = value
}

function updateBorder(key: string, event: Event) {
  const value = Number((event.target as HTMLSelectElement).value)
  if (value !== 0 && (!capabilitiesReady.value || !props.capabilities?.elementTypes.includes(5))) return
  if (props.row) { props.row.Border ??= {}; props.row.Border[key as 'TopStyle'] = value }
  else if (props.cell) { props.cell.Border ??= {}; props.cell.Border[key as 'TopStyle'] = value }
}

function updatePageBorder(key: string, event: Event) {
  const value = Number((event.target as HTMLSelectElement).value)
  if (value !== 0 && (!capabilitiesReady.value || !props.capabilities?.elementTypes.includes(5))) return
  props.template.Border ??= {}
  props.template.Border[key as 'TopStyle'] = value
}

function borderStyleDisabled(value: number): boolean {
  return value !== 0 && (!capabilitiesReady.value || !props.capabilities?.elementTypes.includes(5))
}

function hasBorderStyle(border?: { TopStyle?: number; RightStyle?: number; BottomStyle?: number; LeftStyle?: number }): boolean {
  return [border?.TopStyle, border?.RightStyle, border?.BottomStyle, border?.LeftStyle]
    .some((value) => value !== undefined && value !== 0)
}

function setElementMargin(element: TemplateElement, side: 'Top' | 'Right' | 'Bottom' | 'Left', event: Event) {
  const value = Number((event.target as HTMLInputElement).value)
  if (!Number.isFinite(value)) return
  element.Margin ??= {}
  element.Margin[side] = props.unit === 'mm' ? mmToDots(value, props.targetDpi) : Math.round(value)
}

function setFontStyleBit(element: FontElement, bit: number, enabled: boolean) {
  const style = element.FontStyle ?? 0
  element.FontStyle = enabled ? style | bit : style & ~bit
}

function fontIsBold(element: FontElement): boolean {
  return Boolean(element.Bold || ((element.FontStyle ?? 0) & 1))
}

function fontIsUnderlined(element: FontElement): boolean {
  return Boolean((element.FontStyle ?? 0) & 4)
}

function fontIsReversed(element: FontElement): boolean {
  return element.Reverse === true
}

function boldDisabled(): boolean {
  return !props.capabilities || !props.capabilities.supportsBoldText
}

function underlineDisabled(element: FontElement): boolean {
  return !props.capabilities || !props.capabilities.supportsUnderlineText ||
    (element.Rotation !== undefined && element.Rotation !== 0 && !props.capabilities.supportsRotatedUnderlineText)
}

function reverseDisabled(): boolean {
  return !props.capabilities || props.capabilities.supportsReverseText !== true
}

function clearBold(element: FontElement) {
  element.Bold = false
  setFontStyleBit(element, 1, false)
}

function clearUnderline(element: FontElement) {
  setFontStyleBit(element, 4, false)
}

function clearReverse(element: FontElement) {
  element.Reverse = false
}

function setQrModuleSize(element: Extract<TemplateElement, { Type: 3 }>, event: Event) {
  const value = Number((event.target as HTMLInputElement).value)
  if (!Number.isInteger(value) || !qrModuleSizeSupported(props.capabilities, value)) return
  element.UnitWidth = value
}

function setBarcodeModuleWidth(element: Extract<TemplateElement, { Type: 4 }>, key: 'NarrowModuleWidth' | 'WideModuleWidth', event: Event) {
  const raw = (event.target as HTMLInputElement).value.trim()
  if (!raw) {
    delete element[key]
    if (key === 'NarrowModuleWidth') syncFixedBarcodeWideWidth(element)
    else if (key === 'WideModuleWidth') syncFixedBarcodeWideWidth(element)
    return
  }
  const value = Number(raw)
  if (!Number.isInteger(value)) return
  if (key === 'WideModuleWidth') {
    const fixed = fixedWideWidthForElement(element)
    if (fixed !== undefined && value !== fixed) return
  }
  const supported = key === 'NarrowModuleWidth'
    ? barcodeModuleWidthSupported(props.capabilities, value)
    : Boolean(props.capabilities?.supportsWideModuleWidth && valueInRange(value, props.capabilities.barcodeModuleWidth))
  if (!supported) return
  element[key] = value
  if (key === 'NarrowModuleWidth') syncFixedBarcodeWideWidth(element)
}

function clearBarcodeModuleWidth(element: Extract<TemplateElement, { Type: 4 }>, key: 'NarrowModuleWidth' | 'WideModuleWidth') {
  delete element[key]
  if (key === 'NarrowModuleWidth' || key === 'WideModuleWidth') syncFixedBarcodeWideWidth(element)
}

function barcodeLineWidthMaximum(element: Extract<TemplateElement, { Type: 4 }>): number {
  const maximum = props.capabilities?.protocol !== 'Cpcl' && element.NarrowModuleWidth === undefined
    ? props.capabilities?.barcodeModuleWidth?.maximum : null
  return maximum === null || maximum === undefined ? 32 : Math.max(1, maximum - 1)
}

function barcodeLineWidthSupported(element: Extract<TemplateElement, { Type: 4 }>): boolean {
  if (!props.capabilities || element.NarrowModuleWidth !== undefined || props.capabilities.protocol === 'Cpcl') return true
  const maximum = props.capabilities.barcodeModuleWidth?.maximum
  const lineWidth = element.LineWidth
  return maximum === null || maximum === undefined || Number.isInteger(lineWidth) && (lineWidth ?? 0) + 1 <= maximum
}

function setBarcodeLineWidth(element: Extract<TemplateElement, { Type: 4 }>, event: Event) {
  const value = Number((event.target as HTMLInputElement).value)
  if (!Number.isInteger(value) || value < 1 || value > barcodeLineWidthMaximum(element)) return
  element.LineWidth = value
  syncFixedBarcodeWideWidth(element)
}

function barcodeModuleDisabled(key: 'NarrowModuleWidth' | 'WideModuleWidth'): boolean {
  if (!props.capabilities) return true
  return key === 'NarrowModuleWidth'
    ? !props.capabilities.supportsExplicitBarcodeModuleWidth
    : !props.capabilities.supportsWideModuleWidth
}

function barcodeHeightSupported(value: number | undefined): boolean {
  return Boolean(props.capabilities && valueInRange(value, props.capabilities.barcodeHeight))
}

function multirowBarcodeHeightMinimum(element: BarcodeElement): number {
  const moduleWidth = element.NarrowModuleWidth ?? ((element.LineWidth ?? 2) + 1)
  return element.BarcodeType === DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE
    ? minimumGs1DataBarStackedOmnidirectionalLinearHeight(moduleWidth)
    : minimumGs1DataBarExpandedStackedLinearHeight(moduleWidth)
}

function barcodeHeightMinimum(element: BarcodeElement): number {
  const capabilityMinimum = props.capabilities?.barcodeHeight?.minimum ?? 1
  return Math.max(capabilityMinimum,
    element.BarcodeType === DATABAR_EXPANDED_STACKED_LINEAR_TYPE ||
      element.BarcodeType === DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE ? multirowBarcodeHeightMinimum(element) : 1)
}

function barcodeHeightLimitInputValue(value: number | null | undefined): number | undefined {
  return value == null ? undefined : Number(dimensionValue(value))
}

function barcodeHeightRangeLabel(element: BarcodeElement): string {
  const limits = props.capabilities?.barcodeHeight
  if (!limits) return ''
  const minimum = dimensionValue(barcodeHeightMinimum(element))
  const maximum = limits.maximum == null ? '以上' : dimensionValue(limits.maximum)
  return `${minimum}–${maximum} ${props.unit === 'mm' ? 'mm' : '点'}`
}

function updateRowCondition(condition: Condition | undefined) {
  if (props.row) props.row.Condition = condition
  else if (props.element) props.element.Condition = condition
}

function conditionEnabled(target?: { Condition?: Condition }) { return Boolean(target?.Condition) }

function enableCondition(target?: { Condition?: Condition }, value = true) {
  if (!target) return
  if (!value) {
    target.Condition = undefined
    emit('update-condition', undefined)
    return
  }
  target.Condition ??= { Path: '', Operator: 'eq', Value: true }
  emit('update-condition', target.Condition)
}

function updateFieldValue(event: Event) {
  if (props.element?.Field) props.element.Field.Value = (event.target as HTMLTextAreaElement).value
}

function updateFieldName(event: Event) {
  if (props.element?.Field) props.element.Field.Name = (event.target as HTMLInputElement).value
}

function updateFieldFormat(format: FieldFormat | undefined) {
  if (!props.element) return
  props.element.Field ??= {}
  if (format) props.element.Field.Format = format
  else delete props.element.Field.Format
}

function updateSecondaryFieldFormat(format: FieldFormat | undefined) {
  if (props.element?.Type !== 11) return
  const element = props.element as Gs1CompositeElement
  element.SecondaryField ??= {}
  if (format) element.SecondaryField.Format = format
  else delete element.SecondaryField.Format
}

function setSecondaryMode(element: Gs1CompositeElement, bound: boolean) {
  element.SecondaryField = bound ? { Name: '' } : { Value: '' }
}

function setCompositeSize(element: Gs1CompositeElement, key: 'ModuleWidth' | 'LinearBarHeight', event: Event) {
  const value = Number((event.target as HTMLInputElement).value)
  const minimum = key !== 'LinearBarHeight' ? 1 : compositeLinearBarHeightMin(element)
  if (!Number.isInteger(value) || value < minimum || value > 2147483647) return
  if (key === 'ModuleWidth') {
    const minimumHeight = element.LinearType === DATABAR_TRUNCATED_LINEAR_TYPE
      ? minimumGs1DataBarTruncatedLinearHeight(value)
      : element.LinearType === DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE
        ? minimumGs1DataBarStackedOmnidirectionalLinearHeight(value)
        : 1
    if (minimumHeight > 2147483647) return
  }
  element[key] = value
  if (key === 'ModuleWidth') {
    const requiredHeight = compositeLinearBarHeightMin(element)
    if (requiredHeight > 1 && (element.LinearBarHeight ?? 0) < requiredHeight && requiredHeight <= 2147483647)
      element.LinearBarHeight = requiredHeight
  }
}

function compositeLinearBarHeightMin(element: Gs1CompositeElement): number {
  if (element.LinearType === DATABAR_STACKED_LINEAR_TYPE)
    return minimumGs1DataBarStackedLinearHeight(element.ModuleWidth ?? 2)
  if (element.LinearType === DATABAR_EXPANDED_STACKED_LINEAR_TYPE)
    return minimumGs1DataBarExpandedStackedLinearHeight(element.ModuleWidth ?? 2)
  if (element.LinearType === DATABAR_TRUNCATED_LINEAR_TYPE)
    return minimumGs1DataBarTruncatedLinearHeight(element.ModuleWidth ?? 2)
  if (element.LinearType === DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE)
    return minimumGs1DataBarStackedOmnidirectionalLinearHeight(element.ModuleWidth ?? 2)
  return 1
}

function setCompositeLinearType(element: Gs1CompositeElement, event: Event) {
  const value = Number((event.target as HTMLSelectElement).value)
  if (value === EAN_8_LINEAR_TYPE || value === EAN_13_LINEAR_TYPE || value === UPC_A_LINEAR_TYPE ||
    value === UPC_E_LINEAR_TYPE || value === GS1_128_LINEAR_TYPE || value === 25 || value === 27 ||
    value === DATABAR_TRUNCATED_LINEAR_TYPE || value === DATABAR_EXPANDED_LINEAR_TYPE ||
    value === DATABAR_STACKED_LINEAR_TYPE || value === DATABAR_EXPANDED_STACKED_LINEAR_TYPE ||
    value === DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE)
    if (setGs1CompositeLinearType(element, value as Gs1CompositeLinearType, props.capabilities) && value !== DATABAR_EXPANDED_STACKED_LINEAR_TYPE) {
      delete element.ExpandedStackedColumns
      delete element.ExpandedStackedRows
    }
}

function setCompositeMode(element: Gs1CompositeElement, event: Event) {
  const value = Number((event.target as HTMLSelectElement).value)
  if (value === 0 || value === 1 || value === 2 || value === GS1_COMPOSITE_CC_C_MODE)
    setGs1CompositeMode(element, value as Gs1CompositeMode, props.capabilities)
}

function compositeLinearTypeDisabled(value: Gs1CompositeLinearType): boolean {
  if (isGs1CompositeRetailLinearType(value)) return !supportsGs1CompositeEanUpc(props.capabilities)
  if (value === GS1_128_LINEAR_TYPE) return !supportsGs1CompositeGs1128(props.capabilities)
  if (value === DATABAR_EXPANDED_LINEAR_TYPE) return !supportsGs1CompositeExpanded(props.capabilities)
  if (value === DATABAR_STACKED_LINEAR_TYPE) return !supportsGs1CompositeStacked(props.capabilities)
  if (value === DATABAR_EXPANDED_STACKED_LINEAR_TYPE) return !supportsGs1CompositeExpandedStacked(props.capabilities)
  if (value === DATABAR_TRUNCATED_LINEAR_TYPE) return !supportsGs1CompositeTruncated(props.capabilities)
  if (value === DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE)
    return !supportsGs1CompositeStackedOmnidirectional(props.capabilities)
  const name = value === 25 ? 'Gs1DataBarOmnidirectional' : 'Gs1DataBarLimited'
  const supported = props.capabilities?.gs1Composite?.linearTypes
  return supported !== undefined && !supported.includes(name)
}

const conditionTarget = computed(() => props.element ?? props.row)
const conditionValue = computed(() => conditionTarget.value?.Condition)
const selectedImageMissing = computed(() => props.element?.Type === 2 && !props.element.Field?.Name && Boolean(props.element.Field?.Value) && !props.resources.some((resource) => resource.key === props.element?.Field?.Value))

function selectImageResource(event: Event) {
  if (props.element?.Type !== 2) return
  props.element.Field = bindImageResource(props.element.Field, (event.target as HTMLSelectElement).value)
}

type ImageCropAxis = keyof ImageCrop

function imageCropMode(element: ImageElement): 'full' | 'explicit' {
  return element.Crop && typeof element.Crop === 'object' ? 'explicit' : 'full'
}

function imageCropValue(element: ImageElement, axis: ImageCropAxis): string {
  const value = element.Crop && typeof element.Crop === 'object' ? element.Crop[axis] : undefined
  return value === undefined ? '' : String(value)
}

function setImageCropMode(element: ImageElement, event: Event) {
  const mode = (event.target as HTMLSelectElement).value
  if (mode === 'explicit') {
    element.Crop ??= { X: 0, Y: 0, Width: 1, Height: 1 }
    return
  }
  // 删除字段而不是写入空对象，确保旧模板继续使用完整源图语义。
  delete element.Crop
}

function setImageCropValue(element: ImageElement, axis: ImageCropAxis, event: Event) {
  const value = Number((event.target as HTMLInputElement).value)
  if (!Number.isInteger(value) || value < 0 || ((axis === 'Width' || axis === 'Height') && value < 1)) return
  element.Crop ??= { X: 0, Y: 0, Width: 1, Height: 1 }
  element.Crop[axis] = value
}

function clearImageCrop(element: ImageElement) {
  delete element.Crop
}

function addParameter() {
  const name = parameterName.value.trim()
  if (!/^[a-zA-Z][a-zA-Z0-9_-]{0,63}$/.test(name)) { parameterError.value = '参数名无效。'; return }
  if (props.definition) {
    props.definition.Parameters ??= {}
    if (name in props.definition.Parameters) { parameterError.value = '参数已存在。'; return }
    props.definition.Parameters[name] = { Required: true }
  } else if (props.element?.Type === 6) {
    props.element.Parameters ??= {}
    if (name in props.element.Parameters) { parameterError.value = '参数已存在。'; return }
    props.element.Parameters[name] = props.fieldPaths[0] ? { Path: props.fieldPaths[0] } : { Value: '' }
  }
  parameterName.value = ''
  parameterError.value = ''
}

function setParameterJson(target: { Value?: unknown; DefaultValue?: unknown }, key: 'Value' | 'DefaultValue', event: Event) {
  try { target[key] = JSON.parse((event.target as HTMLInputElement).value) as never; parameterError.value = '' }
  catch { parameterError.value = '参数值必须是有效 JSON。' }
}

function setParameterMode(binding: { Path?: string; Value?: unknown }, mode: 'path' | 'value') {
  if (mode === 'path') {
    const path = props.fieldPaths[0]
    if (!path) { parameterError.value = '当前没有可用字段路径，无法切换为字段绑定。'; return }
    binding.Path = path
    delete binding.Value
  }
  else { binding.Value = ''; delete binding.Path }
}
</script>

<template>
  <aside class="inspector-panel">
    <header class="panel-heading inspector-heading">
      <div class="panel-title"><Braces :size="15" /><span>属性</span></div>
      <button class="icon-button quiet" type="button" title="属性面板" disabled><ChevronDown :size="15" /></button>
    </header>
    <div class="inspector-scroll">
      <div class="inspector-selection">
        <div class="selection-icon"><Type v-if="element?.Type === 1" :size="16" /><FileText v-else-if="isPage" :size="16" /><Layers3 v-else :size="16" /></div>
        <div><strong>{{ title }}</strong><small>{{ selectedPath || `Version ${template.Version} 模板` }}</small></div>
      </div>
      <p v-if="locked" class="field-note capability-warning">此节点或其父级已锁定；请在结构树中解锁后编辑。</p>
      <fieldset class="inspector-edit-scope" :disabled="locked">

      <template v-if="isPage">
        <section class="inspector-section">
          <div class="section-title"><span>纸张尺寸</span><Ruler :size="13" /></div>
          <label class="field-label">宽度 <span>{{ unit }}</span><input class="field-input" type="number" min="1" :value="dimensionValue(template.Width)" @input="setDimension(template as unknown as Record<string, unknown>, 'Width', $event)" /></label>
          <label class="field-label">高度 <span>{{ unit }}</span><input class="field-input" type="number" min="0" :value="dimensionValue(template.Height)" @input="setDimension(template as unknown as Record<string, unknown>, 'Height', $event)" /></label>
          <div class="dimension-note">{{ dotsToMm(template.Width, targetDpi) }} × {{ dotsToMm(template.Height, targetDpi) }} mm · {{ targetDpi }} DPI</div>
          <label class="toggle-row"><span>按内容计算页高</span><input type="checkbox" :checked="template.AutoHeight" @change="template.AutoHeight = ($event.target as HTMLInputElement).checked" /></label>
          <p v-if="template.AutoHeight && template.Height === 0" class="field-note">Height 为 0 时由渲染选项限制单页最大高度。</p>
          <div class="section-title border-title"><span>页面边框</span></div>
          <div class="numeric-grid">
            <label class="field-label">上<select class="field-input" :value="template.Border?.TopStyle ?? 0" @change="updatePageBorder('TopStyle', $event)"><option :value="0">无</option><option :value="1" :disabled="borderStyleDisabled(1)">实线</option><option :value="2" :disabled="borderStyleDisabled(2)">虚线</option></select></label>
            <label class="field-label">右<select class="field-input" :value="template.Border?.RightStyle ?? 0" @change="updatePageBorder('RightStyle', $event)"><option :value="0">无</option><option :value="1" :disabled="borderStyleDisabled(1)">实线</option><option :value="2" :disabled="borderStyleDisabled(2)">虚线</option></select></label>
            <label class="field-label">下<select class="field-input" :value="template.Border?.BottomStyle ?? 0" @change="updatePageBorder('BottomStyle', $event)"><option :value="0">无</option><option :value="1" :disabled="borderStyleDisabled(1)">实线</option><option :value="2" :disabled="borderStyleDisabled(2)">虚线</option></select></label>
            <label class="field-label">左<select class="field-input" :value="template.Border?.LeftStyle ?? 0" @change="updatePageBorder('LeftStyle', $event)"><option :value="0">无</option><option :value="1" :disabled="borderStyleDisabled(1)">实线</option><option :value="2" :disabled="borderStyleDisabled(2)">虚线</option></select></label>
          </div>
          <p v-if="capabilitiesReady && !capabilities?.elementTypes.includes(5) && hasBorderStyle(template.Border)" class="field-note capability-warning">当前协议不支持页面边框；保留现有值，选择“无”可清除。</p>
        </section>
        <section class="inspector-section">
          <div class="section-title"><span>模板变量</span><span class="section-kicker">$var</span></div>
          <div v-for="(path, name) in template.Variables ?? {}" :key="name" class="variable-row">
            <span>{{ name }}</span><input class="field-input" :aria-label="`变量 ${name} 的字段路径`" :value="path" @input="template.Variables![name] = ($event.target as HTMLInputElement).value" />
            <button type="button" :title="`删除变量 ${name}`" @click="delete template.Variables![name]">×</button>
          </div>
          <label class="field-label">变量名<input v-model="variableName" class="field-input" placeholder="shopName" /></label>
          <label class="field-label">根数据字段路径<input v-model="variablePath" class="field-input" list="business-field-paths" placeholder="Store.Name" /><datalist id="business-field-paths"><option v-for="path in fieldPaths" :key="path" :value="path" /></datalist></label>
          <button class="wide-action" type="button" :disabled="!variableName || !variablePath" @click="addVariable">添加变量</button>
        </section>
        <section class="inspector-section structure-actions">
          <div class="section-title"><span>模板结构</span></div>
          <button class="wide-action" type="button" @click="emit('add-row')">添加行</button>
          <button class="wide-action" type="button" @click="emit('add-column')">添加列到当前行</button>
        </section>
      </template>

      <template v-else-if="definition">
        <section class="inspector-section">
          <div class="section-title"><span>组件定义</span></div>
          <label class="field-label">标识<input class="field-input" :value="definition.Id" readonly /></label>
          <label class="field-label">宽度 <span>{{ unit }}</span><input class="field-input" type="number" min="0" :value="dimensionValue(definition.Width)" @input="setDimension(definition as unknown as Record<string, unknown>, 'Width', $event)" /></label>
          <label class="field-label">高度 <span>{{ unit }}</span><input class="field-input" type="number" min="0" :value="dimensionValue(definition.Height)" @input="setDimension(definition as unknown as Record<string, unknown>, 'Height', $event)" /></label>
        </section>
        <section class="inspector-section">
          <div class="section-title"><span>参数定义</span><span class="section-kicker">$param</span></div>
          <div v-for="(parameter, name) in definition.Parameters ?? {}" :key="name" class="variable-row">
            <span>{{ name }}</span>
            <label class="toggle-row"><span>必填</span><input type="checkbox" :checked="parameter.Required" @change="parameter.Required = ($event.target as HTMLInputElement).checked" /></label>
            <input class="field-input" :aria-label="`${name} 默认值 JSON`" :value="parameter.DefaultValue === undefined ? '' : JSON.stringify(parameter.DefaultValue)" placeholder="默认值 JSON" @change="setParameterJson(parameter, 'DefaultValue', $event)" />
            <button type="button" :title="`删除参数 ${name}`" @click="delete definition.Parameters![name]">×</button>
          </div>
          <label class="field-label">新参数<input v-model="parameterName" class="field-input" placeholder="例如 currency" @keydown.enter="addParameter" /></label>
          <button class="wide-action" type="button" @click="addParameter">添加参数</button>
          <p v-if="parameterError" class="field-note">{{ parameterError }}</p>
        </section>
      </template>

      <template v-else-if="element">
        <section v-if="[1, 2, 3, 4, 10, 11].includes(element.Type) && !element.Field?.Gs1Input" class="inspector-section">
          <div class="section-title"><span>内容绑定</span><span class="section-kicker">Field</span></div>
          <div class="segmented-control">
            <button type="button" :class="{ active: Boolean(element.Field?.Name) }" @click="setFieldMode('bind')">字段</button>
            <button type="button" :class="{ active: !element.Field?.Name }" @click="setFieldMode('static')">固定文字</button>
          </div>
          <label v-if="element.Field?.Name !== undefined" class="field-label">字段路径<input class="field-input" list="business-field-paths" :value="element.Field.Name" placeholder="例如 Product.Name" @input="updateFieldName" /><datalist id="business-field-paths"><option v-for="path in fieldPaths" :key="path" :value="path" /></datalist></label>
          <label v-else class="field-label">显示内容<textarea class="field-input field-textarea" :value="element.Field?.Value ?? ''" rows="3" placeholder="输入打印内容" @input="updateFieldValue" /></label>
          <FieldFormatEditor :format="element.Field?.Format" @update:format="updateFieldFormat" />
          <template v-if="element.Type === 1">
            <label class="toggle-row"><span>文字换行</span><input type="checkbox" :checked="element.Field?.NeedFeedLine !== false" @change="element.Field ??= {}; element.Field.NeedFeedLine = ($event.target as HTMLInputElement).checked" /></label>
            <label class="toggle-row"><span>适配可用宽度</span><input type="checkbox" :checked="element.Field?.Fit" @change="element.Field ??= {}; element.Field.Fit = ($event.target as HTMLInputElement).checked" /></label>
          </template>
          <div v-if="element.Type === 2 && resources.length" class="resource-status">图片包已包含 {{ resources.length }} 个资源</div>
          <div v-if="selectedImageMissing" class="resource-missing">当前资源键尚未导入</div>
          <p v-if="element.Type === 2 && capabilitiesReady && !elementTypeSupported(capabilities, 2)" class="field-note capability-warning">{{ capabilityReason('图片元素') }}</p>
        </section>
        <Gs1AiEditor v-if="structuredGs1Modes(element).length" :template="template" :data="data" :element="element"
          :model-value="element.Field?.Gs1Input" :field-path="`${selectedPath}.Field.Gs1Input`"
          :field-paths="fieldPaths" :allowed-modes="structuredGs1Modes(element)" :enabled="true"
          support-reason="当前元素不支持 GS1 结构化来源。" @update:model-value="setPrimaryGs1Input" />

        <section class="inspector-section">
          <div class="section-title"><span>位置与尺寸</span><span class="section-kicker">{{ unit }}</span></div>
          <label v-if="element.Type === 6" class="field-label">位置模式<select class="field-input" :value="element.StartCoordinate ? 'fixed' : 'flow'" @change="($event.target as HTMLSelectElement).value === 'flow' ? delete element.StartCoordinate : element.StartCoordinate = { X: element.Margin?.Left ?? 0, Y: element.Margin?.Top ?? 0 }"><option value="flow">流式</option><option value="fixed">固定坐标</option></select></label>
          <div class="numeric-grid">
            <label class="field-label">X<input class="field-input" type="number" :value="dimensionValue(element.StartCoordinate?.X ?? (element.Type === 6 ? element.Margin?.Left : 0))" @input="setElementPosition('X', $event)" /></label>
            <label class="field-label">Y<input class="field-input" type="number" :value="dimensionValue(element.StartCoordinate?.Y ?? (element.Type === 6 ? element.Margin?.Top : 0))" @input="setElementPosition('Y', $event)" /></label>
            <label v-if="element.Type !== 5 && element.Type !== 11" class="field-label">宽度<input class="field-input" type="number" min="1" :value="elementDimensionValue(element, 'X')" @input="setElementDimension(element, 'X', $event)" /></label>
            <label v-if="element.Type !== 5 && element.Type !== 11" class="field-label">高度 <span v-if="element.Type === 4 && capabilities?.barcodeHeight">{{ barcodeHeightRangeLabel(element as BarcodeElement) }}</span><input class="field-input" type="number" :min="element.Type === 4 ? barcodeHeightLimitInputValue(barcodeHeightMinimum(element as BarcodeElement)) : 1" :max="element.Type === 4 ? barcodeHeightLimitInputValue(capabilities?.barcodeHeight?.maximum) : undefined" :step="unit === 'mm' ? 'any' : 1" :value="elementDimensionValue(element, 'Y')" :disabled="element.Type === 4 && !capabilitiesReady" @input="setElementDimension(element, 'Y', $event)" /></label>
            <p v-if="element.Type === 4 && capabilitiesReady && !barcodeHeightSupported(element.Height)" class="field-note capability-warning">当前条码高度超出协议范围，请改为支持的值。</p>
            <p v-if="element.Type === 4 && element.BarcodeType === DATABAR_EXPANDED_STACKED_LINEAR_TYPE" class="field-note">Expanded Stacked 的 Height 表示每条数据行高度（打印点）；默认每行 2 个字符对，可按列数配置分行，最大为 11 行。相邻数据行之间增加三个 1X 分隔行；每行至少为模块宽度，34X 是推荐扫码高度。</p>
            <p v-if="element.Type === 4 && element.BarcodeType === DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE" class="field-note">Stacked Omnidirectional 的 Height 表示每条数据行高度（打印点），至少为窄模块宽度的 33 倍；两行之间增加三个 1X 分隔行，总高度为 2H+3X。HRI 为 (01)+GTIN，仅支持 ESC/POS Raster。</p>
            <template v-if="element.Type === 5">
              <label class="field-label">终点 X<input class="field-input" type="number" :value="dimensionValue(element.LineEndCoordinate?.X ?? element.EndCoordinate?.X)" @input="element.LineEndCoordinate ??= { X: 0, Y: 0 }; setDimension(element.LineEndCoordinate as unknown as Record<string, unknown>, 'X', $event)" /></label>
              <label class="field-label">终点 Y<input class="field-input" type="number" :value="dimensionValue(element.LineEndCoordinate?.Y ?? element.EndCoordinate?.Y)" @input="element.LineEndCoordinate ??= { X: 0, Y: 0 }; setDimension(element.LineEndCoordinate as unknown as Record<string, unknown>, 'Y', $event)" /></label>
            </template>
          </div>
          <label v-if="element.Type === 1 || element.Type === 2 || element.Type === 3 || element.Type === 4" class="field-label">旋转<select class="field-input" aria-label="元素旋转" :value="element.Rotation ?? 0" :disabled="!capabilitiesReady" @change="setRotation(element as FontElement | ImageElement | QrElement | BarcodeElement, $event)"><option v-for="option in rotationOptions" :key="option.value" :value="option.value" :disabled="rotationDisabled(element as FontElement | ImageElement | QrElement | BarcodeElement, option.value)">{{ option.label }}</option></select></label>
          <p v-if="(element.Type === 1 || element.Type === 2 || element.Type === 3 || element.Type === 4) && capabilitiesReady && !elementRotationSupported(element)" class="field-note capability-warning">当前旋转值在 {{ capabilities?.protocol === 'EscPos' ? 'ESC/POS' : capabilities?.protocol?.toUpperCase() }} 中不受支持，请改选可用角度。</p>
          <p v-else-if="(element.Type === 1 || element.Type === 2 || element.Type === 3 || element.Type === 4) && !capabilitiesReady" class="field-note capability-warning">{{ capabilityNotice }}</p>
          <label v-if="element.Type === 1" class="field-label">旋转前行宽 <span>{{ unit }} · 0 为自动</span><input class="field-input" type="number" min="0" :value="dimensionValue(element.Width)" @input="setTextWidth(element as FontElement, $event)" /></label>
          <label v-if="element.Type === 11" class="field-label">旋转<select class="field-input" aria-label="Composite 旋转" :value="element.Rotation ?? 0" :disabled="!capabilitiesReady" @change="setRotation(element, $event)"><option v-for="option in rotationOptions" :key="option.value" :value="option.value" :disabled="rotationDisabled(element, option.value)">{{ option.label }}</option></select></label>
        </section>

        <section class="inspector-section">
          <div class="section-title"><span>元素间距</span><span class="section-kicker">{{ unit }}</span></div>
          <div class="numeric-grid">
            <label class="field-label">上<input class="field-input" type="number" :value="dimensionValue(element.Margin?.Top)" @input="setElementMargin(element, 'Top', $event)" /></label>
            <label class="field-label">右<input class="field-input" type="number" :value="dimensionValue(element.Margin?.Right)" @input="setElementMargin(element, 'Right', $event)" /></label>
            <label class="field-label">下<input class="field-input" type="number" :value="dimensionValue(element.Margin?.Bottom)" @input="setElementMargin(element, 'Bottom', $event)" /></label>
            <label class="field-label">左<input class="field-input" type="number" :value="dimensionValue(element.Margin?.Left)" @input="setElementMargin(element, 'Left', $event)" /></label>
          </div>
        </section>

        <section v-if="element.Type === 1" class="inspector-section">
          <div class="section-title"><span>文字样式</span><span class="section-kicker">CPCL FONT</span></div>
           <label v-if="textSizeUsesSelect(element as FontElement)" class="field-label">字号<select class="field-input" :value="element.FontSize || 24" :disabled="!capabilitiesReady" @change="setFontSize(element as FontElement, $event)"><option v-for="size in textSizeChoices(element as FontElement)" :key="size" :value="size" :disabled="!textSizeSupported(capabilities, element as FontElement, size)">{{ size }} 点</option></select></label>
           <label v-else class="field-label">字号 <span v-if="textSizeMinimum(element as FontElement) !== undefined">{{ textSizeMinimum(element as FontElement) }}–{{ textSizeMaximum(element as FontElement) ?? '以上' }} 点</span><input class="field-input" type="number" :min="textSizeMinimum(element as FontElement) ?? 1" :max="textSizeMaximum(element as FontElement) ?? undefined" step="1" :value="element.FontSize || 24" :disabled="!capabilitiesReady" @input="setFontSize(element as FontElement, $event)" /></label>
           <p v-if="capabilitiesReady && !textSizeSupported(capabilities, element as FontElement, (element.FontSize || 24))" class="field-note capability-warning">当前字号不在协议范围内；保留原值，请改为支持的字号。</p>
           <label class="field-label">字体来源<select class="field-input" aria-label="字体来源" :value="fontSource(element as FontElement)" :disabled="!capabilitiesReady" @change="setFontSource(element as FontElement, $event)"><option value="legacy">旧版字体字段</option><option value="default" :disabled="sourceDisabled('default')" :title="sourceTitle('default')">协议默认</option><option value="device" :disabled="sourceDisabled('device')" :title="sourceTitle('device')">设备字体</option><option value="host" :disabled="sourceDisabled('host')" :title="sourceTitle('host')">主机字体</option></select></label>
          <p v-if="capabilitiesReady && !fontSourceSupported(capabilities, fontSource(element as FontElement))" class="field-note capability-warning">当前字体来源在此协议中不受支持，请改选可用来源。</p>
          <p v-else-if="!capabilitiesReady" class="field-note capability-warning">{{ capabilityNotice }}</p>
          <template v-if="fontSource(element as FontElement) === 'legacy'">
            <label class="field-label">字体编号<select class="field-input" :value="element.FontName ?? 0" :disabled="!capabilitiesReady" @change="setLegacyFontName(element as FontElement, $event)"><option v-for="name in legacyFontChoices(element as FontElement)" :key="name" :value="name" :disabled="!legacyDeviceFontSupported(capabilities, name, element.FontSize)">Font {{ name }}{{ name === 0 ? ' · 默认' : '' }}</option></select></label>
            <p v-if="capabilitiesReady && !legacyDeviceFontSupported(capabilities, element.FontName, element.FontSize)" class="field-note capability-warning">当前旧版 FontName 或字号在此协议中不受支持，保留原值，请改为 0 或切换字体来源。</p>
            <label v-if="capabilities?.rasterFonts" class="field-label">固定字体别名 <span>ESC/POS Raster</span><select class="field-input" aria-label="Raster 固定字体别名" :value="element.FontFamily ?? ''" :disabled="!hostFontFamilySupported(capabilities)" @change="setRasterFontAlias(element as FontElement, $event)"><option value="">默认字体</option><option v-for="name in rasterFontChoices(element.FontFamily)" :key="name" :value="name">{{ name }}</option></select></label>
            <p v-if="capabilities?.rasterFonts && element.FontFamily && !rasterFontAliasSupported(element.FontFamily)" class="field-note capability-warning">当前字体别名未在固定 Raster 字体中声明，请改选已声明别名。</p>
            <label v-if="!capabilities?.rasterFonts" class="field-label">系统字体族 <span>ESC/POS Raster</span><input class="field-input" type="text" :disabled="!hostFontFamilySupported(capabilities)" :value="element.FontFamily ?? ''" placeholder="留空自动选择" @input="element.FontFamily = ($event.target as HTMLInputElement).value.trim() || undefined" /></label>
            <button v-if="capabilitiesReady && element.FontFamily && !hostFontFamilySupported(capabilities)" class="field-clear-action" type="button" @click="delete element.FontFamily">清除不支持的系统字体族</button>
            <label class="field-label">设备字体 <span>旧版设备字段</span><input class="field-input" type="text" maxlength="1" :disabled="!capabilitiesReady || !capabilities?.supportsLegacyDeviceFont" :value="element.DeviceFontName ?? ''" placeholder="默认 0" @input="element.DeviceFontName = ($event.target as HTMLInputElement).value.trim() || undefined" /></label>
            <button v-if="capabilitiesReady && element.DeviceFontName && !capabilities?.supportsLegacyDeviceFont" class="field-clear-action" type="button" @click="delete element.DeviceFontName">清除不支持的旧设备字体</button>
          </template>
          <label v-else-if="fontSource(element as FontElement) === 'device'" class="field-label">设备字体名称 <span>协议能力</span><select class="field-input" aria-label="设备字体名称" :value="element.Font?.Name ?? ''" :disabled="!capabilitiesReady" @change="setDeviceFontName(element as FontElement, $event)"><option value="" disabled>选择设备字体</option><option v-for="name in deviceFontChoices(element as FontElement)" :key="name" :value="name" :disabled="deviceFontDisabled(name)">{{ name }}</option></select></label>
          <template v-else-if="fontSource(element as FontElement) === 'host'">
            <label v-if="capabilities?.rasterFonts" class="field-label">固定字体别名 <span>ESC/POS Raster</span><select class="field-input" aria-label="Raster 固定字体别名" :value="element.Font?.Name ?? ''" :disabled="!hostFontFamilySupported(capabilities)" @change="setRasterFontAlias(element as FontElement, $event)"><option value="">默认字体</option><option v-for="name in rasterFontChoices(element.Font?.Name)" :key="name" :value="name">{{ name }}</option></select></label>
            <p v-if="capabilities?.rasterFonts && element.Font?.Name && !rasterFontAliasSupported(element.Font.Name)" class="field-note capability-warning">当前字体别名未在固定 Raster 字体中声明，请改选已声明别名。</p>
            <label v-if="!capabilities?.rasterFonts" class="field-label">主机字体名称 <span>ESC/POS Raster</span><input class="field-input" type="text" aria-label="主机字体名称" :disabled="!hostFontFamilySupported(capabilities)" :value="element.Font?.Name ?? ''" placeholder="例如 Microsoft YaHei" @input="setFontName(element as FontElement, $event)" /></label>
          </template>
          <p v-if="fontSource(element as FontElement) === 'device' && capabilitiesReady && !deviceFontCapability(capabilities, element.Font?.Name)" class="field-note capability-warning">当前设备字体不在能力列表中，保留原值，请改选可用字体。</p>
          <div class="style-controls">
            <label class="toggle-row"><span>粗体</span><input type="checkbox" :checked="fontIsBold(element as FontElement)" :disabled="boldDisabled()" @change="element.Bold = ($event.target as HTMLInputElement).checked; setFontStyleBit(element as FontElement, 1, ($event.target as HTMLInputElement).checked)" /></label>
            <button v-if="boldDisabled() && fontIsBold(element as FontElement)" class="field-clear-action" type="button" @click="clearBold(element as FontElement)">清除不支持的粗体</button>
            <label class="toggle-row"><span>下划线</span><input type="checkbox" :checked="fontIsUnderlined(element as FontElement)" :disabled="underlineDisabled(element as FontElement)" @change="setFontStyleBit(element as FontElement, 4, ($event.target as HTMLInputElement).checked)" /></label>
            <button v-if="underlineDisabled(element as FontElement) && fontIsUnderlined(element as FontElement)" class="field-clear-action" type="button" @click="clearUnderline(element as FontElement)">清除不支持的下划线</button>
            <label class="toggle-row"><span>反白</span><input type="checkbox" :checked="fontIsReversed(element as FontElement)" :disabled="reverseDisabled()" @change="(element as FontElement).Reverse = ($event.target as HTMLInputElement).checked" /></label>
            <button v-if="reverseDisabled() && fontIsReversed(element as FontElement)" class="field-clear-action" type="button" @click="clearReverse(element as FontElement)">清除不支持的反白</button>
            <p v-if="capabilitiesReady && !capabilities?.supportsBoldText && fontIsBold(element as FontElement)" class="field-note capability-warning">当前协议不支持粗体。</p>
            <p v-if="capabilitiesReady && (!capabilities?.supportsUnderlineText || ((element.Rotation ?? 0) !== 0 && !capabilities?.supportsRotatedUnderlineText)) && fontIsUnderlined(element as FontElement)" class="field-note capability-warning">当前协议或旋转角度不支持下划线。</p>
            <p v-if="capabilitiesReady && capabilities?.supportsReverseText !== true && fontIsReversed(element as FontElement)" class="field-note capability-warning">当前协议或布局不支持反白。</p>
            <div class="alignment-control" role="group" aria-label="文字对齐">
              <button type="button" title="左对齐" :class="{ active: (element.TextAlignment ?? 0) === 0 }" @click="element.TextAlignment = 0"><AlignLeft :size="15" /></button>
              <button type="button" title="居中对齐" :class="{ active: element.TextAlignment === 1 }" @click="element.TextAlignment = 1"><AlignCenter :size="15" /></button>
              <button type="button" title="右对齐" :class="{ active: element.TextAlignment === 2 }" @click="element.TextAlignment = 2"><AlignRight :size="15" /></button>
            </div>
          </div>
        </section>

        <section v-if="element.Type === 2" class="inspector-section">
          <div class="section-title"><span>图片设置</span><span class="section-kicker">RESOURCE KEY</span></div>
          <label v-if="resources.length" class="field-label">已导入资源<select class="field-input" :value="element.Field?.Name ? '' : element.Field?.Value ?? ''" @change="selectImageResource"><option value="">选择资源</option><option v-for="resource in resources" :key="resource.key" :value="resource.key">{{ resource.key }}</option></select></label>
          <label class="toggle-row"><span>压缩点阵</span><input type="checkbox" :checked="element.IsCompress" @change="element.IsCompress = ($event.target as HTMLInputElement).checked" /></label>
          <label class="field-label">源图裁切<select class="field-input" aria-label="图片源图裁切" :value="imageCropMode(element as ImageElement)" @change="setImageCropMode(element as ImageElement, $event)"><option value="full">完整源图</option><option value="explicit">显式区域</option></select></label>
          <div v-if="imageCropMode(element as ImageElement) === 'explicit'" class="numeric-grid">
            <label class="field-label">源 X <span>像素</span><input class="field-input" aria-label="图片裁切源 X" type="number" min="0" step="1" :value="imageCropValue(element as ImageElement, 'X')" @input="setImageCropValue(element as ImageElement, 'X', $event)" /></label>
            <label class="field-label">源 Y <span>像素</span><input class="field-input" aria-label="图片裁切源 Y" type="number" min="0" step="1" :value="imageCropValue(element as ImageElement, 'Y')" @input="setImageCropValue(element as ImageElement, 'Y', $event)" /></label>
            <label class="field-label">源宽 <span>像素</span><input class="field-input" aria-label="图片裁切源宽度" type="number" min="1" step="1" :value="imageCropValue(element as ImageElement, 'Width')" @input="setImageCropValue(element as ImageElement, 'Width', $event)" /></label>
            <label class="field-label">源高 <span>像素</span><input class="field-input" aria-label="图片裁切源高度" type="number" min="1" step="1" :value="imageCropValue(element as ImageElement, 'Height')" @input="setImageCropValue(element as ImageElement, 'Height', $event)" /></label>
          </div>
          <button v-if="imageCropMode(element as ImageElement) === 'explicit'" class="field-clear-action" type="button" @click="clearImageCrop(element as ImageElement)">恢复完整源图</button>
          <p v-if="imageCropMode(element as ImageElement) === 'explicit'" class="field-note">坐标和尺寸按源图像素保存；超出源图边界时由打印服务严格拒绝。本地画布仍显示资源原图，最终裁切以引擎预览和输出为准。</p>
        </section>
        <section v-if="element.Type === 3" class="inspector-section">
          <div class="section-title"><span>二维码参数</span></div>
          <label class="field-label">模块宽度 <span v-if="capabilities?.qrModuleSize">{{ capabilities.qrModuleSize.minimum }}–{{ capabilities.qrModuleSize.maximum ?? '以上' }}</span><input class="field-input" type="number" :min="capabilities?.qrModuleSize?.minimum ?? 1" :max="capabilities?.qrModuleSize?.maximum ?? undefined" :value="element.UnitWidth || 4" :disabled="!capabilitiesReady" @input="setQrModuleSize(element, $event)" /></label>
          <p v-if="capabilitiesReady && !qrModuleSizeSupported(capabilities, element.UnitWidth ?? 4)" class="field-note capability-warning">当前二维码模块宽度超出协议范围，请改为支持的值。</p>
          <label class="field-label">纠错级别<select class="field-input" :value="element.ErrorLevel ?? 'M'" @change="element.ErrorLevel = ($event.target as HTMLSelectElement).value as 'L' | 'M' | 'Q' | 'H'"><option>L</option><option>M</option><option>Q</option><option>H</option></select></label>
          <label class="raster-line-option" title="使用首位 FNC1 的 GS1 QR Code；Native 不支持"><input type="checkbox" aria-label="GS1 QR Code" :checked="element.Gs1 === true" :disabled="gs1TwoDimensionalDisabled(element)" @change="setGs1(element, $event)" /><span>GS1 QR Code</span></label>
          <p v-if="gs1TwoDimensionalDisabled(element)" class="field-note capability-warning">当前 Host 或协议不支持 GS1 QR Code；Native 请改用 ESC/POS Raster。</p>
        </section>
        <section v-if="element.Type === 4" class="inspector-section">
          <div class="section-title"><span>条码参数</span></div>
          <label class="field-label">制式<select class="field-input" :value="element.BarcodeType ?? 0" :disabled="!capabilitiesReady" @change="setBarcodeType(element, $event)"><option v-for="option in barcodeOptions" :key="option[1]" :value="option[1]" :disabled="barcodeTypeDisabled(option[1])" :title="barcodeTypeDisabled(option[1]) ? capabilityReason('该条码制式') : ''">{{ option[0] }}</option></select></label>
          <p v-if="capabilitiesReady && !barcodeTypeSupported(capabilities, element.BarcodeType ?? 0)" class="field-note capability-warning">当前条码制式在此协议中不受支持，请改选可用制式。</p>
          <label v-if="element.BarcodeType === DATABAR_EXPANDED_STACKED_LINEAR_TYPE" class="field-label">Expanded Stacked 每行列数<select class="field-input" aria-label="Expanded Stacked 每行列数" :value="element.ExpandedStackedColumns ?? 'default'" :disabled="!capabilitiesReady" @change="setExpandedStackedColumns(element, 'ordinary', $event)"><option value="default">默认（2 列）</option><option v-for="columns in ordinaryExpandedStackedColumnOptions" :key="columns" :value="columns" :disabled="expandedStackedColumnsDisabled('ordinary', columns)">{{ columns }} 列</option></select></label>
          <p v-if="element.BarcodeType === DATABAR_EXPANDED_STACKED_LINEAR_TYPE && element.ExpandedStackedColumns != null && expandedStackedColumnsWarning('ordinary', element.ExpandedStackedColumns)" class="field-note capability-warning">{{ expandedStackedColumnsWarning('ordinary', element.ExpandedStackedColumns) }}</p>
          <label v-if="element.BarcodeType === DATABAR_EXPANDED_STACKED_LINEAR_TYPE" class="field-label">Expanded Stacked 最大数据行数<select class="field-input" aria-label="Expanded Stacked 最大数据行数" :value="element.ExpandedStackedRows ?? 'default'" :disabled="!capabilitiesReady" @change="setExpandedStackedRows(element, 'ordinary', $event)"><option value="default">不限制</option><option v-for="rows in expandedStackedRowOptions" :key="rows" :value="rows" :disabled="expandedStackedRowsDisabled('ordinary', rows)">{{ rows }} 行</option></select></label>
          <p v-if="element.BarcodeType === DATABAR_EXPANDED_STACKED_LINEAR_TYPE && element.ExpandedStackedRows != null && expandedStackedRowsWarning('ordinary', element.ExpandedStackedRows)" class="field-note capability-warning">{{ expandedStackedRowsWarning('ordinary', element.ExpandedStackedRows) }}</p>
          <p v-if="element.BarcodeType === DATABAR_EXPANDED_STACKED_LINEAR_TYPE && element.ExpandedStackedColumns != null && element.ExpandedStackedRows != null" class="field-note capability-warning">列数与最大行数互斥，请只保留一种布局配置。</p>
          <label class="field-label">可读文字<select class="field-input" aria-label="条码可读文字模式" :value="barcodeHumanReadableMode(element.HumanReadable)" :disabled="!capabilitiesReady" @change="setBarcodeHumanReadable(element, $event)"><option value="default">协议默认</option><option value="on" :disabled="capabilitiesReady && !capabilities?.supportsBarcodeHumanReadable" :title="capabilitiesReady && !capabilities?.supportsBarcodeHumanReadable ? capabilityReason('启用条码可读文字') : ''">开启</option><option value="off">关闭</option></select></label>
          <button v-if="capabilitiesReady && !capabilities?.supportsBarcodeHumanReadable && element.HumanReadable === true" class="field-clear-action" type="button" @click="clearUnsupportedHumanReadable(element)">清除不支持的可读文字设置</button>
          <p v-if="capabilitiesReady && !capabilities?.supportsBarcodeHumanReadable && element.HumanReadable === true" class="field-note capability-warning">当前协议不支持启用条码可读文字；保留现值，清除后可继续预览。</p>
          <button v-if="barcodeHumanReadableReserveInvalid(element)" class="field-clear-action" type="button" @click="clearBarcodeReserveHeight(element)">清除无效的可读文字预留高度</button>
          <p v-if="barcodeHumanReadableReserveInvalid(element)" class="field-note capability-warning">条码可读文字已关闭，当前预留高度无效；清除后才能通过严格预览。</p>
          <div class="numeric-grid">
            <label class="field-label">窄条宽<input class="field-input" type="number" min="1" :max="barcodeLineWidthMaximum(element)" :value="element.LineWidth ?? 2" :disabled="!capabilitiesReady" @input="setBarcodeLineWidth(element, $event)" /></label>
            <p v-if="capabilitiesReady && !barcodeLineWidthSupported(element)" class="field-note capability-warning">当前兼容窄条宽度超出协议范围；保留原值，请改为支持值或设置显式窄条宽度。</p>
            <label v-if="tsplVariableBarcodeRatioEditor(element as BarcodeElement)" class="field-label">TSPL 窄宽比<select class="field-input" aria-label="TSPL 窄宽比" :value="currentTsplBarcodeRatio(element as BarcodeElement) ?? ''" :disabled="!capabilitiesReady" @change="setTsplBarcodeRatio(element as BarcodeElement, $event)"><option value="" disabled>选择受支持比例</option><option v-for="ratio in tsplVariableBarcodeRatios" :key="ratio.value" :value="ratio.value" :disabled="tsplBarcodeRatioDisabled(element as BarcodeElement, ratio.value)" :title="tsplBarcodeRatioDisabled(element as BarcodeElement, ratio.value) ? '当前能力范围无法容纳此比例' : ''">{{ ratio.label }}</option></select></label>
            <label v-else class="field-label">宽窄比<select class="field-input" :value="element.Ratio ?? 2" @change="element.Ratio = Number(($event.target as HTMLSelectElement).value)"><option v-for="ratio in barcodeRatios" :key="ratio.value" :value="ratio.value">{{ ratio.label }}</option></select></label>
          </div>
          <div class="numeric-grid">
            <label class="field-label">显式窄条 <span v-if="capabilities?.barcodeModuleWidth">{{ capabilities.barcodeModuleWidth.minimum }}–{{ capabilities.barcodeModuleWidth.maximum ?? '以上' }}</span><input class="field-input" type="number" :min="capabilities?.barcodeModuleWidth?.minimum ?? 1" :max="capabilities?.barcodeModuleWidth?.maximum ?? undefined" :value="element.NarrowModuleWidth ?? ''" :disabled="barcodeModuleDisabled('NarrowModuleWidth')" @input="setBarcodeModuleWidth(element, 'NarrowModuleWidth', $event)" /></label>
            <button v-if="element.NarrowModuleWidth !== undefined && capabilitiesReady && !barcodeModuleWidthSupported(capabilities, element.NarrowModuleWidth)" class="field-clear-action" type="button" @click="clearBarcodeModuleWidth(element, 'NarrowModuleWidth')">清除不支持的窄条宽度</button>
            <label class="field-label">宽条模块 <span v-if="capabilities?.barcodeModuleWidth">{{ capabilities.barcodeModuleWidth.minimum }}–{{ capabilities.barcodeModuleWidth.maximum ?? '以上' }}</span><input class="field-input" type="number" :min="capabilities?.barcodeModuleWidth?.minimum ?? 1" :max="capabilities?.barcodeModuleWidth?.maximum ?? undefined" :value="element.WideModuleWidth ?? ''" :disabled="barcodeModuleDisabled('WideModuleWidth')" @input="setBarcodeModuleWidth(element, 'WideModuleWidth', $event)" /></label>
            <button v-if="element.WideModuleWidth !== undefined && capabilitiesReady && !wideModuleWidthSupported(capabilities, element.WideModuleWidth)" class="field-clear-action" type="button" @click="clearBarcodeModuleWidth(element, 'WideModuleWidth')">清除不支持的宽条宽度</button>
          </div>
          <label class="field-label">可读文字预留高度 <span>{{ unit }}</span><input class="field-input" type="number" min="0" :value="dimensionValue(element.HumanReadableReserveHeight)" @input="setBarcodeReserveHeight(element, $event)" /></label>
          <p v-if="!tsplVariableBarcodeRatioEditor(element as BarcodeElement) && !barcodeRatioIsValid(element.Ratio)" class="field-note">当前模板参数无效，请改选支持的宽窄比。</p>
          <p v-if="tsplVariableBarcodeRatioEditor(element as BarcodeElement) && !tsplBarcodeRatioIsValid(element as BarcodeElement)" class="field-note capability-warning">当前 TSPL 窄宽比或模块宽度不在设备能力范围内，请重新选择比例。</p>
        </section>
        <section v-if="element.Type === 3 || element.Type === 4 || element.Type === 11" class="inspector-section">
          <div class="section-title"><span>静区</span><span class="section-kicker">打印点</span></div>
          <label class="field-label">配置方式<select class="field-input"
            aria-label="静区配置方式"
            :value="quietZoneMode(element as QrElement | BarcodeElement)"
            :disabled="quietZoneEditorDisabled()"
            @change="setQuietZoneMode(element as QrElement | BarcodeElement, $event)">
            <option value="default">协议默认</option>
            <option value="explicit">显式四边</option>
          </select></label>
          <div v-if="quietZoneMode(element as QrElement | BarcodeElement) === 'explicit'" class="numeric-grid">
            <label class="field-label">左 <span>点</span><input class="field-input" aria-label="静区左侧点数" type="number" min="0" max="2147483647" step="1"
              :value="quietZoneSideValue(element as QrElement | BarcodeElement, 'Left')"
              :disabled="quietZoneEditorDisabled()"
              @input="setQuietZoneSide(element as QrElement | BarcodeElement, 'Left', $event)" /></label>
            <label class="field-label">上 <span>点</span><input class="field-input" aria-label="静区上侧点数" type="number" min="0" max="2147483647" step="1"
              :value="quietZoneSideValue(element as QrElement | BarcodeElement, 'Top')"
              :disabled="quietZoneEditorDisabled()"
              @input="setQuietZoneSide(element as QrElement | BarcodeElement, 'Top', $event)" /></label>
            <label class="field-label">右 <span>点</span><input class="field-input" aria-label="静区右侧点数" type="number" min="0" max="2147483647" step="1"
              :value="quietZoneSideValue(element as QrElement | BarcodeElement, 'Right')"
              :disabled="quietZoneEditorDisabled()"
              @input="setQuietZoneSide(element as QrElement | BarcodeElement, 'Right', $event)" /></label>
            <label class="field-label">下 <span>点</span><input class="field-input" aria-label="静区下侧点数" type="number" min="0" max="2147483647" step="1"
              :value="quietZoneSideValue(element as QrElement | BarcodeElement, 'Bottom')"
              :disabled="quietZoneEditorDisabled()"
              @input="setQuietZoneSide(element as QrElement | BarcodeElement, 'Bottom', $event)" /></label>
          </div>
          <button v-if="quietZoneMode(element as QrElement | BarcodeElement) === 'explicit'" class="field-clear-action" type="button"
            @click="clearQuietZone(element as QrElement | BarcodeElement)">恢复协议默认静区</button>
          <p v-if="quietZoneUnsupported(element as QrElement | BarcodeElement)" class="field-note capability-warning">当前输出能力不支持显式静区；已导入值会保留，清除后恢复协议默认。</p>
          <p v-else-if="!capabilitiesReady" class="field-note capability-warning">{{ capabilityNotice }}</p>
        </section>
        <section v-if="element.Type === 5" class="inspector-section">
          <div class="section-title"><span>线段参数</span></div>
          <label class="field-label">线宽<input class="field-input" type="number" min="1" max="32" :value="element.LineWidth ?? 1" @input="setNumber(element as unknown as Record<string, unknown>, 'LineWidth', $event)" /></label>
        </section>
        <section v-if="element.Type === 6" class="inspector-section">
          <div class="section-title"><span>组合</span></div>
          <label v-if="element.ComponentId" class="field-label">组件引用<input class="field-input" :value="element.ComponentId" readonly /></label>
          <label class="field-label">分页<select class="field-input" :value="defaultGroupPagination(template, element)" @change="element.Pagination = ($event.target as HTMLSelectElement).value as 'KeepTogether' | 'Split'; template.Version = 3"><option value="KeepTogether">整体同页</option><option value="Split">允许跨页</option></select></label>
          <label class="field-label">数据上下文<input class="field-input" :value="element.DataPath ?? ''" list="component-field-paths" placeholder="例如 Product" @input="element.DataPath = ($event.target as HTMLInputElement).value || undefined; template.Version = 3" /></label>
          <div v-for="(binding, name) in element.Parameters ?? {}" :key="name" class="variable-row">
            <span>{{ name }}</span>
            <select class="field-input" :value="binding.Path !== undefined ? 'path' : 'value'" @change="setParameterMode(binding, ($event.target as HTMLSelectElement).value as 'path' | 'value')"><option value="path">字段</option><option value="value">静态</option></select>
            <input v-if="binding.Path !== undefined" class="field-input" :aria-label="`${name} 字段路径`" :value="binding.Path" list="component-field-paths" @input="binding.Path = ($event.target as HTMLInputElement).value" />
            <input v-else class="field-input" :aria-label="`${name} 静态 JSON`" :value="JSON.stringify(binding.Value)" @change="setParameterJson(binding, 'Value', $event)" />
            <button type="button" :title="`删除参数 ${name}`" @click="delete element.Parameters![name]">×</button>
          </div>
          <label class="field-label">新参数<input v-model="parameterName" class="field-input" placeholder="参数名" @keydown.enter="addParameter" /></label>
          <button class="wide-action" type="button" @click="addParameter">添加参数</button>
          <p v-if="parameterError" class="field-note">{{ parameterError }}</p>
          <datalist id="component-field-paths"><option v-for="path in fieldPaths" :key="path" :value="path" /></datalist>
        </section>
        <section v-if="element.Type === 7 || element.Type === 8" class="inspector-section">
          <div class="section-title"><span>图形描边</span></div>
          <label v-if="element.Type === 7" class="toggle-row"><span>填充矩形</span><input type="checkbox" aria-label="填充矩形" :checked="element.Filled === true" :disabled="element.Filled !== true && (!capabilitiesReady || !filledRectangleSupported(capabilities))" @change="setRectangleFilled(element as Extract<TemplateElement, { Type: 7 }>, $event)" /></label>
          <button v-if="element.Type === 7 && filledRectangleUnsupported(element as Extract<TemplateElement, { Type: 7 }>)" class="field-clear-action" type="button" @click="clearUnsupportedFilled(element as Extract<TemplateElement, { Type: 7 }>)">清除不支持的填充</button>
          <p v-if="element.Type === 7 && filledRectangleUnsupported(element as Extract<TemplateElement, { Type: 7 }>)" class="field-note capability-warning">当前协议不支持填充矩形；保留现有值，取消勾选可清除。</p>
          <label class="field-label">线宽<input class="field-input" type="number" min="1" max="16" :value="element.LineWidth ?? 1" @input="setNumber(element as unknown as Record<string, unknown>, 'LineWidth', $event)" /></label>
        </section>
        <section v-if="element.Type === 9" class="inspector-section">
          <div class="section-title"><span>图标</span></div>
          <label class="field-label">图形<select class="field-input" :value="element.Icon" @change="element.Icon = ($event.target as HTMLSelectElement).value as 'check' | 'cross' | 'info'"><option value="check" :disabled="Boolean(capabilities?.labelProfile && !capabilities.elementTypes.includes(2))">勾选</option><option value="cross" :disabled="Boolean(capabilities?.labelProfile && !capabilities.elementTypes.includes(2))">关闭</option><option value="info">信息</option></select></label>
          <label class="field-label">线宽<input class="field-input" type="number" min="1" max="16" :value="element.LineWidth ?? 2" @input="setNumber(element as unknown as Record<string, unknown>, 'LineWidth', $event)" /></label>
        </section>
        <section v-if="element.Type === 10" class="inspector-section">
          <div class="section-title"><span>二维点阵码</span></div>
          <label class="field-label">制式<select class="field-input" :value="element.Format" @change="setMatrixFormat(element, $event)"><option value="DataMatrix">DataMatrix</option><option value="PDF417">PDF417</option></select></label>
          <label class="raster-line-option" title="仅 DataMatrix 支持 GS1；PDF417 禁用"><input type="checkbox" aria-label="GS1 DataMatrix" :checked="element.Gs1 === true" :disabled="gs1TwoDimensionalDisabled(element)" @change="setGs1(element, $event)" /><span>GS1 DataMatrix</span></label>
          <p v-if="element.Format === 'PDF417'" class="field-note">PDF417 不支持 GS1 模式。</p>
          <p v-else-if="gs1TwoDimensionalDisabled(element)" class="field-note capability-warning">当前 Host 或协议不支持 GS1 DataMatrix。</p>
        </section>

        <section v-if="element.Type === 11" class="inspector-section">
          <div class="section-title"><span>GS1 Composite</span></div>
          <label class="field-label">线性组件<select aria-label="Composite 线性组件" class="field-input" :value="element.LinearType ?? 25" @change="setCompositeLinearType(element, $event)"><option :value="25" :disabled="compositeLinearTypeDisabled(25)">Omnidirectional</option><option :value="27" :disabled="compositeLinearTypeDisabled(27)">Limited</option><option :value="GS1_128_LINEAR_TYPE" :disabled="compositeLinearTypeDisabled(GS1_128_LINEAR_TYPE)" :title="compositeLinearTypeDisabled(GS1_128_LINEAR_TYPE) ? '当前 Host 未协商 GS1-128 + CC-C Composite' : ''">GS1-128</option><option :value="DATABAR_EXPANDED_LINEAR_TYPE" :disabled="compositeLinearTypeDisabled(DATABAR_EXPANDED_LINEAR_TYPE)" :title="compositeLinearTypeDisabled(DATABAR_EXPANDED_LINEAR_TYPE) ? '当前 Host 未协商 GS1 DataBar Expanded Composite' : ''">Expanded</option><option :value="DATABAR_STACKED_LINEAR_TYPE" :disabled="compositeLinearTypeDisabled(DATABAR_STACKED_LINEAR_TYPE)" :title="compositeLinearTypeDisabled(DATABAR_STACKED_LINEAR_TYPE) ? '当前 Host 未协商 GS1 DataBar Stacked Composite' : ''">Stacked</option><option :value="DATABAR_EXPANDED_STACKED_LINEAR_TYPE" :disabled="compositeLinearTypeDisabled(DATABAR_EXPANDED_STACKED_LINEAR_TYPE)" :title="compositeLinearTypeDisabled(DATABAR_EXPANDED_STACKED_LINEAR_TYPE) ? '当前 Host 未协商 GS1 DataBar Expanded Stacked Composite' : ''">Expanded Stacked</option><option :value="EAN_8_LINEAR_TYPE" :disabled="compositeLinearTypeDisabled(EAN_8_LINEAR_TYPE)" :title="compositeLinearTypeDisabled(EAN_8_LINEAR_TYPE) ? '当前 Host 未协商 EAN/UPC Composite 功能' : ''">EAN-8</option><option :value="EAN_13_LINEAR_TYPE" :disabled="compositeLinearTypeDisabled(EAN_13_LINEAR_TYPE)" :title="compositeLinearTypeDisabled(EAN_13_LINEAR_TYPE) ? '当前 Host 未协商 EAN/UPC Composite 功能' : ''">EAN-13</option><option :value="UPC_A_LINEAR_TYPE" :disabled="compositeLinearTypeDisabled(UPC_A_LINEAR_TYPE)" :title="compositeLinearTypeDisabled(UPC_A_LINEAR_TYPE) ? '当前 Host 未协商 EAN/UPC Composite 功能' : ''">UPC-A</option><option :value="UPC_E_LINEAR_TYPE" :disabled="compositeLinearTypeDisabled(UPC_E_LINEAR_TYPE)" :title="compositeLinearTypeDisabled(UPC_E_LINEAR_TYPE) ? '当前 Host 未协商 EAN/UPC Composite 功能' : ''">UPC-E</option><option :value="DATABAR_TRUNCATED_LINEAR_TYPE" :disabled="compositeLinearTypeDisabled(DATABAR_TRUNCATED_LINEAR_TYPE)" :title="compositeLinearTypeDisabled(DATABAR_TRUNCATED_LINEAR_TYPE) ? '当前 Host 未协商 GS1 DataBar Truncated Composite 功能' : ''">Truncated</option><option :value="DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE" :disabled="compositeLinearTypeDisabled(DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE)" :title="compositeLinearTypeDisabled(DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE) ? '当前 Host 未协商 GS1 DataBar Stacked Omnidirectional Composite 功能' : ''">Stacked Omnidirectional</option></select></label>
          <label class="field-label">二维模式<select aria-label="Composite 二维模式" class="field-input" :value="element.Mode ?? 0" @change="setCompositeMode(element, $event)"><option v-for="mode in gs1CompositeModeOptions" :key="mode.value" :value="mode.value" :disabled="mode.disabled" :title="mode.reason">{{ mode.label }}</option></select></label>
          <label class="field-label">模块宽度（点）<input class="field-input" type="number" min="1" step="1" aria-label="Composite 模块宽度" :value="element.ModuleWidth ?? 2" @input="setCompositeSize(element, 'ModuleWidth', $event)" /></label>
          <label class="field-label">线性条高（点）<input class="field-input" type="number" :min="compositeLinearBarHeightMin(element)" step="1" aria-label="Composite 线性条高" :value="element.LinearBarHeight ?? 40" @input="setCompositeSize(element, 'LinearBarHeight', $event)" /></label>
          <template v-if="!element.SecondaryField.Gs1Input">
            <div class="section-title"><span>次数据绑定</span><span class="section-kicker">SecondaryField</span></div>
            <div class="segmented-control"><button type="button" :class="{ active: element.SecondaryField.Name !== undefined }" @click="setSecondaryMode(element, true)">字段</button><button type="button" :class="{ active: element.SecondaryField.Name === undefined }" @click="setSecondaryMode(element, false)">固定文字</button></div>
            <label v-if="element.SecondaryField.Name !== undefined" class="field-label">次字段路径<input class="field-input" aria-label="Composite 次字段路径" list="business-field-paths" :value="element.SecondaryField.Name" @input="element.SecondaryField.Name = ($event.target as HTMLInputElement).value" /></label>
            <label v-else class="field-label">括号式 GS1 AI<textarea class="field-input field-textarea" aria-label="Composite 次数据" :value="element.SecondaryField.Value ?? ''" @input="element.SecondaryField.Value = ($event.target as HTMLTextAreaElement).value" /></label>
            <FieldFormatEditor :format="element.SecondaryField.Format" title="次字段格式" @update:format="updateSecondaryFieldFormat" />
          </template>
          <Gs1AiEditor :template="template" :data="data" :element="element"
            :model-value="element.SecondaryField.Gs1Input" :field-path="`${selectedPath}.SecondaryField.Gs1Input`"
            :field-paths="fieldPaths" :allowed-modes="[0]" :enabled="true"
            support-reason="Composite 次数据仅支持 GS1 Elements。" @update:model-value="setSecondaryGs1Input" />
          <p class="field-note">{{ element.LinearType === GS1_128_LINEAR_TYPE ? '上方内容绑定为括号式 GS1 AI 主数据，例如 (01)12345678901231，规范化后最多 48 字符。' : isGs1CompositeRetailLinearType(element.LinearType) ? 'EAN/UPC 主码只接受 ASCII 数字，可选用单个 + 添加 2 或 5 位 ASCII 数字，例如 12345670+12、1234567890128+51234；附加码不补零、不修剪。UPC-E 支持 6、7 或 8 位，6 位输入默认号码系统 0。' : element.LinearType === DATABAR_EXPANDED_LINEAR_TYPE || element.LinearType === DATABAR_EXPANDED_STACKED_LINEAR_TYPE ? 'Expanded 主数据使用括号式 GS1 AI，例如 (01)09506000134352(10)LOT-A；上方默认值可按业务数据修改。' : element.LinearType === DATABAR_TRUNCATED_LINEAR_TYPE || element.LinearType === DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE ? '主数据与 Omnidirectional 相同：13 位 payload，或有效 14 位 GTIN/括号式 (01) AI。' : '上方内容绑定为主 GTIN。' }}次数据使用括号式 GS1 AI。完整符号与静区一起旋转，尺寸以 Host 编码为准；可读文字请添加独立文字元素。</p>
          <p v-if="isGs1CompositeRetailLinearType(element.LinearType) && !supportsGs1CompositeEanUpc(capabilities)" class="field-note capability-warning">当前 Host 未协商 EAN/UPC Composite；请更新 Host 后再预览或输出。</p>
          <p v-if="isGs1CompositeRetailLinearType(element.LinearType) && element.Field !== undefined && element.Field.Name === undefined && gs1CompositeRetailHasAddon(element.Field.Value) && !supportsGs1CompositeEanUpcAddons(capabilities)" class="field-note capability-warning">当前 Host 未协商 EAN/UPC Composite 附加码；原输入会保留，但带附加码的预览与输出不可用。</p>
          <p v-if="element.LinearType === GS1_128_LINEAR_TYPE && (element.Mode === 1 || element.Mode === 2) && !supportsGs1CompositeGs1128CcAb(capabilities)" class="field-note capability-warning">当前 Host 未协商 GS1-128 + CC-A/CC-B Composite；导入的模式值会保留，但预览与输出不可用。</p>
          <p v-if="element.LinearType === GS1_128_LINEAR_TYPE && element.Mode !== 1 && element.Mode !== 2 && !supportsGs1CompositeGs1128CcAb(capabilities)" class="field-note capability-warning">当前 Host 未协商 GS1-128 + CC-A/CC-B Composite；CC-A 与 CC-B 选项已禁用。</p>
          <p v-if="element.LinearType === GS1_128_LINEAR_TYPE && (element.Mode === undefined || element.Mode === 0 || element.Mode === GS1_COMPOSITE_CC_C_MODE) && !supportsGs1CompositeCcC(capabilities)" class="field-note capability-warning">当前 Host 未协商 GS1-128 + CC-C；此组合的预览与输出不可用。</p>
          <p v-if="element.LinearType === DATABAR_EXPANDED_LINEAR_TYPE && !supportsGs1CompositeExpanded(capabilities)" class="field-note capability-warning">当前 Host 未协商 GS1 DataBar Expanded Composite；请更新 Host 后再预览或输出。</p>
          <p v-if="element.LinearType === DATABAR_STACKED_LINEAR_TYPE" class="field-note">Stacked 线性条高表示两行数据条总高，不含中间 1 模块分隔行；两行按 5:7 比例取整分配，总高至少为模块宽度的 12 倍。</p>
          <p v-if="element.LinearType === DATABAR_STACKED_LINEAR_TYPE && !supportsGs1CompositeStacked(capabilities)" class="field-note capability-warning">当前 Host 未协商 GS1 DataBar Stacked Composite；请更新 Host 后再预览或输出。</p>
          <p v-if="element.LinearType === DATABAR_EXPANDED_STACKED_LINEAR_TYPE" class="field-note">Expanded Stacked 线性条高表示每条数据行高度（打印点），默认每行 2 个字符对，可配置为 2–11 对、最多 6 行；相邻数据行之间增加三个 1X 分隔行。每行至少为模块宽度，34X 为推荐扫码高度。</p>
          <p v-if="element.LinearType === DATABAR_EXPANDED_STACKED_LINEAR_TYPE && !supportsGs1CompositeExpandedStacked(capabilities)" class="field-note capability-warning">当前 Host 未协商 GS1 DataBar Expanded Stacked Composite；请更新 Host 后再预览或输出。</p>
          <label v-if="element.LinearType === DATABAR_EXPANDED_STACKED_LINEAR_TYPE" class="field-label">Expanded Stacked 每行列数<select class="field-input" aria-label="Composite Expanded Stacked 每行列数" :value="element.ExpandedStackedColumns ?? 'default'" :disabled="!capabilitiesReady" @change="setExpandedStackedColumns(element, 'composite', $event)"><option value="default">默认（2 列）</option><option v-for="columns in compositeExpandedStackedColumnOptions" :key="columns" :value="columns" :disabled="expandedStackedColumnsDisabled('composite', columns)">{{ columns }} 列</option></select></label>
          <p v-if="element.LinearType === DATABAR_EXPANDED_STACKED_LINEAR_TYPE && element.ExpandedStackedColumns != null && expandedStackedColumnsWarning('composite', element.ExpandedStackedColumns)" class="field-note capability-warning">{{ expandedStackedColumnsWarning('composite', element.ExpandedStackedColumns) }}</p>
          <label v-if="element.LinearType === DATABAR_EXPANDED_STACKED_LINEAR_TYPE" class="field-label">Expanded Stacked 最大数据行数<select class="field-input" aria-label="Composite Expanded Stacked 最大数据行数" :value="element.ExpandedStackedRows ?? 'default'" :disabled="!capabilitiesReady" @change="setExpandedStackedRows(element, 'composite', $event)"><option value="default">不限制</option><option v-for="rows in expandedStackedRowOptions" :key="rows" :value="rows" :disabled="expandedStackedRowsDisabled('composite', rows)">{{ rows }} 行</option></select></label>
          <p v-if="element.LinearType === DATABAR_EXPANDED_STACKED_LINEAR_TYPE && element.ExpandedStackedRows != null && expandedStackedRowsWarning('composite', element.ExpandedStackedRows)" class="field-note capability-warning">{{ expandedStackedRowsWarning('composite', element.ExpandedStackedRows) }}</p>
          <p v-if="element.LinearType === DATABAR_EXPANDED_STACKED_LINEAR_TYPE && element.ExpandedStackedColumns != null && element.ExpandedStackedRows != null" class="field-note capability-warning">列数与最大行数互斥，请只保留一种布局配置。</p>
          <p v-if="element.LinearType === DATABAR_TRUNCATED_LINEAR_TYPE" class="field-note">Truncated 条高至少为模块宽度的 13 倍；固定宽度为 100X，总高度为 H+(2R+1)X。</p>
          <p v-if="element.LinearType === DATABAR_TRUNCATED_LINEAR_TYPE && !supportsGs1CompositeTruncated(capabilities)" class="field-note capability-warning">当前 Host 未协商 GS1 DataBar Truncated Composite；请更新 Host 后再预览或输出。</p>
          <p v-if="element.LinearType === DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE" class="field-note">Stacked Omnidirectional 每条数据行高度至少为 33X；两行之间三个 1X 分隔，Composite 外再加一条 1X 分隔。宽度为 56X，总高度为 2H+(2R+4)X。</p>
          <p v-if="element.LinearType === DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE && !supportsGs1CompositeStackedOmnidirectional(capabilities)" class="field-note capability-warning">当前 Host 未协商 GS1 DataBar Stacked Omnidirectional Composite；请更新 Host 后再预览或输出。</p>
          <p v-if="isGs1CompositeRetailLinearType(element.LinearType)" class="field-note">EAN/UPC Composite 线性条高表示数据条高度，不含分隔；线性条与二维组件之间固定增加三条 2X 分隔行。</p>
          <p v-if="!capabilitiesReady || !elementTypeSupported(capabilities, 11)" class="field-note capability-warning">当前 Host 尚未确认 Composite 能力，请更新或连接 Host 后验证。</p>
        </section>

        <section class="inspector-section condition-section">
          <div class="section-title"><span>条件显示</span><label class="small-toggle"><input type="checkbox" :checked="conditionEnabled(conditionTarget)" @change="enableCondition(conditionTarget, ($event.target as HTMLInputElement).checked)" /></label></div>
          <ConditionEditor v-if="conditionEnabled(conditionTarget)" :model-value="conditionValue" @update:model-value="updateRowCondition" />
          <p v-else class="field-note">此元素始终参与渲染</p>
        </section>
      </template>

      <template v-else-if="row">
        <section class="inspector-section">
          <div class="section-title"><span>行布局</span></div>
          <label class="field-label">行高 <span>{{ unit }}</span><input class="field-input" type="number" min="0" :value="dimensionValue(row.Height)" @input="setDimension(row as unknown as Record<string, unknown>, 'Height', $event)" /></label>
          <label class="field-label">重复数据源<input class="field-input" :value="row.RepeatSource ?? ''" placeholder="例如 Items" @input="row.RepeatSource = ($event.target as HTMLInputElement).value || undefined" /></label>
          <label v-if="template.Version >= 2 && isTopLevelRow" class="field-label">页面分区<select class="field-input" :value="row.Section ?? 'Body'" @change="row.Section = ($event.target as HTMLSelectElement).value as Row['Section']"><option value="Body">动态正文</option><option value="DocumentHeader">文档顶部</option><option value="PageHeader">每页顶部</option><option value="DocumentFooter">文档底部</option><option value="PageFooter">每页底部</option><option value="TableHeader">明细表头</option></select></label>
          <label v-else class="toggle-row"><span>每页重复表头</span><input type="checkbox" :checked="row.IsHeader" @change="row.IsHeader = ($event.target as HTMLInputElement).checked" /></label>
        </section>
        <section class="inspector-section condition-section">
          <div class="section-title"><span>行显示条件</span><label class="small-toggle"><input type="checkbox" :checked="conditionEnabled(row)" @change="enableCondition(row, ($event.target as HTMLInputElement).checked)" /></label></div>
          <ConditionEditor v-if="conditionEnabled(row)" :model-value="row.Condition" @update:model-value="updateRowCondition" />
        </section>
        <section class="inspector-section"><div class="section-title"><span>边框</span></div>
          <div class="numeric-grid">
            <label class="field-label">上<select class="field-input" :value="row.Border?.TopStyle ?? 0" @change="updateBorder('TopStyle', $event)"><option :value="0">无</option><option :value="1" :disabled="borderStyleDisabled(1)">实线</option><option :value="2" :disabled="borderStyleDisabled(2)">虚线</option></select></label>
            <label class="field-label">右<select class="field-input" :value="row.Border?.RightStyle ?? 0" @change="updateBorder('RightStyle', $event)"><option :value="0">无</option><option :value="1" :disabled="borderStyleDisabled(1)">实线</option><option :value="2" :disabled="borderStyleDisabled(2)">虚线</option></select></label>
            <label class="field-label">下<select class="field-input" :value="row.Border?.BottomStyle ?? 0" @change="updateBorder('BottomStyle', $event)"><option :value="0">无</option><option :value="1" :disabled="borderStyleDisabled(1)">实线</option><option :value="2" :disabled="borderStyleDisabled(2)">虚线</option></select></label>
            <label class="field-label">左<select class="field-input" :value="row.Border?.LeftStyle ?? 0" @change="updateBorder('LeftStyle', $event)"><option :value="0">无</option><option :value="1" :disabled="borderStyleDisabled(1)">实线</option><option :value="2" :disabled="borderStyleDisabled(2)">虚线</option></select></label>
          </div>
          <p v-if="capabilitiesReady && !capabilities?.elementTypes.includes(5) && hasBorderStyle(row.Border)" class="field-note capability-warning">当前协议不支持行边框；保留现有值，选择“无”可清除。</p>
        </section>
      </template>

      <template v-else-if="cell">
        <section class="inspector-section">
          <div class="section-title"><span>列布局</span></div>
          <label class="field-label">列宽 <span>{{ unit }}</span><input class="field-input" type="number" min="0" :value="dimensionValue(cell.Width)" @input="setDimension(cell as unknown as Record<string, unknown>, 'Width', $event)" /></label>
          <p class="field-note">列宽为 0 时由父级自动分配剩余宽度。</p>
        </section>
        <section class="inspector-section"><div class="section-title"><span>边框</span></div>
          <div class="numeric-grid">
            <label class="field-label">上<select class="field-input" :value="cell.Border?.TopStyle ?? 0" @change="updateBorder('TopStyle', $event)"><option :value="0">无</option><option :value="1" :disabled="borderStyleDisabled(1)">实线</option><option :value="2" :disabled="borderStyleDisabled(2)">虚线</option></select></label>
            <label class="field-label">右<select class="field-input" :value="cell.Border?.RightStyle ?? 0" @change="updateBorder('RightStyle', $event)"><option :value="0">无</option><option :value="1" :disabled="borderStyleDisabled(1)">实线</option><option :value="2" :disabled="borderStyleDisabled(2)">虚线</option></select></label>
            <label class="field-label">下<select class="field-input" :value="cell.Border?.BottomStyle ?? 0" @change="updateBorder('BottomStyle', $event)"><option :value="0">无</option><option :value="1" :disabled="borderStyleDisabled(1)">实线</option><option :value="2" :disabled="borderStyleDisabled(2)">虚线</option></select></label>
            <label class="field-label">左<select class="field-input" :value="cell.Border?.LeftStyle ?? 0" @change="updateBorder('LeftStyle', $event)"><option :value="0">无</option><option :value="1" :disabled="borderStyleDisabled(1)">实线</option><option :value="2" :disabled="borderStyleDisabled(2)">虚线</option></select></label>
          </div>
          <p v-if="capabilitiesReady && !capabilities?.elementTypes.includes(5) && hasBorderStyle(cell.Border)" class="field-note capability-warning">当前协议不支持列边框；保留现有值，选择“无”可清除。</p>
        </section>
        <section class="inspector-section structure-actions">
          <div class="section-title"><span>嵌套布局</span></div>
          <button class="wide-action" type="button" @click="emit('add-nested-row')">在此列中添加嵌套行</button>
        </section>
      </template>
      <div v-else class="empty-hint inspector-empty">从结构树或画布中选择一项</div>
      </fieldset>
    </div>
  </aside>
</template>
