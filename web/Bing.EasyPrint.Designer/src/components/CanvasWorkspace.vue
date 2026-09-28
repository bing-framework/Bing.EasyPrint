<script setup lang="ts">
import { computed, onBeforeUnmount, ref, shallowRef, watch } from 'vue'
import { Grid2X2, Minus, Plus, Ruler } from '@lucide/vue'
import type { BpTemplate, PreviewElement, PreviewPage, TemplateElement, TemplateResource } from '../model'
import { dotsToMm, getAlignmentSnap, snapDeltaToGrid } from '../coordinates'
import { findCell, findElement, findRow } from '../tree'

const props = defineProps<{
  template: BpTemplate
  page: PreviewPage | undefined
  selectedPath: string
  selectedPaths?: string[]
  zoom: number
  unit: 'pt' | 'mm'
  source: 'service' | 'local'
  pageIndex: number
  pageCount: number
  resources: TemplateResource[]
  mode: 'select' | 'pan'
}>()
const emit = defineEmits<{
  select: [path: string, additive?: boolean]
  move: [value: { path: string; dx: number; dy: number; rendered?: PreviewElement }]
  resize: [value: { path: string; width: number; height: number }]
  'update:zoom': [zoom: number]
  'page-change': [page: number]
  'drop-element': [value: { type: TemplateElement['Type']; x: number; y: number; cellPath?: string; multiline?: boolean }]
}>()

const scale = computed(() => 0.72 * props.zoom)
const pageWidth = computed(() => props.page?.width || props.template.Width)
const pageHeight = computed(() => props.page?.height || props.template.Height || 420)
const stageWidth = computed(() => Math.ceil(pageWidth.value * scale.value))
const stageHeight = computed(() => Math.ceil(pageHeight.value * scale.value))
const elements = computed(() => (props.page?.elements ?? [])
  .filter((element) => element.kind !== 'row' && element.kind !== 'cell' && element.type >= 1 && element.type <= 10))
const frames = computed(() => (props.page?.elements ?? []).filter((element) => element.kind === 'row' || element.kind === 'cell'))
const borderSides = ['Top', 'Right', 'Bottom', 'Left'] as const
const ticks = computed(() => Array.from({ length: Math.ceil(pageWidth.value / 40) + 1 }, (_, index) => index * 40).filter((value) => value <= pageWidth.value))
const verticalTicks = computed(() => Array.from({ length: Math.ceil(pageHeight.value / 40) + 1 }, (_, index) => index * 40).filter((value) => value <= pageHeight.value))
const imageAssets = shallowRef<Record<string, HTMLImageElement>>({})
const guides = ref<{ x?: number; y?: number }>({})
const stageHost = ref<HTMLDivElement>()
const pageFrame = ref<HTMLDivElement>()
let panStart: { x: number; y: number; left: number; top: number } | undefined

watch(() => props.resources.map((resource) => `${resource.key}\n${resource.dataUrl}`).join('\n'), () => {
  const next: Record<string, HTMLImageElement> = {}
  for (const resource of props.resources) {
    const existing = imageAssets.value[resource.key]
    if (existing?.src === resource.dataUrl && existing.complete) { next[resource.key] = existing; continue }
    const image = new Image()
    image.onload = () => { imageAssets.value = { ...imageAssets.value, [resource.key]: image } }
    image.src = resource.dataUrl
    next[resource.key] = image
  }
  imageAssets.value = next
}, { immediate: true })

onBeforeUnmount(() => {
  for (const image of Object.values(imageAssets.value)) image.src = ''
})

function nodeRect(element: PreviewElement) {
  const selected = element.sourcePath === props.selectedPath || Boolean(props.selectedPaths?.includes(element.sourcePath))
  const color = selected ? '#0a8792' : element.type === 1 ? '#315e64' : element.type === 2 ? '#9d6836' : element.type === 3 ? '#4f6e9e' : element.type === 4 ? '#587a5b' : '#79868a'
  return {
    x: element.x * scale.value,
    y: element.y * scale.value,
    width: Math.max(2, element.width * scale.value),
    height: Math.max(2, element.height * scale.value),
    fill: selected ? 'rgba(10, 135, 146, 0.10)' : 'rgba(255,255,255,0.86)',
    stroke: color,
    strokeWidth: selected ? 1.8 : 1,
    dash: selected ? [] : [4, 3],
    cornerRadius: 1,
  }
}

