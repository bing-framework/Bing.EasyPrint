import type { DesignerCapabilities } from './capabilities'
import type { Gs1CompositeElement } from './model'

export const GS1_128_LINEAR_TYPE = 11 as const
export const EAN_8_LINEAR_TYPE = 4 as const
export const EAN_13_LINEAR_TYPE = 5 as const
export const UPC_A_LINEAR_TYPE = 6 as const
export const UPC_E_LINEAR_TYPE = 7 as const
export const DATABAR_OMNIDIRECTIONAL_LINEAR_TYPE = 25 as const
export const DATABAR_TRUNCATED_LINEAR_TYPE = 26 as const
export const DATABAR_LIMITED_LINEAR_TYPE = 27 as const
export const DATABAR_EXPANDED_LINEAR_TYPE = 28 as const
export const DATABAR_STACKED_LINEAR_TYPE = 30 as const
export const DATABAR_EXPANDED_STACKED_LINEAR_TYPE = 31 as const
export const DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE = 32 as const
export const GS1_COMPOSITE_CC_C_MODE = 3 as const
export const GS1_COMPOSITE_GS1_128_CC_AB_FEATURE = 'gs1-composite-gs1-128-ab' as const
export const GS1_COMPOSITE_EXPANDED_STACKED_FEATURE = 'gs1-composite-expanded-stacked' as const
export const GS1_DATABAR_EXPANDED_STACKED_COLUMNS_FEATURE = 'gs1-databar-expanded-stacked-columns' as const
export const GS1_DATABAR_EXPANDED_STACKED_ROWS_FEATURE = 'gs1-databar-expanded-stacked-rows' as const
export const GS1_COMPOSITE_EAN_UPC_FEATURE = 'gs1-composite-ean-upc' as const
export const GS1_COMPOSITE_EAN_UPC_ADDONS_FEATURE = 'gs1-composite-ean-upc-addons' as const
export const GS1_COMPOSITE_TRUNCATED_FEATURE = 'gs1-composite-truncated' as const
export const GS1_COMPOSITE_STACKED_OMNIDIRECTIONAL_FEATURE = 'gs1-composite-stacked-omnidirectional' as const
export const DATABAR_STACKED_MIN_HEIGHT_MODULES = 12 as const
export const DATABAR_TRUNCATED_MIN_HEIGHT_MODULES = 13 as const
export const DATABAR_STACKED_OMNIDIRECTIONAL_MIN_HEIGHT_MODULES = 33 as const
export const DEFAULT_GS1_128_COMPOSITE_PRIMARY = '(01)12345678901231'
export const DEFAULT_GS1_EXPANDED_COMPOSITE_PRIMARY = '(01)09506000134352(10)LOT-A'
export const DEFAULT_GS1_EAN_UPC_COMPOSITE_PRIMARY: Record<Gs1CompositeRetailLinearType, string> = {
  [EAN_8_LINEAR_TYPE]: '12345670',
  [EAN_13_LINEAR_TYPE]: '1234567890128',
  [UPC_A_LINEAR_TYPE]: '012345678905',
  [UPC_E_LINEAR_TYPE]: '01234565',
}

export type Gs1CompositeRetailLinearType = typeof EAN_8_LINEAR_TYPE | typeof EAN_13_LINEAR_TYPE |
  typeof UPC_A_LINEAR_TYPE | typeof UPC_E_LINEAR_TYPE

export type Gs1CompositeLinearType = typeof GS1_128_LINEAR_TYPE |
  Gs1CompositeRetailLinearType |
  typeof DATABAR_OMNIDIRECTIONAL_LINEAR_TYPE | typeof DATABAR_LIMITED_LINEAR_TYPE |
  typeof DATABAR_TRUNCATED_LINEAR_TYPE | typeof DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE |
  typeof DATABAR_EXPANDED_LINEAR_TYPE | typeof DATABAR_STACKED_LINEAR_TYPE |
  typeof DATABAR_EXPANDED_STACKED_LINEAR_TYPE
export type Gs1CompositeMode = 0 | 1 | 2 | typeof GS1_COMPOSITE_CC_C_MODE
export type Gs1CompositeModeChoice = { value: Gs1CompositeMode; label: string; disabled: boolean; reason?: string }

