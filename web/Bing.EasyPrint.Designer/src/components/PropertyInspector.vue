<script setup lang="ts">
import { computed, ref } from 'vue'
import { AlignCenter, AlignLeft, AlignRight, Braces, ChevronDown, FileText, Layers3, Ruler, Type } from '@lucide/vue'
import type { BpTemplate, Cell, Condition, FontElement, Row, TemplateElement } from '../model'
import { dotsToMm, mmToDots } from '../coordinates'
import { bindImageResource } from '../binding'
import ConditionEditor from './ConditionEditor.vue'

const props = defineProps<{
  template: BpTemplate
  selectedPath: string
  element?: TemplateElement
  row?: Row
  cell?: Cell
  unit: 'pt' | 'mm'
  resources: Array<{ key: string; fileName: string }>
  fieldPaths: string[]
}>()
const emit = defineEmits<{ 'toggle-condition': [value: boolean]; 'update-condition': [value: Condition | undefined]; 'set-position': [axis: 'X' | 'Y', value: number]; 'add-row': []; 'add-column': []; 'add-nested-row': []; remove: [] }>()
const typeName: Record<number, string> = { 1: '文字', 2: '图片', 3: '二维码', 4: '一维条码', 5: '线段', 6: '组合', 7: '矩形', 8: '椭圆', 9: '图标', 10: '二维点阵码' }
const variableName = ref('')
const variablePath = ref('')
const isPage = computed(() => !props.selectedPath)
const isTopLevelRow = computed(() => /^Rows\[\d+\]$/.test(props.selectedPath))
const title = computed(() => isPage.value ? '页面属性' : props.element ? `${typeName[props.element.Type]}属性` : props.row ? '行属性' : props.cell ? '列属性' : '属性')
const barcodeOptions = [
  ['CODE 128', 0], ['CODE 39', 1], ['CODE 93', 2], ['Codabar', 3], ['EAN-8', 4], ['EAN-13', 5], ['UPC-A', 6], ['UPC-E', 7], ['I2OF5', 8], ['I2OF5C', 9], ['I2OF5G', 10], ['UCC/EAN-128', 11], ['MSI', 12], ['POSTNET', 13], ['FIM', 14],
] as const
const fontSizes = [16, 24, 32, 48, 64, 72, 80, 96, 112, 120, 128, 144, 160, 168, 176, 192, 208, 216, 224, 240, 256, 264, 288, 312, 336, 360, 384]
const barcodeRatios = [
  ...[0, 1, 2, 3, 4].map((value) => ({ value, label: `${value} · ${[1.5, 2, 2.5, 3, 3.5][value]}:1` })),
  ...Array.from({ length: 11 }, (_, index) => ({ value: index + 20, label: `${index + 20} · ${(2 + index / 10).toFixed(1)}:1` })),
]

function nativeFont(size: number): number {
  return size % 24 === 0 ? 24 : 55
}

function setFontSize(element: FontElement, event: Event) {
  const size = Number((event.target as HTMLSelectElement).value)
  if (!fontSizes.includes(size)) return
  element.FontSize = size
  if (element.FontName !== 0 && element.FontName !== nativeFont(size)) element.FontName = 0
}

function dimensionValue(value: number | undefined): string {
  const actual = value ?? 0
  return props.unit === 'mm' ? String(dotsToMm(actual)) : String(actual)
}

function setDimension(target: Record<string, unknown>, key: string, event: Event) {
  const value = Number((event.target as HTMLInputElement).value)
  if (!Number.isFinite(value)) return
  target[key] = props.unit === 'mm' ? mmToDots(value) : Math.round(value)
}

function setElementPosition(axis: 'X' | 'Y', event: Event) {
  const value = Number((event.target as HTMLInputElement).value)
  if (!Number.isFinite(value)) return
  const target = props.unit === 'mm' ? mmToDots(value) : Math.round(value)
  emit('set-position', axis, target)
}

function setNumber(target: Record<string, unknown>, key: string, event: Event) {
  const value = Number((event.target as HTMLInputElement).value)
  if (Number.isFinite(value)) target[key] = Math.round(value)
}

function mmOrDots(value: number) {
  return props.unit === 'mm' ? mmToDots(value) : Math.round(value)
}