type KonvaDragEvent = { target: { x: { (): number; (value: number): unknown }; y: { (): number; (value: number): unknown } } }

function groupConfig(element: PreviewElement) {
  return {
    ...nodeRect(element),
    draggable: props.mode === 'select',
    listening: props.mode === 'select',
    onClick: (event: { evt?: { ctrlKey?: boolean; metaKey?: boolean; shiftKey?: boolean } }) => emit('select', element.sourcePath,
      Boolean(event.evt?.ctrlKey || event.evt?.metaKey || event.evt?.shiftKey)),
    onTap: () => emit('select', element.sourcePath),
    onDragStart: () => emit('select', element.sourcePath),
    onDragMove: (event: KonvaDragEvent) => onDragMove(element, event),
    onDragEnd: (event: KonvaDragEvent) => onDragEnd(element, event),
  }
}

function onDragMove(element: PreviewElement, event: KonvaDragEvent) {
  const definition = findElement(props.template, element.sourcePath)
  const start = definition?.StartCoordinate
  const dx = snapDeltaToGrid(start?.X ?? element.x, event.target.x() / scale.value - element.x)
  const dy = snapDeltaToGrid(start?.Y ?? element.y, event.target.y() / scale.value - element.y)
  const moving = { x: element.x + dx, y: element.y + dy, width: element.width, height: element.height }
  const others = elements.value.filter((item) => item.sourcePath !== element.sourcePath).map((item) => ({
    x: item.x, y: item.y, width: item.width, height: item.height,
  }))
  const alignment = getAlignmentSnap(moving, others, 6)
  guides.value = { x: alignment.x, y: alignment.y }
  event.target.x((moving.x + alignment.dx) * scale.value)
  event.target.y((moving.y + alignment.dy) * scale.value)
}

function onDragEnd(element: PreviewElement, event: KonvaDragEvent) {
  const definition = findElement(props.template, element.sourcePath)
  const rawDx = event.target.x() / scale.value - element.x
  const rawDy = event.target.y() / scale.value - element.y
  const dx = guides.value.x === undefined
    ? snapDeltaToGrid(definition?.StartCoordinate?.X ?? element.x, rawDx)
    : Math.round(rawDx)
  const dy = guides.value.y === undefined
    ? snapDeltaToGrid(definition?.StartCoordinate?.Y ?? element.y, rawDy)
    : Math.round(rawDy)
  guides.value = {}
  if (dx || dy) emit('move', { path: element.sourcePath, dx, dy, rendered: element })
}

function textConfig(element: PreviewElement) {
  const definition = findElement(props.template, element.sourcePath)
  const fontSize = definition?.Type === 1 ? (definition.FontSize || 24) : 14
  return {
    x: 0,
    y: 0,
    width: Math.max(1, element.width * scale.value + 2),
    height: Math.max(1, element.height * scale.value),
    text: element.text || (element.type === 1 ? '文字' : ''),
    fontSize: Math.max(8, fontSize * scale.value * 0.7),
    fontFamily: 'Arial, "Microsoft YaHei", sans-serif',
    fontStyle: definition?.Type === 1 && definition.Bold ? 'bold' : 'normal',
    fill: '#1c2e32',
    align: definition?.Type === 1 && definition.TextAlignment === 1 ? 'center' : definition?.Type === 1 && definition.TextAlignment === 2 ? 'right' : 'left',
    verticalAlign: 'middle',
    wrap: 'none',
    listening: false,
  }
}

function linePoints(element: PreviewElement): number[] {
  const definition = findElement(props.template, element.sourcePath)
  if (definition?.Type !== 5) return [0, 0, element.width * scale.value, 0]
  const start = definition.StartCoordinate ?? { X: 0, Y: 0 }
  const end = definition.LineEndCoordinate ?? definition.EndCoordinate ?? start
  const minX = Math.min(start.X, end.X)
  const minY = Math.min(start.Y, end.Y)
  return [(start.X - minX) * scale.value, (start.Y - minY) * scale.value, (end.X - minX) * scale.value, (end.Y - minY) * scale.value]
}

