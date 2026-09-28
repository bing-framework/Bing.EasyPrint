<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import {
  AlertTriangle, ArrowDownToLine, Barcode, Check, ChevronDown, ChevronRight, CircleHelp, CodeXml,
  Download, FilePlus2, FileText, ImagePlus, Layers3, Minus, PanelLeft, PanelRight, Play, Printer,
  QrCode, Redo2, RotateCcw, Ruler, Search, Type, Upload, X, Undo2, ZoomIn, ZoomOut,
  Circle, Hand, MousePointer2, Shapes, Square, Table2, Layers, Ungroup, Unlink, Pencil, Trash2, Plus, ArrowUp, ArrowDown,
} from '@lucide/vue'
import CanvasWorkspace from './components/CanvasWorkspace.vue'
import PropertyInspector from './components/PropertyInspector.vue'
import TemplateTree from './components/TemplateTree.vue'
import { DesignerValidationError, loadDesignerSamples, previewTemplate, renderTemplate, type DesignerSample } from './api'
import { exportTemplateBundle, importTemplateBundle, MAX_TEMPLATE_JSON_BYTES, resourceForImage } from './archive'
import { buildLocalLayout } from './layout'
import { bindImageResource, collectArrayPaths, collectBindingPaths, collectRootFieldPaths, resolvePath, setPathValue } from './binding'
import { assertTemplate, cloneTemplate, type BpTemplate, type PreviewElement, type PreviewPage, type TemplateElement, type TemplateResource } from './model'
import { makeSample, sampleData, type SampleKey } from './samples'
import { findCell, findElement, findRow, listTemplateTree } from './tree'
import { SnapshotHistory } from './history'
import { dotsToMm, getNudgeDelta, materializeElementPosition, translateElement } from './coordinates'
import { addNestedRow as appendNestedRow } from './structure'
import { appendComponent, appendDataTable, appendStaticTable, detachComponent, enableV2, groupCellElements, removeComponent, renameComponent, saveGroupComponent, ungroupElement, type ElementBounds } from './componentsModel'
import { parseTextTable } from './textTable'

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
const sampleId = ref<string>('blank')
const remoteSamples = ref<DesignerSample[]>([])
const serviceSampleError = ref('')
const unit = ref<'pt' | 'mm'>('pt')
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
const previewErrorPath = ref('')
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
const dataTextarea = ref<HTMLTextAreaElement>()
const treeCollapsed = ref(false)
const searchText = ref('')

let history = new SnapshotHistory(template.value)
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
const activeRepeatSources = computed(() => {
  const path = selectedPath.value
  const sources: string[] = []
  for (const match of path.matchAll(/(?:^|\.)Rows\[\d+\]/g)) {
    const row = findRow(template.value, path.slice(0, match.index + match[0].length))
    if (row?.RepeatSource) sources.push(row.RepeatSource)
  }
  return sources
})
const bindingFieldPaths = computed(() => [...new Set([
  ...collectBindingPaths(parsedData.value.value, activeRepeatSources.value, template.value.Variables),
  ...Object.keys(template.value.Variables ?? {}).map((name) => `$var.${name}`),
])])
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
const selectedRow = computed(() => findRow(template.value, selectedPath.value))
const selectedCell = computed(() => findCell(template.value, selectedPath.value))
const currentPage = computed(() => previewPages.value[previewIndex.value])
const sampleOptions = computed(() => [
  ...Object.entries(localSampleLabels).map(([id, name]) => ({ id, name, source: 'local' as const })),
  ...remoteSamples.value.map((sample) => ({ id: `service:${sample.id}`, name: `${sample.name} · 服务端`, source: 'service' as const })),
])

watch(template, () => {
  if (historyApplying) return
  if (historyTimer) clearTimeout(historyTimer)
  historyTimer = setTimeout(() => { history.record(template.value); syncHistoryState() }, 250)
  schedulePreview()
}, { deep: true, flush: 'sync' })

watch(dataText, () => schedulePreview(), { flush: 'post' })

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
})