const dataBarModes: Gs1CompositeModeChoice[] = [
  { value: 0, label: 'Auto', disabled: false },
  { value: 1, label: 'CC-A', disabled: false },
  { value: 2, label: 'CC-B', disabled: false },
]
const gs1_128Modes: Gs1CompositeModeChoice[] = [
  { value: 0, label: 'Auto', disabled: false },
  { value: 1, label: 'CC-A', disabled: false },
  { value: 2, label: 'CC-B', disabled: false },
  { value: GS1_COMPOSITE_CC_C_MODE, label: 'CC-C', disabled: false },
]

export function isValidGs1CompositeCombination(linearType: number | undefined, mode: number | undefined): boolean {
  const linear = linearType ?? DATABAR_OMNIDIRECTIONAL_LINEAR_TYPE
  const selectedMode = mode ?? 0
  if (isGs1CompositeRetailLinearType(linear)) return selectedMode === 0 || selectedMode === 1 || selectedMode === 2
  if (linear === GS1_128_LINEAR_TYPE) return selectedMode === 0 || selectedMode === 1 || selectedMode === 2 ||
    selectedMode === GS1_COMPOSITE_CC_C_MODE
  if (linear === DATABAR_EXPANDED_LINEAR_TYPE || linear === DATABAR_STACKED_LINEAR_TYPE ||
    linear === DATABAR_EXPANDED_STACKED_LINEAR_TYPE || linear === DATABAR_TRUNCATED_LINEAR_TYPE ||
    linear === DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE)
    return selectedMode === 0 || selectedMode === 1 || selectedMode === 2
  return (linear === DATABAR_OMNIDIRECTIONAL_LINEAR_TYPE || linear === DATABAR_LIMITED_LINEAR_TYPE) &&
    (selectedMode === 0 || selectedMode === 1 || selectedMode === 2)
}

export function isGs1CompositeRetailLinearType(linearType: number | undefined): linearType is Gs1CompositeRetailLinearType {
  return linearType === EAN_8_LINEAR_TYPE || linearType === EAN_13_LINEAR_TYPE ||
    linearType === UPC_A_LINEAR_TYPE || linearType === UPC_E_LINEAR_TYPE
}

export function isGs1CompositeEanUpcCombination(linearType: number | undefined, mode: number | undefined): boolean {
  return isGs1CompositeRetailLinearType(linearType) && (mode === undefined || mode === 0 || mode === 1 || mode === 2)
}

export function supportsGs1CompositeEanUpc(capabilities?: DesignerCapabilities): boolean {
  const requiredTypes = ['Ean8', 'Ean13', 'UpcA', 'UpcE']
  return Boolean(capabilities?.gs1CompositeFeature && capabilities.supportsGs1Composite &&
    capabilities.elementTypes.includes(11) && capabilities.gs1CompositeEanUpcFeature &&
    capabilities.gs1Composite?.modes.includes('Auto') && capabilities.gs1Composite.modes.includes('CcA') &&
    capabilities.gs1Composite.modes.includes('CcB') &&
    requiredTypes.every((type) => capabilities.gs1Composite?.linearTypes.includes(type)))
}

export function supportsGs1CompositeEanUpcAddons(capabilities?: DesignerCapabilities): boolean {
  return Boolean(supportsGs1CompositeEanUpc(capabilities) && capabilities?.gs1CompositeEanUpcAddonsFeature &&
    capabilities.gs1Composite?.primaryFormats?.includes('ean-upc-digits-with-addon'))
}

export function gs1CompositeRetailHasAddon(value: string | undefined): boolean {
  return value?.includes('+') ?? false
}

function expectedRetailPrimaryLength(linearType: Gs1CompositeRetailLinearType): string {
  switch (linearType) {
    case EAN_8_LINEAR_TYPE: return '7 或 8 位'
    case EAN_13_LINEAR_TYPE: return '12 或 13 位'
    case UPC_A_LINEAR_TYPE: return '11 或 12 位'
    case UPC_E_LINEAR_TYPE: return '6、7 或 8 位'
  }
}

function checkDigit(data: string): string {
  let sum = 0
  let weight = 3
  for (let index = data.length - 1; index >= 0; index -= 1) {
    sum += Number(data[index]) * weight
    weight = weight === 3 ? 1 : 3
  }
  return String((10 - sum % 10) % 10)
}

function expandUpcE(system: string, payload: string): string {
  const [first, second, third, fourth, fifth, last] = payload
  if (last === '0' || last === '1' || last === '2') return `${system}${first}${second}${last}0000${third}${fourth}${fifth}`
  if (last === '3') return `${system}${first}${second}${third}00000${fourth}${fifth}`
  if (last === '4') return `${system}${first}${second}${third}${fourth}00000${fifth}`
  return `${system}${first}${second}${third}${fourth}${fifth}0000${last}`
}