function lineWidth(element: PreviewElement): number {
  const definition = findElement(props.template, element.sourcePath)
  return definition?.Type === 5 ? definition.LineWidth || 1 : 1
}

function strokeWidth(element: PreviewElement): number {
  const definition = findElement(props.template, element.sourcePath)
  return definition && 'LineWidth' in definition ? Math.max(1, (definition.LineWidth ?? 1) * scale.value) : 1
}

function frameBorder(element: PreviewElement) {
  return element.kind === 'row' ? findRow(props.template, element.sourcePath)?.Border
    : findCell(props.template, element.sourcePath)?.Border
}

function borderStyle(element: PreviewElement, side: typeof borderSides[number]) {
  const border = frameBorder(element)
  return side === 'Top' ? border?.TopStyle : side === 'Right' ? border?.RightStyle
    : side === 'Bottom' ? border?.BottomStyle : border?.LeftStyle
}

function borderPoints(element: PreviewElement, side: 'Top' | 'Right' | 'Bottom' | 'Left') {
  const x = element.x * scale.value
  const y = element.y * scale.value
  const width = element.width * scale.value
  const height = element.height * scale.value
  if (side === 'Top') return [x, y, x + width, y]
  if (side === 'Right') return [x + width, y, x + width, y + height]
  if (side === 'Bottom') return [x, y + height, x + width, y + height]
  return [x, y, x, y + height]
}

function iconKind(element: PreviewElement) {
  const definition = findElement(props.template, element.sourcePath)
  return definition?.Type === 9 ? definition.Icon : 'check'
}

function resizable(element: PreviewElement) {
  return [2, 3, 4, 6, 7, 8, 9, 10].includes(element.type)
}

function resizeConfig(element: PreviewElement) {
  return {
    x: nodeRect(element).width,
    y: nodeRect(element).height,
    radius: 6,
    fill: '#fff',
    stroke: '#0a8792',
    strokeWidth: 1.5,
    draggable: true,
    onDragStart: (event: { cancelBubble: boolean }) => { event.cancelBubble = true },
    onDragMove: (event: { cancelBubble: boolean }) => { event.cancelBubble = true },
    onDragEnd: (event: { cancelBubble: boolean; target: { x: () => number; y: () => number } }) => {
      event.cancelBubble = true
      emit('resize', {
        path: element.sourcePath,
        width: Math.max(8, Math.round(event.target.x() / scale.value)),
        height: Math.max(8, Math.round(event.target.y() / scale.value)),
      })
    },
  }
}

function previewLabel(element: PreviewElement) {
  const values: Record<number, string> = { 2: '图片资源', 3: 'QR', 4: '条码', 6: '组合', 9: '图标', 10: '二维点阵码' }
  return values[element.type] ?? ''
}

function resourceImage(element: PreviewElement): HTMLImageElement | undefined {
  const key = element.resourceKey ?? element.text ?? ''
  const image = imageAssets.value[key]
  return image?.complete && image.naturalWidth > 0 ? image : undefined
}

function zoomBy(amount: number) {
  emit('update:zoom', Math.max(0.5, Math.min(1.5, Math.round((props.zoom + amount) * 100) / 100)))
}

function panDown(event: PointerEvent) {
  if (props.mode !== 'pan' || !stageHost.value) return
  panStart = { x: event.clientX, y: event.clientY, left: stageHost.value.scrollLeft, top: stageHost.value.scrollTop }
  stageHost.value.setPointerCapture(event.pointerId)
  event.preventDefault()
}

function panMove(event: PointerEvent) {
  if (!panStart || !stageHost.value) return
  stageHost.value.scrollLeft = panStart.left - (event.clientX - panStart.x)
  stageHost.value.scrollTop = panStart.top - (event.clientY - panStart.y)
}

function panUp(event: PointerEvent) {
  if (panStart && stageHost.value?.hasPointerCapture(event.pointerId)) stageHost.value.releasePointerCapture(event.pointerId)
  panStart = undefined
}

