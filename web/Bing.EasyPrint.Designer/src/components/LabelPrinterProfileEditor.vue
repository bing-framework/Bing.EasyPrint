<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import type { LabelPrinterProfile, TsplPrinterProfile, ZplPrinterProfile } from '../api'
import { normalizeLabelPrinterProfile } from '../designerProject'

const props = defineProps<{ protocol: 'Tspl' | 'Zpl'; modelValue?: TsplPrinterProfile | ZplPrinterProfile }>()
const emit = defineEmits<{
  'update:modelValue': [value: TsplPrinterProfile | ZplPrinterProfile | undefined]
  validity: [message: string]
}>()
const label = computed(() => props.protocol === 'Tspl' ? 'TSPL' : 'ZPL')
const expanded = ref(false)
const error = ref('')
const draft = reactive<Record<string, string>>({})
const limits = [
  { key: 'printableWidthDots', label: '可打印宽度', unit: '点' },
  { key: 'maxLabelHeightDots', label: '最大标签高度', unit: '点' },
  { key: 'maxImageDataBytes', label: '单条图片上限', unit: '单色字节' },
] as const
const flags = [
  { key: 'supportsImages', label: '支持图片' },
  { key: 'supportsGraphics', label: '支持线框' },
  { key: 'supportsNativeBarcode', label: '支持原生条码' },
  { key: 'supportsNativeBarcodeHumanReadable', label: '支持原生条码可读文字' },
  { key: 'supportsNativeQrCode', label: '支持原生二维码' },
  { key: 'supportsRotation', label: '支持文字及条码旋转' },
] as const

function report(message: string) { error.value = message; emit('validity', message) }
watch(() => ({ profile: props.modelValue, protocol: props.protocol }), ({ profile }) => {
  draft.name = profile?.name ?? 'Custom'
  draft.firmwareVersion = profile?.firmwareVersion ?? ''
  for (const { key } of limits) draft[key] = profile?.[key]?.toString() ?? ''
  for (const { key } of flags) draft[key] = String(profile?.[key] ?? true)
  draft.supportsCut = String((profile as ZplPrinterProfile | undefined)?.supportsCut ?? true)
  draft.fontMode = profile?.deviceFontNames === undefined ? 'all' : profile.deviceFontNames.length ? 'list' : 'none'
  draft.fonts = profile?.deviceFontNames?.join(', ') ?? '0'
  draft.encodings = profile?.encodingCodePages?.join(', ') ?? ''
  draft.ranges = profile?.characterRanges?.map((range) => `${range.first}-${range.last}`).join(', ') ?? ''
  draft.hriTypes = profile?.supportedNativeBarcodeHumanReadableTypes?.join(', ') ?? ''
  draft.qrRotations = profile?.supportedNativeQrCodeRotations === undefined
    ? ''
    : profile.supportedNativeQrCodeRotations.length === 0
      ? '[]'
      : profile.supportedNativeQrCodeRotations.join(', ')
  if (props.protocol === 'Tspl') {
    const cutter = (profile as TsplPrinterProfile | undefined)?.cutter
    draft.cutterEnabled = String(cutter !== undefined)
    draft.cutterStrategy = cutter?.strategy ?? 'Batch'
    draft.cutterFull = String(cutter?.supportedCutModes?.includes('Full') ?? true)
    draft.cutterPartial = String(cutter?.supportedCutModes?.includes('Partial') ?? false)
    draft.cutterEndOfJob = String(cutter?.supportsEndOfJob ?? true)
    draft.cutterFeedDots = cutter?.cutPositionFeedDots?.toString() ?? ''
  } else {
    const cutter = (profile as ZplPrinterProfile | undefined)?.cutter
    draft.cutterEnabled = String(cutter !== undefined)
    draft.cutterStrategy = cutter?.strategy ?? 'Cutter'
    draft.cutterFull = String(cutter?.supportedCutModes?.includes('Full') ?? true)
    draft.cutterPartial = String(cutter?.supportedCutModes?.includes('Partial') ?? false)
    draft.cutterTearOff = String(cutter?.supportsTearOff ?? false)
    draft.cutterPartialMillimeters = cutter?.kioskPartialUncutMillimeters?.toString() ?? ''
    draft.cutterMarginMillimeters = cutter?.kioskCutMarginMillimeters?.toString() ?? ''
  }
  report('')
}, { immediate: true, deep: true })