/** 返回 EAN/UPC 主数据的输入错误；动态字段由 Host 在解析后校验。 */
export function gs1CompositeRetailPrimaryError(linearType: number | undefined, value: string): string | undefined {
  if (!isGs1CompositeRetailLinearType(linearType)) return undefined
  const separator = value.indexOf('+')
  const primary = separator < 0 ? value : value.slice(0, separator)
  const addon = value.slice(separator + 1)
  if (separator >= 0 && (separator !== value.lastIndexOf('+') ||
    (addon.length !== 2 && addon.length !== 5) || /[^0-9]/.test(addon))) {
    return 'EAN/UPC Composite 附加码必须是单个 + 后的 2 或 5 位 ASCII 数字。'
  }
  if (!primary || /[^0-9]/.test(primary)) return 'EAN/UPC Composite 主数据只接受 ASCII 数字，不支持 AI 括号。'
  const validLength = linearType === EAN_8_LINEAR_TYPE ? primary.length === 7 || primary.length === 8
    : linearType === EAN_13_LINEAR_TYPE ? primary.length === 12 || primary.length === 13
      : linearType === UPC_A_LINEAR_TYPE ? primary.length === 11 || primary.length === 12
        : primary.length === 6 || primary.length === 7 || primary.length === 8
  if (!validLength) return `${linearType === EAN_8_LINEAR_TYPE ? 'EAN-8' : linearType === EAN_13_LINEAR_TYPE ? 'EAN-13' : linearType === UPC_A_LINEAR_TYPE ? 'UPC-A' : 'UPC-E'} 主数据长度必须为${expectedRetailPrimaryLength(linearType)}。`

  if (linearType === UPC_E_LINEAR_TYPE) {
    const normalized = primary.length === 6 ? `0${primary}` : primary
    const system = normalized[0]!
    const payload = normalized.slice(1, 7)
    if (system !== '0' && system !== '1') return 'UPC-E 号码系统只支持 0 或 1。'
    const p2 = payload[2]
    const p3 = payload[3]
    const p4 = payload[4]
    const p5 = payload[5]
    if (p5 === '3' && Number(p2) <= 2 || p5 === '4' && p3 === '0' ||
      p5 !== undefined && Number(p5) >= 5 && p4 === '0') {
      return 'UPC-E 主数据不符合零压缩规则。'
    }
    const expanded = expandUpcE(system, payload)
    if (normalized.length === 8 && normalized[7] !== checkDigit(expanded)) return 'UPC-E 校验位不匹配。'
    return undefined
  }

  if (primary.length === (linearType === EAN_8_LINEAR_TYPE ? 8 : linearType === EAN_13_LINEAR_TYPE ? 13 : 12)) {
    if (primary[primary.length - 1] !== checkDigit(primary.slice(0, -1))) return 'EAN/UPC 校验位不匹配。'
  }
  return undefined
}

export function minimumGs1DataBarStackedLinearHeight(moduleWidth: number): number {
  return DATABAR_STACKED_MIN_HEIGHT_MODULES * moduleWidth
}

export function minimumGs1DataBarTruncatedLinearHeight(moduleWidth: number): number {
  return DATABAR_TRUNCATED_MIN_HEIGHT_MODULES * moduleWidth
}

export function minimumGs1DataBarStackedOmnidirectionalLinearHeight(moduleWidth: number): number {
  return DATABAR_STACKED_OMNIDIRECTIONAL_MIN_HEIGHT_MODULES * moduleWidth
}

export function isGs1CompositeCcCCombination(linearType: number | undefined, mode: number | undefined): boolean {
  return linearType === GS1_128_LINEAR_TYPE && (mode === undefined || mode === 0 || mode === GS1_COMPOSITE_CC_C_MODE)
}

export function isGs1CompositeGs1128CcAbCombination(linearType: number | undefined, mode: number | undefined): boolean {
  return linearType === GS1_128_LINEAR_TYPE && (mode === 1 || mode === 2)
}

export function supportsGs1CompositeCcC(capabilities?: DesignerCapabilities): boolean {
  return Boolean(capabilities?.gs1CompositeFeature && capabilities.supportsGs1Composite &&
    capabilities.elementTypes.includes(11) &&
    capabilities.gs1CompositeCcCFeature && capabilities.gs1Composite?.modes.includes('Auto') &&
    capabilities.gs1Composite.modes.includes('CcC') &&
    capabilities.gs1Composite.linearTypes.includes('UccEan128'))
}

