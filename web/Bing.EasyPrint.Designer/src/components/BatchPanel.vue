<script setup lang="ts">
import { computed, onUnmounted, ref, toRaw, watch } from 'vue'
import { Download, FileUp, Play, X } from '@lucide/vue'
import {
  applyCsvMapping, detectBatchFormat, mergeBatchArchives, parseBatchInput,
  type BatchDataRecord, type BatchErrorRow, type BatchInputFormat, type BatchManifest, type BatchRecordResult,
} from '../batch'
import { renderBatchChunk } from '../api'
import { cloneJsonPreservingPrecision } from '../losslessJson'
import type { BatchEditorState } from '../batchEditorState'

const props = defineProps<{
  baseBody: Record<string, unknown> | null
  previewRecord?: (data: Record<string, unknown>) => Promise<void>
  state: BatchEditorState
  exportCapabilities: { pdf: boolean; svg: boolean }
}>()
const emit = defineEmits<{ 'state-change': [state: BatchEditorState] }>()

const CHUNK_SIZE = 25
const RECORD_LIMIT = 1000
const PAGE_LIMIT = 1000
const BYTE_LIMIT = 32 * 1024 * 1024

const fileInput = ref<HTMLInputElement>()
const fileName = ref(props.state.fileName)
const rawText = ref(props.state.rawText)
const format = ref<BatchInputFormat>(props.state.format)
const headers = ref<string[]>([])
const rows = ref<string[][]>([])
const records = ref<BatchDataRecord[]>([])
const mapping = ref<Record<string, string>>({ ...props.state.mapping })
const selected = ref<Set<number>>(new Set(props.state.selectedIndices))
const copiesField = ref(props.state.copiesField)
const exportFormats = ref<('pdf' | 'svg')[]>([...props.state.exportFormats])
const parseError = ref('')
const runError = ref('')
const status = ref<'idle' | 'running' | 'completed' | 'partial' | 'all_failed' | 'cancelled' | 'error'>('idle')
const progress = ref(0)
const total = ref(0)
const manifest = ref<BatchManifest | null>(null)
const resultBlob = ref<Blob | null>(null)
const failureFilter = ref<'all' | 'failed'>('all')
let abortController: AbortController | null = null
let runId = 0
let lastPublishedState: BatchEditorState | undefined

function publishState() {
  const state: BatchEditorState = {
    fileName: fileName.value,
    rawText: rawText.value,
    format: format.value,
    mapping: mapping.value,
    selectedIndices: [...selected.value].sort((left, right) => left - right),
    copiesField: copiesField.value,
    exportFormats: [...exportFormats.value],
  }
  lastPublishedState = state
  emit('state-change', state)
}

watch(() => props.state, (state) => {
  if (toRaw(state) === lastPublishedState) return
  fileName.value = state.fileName
  rawText.value = state.rawText
  format.value = state.format
  mapping.value = { ...state.mapping }
  selected.value = new Set(state.selectedIndices)
  copiesField.value = state.copiesField
  exportFormats.value = [...state.exportFormats]
  lastPublishedState = state
  reload(true)
}, { flush: 'sync' })

const selectedRecords = computed(() => records.value.filter((record) => selected.value.has(record.index)))
const availableFields = computed(() => {
  const paths = new Set<string>()
  for (const record of records.value.slice(0, 25)) collectPaths(record.data, '', paths)
  return Array.from(paths).sort()
})
const batchDisabledReason = computed(() => {
  if (parseError.value) return parseError.value
  if (status.value === 'running') return '批量任务正在运行，请等待或取消。'
  if (!props.baseBody) return '当前输出配置不可用于批量生成。'
  if (props.baseBody.job !== undefined) return '批量接口不支持旧 job 参数；请关闭任务级 job 后使用份数字段。'
  if (!selectedRecords.value.length) return '请选择至少一条记录。'
  if (selectedRecords.value.length > RECORD_LIMIT) return `一次最多处理 ${RECORD_LIMIT} 条记录。`
  if (exportFormats.value.includes('pdf') && !props.exportCapabilities.pdf)
    return '当前Host或输出配置不支持批量PDF导出。'
  if (exportFormats.value.includes('svg') && !props.exportCapabilities.svg)
    return '当前Host不支持批量SVG导出。'
  return ''
})
const recordStatus = computed(() => new Map((manifest.value?.records ?? [])
  .map((record) => [record.index, record] as const)))
const visibleRecords = computed<BatchRecordResult[]>(() => failureFilter.value === 'failed'
  ? (manifest.value?.records ?? []).filter((record) => record.status === 'failed')
  : (manifest.value?.records ?? []))
const visibleErrors = computed(() => manifest.value?.errors ?? [])
const resultLabel = computed(() => {
  if (status.value === 'all_failed') return '下载错误报告'
  if (status.value === 'partial') return '下载结果（部分完成）'
  return '下载结果'
})