function dropTool(event: DragEvent) {
  const raw = event.dataTransfer?.getData('application/x-easyprint-element')
  const [typeText, variant] = (raw ?? '').split(':')
  const type = Number(typeText)
  if (!Number.isInteger(type) || type < 1 || type > 10 || !pageFrame.value) return
  const bounds = pageFrame.value.getBoundingClientRect()
  const x = Math.round((event.clientX - bounds.left) / scale.value)
  const y = Math.round((event.clientY - bounds.top) / scale.value)
  if (x < 0 || y < 0 || x >= pageWidth.value || y >= pageHeight.value) return
  const cell = (props.page?.elements ?? []).filter((item) => item.kind === 'cell' &&
    x >= item.x && x < item.x + item.width && y >= item.y && y < item.y + item.height)
    .sort((a, b) => b.sourcePath.length - a.sourcePath.length)[0]
  emit('drop-element', { type: type as TemplateElement['Type'], x: x - (cell?.x ?? 0), y: y - (cell?.y ?? 0), cellPath: cell?.sourcePath,
    multiline: variant === 'multiline' })
}
</script>

<template>
    <section class="canvas-workspace" title="拖动对象吸附至 4 点网格；选择对象后使用方向键微调，Shift + 方向键移动 10 点">
    <div class="canvas-header">
      <div class="canvas-breadcrumb"><span>画布</span><span class="breadcrumb-sep">/</span><strong>{{ pageWidth }} × {{ pageHeight }} 点</strong><span class="dimension-conversion">{{ dotsToMm(pageWidth) }} × {{ dotsToMm(pageHeight) }} mm</span></div>
      <div class="canvas-controls">
        <span class="preview-source" :class="source"><i></i>{{ source === 'service' ? '引擎预览' : '本地近似' }}</span>
        <span class="control-divider"></span>
        <button class="icon-button" type="button" title="缩小画布" @click="zoomBy(-0.1)"><Minus :size="15" /></button>
        <span class="zoom-value">{{ Math.round(zoom * 100) }}%</span>
        <button class="icon-button" type="button" title="放大画布" @click="zoomBy(0.1)"><Plus :size="15" /></button>
        <span class="control-divider"></span>
        <span class="canvas-unit"><Ruler :size="14" />{{ unit === 'pt' ? '打印点' : '毫米' }}</span>
      </div>
    </div>
    <div class="ruler-label horizontal"><span v-for="tick in ticks" :key="tick" :style="{ left: `${tick * scale + 34}px` }">{{ unit === 'mm' ? dotsToMm(tick) : tick }}</span></div>
    <div class="ruler-label vertical"><span v-for="tick in verticalTicks" :key="tick" :style="{ top: `${tick * scale + 34}px` }">{{ unit === 'mm' ? dotsToMm(tick) : tick }}</span></div>
    <div class="canvas-scroll" ref="stageHost" :class="{ 'pan-mode': mode === 'pan' }" @pointerdown="panDown" @pointermove="panMove" @pointerup="panUp" @pointercancel="panUp" @dragover.prevent @drop.prevent="dropTool">
      <div class="canvas-origin"><Ruler :size="13" /><span>{{ unit === 'mm' ? 'mm' : 'pt' }}</span></div>
      <div class="page-frame" ref="pageFrame" :style="{ width: `${stageWidth}px`, height: `${stageHeight}px` }">
        <v-stage :config="{ width: stageWidth, height: stageHeight }">
          <v-layer>
            <v-rect :config="{ x: 0, y: 0, width: stageWidth, height: stageHeight, fill: '#ffffff', stroke: '#c8d1d3', strokeWidth: 1, shadowColor: 'rgba(20, 38, 41, 0.16)', shadowBlur: 18, shadowOffset: { x: 0, y: 7 }, shadowOpacity: 0.7 }" />
            <template v-for="tick in ticks" :key="`grid-x-${tick}`">
              <v-line :config="{ points: [tick * scale, 0, tick * scale, stageHeight], stroke: tick % 200 === 0 ? '#e6ebec' : '#f0f3f4', strokeWidth: 1, listening: false }" />
            </template>
            <template v-for="tick in verticalTicks" :key="`grid-y-${tick}`">
              <v-line :config="{ points: [0, tick * scale, stageWidth, tick * scale], stroke: tick % 200 === 0 ? '#e6ebec' : '#f0f3f4', strokeWidth: 1, listening: false }" />
            </template>
            <template v-for="frame in frames" :key="`border-${frame.sourcePath}-${frame.instanceIndices?.join('.') ?? ''}`">
              <template v-for="side in borderSides" :key="side">
                <v-line v-if="borderStyle(frame, side)" :config="{ points: borderPoints(frame, side), stroke: '#46595d', strokeWidth: 1,
                  dash: borderStyle(frame, side) === 2 ? [4, 3] : [], listening: false }" />
              </template>
            </template>
            <v-group v-for="element in elements" :key="`${element.sourcePath}-${element.instanceIndices?.join('.') ?? ''}`" :config="groupConfig(element)">
              <template v-if="element.type === 1">
                <v-rect :config="{ x: 0, y: 0, width: nodeRect(element).width, height: nodeRect(element).height, fill: nodeRect(element).fill, stroke: nodeRect(element).stroke, strokeWidth: nodeRect(element).strokeWidth, dash: nodeRect(element).dash, listening: false }" />
                <v-text :config="textConfig(element)" />
              </template>
              <template v-else-if="element.type === 5">
                <v-line :config="{ points: linePoints(element), stroke: '#73584c', strokeWidth: Math.max(1, lineWidth(element) * scale), lineCap: 'round', listening: false }" />
              </template>
              <template v-else-if="element.type === 7">
                <v-rect :config="{ x: 0, y: 0, width: nodeRect(element).width, height: nodeRect(element).height, stroke: '#53656a', strokeWidth: strokeWidth(element), listening: false }" />
              </template>
              <template v-else-if="element.type === 8">
                <v-ellipse :config="{ x: nodeRect(element).width / 2, y: nodeRect(element).height / 2, radiusX: nodeRect(element).width / 2, radiusY: nodeRect(element).height / 2, stroke: '#53656a', strokeWidth: strokeWidth(element), listening: false }" />
              </template>
              <template v-else-if="element.type === 6">
                <v-rect :config="{ x: 0, y: 0, width: nodeRect(element).width, height: nodeRect(element).height, stroke: '#4a8690', strokeWidth: 1, dash: [5, 3], listening: false }" />
              </template>
              <template v-else-if="element.type === 9">
                <v-rect :config="{ x: 0, y: 0, width: nodeRect(element).width, height: nodeRect(element).height, stroke: '#72898d', strokeWidth: 1, dash: [3, 2], listening: false }" />
                <v-line v-if="iconKind(element) === 'check'" :config="{ points: [nodeRect(element).width * .18, nodeRect(element).height * .52, nodeRect(element).width * .42, nodeRect(element).height * .75, nodeRect(element).width * .84, nodeRect(element).height * .22], stroke: '#263f43', strokeWidth: strokeWidth(element), listening: false }" />
                <template v-else-if="iconKind(element) === 'cross'">
                  <v-line v-for="points in [[.2, .2, .8, .8], [.8, .2, .2, .8]]" :key="points.join('-')" :config="{ points: [points[0] * nodeRect(element).width, points[1] * nodeRect(element).height, points[2] * nodeRect(element).width, points[3] * nodeRect(element).height], stroke: '#263f43', strokeWidth: strokeWidth(element), listening: false }" />
                </template>
                <template v-else>
                  <v-circle :config="{ x: nodeRect(element).width / 2, y: nodeRect(element).height / 2, radius: Math.min(nodeRect(element).width, nodeRect(element).height) * .4, stroke: '#263f43', strokeWidth: strokeWidth(element), listening: false }" />
                  <v-text :config="{ ...textConfig(element), text: 'i', align: 'center', fontSize: Math.max(10, element.height * scale * .5) }" />
                </template>
              </template>
              <template v-else-if="element.type === 3">
                <v-rect :config="{ x: 0, y: 0, width: nodeRect(element).width, height: nodeRect(element).height, fill: '#fff', stroke: nodeRect(element).stroke, strokeWidth: 1, listening: false }" />
                <v-text :config="{ ...textConfig(element), text: previewLabel(element), fontSize: Math.max(9, 13 * scale), align: 'center', fill: '#4b6490' }" />
              </template>
              <template v-else-if="element.type === 4">
                <v-rect :config="{ x: 0, y: 0, width: nodeRect(element).width, height: nodeRect(element).height, fill: '#fff', stroke: nodeRect(element).stroke, strokeWidth: 1, listening: false }" />
                <v-line v-for="bar in 20" :key="bar" :config="{ points: [bar * nodeRect(element).width / 21, 5 * scale, bar * nodeRect(element).width / 21, Math.max(8 * scale, nodeRect(element).height - 5 * scale)], stroke: bar % 3 ? '#253b33' : '#83918a', strokeWidth: bar % 4 === 0 ? 2 * scale : 1 * scale, listening: false }" />
              </template>
              <template v-else-if="element.type === 2">
                <v-image v-if="resourceImage(element)" :config="{ x: 0, y: 0, width: nodeRect(element).width, height: nodeRect(element).height, image: resourceImage(element), listening: false }" />
                <template v-else>
                  <v-rect :config="{ x: 0, y: 0, width: nodeRect(element).width, height: nodeRect(element).height, fill: '#f4f1e9', stroke: nodeRect(element).stroke, strokeWidth: 1, dash: [4, 3], listening: false }" />
                  <v-text :config="{ ...textConfig(element), text: previewLabel(element), fontSize: Math.max(9, 13 * scale), align: 'center', fill: '#805d35' }" />
                </template>
              </template>
              <template v-else>
                <v-rect :config="{ x: 0, y: 0, width: nodeRect(element).width, height: nodeRect(element).height, fill: '#f4f1e9', stroke: nodeRect(element).stroke, strokeWidth: 1, dash: [4, 3], listening: false }" />
                <v-text :config="{ ...textConfig(element), text: previewLabel(element), fontSize: Math.max(9, 13 * scale), align: 'center', fill: '#805d35' }" />
              </template>
              <v-rect :config="{ x: -4, y: -4, width: Math.max(12, nodeRect(element).width + 8), height: Math.max(12, nodeRect(element).height + 8), fill: '#ffffff', opacity: 0.001, listening: true, perfectDrawEnabled: false }" />
              <v-rect v-if="selectedPath === element.sourcePath || selectedPaths?.includes(element.sourcePath)" :config="{ x: -3, y: -3, width: nodeRect(element).width + 6, height: nodeRect(element).height + 6, stroke: '#0a8792', strokeWidth: 1, dash: [3, 2], listening: false }" />
              <v-circle v-if="selectedPath === element.sourcePath && resizable(element) && mode === 'select'" :config="resizeConfig(element)" />
            </v-group>
            <v-line v-if="guides.x !== undefined" :config="{ name: 'alignment-guide-x', points: [guides.x * scale, 0, guides.x * scale, stageHeight], stroke: '#d0814f', strokeWidth: 1, dash: [4, 3], listening: false }" />
            <v-line v-if="guides.y !== undefined" :config="{ name: 'alignment-guide-y', points: [0, guides.y * scale, stageWidth, guides.y * scale], stroke: '#d0814f', strokeWidth: 1, dash: [4, 3], listening: false }" />
          </v-layer>
        </v-stage>
      </div>
    </div>
    <footer class="canvas-footer">
      <div class="canvas-legend"><span><i class="legend-swatch text"></i>文字</span><span><i class="legend-swatch code"></i>码制</span><span><i class="legend-swatch line"></i>线段</span><span><Grid2X2 :size="13" />网格 40 点</span></div>
      <div v-if="pageCount > 1" class="page-pager">
        <button class="icon-button" type="button" :disabled="pageIndex === 0" title="上一页" @click="emit('page-change', pageIndex - 1)">‹</button>
        <span>{{ pageIndex + 1 }} / {{ pageCount }}</span>
        <button class="icon-button" type="button" :disabled="pageIndex >= pageCount - 1" title="下一页" @click="emit('page-change', pageIndex + 1)">›</button>
      </div>
      <span v-else class="canvas-footer-note">拖动对象调整位置 · 单位以打印点保存</span>
    </footer>
  </section>
</template>
