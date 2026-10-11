<script setup lang="ts">
import { computed } from 'vue'
import { ChevronDown, ChevronRight, FileText, Layers3, Rows3, Type } from '@lucide/vue'
import { listTemplateTree, type TreeEntry } from '../tree'
import type { BpTemplate } from '../model'

const props = defineProps<{ template: BpTemplate; selectedPath: string; selectedPaths?: string[]; collapsed: boolean; query?: string }>()
const emit = defineEmits<{ select: [path: string, additive?: boolean, occurrencePath?: string]; toggle: [] }>()
const entries = computed(() => {
  const all = listTemplateTree(props.template)
  const query = props.query?.trim().toLocaleLowerCase()
  return query ? all.filter((entry) => entry.label.toLocaleLowerCase().includes(query)) : all
})
const typeLabel: Record<number, string> = { 1: '文字', 2: '图片', 3: '二维码', 4: '条码', 5: '线段' }

function iconFor(entry: TreeEntry) {
  if (entry.kind === 'component') return Layers3
  if (entry.kind === 'row') return Rows3
  if (entry.kind === 'cell') return Layers3
  if (entry.type === 1) return Type
  return FileText
}
</script>

<template>
  <section class="tree-panel" :class="{ 'is-collapsed': collapsed }">
    <header class="panel-heading">
      <div class="panel-title"><Layers3 :size="15" /><span>结构</span></div>
      <button class="icon-button quiet" type="button" title="收起结构面板" @click="emit('toggle')"><ChevronDown :size="15" /></button>
    </header>
    <div v-if="!collapsed" class="tree-content">
      <button class="tree-row page-tree-row" :class="{ selected: selectedPath === '' }" type="button" @click="emit('select', '')">
        <span class="tree-indent"></span><FileText :size="14" /><span class="tree-label">页面</span><span class="tree-meta">{{ template.Width }} × {{ template.Height }}</span>
      </button>
      <div v-for="entry in entries" :key="entry.key" class="tree-row" :class="[`tree-${entry.kind}`, { selected: selectedPath === entry.sourcePath || selectedPaths?.includes(entry.sourcePath) }]">
        <span class="tree-indent" :style="{ width: `${entry.depth * 14 + 8}px` }"></span>
        <ChevronRight v-if="entry.kind === 'row' || entry.kind === 'cell' || entry.kind === 'component'" :size="12" class="tree-caret" />
        <component :is="iconFor(entry)" :size="14" class="tree-icon" />
        <button class="tree-select" type="button" :title="entry.path !== entry.sourcePath ? `编辑共享定义 ${entry.sourcePath}` : entry.path" @click="emit('select', entry.sourcePath, $event.ctrlKey || $event.metaKey || $event.shiftKey, entry.path)">
          <span class="tree-label">{{ entry.label }}</span>
          <span v-if="entry.type" class="tree-meta">{{ typeLabel[entry.type] }}</span>
        </button>
      </div>
      <div v-if="entries.length === 0" class="empty-hint">模板还没有行或元素</div>
    </div>
    <div v-else class="collapsed-label">结<br />构</div>
  </section>
</template>
