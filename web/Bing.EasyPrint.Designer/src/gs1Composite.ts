import type { DesignerCapabilities } from './capabilities'
import type { Gs1CompositeElement } from './model'

export const GS1_128_LINEAR_TYPE = 11 as const
export const DATABAR_OMNIDIRECTIONAL_LINEAR_TYPE = 25 as const
export const DATABAR_LIMITED_LINEAR_TYPE = 27 as const
export const DATABAR_EXPANDED_LINEAR_TYPE = 28 as const
export const GS1_COMPOSITE_CC_C_MODE = 3 as const
export const DEFAULT_GS1_128_COMPOSITE_PRIMARY = '(01)12345678901231'
export const DEFAULT_GS1_EXPANDED_COMPOSITE_PRIMARY = '(01)09506000134352(10)LOT-A'

export type Gs1CompositeLinearType = typeof GS1_128_LINEAR_TYPE |
  typeof DATABAR_OMNIDIRECTIONAL_LINEAR_TYPE | typeof DATABAR_LIMITED_LINEAR_TYPE | typeof DATABAR_EXPANDED_LINEAR_TYPE
export type Gs1CompositeMode = 0 | 1 | 2 | typeof GS1_COMPOSITE_CC_C_MODE
export type Gs1CompositeModeChoice = { value: Gs1CompositeMode; label: string; disabled: boolean }

const dataBarModes: Gs1CompositeModeChoice[] = [
  { value: 0, label: 'Auto', disabled: false },
  { value: 1, label: 'CC-A', disabled: false },
  { value: 2, label: 'CC-B', disabled: false },
]
const gs1_128Modes: Gs1CompositeModeChoice[] = [
  { value: 0, label: 'Auto', disabled: false },
  { value: GS1_COMPOSITE_CC_C_MODE, label: 'CC-C', disabled: false },
]

export function isValidGs1CompositeCombination(linearType: number | undefined, mode: number | undefined): boolean {
  const linear = linearType ?? DATABAR_OMNIDIRECTIONAL_LINEAR_TYPE
  const selectedMode = mode ?? 0
  if (linear === GS1_128_LINEAR_TYPE) return selectedMode === 0 || selectedMode === GS1_COMPOSITE_CC_C_MODE
  if (linear === DATABAR_EXPANDED_LINEAR_TYPE) return selectedMode === 0 || selectedMode === 1 || selectedMode === 2
  return (linear === DATABAR_OMNIDIRECTIONAL_LINEAR_TYPE || linear === DATABAR_LIMITED_LINEAR_TYPE) &&
    (selectedMode === 0 || selectedMode === 1 || selectedMode === 2)
}

export function isGs1CompositeCcCCombination(linearType: number | undefined, mode: number | undefined): boolean {
  return linearType === GS1_128_LINEAR_TYPE && (mode === undefined || mode === 0 || mode === GS1_COMPOSITE_CC_C_MODE)
}

export function supportsGs1CompositeCcC(capabilities?: DesignerCapabilities): boolean {
  return Boolean(capabilities?.gs1CompositeFeature && capabilities.supportsGs1Composite &&
    capabilities.elementTypes.includes(11) &&
    capabilities.gs1CompositeCcCFeature && capabilities.gs1Composite?.modes.includes('CcC') &&
    capabilities.gs1Composite.linearTypes.includes('UccEan128'))
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

export function gs1CompositeModeChoices(linearType: number | undefined,
  capabilities?: DesignerCapabilities): Gs1CompositeModeChoice[] {
  const choices = linearType === GS1_128_LINEAR_TYPE ? gs1_128Modes : dataBarModes
  const supportedModes = capabilities?.gs1Composite?.modes
  return choices.map((choice) => ({
    ...choice,
    disabled: choice.disabled || (linearType === GS1_128_LINEAR_TYPE
      ? !supportsGs1CompositeCcC(capabilities)
      : supportedModes !== undefined && !supportedModes.includes(
        choice.value === 0 ? 'Auto' : choice.value === 1 ? 'CcA' : 'CcB')),
  }))
}

export function setGs1CompositeLinearType(element: Gs1CompositeElement, linearType: Gs1CompositeLinearType,
  capabilities?: DesignerCapabilities): boolean {
  if (linearType === GS1_128_LINEAR_TYPE && !supportsGs1CompositeCcC(capabilities)) return false
  if (linearType === DATABAR_EXPANDED_LINEAR_TYPE && !supportsGs1CompositeExpanded(capabilities)) return false
  element.LinearType = linearType
  if (linearType === GS1_128_LINEAR_TYPE) {
    element.Mode = GS1_COMPOSITE_CC_C_MODE
    if (!element.Field?.Name && !element.Field?.Value?.trim()) {
      element.Field = { ...(element.Field ?? {}), Value: DEFAULT_GS1_128_COMPOSITE_PRIMARY }
    }
  } else if (linearType === DATABAR_EXPANDED_LINEAR_TYPE) {
    if (element.Mode === GS1_COMPOSITE_CC_C_MODE) element.Mode = 0
    if (!element.Field?.Name && !element.Field?.Value?.trim()) {
      element.Field = { ...(element.Field ?? {}), Value: DEFAULT_GS1_EXPANDED_COMPOSITE_PRIMARY }
    }
  } else if (element.Mode === GS1_COMPOSITE_CC_C_MODE) {
    element.Mode = 0
  }
  return true
}

export function setGs1CompositeMode(element: Gs1CompositeElement, mode: Gs1CompositeMode,
  capabilities?: DesignerCapabilities): boolean {
  if (!isValidGs1CompositeCombination(element.LinearType, mode)) return false
  if (isGs1CompositeCcCCombination(element.LinearType, mode) && !supportsGs1CompositeCcC(capabilities)) return false
  if (isGs1CompositeExpandedCombination(element.LinearType, mode) && !supportsGs1CompositeExpanded(capabilities)) return false
  element.Mode = mode
  return true
}
