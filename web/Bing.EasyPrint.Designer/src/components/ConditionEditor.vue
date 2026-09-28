<script setup lang="ts">
import { computed } from 'vue'
import { Plus, Trash2 } from '@lucide/vue'
import type { Condition } from '../model'

const props = withDefaults(defineProps<{ modelValue?: Condition; depth?: number }>(), { depth: 0 })
const emit = defineEmits<{ 'update:modelValue': [value: Condition | undefined] }>()
const current = computed(() => props.modelValue)
const isGroup = computed(() => current.value?.Operator === 'and' || current.value?.Operator === 'or')

function update(patch: Partial<Condition>) {
  const next = { ...(current.value ?? { Operator: 'eq' as const }), ...patch }
  emit('update:modelValue', next)
}

function updateChild(index: number, condition: Condition | undefined) {
  const children = [...(current.value?.Conditions ?? [])]
  if (condition) children[index] = condition
  else children.splice(index, 1)
  update({ Conditions: children })
}

function addChild() {
  const children = [...(current.value?.Conditions ?? []), { Path: '', Operator: 'eq' as const, Value: '' }]
  update({ Conditions: children })
}

function parseScalar(value: string): unknown {
  if (value === 'true') return true
  if (value === 'false') return false
  if (value === 'null') return null
  if (/^-?(?:0|[1-9]\d*)(?:\.\d+)?$/.test(value)) return Number(value)
  return value
}

const scalarValue = computed(() => current.value?.Value == null ? '' : String(current.value.Value))
</script>

<template>
  <div class="condition-editor" :class="{ 'condition-nested': depth > 0 }">
    <div class="condition-row">
      <input class="field-input condition-path" :value="current?.Path ?? ''" placeholder="字段路径，如 IsPromotion" :disabled="isGroup" @input="update({ Path: ($event.target as HTMLInputElement).value })" />
      <select class="field-input condition-op" :value="current?.Operator ?? 'eq'" @change="update({ Operator: ($event.target as HTMLSelectElement).value as Condition['Operator'], Conditions: isGroup ? current?.Conditions ?? [] : undefined })">
        <option value="eq">等于</option><option value="ne">不等于</option><option value="gt">大于</option><option value="ge">大于等于</option><option value="lt">小于</option><option value="le">小于等于</option><option value="isNull">为空</option><option value="notNull">不为空</option><option value="and">且</option><option value="or">或</option>
      </select>
      <input v-if="!isGroup && current?.Operator !== 'isNull' && current?.Operator !== 'notNull'" class="field-input condition-value" :value="scalarValue" placeholder="值" @input="update({ Value: parseScalar(($event.target as HTMLInputElement).value) })" />
    </div>
    <div v-if="isGroup" class="condition-children">
      <ConditionEditor v-for="(child, index) in current?.Conditions ?? []" :key="index" :model-value="child" :depth="depth + 1" @update:model-value="updateChild(index, $event)" />
      <button v-if="depth < 4" class="text-button condition-add" type="button" @click="addChild"><Plus :size="13" /> 添加子条件</button>
      <span v-else class="field-note">条件嵌套最多 4 层</span>
    </div>
    <button v-if="depth > 0" class="icon-button quiet condition-remove" type="button" title="删除条件" @click="emit('update:modelValue', undefined)"><Trash2 :size="13" /></button>
  </div>
</template>