function showToast(message: string, tone: 'success' | 'warning' | 'error' = 'success') {
  toast.value = message
  toastTone.value = tone
  if (toastTimer) clearTimeout(toastTimer)
  toastTimer = setTimeout(() => { toast.value = '' }, 3600)
}

function replaceTemplate(next: BpTemplate, path = '') {
  assertTemplate(next)
  historyApplying = true
  template.value = cloneTemplate(next)
  history = new SnapshotHistory(template.value)
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

function locatePreviewError() {
  if (!previewErrorPath.value) return
  const path = previewErrorPath.value
  const index = previewPages.value.findIndex((page) => page.elements.some((element) => element.sourcePath === path))
  if (index >= 0) previewIndex.value = index
  selectElement(path)
  mobileLayersOpen.value = true
}

function exportComponents() {
  if (!template.value.Components?.length) return
  downloadBlob(new Blob([JSON.stringify({ Version: 1, Components: template.value.Components }, null, 2)],
    { type: 'application/json;charset=utf-8' }), 'easyprint-components.json')
}

async function importComponents(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  try {
    if (file.size > MAX_TEMPLATE_JSON_BYTES) throw new Error('组件库超过 1 MiB 上限。')
    const contents = JSON.parse(await file.text()) as { Version?: number; Components?: BpTemplate['Components'] }
    if (contents.Version !== 1 || !Array.isArray(contents.Components) || !contents.Components.length)
      throw new Error('组件库格式无效。')
    const next = cloneTemplate(template.value)
    next.Version = 2
    next.Components = [...(next.Components ?? []), ...contents.Components]
    assertTemplate(next)
    template.value = next
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

function schedulePreview() {
  if (previewTimer) clearTimeout(previewTimer)
  previewTimer = setTimeout(() => { void refreshPreview() }, 400)
}

async function refreshPreview() {
  if (parsedData.value.error) {
    previewError.value = `业务数据 JSON 无效：${parsedData.value.error}`
    previewErrorPath.value = ''
    previewPages.value = []
    statusMessage.value = '业务数据有误'
    statusTone.value = 'error'
    return
  }
  const request = ++previewRequest
  previewAbort?.abort()
  previewAbort = new AbortController()
  previewBusy.value = true
  try {
    const result = await previewTemplate(template.value, parsedData.value.value, resources.value, previewAbort.signal)
    if (request !== previewRequest) return
    if (result.errors?.length) {
      previewError.value = result.errors.map((item) => `${item.sourcePath ? `${item.sourcePath}: ` : ''}${item.message}`).join('\n')
      previewErrorPath.value = result.errors.find((item) => item.sourcePath)?.sourcePath ?? ''
      previewPages.value = []
      previewSource.value = 'service'
      statusMessage.value = '模板校验失败'
      statusTone.value = 'error'
      return
    }
    previewError.value = ''
    previewErrorPath.value = ''
    previewPages.value = result.pages
    previewIndex.value = Math.min(previewIndex.value, Math.max(0, result.pages.length - 1))
    previewSource.value = 'service'
    statusMessage.value = `引擎预览 · ${result.pages.length} 页`
    statusTone.value = 'success'
  } catch (error) {
    if (request !== previewRequest || (error instanceof DOMException && error.name === 'AbortError')) return
    if (error instanceof DesignerValidationError) {
      previewError.value = error.message
      previewErrorPath.value = /^((?:Rows\[\d+\])(?:\.(?:Cells|Rows|Elements)\[\d+\])*)/.exec(error.message)?.[1] ?? ''
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
    previewError.value = [...local.errors, ...missingImages].map((item) => `${item.sourcePath}: ${item.message}`).join('\n')
    previewErrorPath.value = local.errors[0]?.sourcePath ?? missingImages[0]?.sourcePath ?? ''
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
const historyState = ref(0)
const canUndo = computed(() => { historyState.value; return history.canUndo })
const canRedo = computed(() => { historyState.value; return history.canRedo })

function undo() {
  if (historyTimer) clearTimeout(historyTimer)
  history.record(template.value)
  const previous = history.undo()
  if (!previous) return
  historyApplying = true
  template.value = previous
  syncHistoryState()
  nextTick(() => { historyApplying = false })
  showToast('已撤销')
  schedulePreview()
}

function redo() {
  const next = history.redo()
  if (!next) return
  historyApplying = true
  template.value = next
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
  if (type === 7) return { Type: 7, Width: 120, Height: 64, LineWidth: 1, StartCoordinate: start }
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

function addElement(type: TemplateElement['Type']) {
  if (type > 5) enableV2(template.value)
  const cell = activeCell()
  cell.Elements ??= []
  const group = selectedElement.value?.Type === 6 ? selectedElement.value : undefined
  const groupElements = group?.ComponentId
    ? template.value.Components?.find((item) => item.Id === group.ComponentId)?.Elements : group?.Elements
  const target = groupElements ?? cell.Elements
  const element = defaultElement(type)
  element.StartCoordinate = { X: group ? 8 : 24, Y: (group ? 8 : 36) + target.length * 16 }
  if (element.Type === 5) element.LineEndCoordinate = { X: 224, Y: element.StartCoordinate.Y }
  else if (type <= 5) element.EndCoordinate = { X: element.StartCoordinate.X + (element.Type === 1 ? 220 : element.Type === 4 ? 220 : 100), Y: element.StartCoordinate.Y + (element.Type === 1 ? 36 : element.Type === 4 ? 46 : 100) }
  if (group && element.Type === 1) element.EndCoordinate = { X: Math.max(element.StartCoordinate.X + 16, (group.Width || 120) - 8), Y: element.StartCoordinate.Y + 36 }
  target.push(element)
  const parentPath = groupElements ? selectedPath.value : activeCellPath()
  selectedPath.value = `${parentPath}.Elements[${target.length - 1}]`
  showToast(`已添加${({ 1: '文字', 2: '图片', 3: '二维码', 4: '条码', 5: '线段', 6: '组合', 7: '矩形', 8: '椭圆', 9: '图标', 10: '二维点阵码' } as Record<number, string>)[type]}`)
}

function addMultilineText() {
  addElement(1)
  const element = findElement(template.value, selectedPath.value)
  if (element?.Type === 1) {
    element.Field ??= {}
    element.Field.NeedFeedLine = true
  }
}

function dragTool(event: DragEvent, type: TemplateElement['Type'], multiline = false) {
  event.dataTransfer?.setData('application/x-easyprint-element', multiline ? `${type}:multiline` : String(type))
  if (event.dataTransfer) event.dataTransfer.effectAllowed = 'copy'
}

function addElementAt(value: { type: TemplateElement['Type']; x: number; y: number; cellPath?: string; multiline?: boolean }) {
  if (value.cellPath && findCell(template.value, value.cellPath)) selectedPath.value = value.cellPath
  addElement(value.type)
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

function selectElement(path: string, additive = false) {
  selectedPaths.value = additive ? selectedPaths.value.includes(path)
    ? selectedPaths.value.filter((item) => item !== path) : [...selectedPaths.value, path] : path ? [path] : []
  selectedPath.value = selectedPaths.value.at(-1) ?? ''
  mobileLayersOpen.value = false
}

function resizeElement(value: { path: string; width: number; height: number }) {
  const element = findElement(template.value, value.path)
  if (!element) return
  const start = element.StartCoordinate ?? { X: 0, Y: 0 }
  if (element.Type === 9) element.Size = Math.max(8, Math.min(value.width, value.height))
  else if ('Width' in element && 'Height' in element) {
    element.Width = Math.max(8, value.width)
    element.Height = Math.max(8, value.height)
    if (element.EndCoordinate) element.EndCoordinate = { X: start.X + element.Width, Y: start.Y + element.Height }
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
  if (!element.StartCoordinate) {
    const pages = [currentPage.value, ...previewPages.value].filter((page): page is PreviewPage => Boolean(page))
    const page = pages.find((candidate) => candidate.elements.some((part) => part === value.rendered))
      ?? pages.find((candidate) => candidate.elements.some((part) => part.sourcePath === value.path))
    const rendered = value.rendered ?? page?.elements.find((part) => part.sourcePath === value.path && part.kind === 'element')
    const parentPath = value.path.slice(0, value.path.lastIndexOf('.Elements['))
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
    const target = cell?.Elements ?? (group?.Type === 6 ? group.ComponentId
      ? template.value.Components?.find((item) => item.Id === group.ComponentId)?.Elements : group.Elements : undefined)
    target?.splice(Number(elementMatch[2]), 1)
    selectedPath.value = elementMatch[1]
    return
  }
  const cellMatch = /^(.*)\.Cells\[(\d+)\]$/.exec(selectedPath.value)
  if (cellMatch) {
    const row = findRow(template.value, cellMatch[1])
    row?.Cells?.splice(Number(cellMatch[2]), 1)
    selectedPath.value = cellMatch[1]
    return
  }
  const nestedRowMatch = /^(.*\.Cells\[\d+\])\.Rows\[(\d+)\]$/.exec(selectedPath.value)
  if (nestedRowMatch) {
    findCell(template.value, nestedRowMatch[1])?.Rows?.splice(Number(nestedRowMatch[2]), 1)
    selectedPath.value = nestedRowMatch[1]
    return
  }
  const rowMatch = /^Rows\[(\d+)\]$/.exec(selectedPath.value)
  if (rowMatch) {
    template.value.Rows.splice(Number(rowMatch[1]), 1)
    selectedPath.value = ''
  }
}

function openTemplatePicker() { jsonInput.value?.click() }
function openImagePicker() { imageInput.value?.click() }

async function importFile(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0]
  if (!file) return
  try {
    if (file.name.toLowerCase().endsWith('.zip')) {
      const imported = await importTemplateBundle(file)
      resources.value = imported.resources
      templateName.value = file.name.replace(/\.zip$/i, '')
      replaceTemplate(imported.template)
      showToast(`已导入模板包 · ${imported.resources.length} 个资源`)
    } else {
      if (file.size > MAX_TEMPLATE_JSON_BYTES) throw new Error('JSON 模板超过 1 MiB 上限。')
      const text = await file.text()
      const value: unknown = JSON.parse(text)
      assertTemplate(value)
      resources.value = []
      templateName.value = file.name.replace(/\.json$/i, '')
      replaceTemplate(value)
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
    const blob = await exportTemplateBundle(template.value, resources.value)
    downloadBlob(blob, 'easyprint-template-package.zip')
    showToast(`模板包已导出 · ${resources.value.length} 个资源`)
  } catch (error) { showToast(error instanceof Error ? error.message : '模板包导出失败。', 'error') }
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
      addElement(2)
      const added = findElement(template.value, selectedPath.value)
      if (added?.Type === 2) added.Field = bindImageResource(added.Field, key)
    }
    showToast(`图片资源已导入：${file.name}`)
  } catch (error) { showToast(error instanceof Error ? error.message : '图片导入失败。', 'error') }
  finally { input.value = '' }
}

async function exportCpcl() {
  if (parsedData.value.error) { showToast('请先修正业务数据 JSON。', 'error'); return }
  try {
    const blob = await renderTemplate(template.value, parsedData.value.value, resources.value)
    downloadBlob(blob, 'easyprint-cpcl-pages.zip')
    showToast('CPCL 页面包已导出，没有连接打印机')
  } catch (error) {
    showToast(error instanceof Error ? error.message : 'CPCL 导出失败。', 'error')
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
      <div class="file-state"><FileText :size="14" /><span>{{ templateName }}</span><span class="dirty-dot"></span></div>
      <div class="header-actions">
        <button class="icon-button header-quiet" type="button" title="撤销 Ctrl+Z" :disabled="!canUndo" @click="undo"><Undo2 :size="16" /></button>
        <button class="icon-button header-quiet" type="button" title="重做 Ctrl+Y" :disabled="!canRedo" @click="redo"><Redo2 :size="16" /></button>
        <span class="header-divider small"></span>
        <label class="unit-selector" title="坐标单位"><Ruler :size="14" /><select v-model="unit"><option value="pt">点</option><option value="mm">mm</option></select><ChevronDown :size="12" /></label>
        <button class="button-secondary" type="button" @click="openTemplatePicker"><Upload :size="15" /><span>导入</span></button>
        <button class="button-secondary" type="button" @click="exportJson"><Download :size="15" /><span>导出 JSON</span></button>
        <button v-if="resources.length" class="button-secondary has-resource" type="button" @click="exportBundle"><ArrowDownToLine :size="15" /><span>模板包</span></button>
        <button class="button-primary" type="button" :disabled="previewBusy" @click="exportCpcl"><Printer :size="15" /><span>生成 CPCL</span></button>
      </div>
      <button class="mobile-panel-button" type="button" title="打开结构树" @click="mobileLayersOpen = !mobileLayersOpen; mobileInspectorOpen = false"><PanelLeft :size="17" /></button>
      <button class="mobile-panel-button" type="button" title="打开属性" @click="mobileInspectorOpen = !mobileInspectorOpen; mobileLayersOpen = false"><PanelRight :size="17" /></button>
      <input ref="jsonInput" type="file" accept=".json,.zip,application/json,application/zip" hidden @change="importFile" />
      <input ref="imageInput" type="file" accept="image/*" hidden @change="importImage" />
      <input ref="componentInput" type="file" accept=".json,application/json" hidden @change="importComponents" />
    </header>

    <div class="workbench">
      <div class="layers-shell" :class="{ 'mobile-open': mobileLayersOpen }">
        <TemplateTree :template="template" :selected-path="selectedPath" :selected-paths="selectedPaths" :collapsed="treeCollapsed" :query="searchText" @select="selectElement" @toggle="treeCollapsed = !treeCollapsed" />
        <div class="tree-tools">
          <label class="search-control"><Search :size="14" /><input v-model="searchText" placeholder="搜索元素" /><span v-if="searchText">{{ filteredTreeCount }}</span></label>
          <div class="tools-heading"><span>添加元素</span><span>CPCL</span></div>
          <div class="element-tools">
            <button type="button" draggable="true" title="单行文字，可拖入画布 (T)" @click="addElement(1)" @dragstart="dragTool($event, 1)"><Type :size="15" /><span>单行文字</span><kbd>T</kbd></button>
            <button type="button" draggable="true" title="导入图片并添加元素 (I)；也可拖入画布后绑定资源" @click="openImagePicker" @dragstart="dragTool($event, 2)"><ImagePlus :size="15" /><span>图片</span><kbd>I</kbd></button>
            <button type="button" draggable="true" title="二维码，可拖入画布 (Q)" @click="addElement(3)" @dragstart="dragTool($event, 3)"><QrCode :size="15" /><span>二维码</span><kbd>Q</kbd></button>
            <button type="button" draggable="true" title="一维条码，可拖入画布 (B)" @click="addElement(4)" @dragstart="dragTool($event, 4)"><Barcode :size="15" /><span>一维条码</span><kbd>B</kbd></button>
            <button type="button" draggable="true" title="线段，可拖入画布 (L)" @click="addElement(5)" @dragstart="dragTool($event, 5)"><Minus :size="15" /><span>线段</span><kbd>L</kbd></button>
          </div>
          <button class="advanced-toggle" type="button" :aria-expanded="showAdvancedTools" @click="showAdvancedTools = !showAdvancedTools"><Shapes :size="14" />组件与表格<ChevronDown :size="13" /></button>
          <template v-if="showAdvancedTools">
            <div class="element-tools advanced-tools">
              <button type="button" draggable="true" title="多行文字，可拖入画布" @click="addMultilineText" @dragstart="dragTool($event, 1, true)"><Type :size="15" /><span>多行文字</span></button>
              <button type="button" draggable="true" title="矩形，可拖入画布" @click="addElement(7)" @dragstart="dragTool($event, 7)"><Square :size="15" /><span>矩形</span></button>
              <button type="button" draggable="true" title="椭圆，可拖入画布" @click="addElement(8)" @dragstart="dragTool($event, 8)"><Circle :size="15" /><span>椭圆</span></button>
              <button type="button" draggable="true" title="图标，可拖入画布" @click="addElement(9)" @dragstart="dragTool($event, 9)"><Shapes :size="15" /><span>图标</span></button>
              <button type="button" draggable="true" title="二维点阵码，可拖入画布" @click="addElement(10)" @dragstart="dragTool($event, 10)"><QrCode :size="15" /><span>PDF417 / DM</span></button>
              <button type="button" title="配置动态表格" @click="openTableWizard"><Table2 :size="15" /><span>动态表格</span></button>
              <button type="button" title="将所选元素编组；未多选时组合当前单元格" @click="groupCurrentCell"><Layers :size="15" /><span>组合元素</span></button>
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
            <div v-if="selectedElement?.Type === 6 && selectedElement.Elements?.length" class="component-save">
              <input v-model="componentName" aria-label="组件标识" placeholder="组件标识，如 orderBadge" />
              <button class="button-secondary" type="button" @click="saveSelectedGroup">保存组件</button>
            </div>
            <div v-if="template.Components?.length" class="component-library">
              <div class="tools-heading"><span>当前模板组件</span><span>{{ template.Components.length }}</span></div>
              <div v-for="item in template.Components" :key="item.Id" class="component-entry">
                <button type="button" :title="`插入组件 ${item.Id}`" @click="insertComponent(item.Id)"><Layers :size="14" />{{ item.Id }}</button>
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
              <button type="button" title="导出当前模板组件定义" :disabled="!template.Components?.length" @click="exportComponents"><Download :size="13" />导出组件</button>
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
            <button class="tool-button" type="button" title="文字" @click="addElement(1)"><Type :size="16" /></button>
            <button class="tool-button" type="button" title="图片" @click="openImagePicker"><ImagePlus :size="16" /></button>
            <button class="tool-button" type="button" title="二维码" @click="addElement(3)"><QrCode :size="16" /></button>
            <button class="tool-button" type="button" title="条码" @click="addElement(4)"><Barcode :size="16" /></button>
            <button class="tool-button" type="button" title="线段" @click="addElement(5)"><Minus :size="16" /></button>
          </div>
          <span class="toolbar-divider"></span>
          <div class="element-toolbar-group">
            <button class="tool-button" type="button" title="添加行" @click="addRow"><FilePlus2 :size="15" /></button>
            <button class="tool-button" type="button" title="添加列" @click="addColumn"><Layers3 :size="15" /></button>
            <button class="tool-button tool-delete" type="button" title="删除所选项" :disabled="!selectedPath" @click="removeSelected"><X :size="15" /></button>
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

        <CanvasWorkspace
          :template="template"
          :page="currentPage"
          :selected-path="selectedPath"
          :selected-paths="selectedPaths"
          :zoom="zoom"
          :unit="unit"
          :source="previewSource"
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
          :row="selectedRow"
          :cell="selectedCell"
          :unit="unit"
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
      <div class="status-right"><span>{{ template.Width }} × {{ template.Height }} pt</span><span class="status-dot"></span><span>{{ dotsToMm(template.Width) }} × {{ dotsToMm(template.Height) }} mm</span><span class="status-dot"></span><span>200 DPI</span><button class="icon-button quiet" type="button" title="帮助" @click="showToast('页面尺寸以打印点保存；本地预览仅用于布局参考。', 'warning')"><CircleHelp :size="14" /></button></div>
    </footer>

    <Transition name="toast"><div v-if="toast" class="toast-message" :class="`toast-${toastTone}`"><Check v-if="toastTone === 'success'" :size="15" /><AlertTriangle v-else :size="15" />{{ toast }}<button type="button" @click="toast = ''"><X :size="14" /></button></div></Transition>
    <div v-if="mobileLayersOpen || mobileInspectorOpen" class="mobile-scrim" @click="mobileLayersOpen = false; mobileInspectorOpen = false"></div>
  </main>
</template>
