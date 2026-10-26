<script setup lang="ts">
import { ref } from 'vue'
import type { FieldFormat, FieldFormatKind } from '../model'

const props = defineProps<{
  format?: FieldFormat
  title?: string
}>()

const emit = defineEmits<{ 'update:format': [FieldFormat | undefined] }>()
const mapError = ref('')

function copyFormat(): FieldFormat {
  return {
    ...(props.format ?? {}),
    ...(props.format?.Map === undefined ? {} : { Map: { ...props.format.Map } }),
  }
}

function enable() {
  emit('update:format', props.format ?? { Kind: 'Text', PadCharacter: '0' })
}

function disable() {
  mapError.value = ''
  emit('update:format', undefined)
}

function update(key: keyof FieldFormat, value: string | number | undefined) {
  const next = copyFormat()
  if (value === undefined || value === '') delete (next as Record<string, unknown>)[key]
  else (next as Record<string, unknown>)[key] = value
  emit('update:format', next)
}

function updateKind(event: Event) {
  update('Kind', (event.target as HTMLSelectElement).value as FieldFormatKind)
}

function updatePadLength(event: Event) {
  const value = Number((event.target as HTMLInputElement).value)
  update('PadLength', Number.isInteger(value) && value >= 0 ? value : undefined)
}

function updateMap(event: Event) {
  const text = (event.target as HTMLTextAreaElement).value
  try {
    const parsed = JSON.parse(text || '{}') as unknown
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed) ||
      Object.entries(parsed).some(([key, value]) => !key || typeof value !== 'string')) {
      throw new Error('映射必须是非空键和字符串值的 JSON 对象。')
    }
    const next = copyFormat()
    next.Map = Object.fromEntries(Object.entries(parsed as Record<string, string>))
    mapError.value = ''
    emit('update:format', next)
  } catch (error) {
    mapError.value = error instanceof Error ? error.message : '映射 JSON 无效。'
  }
}
</script>

<template>
  <div class="field-format-editor" data-testid="field-format-editor">
    <div class="section-title"><span>{{ title ?? '字段格式' }}</span><span class="section-kicker">Format</span></div>
    <template v-if="format">
      <label class="field-label">类型
        <select class="field-input" data-testid="field-format-kind" :value="format.Kind ?? 'Text'" @change="updateKind">
          <option value="Text">文本</option>
          <option value="Number">数字</option>
          <option value="Currency">金额</option>
          <option value="Date">日期</option>
          <option value="Enum">枚举映射</option>
        </select>
      </label>
      <label v-if="format.Kind !== 'Text' && format.Kind !== 'Enum'" class="field-label">输出格式
        <input class="field-input" data-testid="field-format-pattern" :value="format.Pattern ?? ''" placeholder="例如 N2 / C2 / yyyy-MM-dd" @change="update('Pattern', ($event.target as HTMLInputElement).value)" />
      </label>
      <label v-if="format.Kind === 'Date'" class="field-label">输入格式 <span>可空，ISO 默认</span>
        <input class="field-input" :value="format.InputFormat ?? ''" placeholder="例如 yyyy-MM-ddTHH:mm:sszzz" @change="update('InputFormat', ($event.target as HTMLInputElement).value)" />
      </label>
      <label v-if="format.Kind !== 'Text' && format.Kind !== 'Enum'" class="field-label">文化 <span>默认 Invariant</span>
        <input class="field-input" data-testid="field-format-culture" :value="format.Culture ?? ''" placeholder="例如 zh-CN" @change="update('Culture', ($event.target as HTMLInputElement).value)" />
      </label>
      <label class="field-label">空值文本
        <input class="field-input" :value="format.NullText ?? ''" placeholder="仅实际 null 使用" @change="update('NullText', ($event.target as HTMLInputElement).value)" />
      </label>
      <div class="numeric-grid">
        <label class="field-label">补齐长度
          <input class="field-input" type="number" min="0" step="1" :value="format.PadLength ?? ''" @change="updatePadLength" />
        </label>
        <label class="field-label">补齐字符
          <input class="field-input" maxlength="1" :value="format.PadCharacter ?? '0'" @change="update('PadCharacter', ($event.target as HTMLInputElement).value)" />
        </label>
      </div>
      <label class="field-label">前缀
        <input class="field-input" :value="format.Prefix ?? ''" @change="update('Prefix', ($event.target as HTMLInputElement).value)" />
      </label>
      <label class="field-label">后缀
        <input class="field-input" :value="format.Suffix ?? ''" @change="update('Suffix', ($event.target as HTMLInputElement).value)" />
      </label>
      <label v-if="format.Kind === 'Enum'" class="field-label">映射 JSON
        <textarea class="field-input field-textarea" rows="4" :value="JSON.stringify(format.Map ?? {}, null, 2)" @change="updateMap" />
      </label>
      <p v-if="mapError" class="field-note capability-warning">{{ mapError }}</p>
      <button class="wide-action" type="button" @click="disable">移除字段格式</button>
    </template>
    <button v-else class="wide-action" data-testid="field-format-add" type="button" @click="enable">添加字段格式</button>
  </div>
</template>
