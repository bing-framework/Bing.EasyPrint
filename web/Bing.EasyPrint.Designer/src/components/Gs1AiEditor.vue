<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { ArrowDown, ArrowLeftRight, ArrowUp, LoaderCircle, Plus, Search, Trash2 } from '@lucide/vue'
import { convertGs1Input, loadGs1AiCatalog, validateGs1Input, type Gs1AiCatalogEntry } from '../api'
import type { BpTemplate, FieldFormat, Gs1Input, Gs1InputItem, Gs1InputMode, Gs1InputQueryParameter, TemplateElement } from '../model'
import FieldFormatEditor from './FieldFormatEditor.vue'

const props = defineProps<{
  modelValue?: Gs1Input
  template: BpTemplate
  data: unknown
  element: TemplateElement
  fieldPath: string
  fieldPaths: string[]
  allowedModes: Gs1InputMode[]
  enabled: boolean
  supportReason: string
}>()
const emit = defineEmits<{ 'update:modelValue': [value: Gs1Input | undefined] }>()

const catalog = ref<Gs1AiCatalogEntry[]>([])
const catalogState = ref<'loading' | 'ready' | 'error'>('loading')
const catalogError = ref('')
const searchText = ref('')
const selectedAi = ref('')
const validationState = ref<'idle' | 'loading' | 'valid' | 'pending' | 'invalid' | 'error'>('idle')
const validationErrors = ref<Array<{
  ai?: string
  itemIndex?: number
  instanceIndices?: number[]
  message: string
}>>([])
const conversionText = ref('')
const conversionTarget = ref<'elements' | 'digital-link'>('digital-link')
const conversionError = ref('')
const overrideUriSettings = ref(false)
const converting = ref(false)
let catalogController: AbortController | undefined
let validationController: AbortController | undefined
let conversionController: AbortController | undefined
let validationTimer: ReturnType<typeof setTimeout> | undefined
let validationRevision = 0

const canEdit = computed(() => props.enabled && catalogState.value === 'ready')
const filteredEntries = computed(() => {
  const query = searchText.value.trim().toLocaleLowerCase()
  return catalog.value.filter((entry) => !query ||
    `${entry.ai} ${entry.title} ${entry.specification} ${entry.attributes}`.toLocaleLowerCase().includes(query))
})
const selectedEntry = computed(() => catalog.value.find((entry) => entry.ai === selectedAi.value))
const selectedEntrySupported = computed(() => selectedEntry.value != null &&
  (props.modelValue?.Mode !== 1 || selectedEntry.value.role !== 'Unsupported'))