export function supportsGs1CompositeGs1128CcAb(capabilities?: DesignerCapabilities): boolean {
  return Boolean(capabilities?.gs1CompositeFeature && capabilities.supportsGs1Composite &&
    capabilities.elementTypes.includes(11) && capabilities.gs1CompositeGs1128CcAbFeature &&
    capabilities.gs1Composite?.modes.includes('CcA') && capabilities.gs1Composite.modes.includes('CcB') &&
    capabilities.gs1Composite.linearTypes.includes('UccEan128'))
}

export function supportsGs1CompositeGs1128(capabilities?: DesignerCapabilities): boolean {
  return supportsGs1CompositeCcC(capabilities) || supportsGs1CompositeGs1128CcAb(capabilities)
}

export function isGs1CompositeExpandedCombination(linearType: number | undefined, mode: number | undefined): boolean {
  return linearType === DATABAR_EXPANDED_LINEAR_TYPE &&
    (mode === undefined || mode === 0 || mode === 1 || mode === 2)
}

export function supportsGs1CompositeExpanded(capabilities?: DesignerCapabilities): boolean {
  return Boolean(capabilities?.gs1CompositeFeature && capabilities.supportsGs1Composite &&
    capabilities.elementTypes.includes(11) && capabilities.gs1CompositeExpandedFeature &&
    capabilities.gs1Composite?.modes.includes('Auto') && capabilities.gs1Composite.modes.includes('CcA') &&
    capabilities.gs1Composite.modes.includes('CcB') &&
    capabilities.gs1Composite.linearTypes.includes('Gs1DataBarExpanded'))
}

export function isGs1CompositeStackedCombination(linearType: number | undefined, mode: number | undefined): boolean {
  return linearType === DATABAR_STACKED_LINEAR_TYPE &&
    (mode === undefined || mode === 0 || mode === 1 || mode === 2)
}

export function supportsGs1CompositeStacked(capabilities?: DesignerCapabilities): boolean {
  return Boolean(capabilities?.gs1CompositeFeature && capabilities.supportsGs1Composite &&
    capabilities.elementTypes.includes(11) && capabilities.gs1CompositeStackedFeature &&
    capabilities.gs1Composite?.modes.includes('Auto') && capabilities.gs1Composite.modes.includes('CcA') &&
    capabilities.gs1Composite.modes.includes('CcB') &&
    capabilities.gs1Composite.linearTypes.includes('Gs1DataBarStacked'))
}

export function isGs1CompositeTruncatedCombination(linearType: number | undefined, mode: number | undefined): boolean {
  return linearType === DATABAR_TRUNCATED_LINEAR_TYPE &&
    (mode === undefined || mode === 0 || mode === 1 || mode === 2)
}

export function supportsGs1CompositeTruncated(capabilities?: DesignerCapabilities): boolean {
  return Boolean(capabilities?.gs1CompositeFeature && capabilities.supportsGs1Composite &&
    capabilities.elementTypes.includes(11) && capabilities.gs1CompositeTruncatedFeature &&
    capabilities.gs1Composite?.modes.includes('Auto') && capabilities.gs1Composite.modes.includes('CcA') &&
    capabilities.gs1Composite.modes.includes('CcB') &&
    capabilities.gs1Composite.linearTypes.includes('Gs1DataBarTruncated'))
}

export function isGs1CompositeStackedOmnidirectionalCombination(linearType: number | undefined,
  mode: number | undefined): boolean {
  return linearType === DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE &&
    (mode === undefined || mode === 0 || mode === 1 || mode === 2)
}

export function supportsGs1CompositeStackedOmnidirectional(capabilities?: DesignerCapabilities): boolean {
  return Boolean(capabilities?.gs1CompositeFeature && capabilities.supportsGs1Composite &&
    capabilities.elementTypes.includes(11) && capabilities.gs1CompositeStackedOmnidirectionalFeature &&
    capabilities.gs1Composite?.modes.includes('Auto') && capabilities.gs1Composite.modes.includes('CcA') &&
    capabilities.gs1Composite.modes.includes('CcB') &&
    capabilities.gs1Composite.linearTypes.includes('Gs1DataBarStackedOmnidirectional'))
}