function openFile() { fileInput.value?.click() }

async function selectFile(event: Event) {
  if (status.value === 'running') return
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  try {
    const text = await file.text()
    fileName.value = file.name
    rawText.value = text
    format.value = detectBatchFormat(file.name, text)
    reload()
    publishState()
    if (!parseError.value) await previewSelected()
  } catch (error) {
    parseError.value = error instanceof Error ? error.message : '读取批量文件失败。'
  } finally { input.value = '' }
}

function reload(preserveSelection = false) {
  if (status.value === 'running') { runError.value = '请先取消当前批量任务。'; return }
  const previousSelection = preserveSelection ? new Set(selected.value) : undefined
  const previousMapping = preserveSelection ? mapping.value : undefined
  parseError.value = ''
  runError.value = ''
  manifest.value = null
  resultBlob.value = null
  status.value = 'idle'
  try {
    const parsed = parseBatchInput(rawText.value, format.value)
    headers.value = parsed.headers
    rows.value = parsed.rows
    mapping.value = previousMapping && Object.keys(previousMapping).length ? { ...previousMapping } : parsed.mapping
    records.value = parsed.format === 'csv'
      ? applyCsvMapping(parsed.headers, parsed.rows, mapping.value)
      : parsed.records
    if (records.value.length > RECORD_LIMIT) throw new Error(`一次最多导入 ${RECORD_LIMIT} 条记录。`)
    selected.value = previousSelection
      ? new Set(records.value.filter((record) => previousSelection.has(record.index)).map((record) => record.index))
      : new Set(records.value.map((record) => record.index))
  } catch (error) {
    records.value = []
    selected.value = new Set()
    parseError.value = error instanceof Error ? error.message : '批量数据解析失败。'
  }
  publishState()
}

function updateMapping(header: string, event: Event) {
  if (status.value === 'running') return
  mapping.value = { ...mapping.value, [header]: (event.target as HTMLInputElement).value.trim() }
  try {
    records.value = applyCsvMapping(headers.value, rows.value, mapping.value)
    parseError.value = ''
  } catch (error) { parseError.value = error instanceof Error ? error.message : '字段映射无效。' }
  publishState()
}

function toggleRecord(index: number) {
  if (status.value === 'running') return
  const next = new Set(selected.value)
  if (next.has(index)) next.delete(index)
  else next.add(index)
  selected.value = next
  publishState()
}

function selectAll() { selected.value = new Set(records.value.map((record) => record.index)); publishState() }
function selectNone() { selected.value = new Set(); publishState() }

function toggleExportFormat(format: 'pdf' | 'svg', event: Event) {
  const enabled = (event.target as HTMLInputElement).checked
  exportFormats.value = enabled
    ? [...new Set([...exportFormats.value, format])]
    : exportFormats.value.filter((item) => item !== format)
  publishState()
}

async function previewSelected() {
  if (!props.previewRecord || !selectedRecords.value.length) return
  await props.previewRecord(selectedRecords.value[0]!.data)
}

async function run() {
  if (batchDisabledReason.value || !props.baseBody) {
    runError.value = batchDisabledReason.value || '当前输出配置不可用于批量生成。'
    return
  }
  const id = ++runId
  const controller = new AbortController()
  abortController = controller
  const sourceRecords = selectedRecords.value.map((record) => cloneJsonPreservingPrecision(record))
  const sourceCopiesField = copiesField.value
  const sourceExportFormats = [...exportFormats.value]
  const sourceBase = cloneJsonPreservingPrecision(props.baseBody)
  if (sourceCopiesField) delete sourceBase.copies
  status.value = 'running'
  runError.value = ''
  manifest.value = null
  resultBlob.value = null
  progress.value = 0
  total.value = sourceRecords.length
  const chunks: Blob[] = []
  let remainingPages = PAGE_LIMIT
  let remainingBytes = BYTE_LIMIT
  try {
    for (let offset = 0; offset < sourceRecords.length; offset += CHUNK_SIZE) {
      controller.signal.throwIfAborted()
      if (id !== runId) throw new DOMException('Aborted', 'AbortError')
      const chunk = sourceRecords.slice(offset, offset + CHUNK_SIZE)
      const result = await renderBatchChunk(sourceBase, {
        records: chunk,
        recordOffset: offset,
        totalRecords: sourceRecords.length,
        ...(sourceCopiesField ? { copiesField: sourceCopiesField } : {}),
        ...(sourceExportFormats.length ? { exportFormats: sourceExportFormats } : {}),
        remainingPages,
        remainingBytes,
      }, controller.signal)
      if (id !== runId) throw new DOMException('Aborted', 'AbortError')
      remainingPages = result.remainingPages
      remainingBytes = result.remainingBytes
      chunks.push(result.blob)
      progress.value += chunk.length
      if (remainingPages < 0 || remainingBytes < 0) throw new Error('批量结果超过服务端剩余预算。')
    }
    const merged = await mergeBatchArchives(chunks, BYTE_LIMIT, controller.signal)
    if (id !== runId || controller.signal.aborted) throw new DOMException('Aborted', 'AbortError')
    manifest.value = merged.manifest
    resultBlob.value = merged.blob
    status.value = merged.manifest.status
  } catch (error) {
    if (id !== runId) return
    if (error instanceof DOMException && error.name === 'AbortError') {
      status.value = 'cancelled'
      runError.value = '批量任务已取消；未生成下载文件。'
    } else {
      status.value = 'error'
      runError.value = error instanceof Error ? error.message : '批量生成失败。'
    }
    resultBlob.value = null
    manifest.value = null
  } finally {
    if (id === runId && abortController === controller) abortController = null
  }
}