const conversionSourceIsUri = computed(() => /^https?:\/\//i.test(conversionText.value.trim()))
const currentItemStatus = computed(() => validationState.value === 'loading' ? '校验中' :
  validationState.value === 'valid' ? '有效' : validationState.value === 'pending' ? '待数据' :
    validationState.value === 'invalid' ? '无效' : validationState.value === 'error' ? 'Host 不可用' : '')
const datalistId = computed(() => `gs1-path-${props.fieldPath.replace(/[^a-zA-Z0-9_-]/g, '-')}`)

onMounted(() => { void refreshCatalog() })
onBeforeUnmount(() => {
  catalogController?.abort()
  validationController?.abort()
  conversionController?.abort()
  if (validationTimer) clearTimeout(validationTimer)
})

watch(() => [props.modelValue, props.template, props.data] as const,
  () => scheduleValidation(), { deep: true, flush: 'post' })

async function refreshCatalog(): Promise<void> {
  catalogController?.abort()
  catalogController = new AbortController()
  catalogState.value = 'loading'
  catalogError.value = ''
  try {
    const result = await loadGs1AiCatalog(catalogController.signal)
    if (catalogController.signal.aborted) return
    catalog.value = result.entries
    catalogState.value = 'ready'
    scheduleValidation()
  } catch (error) {
    if (catalogController.signal.aborted) return
    catalogState.value = 'error'
    catalogError.value = error instanceof Error ? error.message : 'GS1 AI 目录不可用。'
  }
}

function activeInput(): Gs1Input | undefined {
  return props.modelValue
}

function updateInput(patch: Partial<Gs1Input>): void {
  const current = activeInput()
  if (!current || !canEdit.value) return
  emit('update:modelValue', { ...current, ...patch })
  conversionError.value = ''
}

function enableEditor(): void {
  if (!canEdit.value || !props.enabled) return
  emit('update:modelValue', { Mode: props.allowedModes[0] ?? 0, ValidateAssociations: false, Items: [], QueryParameters: [] })
}

function removeEditor(): void {
  if (!canEdit.value) return
  emit('update:modelValue', undefined)
}

function updateMode(event: Event): void {
  updateInput({ Mode: Number((event.target as HTMLSelectElement).value) as Gs1Input['Mode'] })
}

function addAi(): void {
  const entry = selectedEntry.value
  const current = activeInput()
  if (!entry || !current || current.Mode === 1 && entry.role === 'Unsupported' || !canEdit.value) return
  updateInput({ Items: [...current.Items, { Ai: entry.ai, Value: '', Placement: 0 }] })
  searchText.value = ''
  selectedAi.value = ''
}

function updateItem(index: number, patch: Partial<Gs1InputItem>): void {
  const current = activeInput()
  if (!current) return
  updateInput({ Items: current.Items.map((item, itemIndex) => itemIndex === index ? { ...item, ...patch } : item) })
}

function moveItem(index: number, direction: -1 | 1): void {
  const current = activeInput()
  if (!current) return
  const target = index + direction
  if (target < 0 || target >= current.Items.length) return
  const items = [...current.Items]
  const [item] = items.splice(index, 1)
  items.splice(target, 0, item!)
  updateInput({ Items: items })
}

function removeItem(index: number): void {
  const current = activeInput()
  if (current) updateInput({ Items: current.Items.filter((_, itemIndex) => itemIndex !== index) })
}

function setItemBinding(index: number, bind: boolean): void {
  const item = activeInput()?.Items[index]
  if (!item) return
  if (bind) updateItem(index, { Name: item.Name ?? '', Value: undefined })
  else updateItem(index, { Value: item.Value ?? '', Name: undefined })
}

function updateItemFormat(index: number, format: FieldFormat | undefined): void {
  updateItem(index, { Format: format })
}

function addQueryParameter(): void {
  const current = activeInput()
  if (current) updateInput({ QueryParameters: [...(current.QueryParameters ?? []), { Key: '', Value: '' }] })
}

function updateQueryParameter(index: number, patch: Partial<Gs1InputQueryParameter>): void {
  const current = activeInput()
  if (!current) return
  const parameters = current.QueryParameters ?? []
  updateInput({ QueryParameters: parameters.map((item, itemIndex) =>
    itemIndex === index ? { ...item, ...patch } : item) })
}

function moveQueryParameter(index: number, direction: -1 | 1): void {
  const current = activeInput()
  if (!current) return
  const parameters = [...(current.QueryParameters ?? [])]
  const target = index + direction
  if (target < 0 || target >= parameters.length) return
  const [item] = parameters.splice(index, 1)
  parameters.splice(target, 0, item!)
  updateInput({ QueryParameters: parameters })
}

function removeQueryParameter(index: number): void {
  const current = activeInput()
  if (current) updateInput({ QueryParameters: (current.QueryParameters ?? []).filter((_, itemIndex) => itemIndex !== index) })
}

function entryFor(ai: string): Gs1AiCatalogEntry | undefined {
  return catalog.value.find((entry) => entry.ai === ai)
}

function constraintText(entry?: Gs1AiCatalogEntry): string {
  if (!entry) return ''
  const components = entry.components.map((item) =>
    `${item.type}${item.minimumLength === item.maximumLength ? item.minimumLength : `${item.minimumLength}–${item.maximumLength}`}${item.optional ? '?' : ''}`)
  return `${entry.minimumLength}–${entry.maximumLength} 位${components.length ? ` · ${components.join(' + ')}` : ''}`
}

function scheduleValidation(): void {
  validationRevision++
  const revision = validationRevision
  if (validationTimer) clearTimeout(validationTimer)
  validationController?.abort()
  validationErrors.value = []
  if (!props.modelValue || catalogState.value !== 'ready') {
    validationState.value = 'idle'
    return
  }
  validationState.value = 'loading'
  validationTimer = setTimeout(() => { void runValidation(revision) }, 250)
}

async function runValidation(revision: number): Promise<void> {
  validationController?.abort()
  validationController = new AbortController()
  const signal = validationController.signal
  try {
    const result = await validateGs1Input(props.template, props.data, signal)
    if (signal.aborted || revision !== validationRevision) return
    const fieldItems = result.items.filter((candidate) => candidate.path === props.fieldPath)
    validationErrors.value = result.errors
      .filter((error) => !error.sourcePath || error.sourcePath === props.fieldPath)
      .map((error) => ({
        ai: error.ai ?? undefined,
        itemIndex: error.itemIndex ?? undefined,
        instanceIndices: error.instanceIndices ?? undefined,
        message: error.message,
      }))
    if (validationErrors.value.length || fieldItems.some((item) => item.status === 'invalid')) {
      validationState.value = 'invalid'
    } else if (!fieldItems.length || fieldItems.some((item) => item.status === 'pending-data')) {
      validationState.value = 'pending'
    } else {
      validationState.value = 'valid'
    }
  } catch (error) {
    if (signal.aborted || revision !== validationRevision) return
    validationState.value = 'error'
    validationErrors.value = [{ message: error instanceof Error ? error.message : 'GS1 校验失败。' }]
  }
}

function errorsForItem(item: Gs1InputItem, index: number) {
  return validationErrors.value.filter((error) => error.itemIndex === index ||
    error.itemIndex == null && error.ai === item.Ai)
}

function formatValidationError(error: { instanceIndices?: number[]; message: string }) {
  const location = error.instanceIndices?.length
    ? `实例 ${error.instanceIndices.map((index) => index + 1).join('.')}: ` : ''
  return location + error.message
}

async function convertInput(): Promise<void> {
  conversionError.value = ''
  if (!canEdit.value || !conversionText.value.trim()) return
  conversionController?.abort()
  conversionController = new AbortController()
  converting.value = true
  try {
    const current = activeInput()
    const applyCurrentSettings = !conversionSourceIsUri.value || overrideUriSettings.value
    const result = await convertGs1Input({
      input: conversionText.value,
      targetFormat: conversionTarget.value,
      ...(conversionTarget.value === 'digital-link' ? {
        baseUri: applyCurrentSettings ? current?.BaseUri : undefined,
        fragment: applyCurrentSettings ? current?.Fragment : undefined,
        queryParameters: applyCurrentSettings
          ? current?.QueryParameters?.map((item) => ({ key: item.Key, value: item.Value ?? '' }))
          : undefined,
      } : {}),
      validateAssociations: current?.ValidateAssociations ?? false,
    }, conversionController.signal)
    if (conversionController.signal.aborted) return
    const structured = result.structuredInput
    emit('update:modelValue', {
      Mode: structured.mode,
      BaseUri: structured.baseUri,
      Fragment: structured.fragment,
      ValidateAssociations: structured.validateAssociations,
      Items: structured.items.map((item) => ({ Ai: item.ai, Value: item.value, Placement: item.placement })),
      QueryParameters: structured.queryParameters.map((item) => ({ Key: item.key, Value: item.value })),
    })
  } catch (error) {
    if (conversionController.signal.aborted) return
    conversionError.value = error instanceof Error ? error.message : 'GS1 转换失败。'
  } finally {
    if (!conversionController.signal.aborted) converting.value = false
  }
}
</script>

<template>
  <section class="gs1-editor" data-testid="gs1-ai-editor">
    <div class="gs1-editor-heading">
      <div class="section-title"><span>GS1 AI</span><span class="section-kicker">{{ modelValue ? currentItemStatus : '' }}</span></div>
      <button v-if="modelValue" type="button" class="icon-action" title="移除 GS1 配置" aria-label="移除 GS1 配置"
        :disabled="!canEdit" @click="removeEditor"><Trash2 :size="14" /></button>
    </div>

    <p v-if="!enabled" class="editor-note capability-warning">{{ supportReason }}</p>
    <div v-else-if="catalogState === 'loading'" class="editor-note" role="status"><LoaderCircle class="spin" :size="14" /> 正在读取 GS1 AI 目录</div>
    <div v-else-if="catalogState === 'error'" class="editor-error" role="alert">
      <span>{{ catalogError }}</span>
      <button type="button" @click="refreshCatalog">重试</button>
    </div>

    <button v-if="!modelValue" type="button" class="wide-action" :disabled="!canEdit || !enabled" data-testid="gs1-enable"
      @click="enableEditor"><Plus :size="14" />添加结构化 GS1 来源</button>

    <template v-else>
      <label class="field-label">输出模式
        <select class="field-input" aria-label="GS1 输出模式" :value="modelValue.Mode" :disabled="!canEdit" @change="updateMode">
          <option :value="0" :disabled="!allowedModes.includes(0)">GS1 Elements</option>
          <option :value="1" :disabled="!allowedModes.includes(1)">Digital Link</option>
        </select>
      </label>
      <label v-if="modelValue.Mode === 1" class="field-label">HTTPS 基址
        <input class="field-input" aria-label="Digital Link HTTPS 基址" :value="modelValue.BaseUri ?? ''" :disabled="!canEdit"
          placeholder="https://id.gs1.org" @input="updateInput({ BaseUri: ($event.target as HTMLInputElement).value })" />
      </label>
      <label v-if="modelValue.Mode === 1" class="field-label">Fragment
        <input class="field-input" aria-label="Digital Link Fragment" :value="modelValue.Fragment ?? ''" :disabled="!canEdit"
          @input="updateInput({ Fragment: ($event.target as HTMLInputElement).value })" />
      </label>
      <label class="toggle-row"><span>启用 req/ex 关联校验</span>
        <input type="checkbox" aria-label="GS1 req ex 关联校验" :checked="modelValue.ValidateAssociations === true" :disabled="!canEdit"
          @change="updateInput({ ValidateAssociations: ($event.target as HTMLInputElement).checked })" />
      </label>

      <div class="gs1-add-row">
        <label class="search-field"><Search :size="14" /><input v-model="searchText" aria-label="搜索 GS1 AI" :disabled="!canEdit"
          placeholder="AI、名称或约束" /></label>
        <select v-model="selectedAi" aria-label="选择 GS1 AI" :disabled="!canEdit || !filteredEntries.length">
          <option value="">选择 AI</option>
          <option v-for="entry in filteredEntries" :key="entry.ai" :value="entry.ai"
            :disabled="modelValue.Mode === 1 && entry.role === 'Unsupported'">
            {{ entry.ai }} · {{ entry.title }}{{ entry.role === 'Unsupported' ? ' · Digital Link 不支持' : '' }}
          </option>
        </select>
        <button class="icon-action" type="button" title="添加 AI" aria-label="添加 AI"
          :disabled="!canEdit || !selectedEntrySupported" @click="addAi"><Plus :size="14" /></button>
      </div>

      <div v-if="!modelValue.Items.length" class="editor-note">尚未添加 AI</div>
      <article v-for="(item, index) in modelValue.Items" :key="`${index}:${item.Ai}`" class="gs1-item" :data-testid="`gs1-item-${item.Ai}`">
        <div class="gs1-item-heading">
          <strong>({{ item.Ai }}) {{ entryFor(item.Ai)?.title ?? '未知 AI' }}</strong>
          <div class="gs1-item-actions">
            <button type="button" class="icon-action" :title="`上移 AI ${item.Ai}`" :aria-label="`上移 AI ${item.Ai}`"
              :disabled="!canEdit || index === 0" @click="moveItem(index, -1)"><ArrowUp :size="14" /></button>
            <button type="button" class="icon-action" :title="`下移 AI ${item.Ai}`" :aria-label="`下移 AI ${item.Ai}`"
              :disabled="!canEdit || index === modelValue.Items.length - 1" @click="moveItem(index, 1)"><ArrowDown :size="14" /></button>
            <button type="button" class="icon-action" :title="`删除 AI ${item.Ai}`" :aria-label="`删除 AI ${item.Ai}`"
              :disabled="!canEdit" @click="removeItem(index)"><Trash2 :size="14" /></button>
          </div>
        </div>
        <small v-if="entryFor(item.Ai)" class="gs1-constraints">{{ constraintText(entryFor(item.Ai)) }}<template v-if="entryFor(item.Ai)?.requiresSeparator"> · 需要分隔符</template></small>
        <small v-if="entryFor(item.Ai)?.attributes" class="gs1-rules">{{ entryFor(item.Ai)?.attributes }}</small>
        <div class="segmented-control gs1-value-mode">
          <button type="button" :class="{ active: item.Name === undefined }" :disabled="!canEdit" @click="setItemBinding(index, false)">固定值</button>
          <button type="button" :class="{ active: item.Name !== undefined }" :disabled="!canEdit" @click="setItemBinding(index, true)">字段绑定</button>
        </div>
        <label v-if="item.Name !== undefined" class="field-label">字段路径
          <input class="field-input" :list="datalistId" :aria-label="`AI ${item.Ai} 字段路径`" :value="item.Name" :disabled="!canEdit"
            @input="updateItem(index, { Name: ($event.target as HTMLInputElement).value })" />
          <datalist :id="datalistId"><option v-for="path in fieldPaths" :key="path" :value="path" /></datalist>
        </label>
        <label v-else class="field-label">原始字符串值
          <input class="field-input" :aria-label="`AI ${item.Ai} 固定值`" :value="item.Value ?? ''" :disabled="!canEdit"
            @input="updateItem(index, { Value: ($event.target as HTMLInputElement).value })" />
        </label>
        <label v-if="modelValue.Mode === 1 && item.Ai !== '01'" class="field-label">Digital Link 位置
          <select class="field-input" :aria-label="`AI ${item.Ai} 位置`" :value="item.Placement ?? 0" :disabled="!canEdit"
            @change="updateItem(index, { Placement: Number(($event.target as HTMLSelectElement).value) as Gs1InputItem['Placement'] })">
            <option :value="0">按 AI 目录决定</option>
            <option :value="1">路径限定符</option>
            <option :value="2">查询数据属性</option>
          </select>
        </label>
        <FieldFormatEditor v-if="item.Name !== undefined" :format="item.Format" title="AI 字段格式"
          @update:format="updateItemFormat(index, $event)" />
        <div v-for="(error, errorIndex) in errorsForItem(item, index)" :key="`${error.ai}-${error.itemIndex}-${error.instanceIndices?.join('.')}-${errorIndex}`" class="gs1-item-error">
          {{ formatValidationError(error) }}
        </div>
      </article>

      <section v-if="modelValue.Mode === 1" class="gs1-query-section">
        <div class="section-title"><span>非 GS1 查询参数</span>
          <button type="button" class="icon-action" title="添加查询参数" aria-label="添加查询参数" :disabled="!canEdit"
            @click="addQueryParameter"><Plus :size="14" /></button>
        </div>
        <div v-for="(parameter, index) in modelValue.QueryParameters ?? []" :key="index" class="gs1-query-row">
          <input class="field-input" :aria-label="`查询键 ${index + 1}`" :value="parameter.Key" :disabled="!canEdit"
            placeholder="键" @input="updateQueryParameter(index, { Key: ($event.target as HTMLInputElement).value })" />
          <input class="field-input" :aria-label="`查询值 ${index + 1}`" :value="parameter.Value ?? ''" :disabled="!canEdit"
            placeholder="值" @input="updateQueryParameter(index, { Value: ($event.target as HTMLInputElement).value })" />
          <div class="gs1-item-actions">
            <button type="button" class="icon-action" title="上移查询参数" aria-label="上移查询参数" :disabled="!canEdit || index === 0"
              @click="moveQueryParameter(index, -1)"><ArrowUp :size="14" /></button>
            <button type="button" class="icon-action" title="下移查询参数" aria-label="下移查询参数" :disabled="!canEdit || index === (modelValue.QueryParameters?.length ?? 0) - 1"
              @click="moveQueryParameter(index, 1)"><ArrowDown :size="14" /></button>
            <button type="button" class="icon-action" title="删除查询参数" aria-label="删除查询参数" :disabled="!canEdit"
              @click="removeQueryParameter(index)"><Trash2 :size="14" /></button>
          </div>
        </div>
      </section>

      <div class="gs1-validation" aria-live="polite" :class="`state-${validationState}`">
        <LoaderCircle v-if="validationState === 'loading'" class="spin" :size="14" />
        <template v-if="validationErrors.length">
          <span v-for="(error, index) in validationErrors" :key="`${error.ai}-${error.itemIndex}-${error.instanceIndices?.join('.')}-${index}`">
            {{ formatValidationError(error) }}
          </span>
        </template>
        <span v-else-if="validationState === 'valid'">Host 校验有效</span>
        <span v-else-if="validationState === 'pending'">等待绑定数据</span>
        <span v-else-if="validationState === 'error'">{{ validationErrors[0]?.message ?? 'Host 校验不可用' }}</span>
      </div>

      <details class="gs1-converter">
        <summary>文本转换</summary>
        <label class="field-label">待转换内容
          <textarea v-model="conversionText" class="field-input field-textarea" aria-label="待转换 GS1 内容" rows="3"
            :disabled="!canEdit" placeholder="[01]... 或 https://.../01/..." @input="overrideUriSettings = false" />
        </label>
        <label v-if="conversionSourceIsUri && conversionTarget === 'digital-link'" class="toggle-row">
          <span>应用当前设置覆盖来源 URI</span>
          <input v-model="overrideUriSettings" type="checkbox" aria-label="应用当前设置覆盖来源 URI" :disabled="!canEdit" />
        </label>
        <label class="field-label">目标模式
          <select v-model="conversionTarget" class="field-input" aria-label="GS1 转换目标" :disabled="!canEdit">
            <option value="elements">GS1 Elements</option>
            <option value="digital-link">Digital Link</option>
          </select>
        </label>
        <button class="wide-action" type="button" :disabled="!canEdit || converting || !conversionText.trim()" data-testid="gs1-convert"
          @click="convertInput"><LoaderCircle v-if="converting" class="spin" :size="14" /><ArrowLeftRight v-else :size="14" />转换并载入</button>
        <p v-if="conversionError" class="editor-error" role="alert">{{ conversionError }}</p>
      </details>
    </template>
  </section>
</template>

<style scoped>
.gs1-editor { display: grid; gap: 9px; padding-top: 10px; border-top: 1px solid var(--line, #d9dee5); }
.gs1-editor-heading, .gs1-item-heading, .gs1-item-actions, .gs1-add-row, .search-field, .gs1-query-row { display: flex; align-items: center; }
.gs1-editor-heading, .gs1-item-heading { justify-content: space-between; gap: 8px; }
.gs1-editor :deep(.section-title) { margin: 0; }
.gs1-editor :deep(.field-label) { display: grid; gap: 5px; }
.gs1-editor :deep(.field-input), .gs1-add-row select { min-width: 0; width: 100%; }
.gs1-add-row { gap: 6px; }
.search-field { flex: 1; gap: 6px; min-width: 90px; border: 1px solid var(--line, #cbd2dc); padding: 0 7px; height: 32px; }
.search-field input { min-width: 0; width: 100%; border: 0; outline: 0; background: transparent; color: inherit; }
.gs1-add-row select { flex: 1.1; }
.icon-action { display: inline-grid; place-items: center; width: 28px; height: 28px; flex: 0 0 28px; border: 1px solid var(--line, #d5dae1); border-radius: 4px; background: var(--surface, #fff); color: var(--text, #283341); }
.icon-action:disabled { opacity: .45; }
.wide-action { display: flex; justify-content: center; align-items: center; gap: 6px; width: 100%; }
.gs1-item { display: grid; gap: 6px; border-bottom: 1px solid var(--line, #e0e4e9); padding: 8px 0 10px; }
.gs1-item-heading strong { min-width: 0; overflow-wrap: anywhere; font-size: 12px; }
.gs1-item-actions { gap: 3px; flex: 0 0 auto; }
.gs1-constraints, .gs1-rules { color: var(--muted, #657181); font-size: 11px; line-height: 1.4; overflow-wrap: anywhere; }
.gs1-rules { font-family: ui-monospace, SFMono-Regular, Consolas, monospace; }
.gs1-value-mode { display: grid; grid-template-columns: 1fr 1fr; }
.gs1-query-section { display: grid; gap: 6px; }
.gs1-query-section .section-title { justify-content: space-between; align-items: center; }
.gs1-query-row { gap: 4px; min-width: 0; }
.gs1-query-row .field-input { flex: 1; width: 0; }
.editor-note, .gs1-validation { display: flex; align-items: center; gap: 6px; min-height: 18px; color: var(--muted, #657181); font-size: 12px; }
.gs1-validation.state-invalid, .gs1-item-error, .editor-error { color: var(--danger, #a12d2d); font-size: 12px; line-height: 1.4; overflow-wrap: anywhere; }
.gs1-validation.state-valid { color: var(--success, #317348); }
.gs1-item-error { padding: 5px 7px; background: color-mix(in srgb, #b42318 7%, transparent); border-radius: 4px; }
.editor-error { display: flex; justify-content: space-between; align-items: center; gap: 8px; }
.gs1-converter { border-top: 1px solid var(--line, #d9dee5); padding-top: 8px; }
.gs1-converter summary { cursor: pointer; color: var(--text, #283341); font-size: 12px; }
.gs1-converter[open] { display: grid; gap: 8px; }
.spin { animation: spin 1s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }
@media (max-width: 380px) {
  .gs1-add-row { flex-wrap: wrap; }
  .search-field { flex: 1 1 100%; }
  .gs1-add-row select { flex: 1; }
  .gs1-query-row { flex-wrap: wrap; }
  .gs1-query-row .field-input { flex: 1 1 40%; }
}
</style>