function elementDimensionValue(element: TemplateElement, axis: 'X' | 'Y'): string {
  const start = axis === 'X' ? element.StartCoordinate?.X ?? 0 : element.StartCoordinate?.Y ?? 0
  if (element.Type === 9) return dimensionValue(element.Size)
  if (element.Type === 6 || element.Type === 7 || element.Type === 8 || element.Type === 10)
    return dimensionValue(axis === 'X' ? element.Width : element.Height)
  if (element.Type === 2) return dimensionValue(axis === 'X' ? element.Width : element.Height)
  const end = element.EndCoordinate
  if (end) return dimensionValue(Math.max(0, (axis === 'X' ? end.X : end.Y) - start))
  if (element.Type === 3 || element.Type === 4) return dimensionValue(axis === 'X' ? element.Width : element.Height)
  return dimensionValue(axis === 'X' ? props.template.Width : props.template.Height)
}

function setElementDimension(element: TemplateElement, axis: 'X' | 'Y', event: Event) {
  const input = event.target as HTMLInputElement
  const value = Number(input.value)
  if (!Number.isFinite(value)) return
  const size = Math.max(1, mmOrDots(value))
  if (element.Type === 9) { element.Size = size; return }
  if (element.Type === 6 || element.Type === 7 || element.Type === 8 || element.Type === 10) {
    if (axis === 'X') element.Width = size
    else element.Height = size
    return
  }
  const start = element.StartCoordinate ?? { X: 0, Y: 0 }
  element.StartCoordinate ??= start
  element.EndCoordinate ??= { X: start.X + size, Y: start.Y + size }
  element.EndCoordinate[axis] = (axis === 'X' ? start.X : start.Y) + size
  if (element.Type === 2 || element.Type === 3 || element.Type === 4) {
    if (axis === 'X') element.Width = size
    else element.Height = size
  }
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

function setMatrixFormat(element: Extract<TemplateElement, { Type: 10 }>, event: Event) {
  element.Format = (event.target as HTMLSelectElement).value as 'DataMatrix' | 'PDF417'
  if (element.Format === 'PDF417') {
    element.Width = Math.max(element.Width, 256)
    element.Height = Math.max(element.Height, 80)
  }
}

function barcodeRatioIsValid(value: number | undefined): boolean {
  return value !== undefined && (value >= 0 && value <= 4 || value >= 20 && value <= 30)
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

function updateBorder(key: string, event: Event) {
  const value = Number((event.target as HTMLSelectElement).value)
  if (props.row) { props.row.Border ??= {}; props.row.Border[key as 'TopStyle'] = value }
  else if (props.cell) { props.cell.Border ??= {}; props.cell.Border[key as 'TopStyle'] = value }
}

function updatePageBorder(key: string, event: Event) {
  const value = Number((event.target as HTMLSelectElement).value)
  props.template.Border ??= {}
  props.template.Border[key as 'TopStyle'] = value
}

function setElementMargin(element: TemplateElement, side: 'Top' | 'Right' | 'Bottom' | 'Left', event: Event) {
  const value = Number((event.target as HTMLInputElement).value)
  if (!Number.isFinite(value)) return
  element.Margin ??= {}
  element.Margin[side] = props.unit === 'mm' ? mmToDots(value) : Math.round(value)
}

function setFontStyleBit(element: FontElement, bit: number, enabled: boolean) {
  const style = element.FontStyle ?? 0
  element.FontStyle = enabled ? style | bit : style & ~bit
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

const conditionTarget = computed(() => props.element ?? props.row)
const conditionValue = computed(() => conditionTarget.value?.Condition)
const selectedImageMissing = computed(() => props.element?.Type === 2 && !props.element.Field?.Name && Boolean(props.element.Field?.Value) && !props.resources.some((resource) => resource.key === props.element?.Field?.Value))

function selectImageResource(event: Event) {
  if (props.element?.Type !== 2) return
  props.element.Field = bindImageResource(props.element.Field, (event.target as HTMLSelectElement).value)
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

      <template v-if="isPage">
        <section class="inspector-section">
          <div class="section-title"><span>纸张尺寸</span><Ruler :size="13" /></div>
          <label class="field-label">宽度 <span>{{ unit }}</span><input class="field-input" type="number" min="1" :value="dimensionValue(template.Width)" @input="setDimension(template as unknown as Record<string, unknown>, 'Width', $event)" /></label>
          <label class="field-label">高度 <span>{{ unit }}</span><input class="field-input" type="number" min="0" :value="dimensionValue(template.Height)" @input="setDimension(template as unknown as Record<string, unknown>, 'Height', $event)" /></label>
          <div class="dimension-note">{{ dotsToMm(template.Width) }} × {{ dotsToMm(template.Height) }} mm · 200 DPI</div>
          <label class="toggle-row"><span>按内容计算页高</span><input type="checkbox" :checked="template.AutoHeight" @change="template.AutoHeight = ($event.target as HTMLInputElement).checked" /></label>
          <p v-if="template.AutoHeight && template.Height === 0" class="field-note">Height 为 0 时由渲染选项限制单页最大高度。</p>
          <div class="section-title border-title"><span>页面边框</span></div>
          <div class="numeric-grid">
            <label class="field-label">上<select class="field-input" :value="template.Border?.TopStyle ?? 0" @change="updatePageBorder('TopStyle', $event)"><option :value="0">无</option><option :value="1">实线</option><option :value="2">虚线</option></select></label>
            <label class="field-label">右<select class="field-input" :value="template.Border?.RightStyle ?? 0" @change="updatePageBorder('RightStyle', $event)"><option :value="0">无</option><option :value="1">实线</option><option :value="2">虚线</option></select></label>
            <label class="field-label">下<select class="field-input" :value="template.Border?.BottomStyle ?? 0" @change="updatePageBorder('BottomStyle', $event)"><option :value="0">无</option><option :value="1">实线</option><option :value="2">虚线</option></select></label>
            <label class="field-label">左<select class="field-input" :value="template.Border?.LeftStyle ?? 0" @change="updatePageBorder('LeftStyle', $event)"><option :value="0">无</option><option :value="1">实线</option><option :value="2">虚线</option></select></label>
          </div>
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

      <template v-else-if="element">
        <section v-if="[1, 2, 3, 4, 10].includes(element.Type)" class="inspector-section">
          <div class="section-title"><span>内容绑定</span><span class="section-kicker">Field</span></div>
          <div class="segmented-control">
            <button type="button" :class="{ active: Boolean(element.Field?.Name) }" @click="setFieldMode('bind')">字段</button>
            <button type="button" :class="{ active: !element.Field?.Name }" @click="setFieldMode('static')">固定文字</button>
          </div>
          <label v-if="element.Field?.Name !== undefined" class="field-label">字段路径<input class="field-input" list="business-field-paths" :value="element.Field.Name" placeholder="例如 Product.Name" @input="updateFieldName" /><datalist id="business-field-paths"><option v-for="path in fieldPaths" :key="path" :value="path" /></datalist></label>
          <label v-else class="field-label">显示内容<textarea class="field-input field-textarea" :value="element.Field?.Value ?? ''" rows="3" placeholder="输入打印内容" @input="updateFieldValue" /></label>
          <template v-if="element.Type === 1">
            <label class="toggle-row"><span>文字换行</span><input type="checkbox" :checked="element.Field?.NeedFeedLine !== false" @change="element.Field ??= {}; element.Field.NeedFeedLine = ($event.target as HTMLInputElement).checked" /></label>
            <label class="toggle-row"><span>适配可用宽度</span><input type="checkbox" :checked="element.Field?.Fit" @change="element.Field ??= {}; element.Field.Fit = ($event.target as HTMLInputElement).checked" /></label>
          </template>
          <div v-if="element.Type === 2 && resources.length" class="resource-status">图片包已包含 {{ resources.length }} 个资源</div>
          <div v-if="selectedImageMissing" class="resource-missing">当前资源键尚未导入</div>
        </section>

        <section class="inspector-section">
          <div class="section-title"><span>位置与尺寸</span><span class="section-kicker">{{ unit }}</span></div>
          <div class="numeric-grid">
            <label class="field-label">X<input class="field-input" type="number" :value="dimensionValue(element.StartCoordinate?.X)" @input="setElementPosition('X', $event)" /></label>
            <label class="field-label">Y<input class="field-input" type="number" :value="dimensionValue(element.StartCoordinate?.Y)" @input="setElementPosition('Y', $event)" /></label>
            <label v-if="element.Type !== 5" class="field-label">宽度<input class="field-input" type="number" min="1" :value="elementDimensionValue(element, 'X')" @input="setElementDimension(element, 'X', $event)" /></label>
            <label v-if="element.Type !== 5" class="field-label">高度<input class="field-input" type="number" min="1" :value="elementDimensionValue(element, 'Y')" @input="setElementDimension(element, 'Y', $event)" /></label>
            <template v-if="element.Type === 5">
              <label class="field-label">终点 X<input class="field-input" type="number" :value="dimensionValue(element.LineEndCoordinate?.X ?? element.EndCoordinate?.X)" @input="element.LineEndCoordinate ??= { X: 0, Y: 0 }; setDimension(element.LineEndCoordinate as unknown as Record<string, unknown>, 'X', $event)" /></label>
              <label class="field-label">终点 Y<input class="field-input" type="number" :value="dimensionValue(element.LineEndCoordinate?.Y ?? element.EndCoordinate?.Y)" @input="element.LineEndCoordinate ??= { X: 0, Y: 0 }; setDimension(element.LineEndCoordinate as unknown as Record<string, unknown>, 'Y', $event)" /></label>
            </template>
          </div>
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
          <label class="field-label">字号<select class="field-input" :value="element.FontSize || 24" @change="setFontSize(element as FontElement, $event)"><option v-for="size in fontSizes" :key="size" :value="size">{{ size }} 点</option></select></label>
          <label class="field-label">字体编号<select class="field-input" :value="element.FontName ?? 0" @change="element.FontName = Number(($event.target as HTMLSelectElement).value)"><option :value="0">Font 0 · 中文/英文</option><option :value="nativeFont(element.FontSize || 24)">Font {{ nativeFont(element.FontSize || 24) }} · CPCL</option></select></label>
          <div class="style-controls">
            <label class="toggle-row"><span>粗体</span><input type="checkbox" :checked="Boolean(element.Bold || ((element.FontStyle ?? 0) & 1))" @change="element.Bold = ($event.target as HTMLInputElement).checked; setFontStyleBit(element as FontElement, 1, ($event.target as HTMLInputElement).checked)" /></label>
            <label class="toggle-row"><span>下划线</span><input type="checkbox" :checked="Boolean((element.FontStyle ?? 0) & 4)" @change="setFontStyleBit(element as FontElement, 4, ($event.target as HTMLInputElement).checked)" /></label>
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
        </section>
        <section v-if="element.Type === 3" class="inspector-section">
          <div class="section-title"><span>二维码参数</span></div>
          <label class="field-label">模块宽度<input class="field-input" type="number" min="1" max="32" :value="element.UnitWidth || 4" @input="setNumber(element as unknown as Record<string, unknown>, 'UnitWidth', $event)" /></label>
          <label class="field-label">纠错级别<select class="field-input" :value="element.ErrorLevel ?? 'M'" @change="element.ErrorLevel = ($event.target as HTMLSelectElement).value as 'L' | 'M' | 'Q' | 'H'"><option>L</option><option>M</option><option>Q</option><option>H</option></select></label>
        </section>
        <section v-if="element.Type === 4" class="inspector-section">
          <div class="section-title"><span>条码参数</span></div>
          <label class="field-label">制式<select class="field-input" :value="element.BarcodeType ?? 0" @change="element.BarcodeType = Number(($event.target as HTMLSelectElement).value)"><option v-for="option in barcodeOptions" :key="option[1]" :value="option[1]">{{ option[0] }}</option></select></label>
          <div class="numeric-grid">
            <label class="field-label">窄条宽<input class="field-input" type="number" min="1" max="32" :value="element.LineWidth ?? 2" @input="setNumber(element as unknown as Record<string, unknown>, 'LineWidth', $event)" /></label>
            <label class="field-label">宽窄比<select class="field-input" :value="element.Ratio ?? 2" @change="element.Ratio = Number(($event.target as HTMLSelectElement).value)"><option v-for="ratio in barcodeRatios" :key="ratio.value" :value="ratio.value">{{ ratio.label }}</option></select></label>
          </div>
          <p v-if="!barcodeRatioIsValid(element.Ratio)" class="field-note">当前模板参数无效，请改选支持的宽窄比。</p>
        </section>
        <section v-if="element.Type === 5" class="inspector-section">
          <div class="section-title"><span>线段参数</span></div>
          <label class="field-label">线宽<input class="field-input" type="number" min="1" max="32" :value="element.LineWidth ?? 1" @input="setNumber(element as unknown as Record<string, unknown>, 'LineWidth', $event)" /></label>
        </section>
        <section v-if="element.Type === 6" class="inspector-section">
          <div class="section-title"><span>组合</span></div>
          <label v-if="element.ComponentId" class="field-label">组件引用<input class="field-input" :value="element.ComponentId" readonly /></label>
          <p v-else class="field-note">子元素位于结构树的组合节点下，可分别调整位置与内容。</p>
        </section>
        <section v-if="element.Type === 7 || element.Type === 8" class="inspector-section">
          <div class="section-title"><span>图形描边</span></div>
          <label class="field-label">线宽<input class="field-input" type="number" min="1" max="16" :value="element.LineWidth ?? 1" @input="setNumber(element as unknown as Record<string, unknown>, 'LineWidth', $event)" /></label>
        </section>
        <section v-if="element.Type === 9" class="inspector-section">
          <div class="section-title"><span>图标</span></div>
          <label class="field-label">图形<select class="field-input" :value="element.Icon" @change="element.Icon = ($event.target as HTMLSelectElement).value as 'check' | 'cross' | 'info'"><option value="check">勾选</option><option value="cross">关闭</option><option value="info">信息</option></select></label>
          <label class="field-label">线宽<input class="field-input" type="number" min="1" max="16" :value="element.LineWidth ?? 2" @input="setNumber(element as unknown as Record<string, unknown>, 'LineWidth', $event)" /></label>
        </section>
        <section v-if="element.Type === 10" class="inspector-section">
          <div class="section-title"><span>二维点阵码</span></div>
          <label class="field-label">制式<select class="field-input" :value="element.Format" @change="setMatrixFormat(element, $event)"><option value="DataMatrix">DataMatrix</option><option value="PDF417">PDF417</option></select></label>
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
          <label v-if="template.Version === 2 && isTopLevelRow" class="field-label">页面分区<select class="field-input" :value="row.Section ?? 'Body'" @change="row.Section = ($event.target as HTMLSelectElement).value as Row['Section']"><option value="Body">动态正文</option><option value="DocumentHeader">文档顶部</option><option value="PageHeader">每页顶部</option><option value="DocumentFooter">文档底部</option><option value="PageFooter">每页底部</option><option value="TableHeader">明细表头</option></select></label>
          <label v-else class="toggle-row"><span>每页重复表头</span><input type="checkbox" :checked="row.IsHeader" @change="row.IsHeader = ($event.target as HTMLInputElement).checked" /></label>
        </section>
        <section class="inspector-section condition-section">
          <div class="section-title"><span>行显示条件</span><label class="small-toggle"><input type="checkbox" :checked="conditionEnabled(row)" @change="enableCondition(row, ($event.target as HTMLInputElement).checked)" /></label></div>
          <ConditionEditor v-if="conditionEnabled(row)" :model-value="row.Condition" @update:model-value="updateRowCondition" />
        </section>
        <section class="inspector-section"><div class="section-title"><span>边框</span></div>
          <div class="numeric-grid">
            <label class="field-label">上<select class="field-input" :value="row.Border?.TopStyle ?? 0" @change="updateBorder('TopStyle', $event)"><option :value="0">无</option><option :value="1">实线</option><option :value="2">虚线</option></select></label>
            <label class="field-label">右<select class="field-input" :value="row.Border?.RightStyle ?? 0" @change="updateBorder('RightStyle', $event)"><option :value="0">无</option><option :value="1">实线</option><option :value="2">虚线</option></select></label>
            <label class="field-label">下<select class="field-input" :value="row.Border?.BottomStyle ?? 0" @change="updateBorder('BottomStyle', $event)"><option :value="0">无</option><option :value="1">实线</option><option :value="2">虚线</option></select></label>
            <label class="field-label">左<select class="field-input" :value="row.Border?.LeftStyle ?? 0" @change="updateBorder('LeftStyle', $event)"><option :value="0">无</option><option :value="1">实线</option><option :value="2">虚线</option></select></label>
          </div>
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
            <label class="field-label">上<select class="field-input" :value="cell.Border?.TopStyle ?? 0" @change="updateBorder('TopStyle', $event)"><option :value="0">无</option><option :value="1">实线</option><option :value="2">虚线</option></select></label>
            <label class="field-label">右<select class="field-input" :value="cell.Border?.RightStyle ?? 0" @change="updateBorder('RightStyle', $event)"><option :value="0">无</option><option :value="1">实线</option><option :value="2">虚线</option></select></label>
            <label class="field-label">下<select class="field-input" :value="cell.Border?.BottomStyle ?? 0" @change="updateBorder('BottomStyle', $event)"><option :value="0">无</option><option :value="1">实线</option><option :value="2">虚线</option></select></label>
            <label class="field-label">左<select class="field-input" :value="cell.Border?.LeftStyle ?? 0" @change="updateBorder('LeftStyle', $event)"><option :value="0">无</option><option :value="1">实线</option><option :value="2">虚线</option></select></label>
          </div>
        </section>
        <section class="inspector-section structure-actions">
          <div class="section-title"><span>嵌套布局</span></div>
          <button class="wide-action" type="button" @click="emit('add-nested-row')">在此列中添加嵌套行</button>
        </section>
      </template>
      <div v-else class="empty-hint inspector-empty">从结构树或画布中选择一项</div>
    </div>
  </aside>
</template>