function parseRanges(raw: string): unknown {
  if (raw.startsWith('[')) return JSON.parse(raw)
  return raw.split(/[,;\n]+/).map((part) => {
    const match = /^\s*(\d+)\s*-\s*(\d+)\s*$/.exec(part)
    if (!match) throw new Error('字库区间需使用 first-last 十进制格式。')
    return { first: Number(match[1]), last: Number(match[2]) }
  })
}

function commit() {
  try {
    const profile: Record<string, unknown> = { name: draft.name }
    if (draft.firmwareVersion) profile.firmwareVersion = draft.firmwareVersion
    for (const { key } of limits) if (draft[key].trim()) profile[key] = Number(draft[key])
    for (const { key } of flags) profile[key] = draft[key] === 'true'
    if (props.protocol === 'Zpl') profile.supportsCut = draft.supportsCut === 'true'
    if (draft.fontMode === 'none') profile.deviceFontNames = []
    else if (draft.fontMode === 'list') {
      profile.deviceFontNames = draft.fonts.trim() ? draft.fonts.trim().split(/[,;\s]+/) : ['']
    }
    if (draft.encodings.trim()) profile.encodingCodePages = draft.encodings.trim().split(/[,;\s]+/).map(Number)
    if (draft.ranges.trim()) profile.characterRanges = parseRanges(draft.ranges.trim())
    if (draft.hriTypes.trim()) profile.supportedNativeBarcodeHumanReadableTypes = draft.hriTypes.trim().split(/[,;\s]+/)
    if (draft.qrRotations.trim() === '[]') profile.supportedNativeQrCodeRotations = []
    else if (draft.qrRotations.trim()) profile.supportedNativeQrCodeRotations = draft.qrRotations.trim().split(/[,;\s]+/)
    if (props.protocol === 'Tspl' && draft.cutterEnabled === 'true') {
      const supportedCutModes: string[] = []
      if (draft.cutterFull === 'true') supportedCutModes.push('Full')
      if (draft.cutterPartial === 'true') supportedCutModes.push('Partial')
      profile.cutter = {
        strategy: draft.cutterStrategy,
        supportedCutModes,
        supportsEndOfJob: draft.cutterEndOfJob === 'true',
        ...(draft.cutterFeedDots.trim() ? { cutPositionFeedDots: Number(draft.cutterFeedDots) } : {}),
      }
    }
    if (props.protocol === 'Zpl' && draft.cutterEnabled === 'true') {
      profile.cutter = {
        strategy: draft.cutterStrategy,
        supportedCutModes: [ ...(draft.cutterFull === 'true' ? ['Full'] : []), ...(draft.cutterPartial === 'true' ? ['Partial'] : []) ],
        supportsTearOff: draft.cutterTearOff === 'true',
        ...(draft.cutterPartialMillimeters.trim() ? { kioskPartialUncutMillimeters: Number(draft.cutterPartialMillimeters) } : {}),
        ...(draft.cutterMarginMillimeters.trim() ? { kioskCutMarginMillimeters: Number(draft.cutterMarginMillimeters) } : {}),
      }
    }
    const validated = normalizeLabelPrinterProfile(profile, props.protocol)
    report('')
    emit('update:modelValue', validated)
  } catch (cause) { report(cause instanceof Error ? cause.message : '机型配置无效。') }
}

function setText(key: string, event: Event) {
  draft[key] = (event.target as HTMLInputElement).value
  if (key === 'cutterStrategy' && draft[key] === 'Batch') draft.cutterFeedDots = ''
  if (props.protocol === 'Zpl' && key === 'cutterStrategy') {
    if (draft[key] === 'Kiosk') draft.cutterMarginMillimeters ||= '9'
    else { draft.cutterPartial = 'false'; draft.cutterPartialMillimeters = ''; draft.cutterMarginMillimeters = '' }
  }
  commit()
}
function setFlag(key: string, event: Event) {
  draft[key] = String((event.target as HTMLInputElement).checked)
  if (props.protocol === 'Zpl' && key === 'cutterPartial' && draft[key] === 'false') draft.cutterPartialMillimeters = ''
  commit()
}
function create() { expanded.value = true; report(''); emit('update:modelValue', { name: 'Custom' } satisfies LabelPrinterProfile) }
function clear() { report(''); emit('update:modelValue', undefined) }
function createCutter() {
  expanded.value = true
  draft.cutterEnabled = 'true'
  draft.cutterStrategy = props.protocol === 'Tspl' ? 'Batch' : 'Cutter'
  draft.cutterFull = 'true'
  draft.cutterPartial = 'false'
  draft.cutterEndOfJob = 'true'
  draft.cutterFeedDots = ''
  draft.cutterTearOff = 'false'
  draft.cutterPartialMillimeters = ''
  draft.cutterMarginMillimeters = ''
  commit()
}
function clearCutter() {
  draft.cutterEnabled = 'false'
  draft.cutterFeedDots = ''
  report('')
  commit()
}
</script>