export function isGs1CompositeExpandedStackedCombination(linearType: number | undefined,
  mode: number | undefined): boolean {
  return linearType === DATABAR_EXPANDED_STACKED_LINEAR_TYPE &&
    (mode === undefined || mode === 0 || mode === 1 || mode === 2)
}

export function minimumGs1DataBarExpandedStackedLinearHeight(moduleWidth: number): number {
  return moduleWidth
}

export function supportsGs1CompositeExpandedStacked(capabilities?: DesignerCapabilities): boolean {
  return Boolean(capabilities?.gs1CompositeFeature && capabilities.supportsGs1Composite &&
    capabilities.elementTypes.includes(11) && capabilities.gs1CompositeExpandedStackedFeature &&
    capabilities.gs1Composite?.modes.includes('Auto') && capabilities.gs1Composite.modes.includes('CcA') &&
    capabilities.gs1Composite.modes.includes('CcB') &&
    capabilities.gs1Composite.linearTypes.includes('Gs1DataBarExpandedStacked'))
}

export function gs1CompositeModeChoices(linearType: number | undefined,
  capabilities?: DesignerCapabilities): Gs1CompositeModeChoice[] {
  const choices = linearType === GS1_128_LINEAR_TYPE ? gs1_128Modes : dataBarModes
  const supportedModes = capabilities?.gs1Composite?.modes
  if (isGs1CompositeRetailLinearType(linearType)) {
    const supported = supportsGs1CompositeEanUpc(capabilities)
    return choices.map((choice) => ({ ...choice, disabled: !supported,
      ...(supported ? {} : { reason: '当前 Host 未协商 EAN/UPC Composite 功能。' }) }))
  }
  if (linearType === GS1_128_LINEAR_TYPE) {
    return choices.map((choice) => {
      const isCcAb = choice.value === 1 || choice.value === 2
      const supported = isCcAb ? supportsGs1CompositeGs1128CcAb(capabilities) : supportsGs1CompositeCcC(capabilities)
      const reason = supported ? undefined : isCcAb
        ? '当前 Host 未协商 GS1-128 + CC-A/CC-B Composite 功能。'
        : '当前 Host 未协商 GS1-128 + CC-C Composite 功能。'
      return { ...choice, disabled: !supported, ...(reason === undefined ? {} : { reason }) }
    })
  }
  if (linearType === DATABAR_TRUNCATED_LINEAR_TYPE || linearType === DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE) {
    const supported = linearType === DATABAR_TRUNCATED_LINEAR_TYPE
      ? supportsGs1CompositeTruncated(capabilities)
      : supportsGs1CompositeStackedOmnidirectional(capabilities)
    const reason = supported ? undefined : linearType === DATABAR_TRUNCATED_LINEAR_TYPE
      ? '当前 Host 未协商 GS1 DataBar Truncated Composite 功能。'
      : '当前 Host 未协商 GS1 DataBar Stacked Omnidirectional Composite 功能。'
    return choices.map((choice) => ({ ...choice, disabled: !supported, ...(reason === undefined ? {} : { reason }) }))
  }
  return choices.map((choice) => {
    const supported = linearType === DATABAR_STACKED_LINEAR_TYPE
      ? supportsGs1CompositeStacked(capabilities)
      : linearType === DATABAR_EXPANDED_STACKED_LINEAR_TYPE
        ? supportsGs1CompositeExpandedStacked(capabilities)
        : supportedModes === undefined || supportedModes.includes(
          choice.value === 0 ? 'Auto' : choice.value === 1 ? 'CcA' : 'CcB')
    const reason = linearType === DATABAR_EXPANDED_STACKED_LINEAR_TYPE && !supported
      ? '当前 Host 未协商 GS1 DataBar Expanded Stacked Composite 功能。'
      : undefined
    return { ...choice, disabled: choice.disabled || !supported,
      ...(reason === undefined ? {} : { reason }) }
  })
}