function cancel() {
  runId++
  abortController?.abort()
  abortController = null
  status.value = 'cancelled'
  resultBlob.value = null
  manifest.value = null
  runError.value = '批量任务已取消；未生成下载文件。'
}

function download() {
  if (!resultBlob.value) return
  const url = URL.createObjectURL(resultBlob.value)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = 'easyprint-batch.zip'
  anchor.click()
  URL.revokeObjectURL(url)
}

function collectPaths(value: unknown, prefix: string, result: Set<string>, depth = 0) {
  if (!value || typeof value !== 'object' || Array.isArray(value) || depth > 4) return
  for (const [key, child] of Object.entries(value as Record<string, unknown>)) {
    const path = prefix ? `${prefix}.${key}` : key
    result.add(path)
    collectPaths(child, path, result, depth + 1)
  }
}

function recordText(record: BatchDataRecord): string {
  return Object.entries(record.data).slice(0, 3).map(([key, value]) => `${key}=${String(value)}`).join(' · ')
}

function errorFor(record: BatchRecordResult): BatchErrorRow | undefined {
  return manifest.value?.errors.find((error) => error.recordIndex === record.index)
}

onUnmounted(() => {
  runId++
  abortController?.abort()
  abortController = null
})
</script>

<template>
  <section class="batch-panel" data-testid="batch-panel">
    <header class="batch-header">
      <div>
        <h3>批量生成</h3>
        <p>UTF-8 CSV / JSON 对象数组 / JSONL。每块 25 条，整批最多 1000 条、1000 页、32 MiB。</p>
      </div>
      <input ref="fileInput" data-testid="batch-file-input" type="file" :disabled="status === 'running'" accept=".csv,.json,.jsonl,.ndjson,.txt" hidden @change="selectFile" />
      <div class="batch-selection">
        <label class="field-label">格式
          <select v-model="format" class="field-input" data-testid="batch-format" :disabled="status === 'running'" @change="reload()">
            <option value="csv">CSV</option>
            <option value="json">JSON 数组</option>
            <option value="jsonl">JSONL</option>
          </select>
        </label>
        <button class="button-secondary" type="button" @click="openFile"><FileUp :size="15" />导入数据</button>
      </div>
    </header>

    <p v-if="fileName" class="field-note">{{ fileName }} · {{ format.toUpperCase() }} · {{ records.length }} 条</p>
    <p v-if="parseError" class="field-note capability-warning" data-testid="batch-parse-error">{{ parseError }}</p>

    <div v-if="headers.length" class="batch-section">
      <h4>字段映射</h4>
      <div class="batch-mapping">
        <label v-for="header in headers" :key="header" class="field-label">{{ header }}
          <input class="field-input" :disabled="status === 'running'" :data-testid="`batch-map-${header}`" :value="mapping[header] ?? header" @change="updateMapping(header, $event)" />
        </label>
      </div>
    </div>

    <div v-if="records.length" class="batch-section">
      <div class="batch-toolbar">
        <label class="field-label">份数字段
          <select v-model="copiesField" class="field-input" :disabled="status === 'running'" data-testid="batch-copies-field" @change="publishState">
            <option value="">使用输出设置份数</option>
            <option v-for="field in availableFields" :key="field" :value="field">{{ field }}</option>
          </select>
        </label>
        <div class="batch-selection">
          <button class="text-button" type="button" :disabled="status === 'running'" @click="selectAll">全选</button>
          <button class="text-button" type="button" :disabled="status === 'running'" @click="selectNone">清空</button>
          <span>{{ selectedRecords.length }} / {{ records.length }}</span>
        </div>
      </div>
      <fieldset class="batch-export-options" :disabled="status === 'running'">
        <legend>可选文件</legend>
        <label class="field-label">
          <input type="checkbox" data-testid="batch-export-pdf" :checked="exportFormats.includes('pdf')"
            :disabled="status === 'running' || !exportCapabilities.pdf" @change="toggleExportFormat('pdf', $event)" />
          整页PDF
        </label>
        <label class="field-label">
          <input type="checkbox" data-testid="batch-export-svg" :checked="exportFormats.includes('svg')"
            :disabled="status === 'running' || !exportCapabilities.svg" @change="toggleExportFormat('svg', $event)" />
          码图SVG
        </label>
      </fieldset>
      <div class="batch-records" data-testid="batch-records">
        <label v-for="record in records" :key="record.index" class="batch-record">
          <input type="checkbox" :disabled="status === 'running'" :checked="selected.has(record.index)" @change="toggleRecord(record.index)" />
          <span class="batch-index">#{{ record.index }}</span>
          <span class="batch-summary">{{ recordText(record) }}</span>
          <span v-if="recordStatus.get(record.index)" :class="['batch-state', recordStatus.get(record.index)!.status]">
            {{ recordStatus.get(record.index)!.status === 'completed' ? '成功' : '失败' }}
          </span>
        </label>
      </div>
    </div>

    <div class="batch-actions">
      <button class="button-secondary" type="button" :disabled="status === 'running' || !selectedRecords.length || !previewRecord" @click="previewSelected"><Play :size="14" />预览选中</button>
      <button class="button-primary" type="button" :disabled="status === 'running' || !selectedRecords.length" @click="run"><Play :size="14" />批量生成</button>
      <button v-if="status === 'running'" class="button-secondary" type="button" @click="cancel"><X :size="14" />取消</button>
      <button v-if="resultBlob" class="button-secondary" type="button" @click="download"><Download :size="14" />{{ resultLabel }}</button>
    </div>

    <div v-if="status === 'running' || progress" class="batch-progress" data-testid="batch-progress">
      <progress :value="progress" :max="total || 1" />
      <span>{{ progress }} / {{ total }}</span>
    </div>
    <p v-if="runError" class="field-note capability-warning" data-testid="batch-error">{{ runError }}</p>
    <p v-if="manifest" class="field-note" data-testid="batch-status">
      状态：{{ manifest.status === 'completed' ? '完成' : manifest.status === 'partial' ? '部分完成' : '全部失败' }} ·
      成功 {{ manifest.succeeded }} · 失败 {{ manifest.failed }} · 物理页 {{ manifest.physicalPages }}
    </p>

    <div v-if="manifest" class="batch-section">
      <div class="batch-toolbar">
        <h4>结果明细</h4>
        <select v-model="failureFilter" class="field-input" aria-label="批量结果筛选">
          <option value="all">全部记录</option>
          <option value="failed">仅失败</option>
        </select>
      </div>
      <div class="batch-errors">
        <p v-for="record in visibleRecords" :key="record.index" class="batch-result-row">
          #{{ record.index }} · {{ record.status === 'completed' ? '成功' : '失败' }}
          <span v-if="record.status === 'completed'"> · {{ record.pageCount }} 页</span>
          <span v-if="errorFor(record)"> · {{ errorFor(record)?.code }} · {{ errorFor(record)?.fieldPath || errorFor(record)?.sourcePath }}</span>
        </p>
        <p v-for="error in visibleErrors" :key="`${error.recordIndex}-${error.code}-${error.message}`" class="batch-error-row">
          #{{ error.recordIndex }} · {{ error.code }} · {{ error.fieldPath || error.sourcePath }} · {{ error.message }}
        </p>
      </div>
    </div>
  </section>
</template>

<style scoped>
.batch-panel { border-top: 1px solid #d7e0df; padding: 16px; margin-top: 12px; background: #fbfdfc; }
.batch-header, .batch-toolbar, .batch-actions, .batch-selection, .batch-progress { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.batch-header { justify-content: space-between; }
.batch-header h3, .batch-section h4 { margin: 0; }
.batch-header p { margin: 4px 0 0; color: #637574; font-size: 12px; }
.batch-section { margin-top: 14px; }
.batch-mapping { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 8px; }
.batch-records { max-height: 260px; overflow: auto; border: 1px solid #d7e0df; border-radius: 6px; background: white; }
.batch-record { display: grid; grid-template-columns: 20px 60px 1fr auto; gap: 8px; align-items: center; padding: 6px 10px; border-bottom: 1px solid #eef2f1; font-size: 12px; }
.batch-index { color: #637574; }
.batch-summary { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.batch-state.completed { color: #177a55; }
.batch-state.failed { color: #b33b31; }
.batch-errors { max-height: 220px; overflow: auto; }
.batch-result-row, .batch-error-row { margin: 4px 0; font-size: 12px; }
.batch-error-row { color: #b33b31; }
</style>