<template>
  <div class="output-settings-title">{{ label }} 机型配置</div>
  <div class="field-actions">
    <button v-if="!modelValue" class="button-secondary" type="button" @click="create">新建{{ label }}机型配置</button>
    <button v-else class="button-secondary" type="button" @click="clear">清除{{ label }}机型配置</button>
  </div>
  <template v-if="modelValue">
    <button class="wide-action" type="button" @click="expanded = !expanded">{{ expanded ? '收起' : '编辑' }}{{ label }}机型参数</button>
    <div v-if="expanded" class="output-settings-grid">
      <label class="field-label">配置名称<input class="field-input" :value="draft.name" :aria-label="`${label} 机型名称`" @input="setText('name', $event)" /></label>
      <label class="field-label">固件版本 <span>来源记录，可选</span><input class="field-input" :value="draft.firmwareVersion" :aria-label="`${label} 固件版本`" @input="setText('firmwareVersion', $event)" /></label>
      <label v-for="field in limits" :key="field.key" class="field-label">{{ field.label }} <span>{{ field.unit }}，可选</span><input class="field-input" type="number" min="1" max="2147483647" step="1" :value="draft[field.key]" :aria-label="`${label} ${field.label}`" @input="setText(field.key, $event)" /></label>
      <label v-for="field in flags" :key="field.key" class="toggle-row"><span>{{ field.label }}</span><input type="checkbox" :checked="draft[field.key] === 'true'" :aria-label="`${label} ${field.label}`" @change="setFlag(field.key, $event)" /></label>
      <label v-if="protocol === 'Zpl'" class="toggle-row"><span>支持现有切纸</span><input type="checkbox" :checked="draft.supportsCut === 'true'" aria-label="ZPL 支持切纸" @change="setFlag('supportsCut', $event)" /></label>
      <label class="field-label">设备字体范围<select class="field-input" :value="draft.fontMode" :aria-label="`${label} 字体范围模式`" @change="setText('fontMode', $event)"><option value="all">协议默认范围</option><option value="list">指定已确认字体</option><option value="none">禁用文字</option></select></label>
      <label v-if="draft.fontMode === 'list'" class="field-label">设备字体 <span>{{ protocol === 'Tspl' ? '0–8' : '0–9 / A–Z' }}</span><input class="field-input" :value="draft.fonts" :aria-label="`${label} 机型字体列表`" placeholder="0, 1" @input="setText('fonts', $event)" /></label>
      <label class="field-label">编码代码页 <span>留空使用协议映射</span><input class="field-input" :value="draft.encodings" :aria-label="`${label} 机型代码页列表`" :placeholder="protocol === 'Tspl' ? '20127, 65001, 936' : '20127, 65001'" @input="setText('encodings', $event)" /></label>
      <label class="field-label">原生 HRI 条码制式 <span>留空不按制式收窄</span><input class="field-input" :value="draft.hriTypes" :aria-label="`${label} 原生 HRI 条码制式`" placeholder="Code128, Ean13" @input="setText('hriTypes', $event)" /></label>
      <label class="field-label">原生二维码旋转 <span>留空不收窄，[] 禁用非零旋转</span><input class="field-input" :value="draft.qrRotations" :aria-label="`${label} 原生二维码旋转`" placeholder="Rotate90, Rotate270" @input="setText('qrRotations', $event)" /></label>
      <label class="field-label">字库覆盖区间 <span>十进制，留空不限制</span><textarea class="field-input" :value="draft.ranges" :aria-label="`${label} 字库覆盖区间`" placeholder="32-126, 19968-40959" @input="setText('ranges', $event)"></textarea></label>
      <template v-if="protocol === 'Tspl'">
        <div class="field-actions">
          <button v-if="draft.cutterEnabled !== 'true'" class="button-secondary" type="button" @click="createCutter">新建 TSPL 切刀配置</button>
          <button v-else class="button-secondary" type="button" @click="clearCutter">清除 TSPL 切刀配置</button>
        </div>
        <template v-if="draft.cutterEnabled === 'true'">
          <label class="field-label">裁切策略<select class="field-input" :value="draft.cutterStrategy" aria-label="TSPL 裁切策略" @change="setText('cutterStrategy', $event)"><option value="Batch">Batch · 批次自动裁切</option><option value="Immediate">Immediate · PRINT 后立即 CUT</option></select></label>
          <div class="toggle-row"><span>支持的切法</span><label><input type="checkbox" :checked="draft.cutterFull === 'true'" aria-label="TSPL 全切" @change="setFlag('cutterFull', $event)" />全切</label><label><input type="checkbox" :checked="draft.cutterPartial === 'true'" aria-label="TSPL 半切" @change="setFlag('cutterPartial', $event)" />半切</label></div>
          <label class="toggle-row"><span>EOJ 完成同步</span><input type="checkbox" :checked="draft.cutterEndOfJob === 'true'" aria-label="TSPL 支持EOJ" @change="setFlag('cutterEndOfJob', $event)" /></label>
          <label v-if="draft.cutterStrategy === 'Immediate'" class="field-label">切刀位置点数 <span>0–9999，按设备实测</span><input class="field-input" type="number" min="0" max="9999" step="1" :value="draft.cutterFeedDots" aria-label="TSPL 切刀位置点数" placeholder="不补偿" @input="setText('cutterFeedDots', $event)" /></label>
          <p class="field-note">按设备手册和实测填写；Batch 不支持切刀位置补偿，策略不会根据固件版本自动推断。</p>
        </template>
      </template>
      <template v-if="protocol === 'Zpl'">
        <div class="field-actions">
          <button v-if="draft.cutterEnabled !== 'true'" class="button-secondary" type="button" @click="createCutter">新建 ZPL 切刀配置</button>
          <button v-else class="button-secondary" type="button" @click="clearCutter">清除 ZPL 切刀配置</button>
        </div>
        <template v-if="draft.cutterEnabled === 'true'">
          <label class="field-label">裁切策略<select class="field-input" :value="draft.cutterStrategy" aria-label="ZPL 裁切策略" @change="setText('cutterStrategy', $event)"><option value="Cutter">Cutter · 批次裁切</option><option value="Delayed">Delayed · 独立延迟裁切文件</option><option value="Kiosk">Kiosk · 已确认的设备模式</option></select></label>
          <label class="toggle-row"><span>确认支持非裁切模式</span><input type="checkbox" :checked="draft.cutterTearOff === 'true'" aria-label="ZPL 支持TearOff" @change="setFlag('cutterTearOff', $event)" /></label>
          <div class="toggle-row"><span>支持的切法</span><label><input type="checkbox" :checked="draft.cutterFull === 'true'" aria-label="ZPL 全切" @change="setFlag('cutterFull', $event)" />全切</label><label><input type="checkbox" :disabled="draft.cutterStrategy !== 'Kiosk'" :checked="draft.cutterPartial === 'true'" aria-label="ZPL 半切" @change="setFlag('cutterPartial', $event)" />半切</label></div>
          <template v-if="draft.cutterStrategy === 'Kiosk'">
            <label class="field-label">裁切边距 <span>2–9 mm，按设备确认</span><input class="field-input" type="number" min="2" max="9" step="1" :value="draft.cutterMarginMillimeters" aria-label="ZPL Kiosk 裁切边距" @input="setText('cutterMarginMillimeters', $event)" /></label>
            <label v-if="draft.cutterPartial === 'true'" class="field-label">半切未切长度 <span>10–60 mm</span><input class="field-input" type="number" min="10" max="60" step="1" :value="draft.cutterPartialMillimeters" aria-label="ZPL Kiosk 半切未切长度" @input="setText('cutterPartialMillimeters', $event)" /></label>
          </template>
          <p class="field-note">按设备手册确认模式；Delayed 导出的页面与裁切文件须依清单分别发送。Kiosk 边距不等于额外进纸补偿。</p>
        </template>
      </template>
      <p class="field-note">按设备手册填写；字体范围为所列字体的共同覆盖。斜线转位图仍受图片能力和上限约束。</p>
    </div>
    <p v-if="error" class="field-note capability-warning" role="alert">{{ error }}</p>
  </template>
</template>