export function setGs1CompositeLinearType(element: Gs1CompositeElement, linearType: Gs1CompositeLinearType,
  capabilities?: DesignerCapabilities): boolean {
  if (linearType === GS1_128_LINEAR_TYPE && !supportsGs1CompositeGs1128(capabilities)) return false
  if (isGs1CompositeRetailLinearType(linearType) && !supportsGs1CompositeEanUpc(capabilities)) return false
  if (linearType === DATABAR_EXPANDED_LINEAR_TYPE && !supportsGs1CompositeExpanded(capabilities)) return false
  if (linearType === DATABAR_STACKED_LINEAR_TYPE && !supportsGs1CompositeStacked(capabilities)) return false
  if (linearType === DATABAR_EXPANDED_STACKED_LINEAR_TYPE && !supportsGs1CompositeExpandedStacked(capabilities)) return false
  if (linearType === DATABAR_TRUNCATED_LINEAR_TYPE && !supportsGs1CompositeTruncated(capabilities)) return false
  if (linearType === DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE &&
    !supportsGs1CompositeStackedOmnidirectional(capabilities)) return false
  const requiredLinearHeight = linearType === DATABAR_TRUNCATED_LINEAR_TYPE
    ? minimumGs1DataBarTruncatedLinearHeight(element.ModuleWidth ?? 2)
    : linearType === DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE
      ? minimumGs1DataBarStackedOmnidirectionalLinearHeight(element.ModuleWidth ?? 2)
      : 1
  if (requiredLinearHeight > 2147483647) return false
  element.LinearType = linearType
  if (linearType === GS1_128_LINEAR_TYPE) {
    element.Mode = supportsGs1CompositeCcC(capabilities) ? GS1_COMPOSITE_CC_C_MODE : 1
    if (!element.Field?.Name && !element.Field?.Value?.trim()) {
      element.Field = { ...(element.Field ?? {}), Value: DEFAULT_GS1_128_COMPOSITE_PRIMARY }
    }
  } else if (linearType === DATABAR_EXPANDED_LINEAR_TYPE) {
    if (element.Mode === GS1_COMPOSITE_CC_C_MODE) element.Mode = 0
    if (!element.Field?.Name && !element.Field?.Value?.trim()) {
      element.Field = { ...(element.Field ?? {}), Value: DEFAULT_GS1_EXPANDED_COMPOSITE_PRIMARY }
    }
  } else if (linearType === DATABAR_EXPANDED_STACKED_LINEAR_TYPE) {
    if (element.Mode === GS1_COMPOSITE_CC_C_MODE) element.Mode = 0
    if (!element.Field?.Name && !element.Field?.Value?.trim()) {
      element.Field = { ...(element.Field ?? {}), Value: DEFAULT_GS1_EXPANDED_COMPOSITE_PRIMARY }
    }
  } else if (isGs1CompositeRetailLinearType(linearType)) {
    if (element.Mode === GS1_COMPOSITE_CC_C_MODE) element.Mode = 0
    if (!element.Field?.Name && !element.Field?.Value?.trim()) {
      element.Field = { ...(element.Field ?? {}), Value: DEFAULT_GS1_EAN_UPC_COMPOSITE_PRIMARY[linearType] }
    }
  } else if (element.Mode === GS1_COMPOSITE_CC_C_MODE) {
    element.Mode = 0
  }
  if (requiredLinearHeight > 1 && (element.LinearBarHeight ?? 0) < requiredLinearHeight)
    element.LinearBarHeight = requiredLinearHeight
  return true
}

export function setGs1CompositeMode(element: Gs1CompositeElement, mode: Gs1CompositeMode,
  capabilities?: DesignerCapabilities): boolean {
  if (!isValidGs1CompositeCombination(element.LinearType, mode)) return false
  if (isGs1CompositeCcCCombination(element.LinearType, mode) && !supportsGs1CompositeCcC(capabilities)) return false
  if (isGs1CompositeGs1128CcAbCombination(element.LinearType, mode) && !supportsGs1CompositeGs1128CcAb(capabilities)) return false
  if (isGs1CompositeExpandedCombination(element.LinearType, mode) && !supportsGs1CompositeExpanded(capabilities)) return false
  if (isGs1CompositeStackedCombination(element.LinearType, mode) && !supportsGs1CompositeStacked(capabilities)) return false
  if (isGs1CompositeExpandedStackedCombination(element.LinearType, mode) &&
    !supportsGs1CompositeExpandedStacked(capabilities)) return false
  if (isGs1CompositeEanUpcCombination(element.LinearType, mode) &&
    !supportsGs1CompositeEanUpc(capabilities)) return false
  if (isGs1CompositeTruncatedCombination(element.LinearType, mode) &&
    !supportsGs1CompositeTruncated(capabilities)) return false
  if (isGs1CompositeStackedOmnidirectionalCombination(element.LinearType, mode) &&
    !supportsGs1CompositeStackedOmnidirectional(capabilities)) return false
  element.Mode = mode
  return true
}
