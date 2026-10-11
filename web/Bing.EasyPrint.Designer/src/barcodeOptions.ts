export const barcodeOptions = [
  ['CODE 128', 0], ['CODE 39', 1], ['CODE 93', 2], ['Codabar', 3], ['EAN-8', 4], ['EAN-13', 5],
  ['UPC-A', 6], ['UPC-E', 7], ['I2OF5', 8], ['I2OF5C', 9], ['I2OF5G', 10], ['UCC/EAN-128', 11],
  ['MSI', 12], ['POSTNET', 13], ['FIM', 14], ['PLANET', 15], ['INTELLIGENT MAIL', 16],
  ['PLESSEY', 17], ['ITF-14', 18], ['EAN-14', 19], ['Code 11', 20], ['China Post', 21],
  ['TELEPEN', 22], ['TELEPENN', 23], ['CODE49', 24],
  ['GS1 DataBar Omnidirectional', 25], ['GS1 DataBar Truncated', 26],
  ['GS1 DataBar Limited', 27], ['GS1 DataBar Expanded', 28], ['CODE128 auto', 29],
] as const

export type TsplVariableBarcodeType = 22 | 23 | 24
export type TsplBarcodeRatioKey = '1:1' | '1:2' | '1:3' | '2:5' | '3:7'
export type BarcodeModuleRange = { minimum: number; maximum: number | null }
export type TsplBarcodeRatioOption = {
  value: TsplBarcodeRatioKey
  label: string
  narrowFactor: number
  wideFactor: number
  legacyRatio: number
}

/** TSPL TELEPEN/TELEPENN/CODE49 的窄条:宽条比例；数值使用最小整数模块对。 */
export const tsplVariableBarcodeRatios: readonly TsplBarcodeRatioOption[] = [
  { value: '1:1', label: '窄条:宽条 1:1', narrowFactor: 1, wideFactor: 1, legacyRatio: 0 },
  { value: '1:2', label: '窄条:宽条 1:2', narrowFactor: 1, wideFactor: 2, legacyRatio: 1 },
  { value: '1:3', label: '窄条:宽条 1:3', narrowFactor: 1, wideFactor: 3, legacyRatio: 3 },
  { value: '2:5', label: '窄条:宽条 2:5', narrowFactor: 2, wideFactor: 5, legacyRatio: 2 },
  { value: '3:7', label: '窄条:宽条 3:7', narrowFactor: 3, wideFactor: 7, legacyRatio: 2 },
]

export function isTsplVariableBarcodeType(type: number | undefined): type is TsplVariableBarcodeType {
  return type === 22 || type === 23 || type === 24
}

/** 返回一对模块宽度对应的 TSPL 原生比例；不成比例时返回 undefined。 */
export function tsplBarcodeRatioForModules(narrow: number | undefined, wide: number | undefined): TsplBarcodeRatioKey | undefined {
  if (!Number.isInteger(narrow) || !Number.isInteger(wide) || narrow === undefined || wide === undefined || narrow < 1 || wide < 1) return undefined
  return tsplVariableBarcodeRatios.find((ratio) => wide * ratio.narrowFactor === narrow * ratio.wideFactor)?.value
}

/**
 * 按能力范围为 TSPL 比例选择最接近首选窄条宽度的整数模块对。
 * 比例为 2:5 或 3:7 时会自动放大最小整数对，避免用不可约的奇数窄条伪造比例。
 */
export function tsplBarcodeModulePairForRatio(
  value: TsplBarcodeRatioKey,
  preferredNarrow: number,
  range?: BarcodeModuleRange | null,
): { narrow: number; wide: number } | undefined {
  const ratio = tsplVariableBarcodeRatios.find((item) => item.value === value)
  if (!ratio || !Number.isInteger(preferredNarrow) || preferredNarrow < 1) return undefined
  const minimum = range?.minimum ?? 1
  const maximum = range?.maximum ?? null
  if (!Number.isInteger(minimum) || minimum < 1 || maximum !== null && (!Number.isInteger(maximum) || maximum < minimum)) return undefined
  const minimumScale = Math.max(Math.ceil(minimum / ratio.narrowFactor), Math.ceil(minimum / ratio.wideFactor))
  const maximumScale = maximum === null
    ? null
    : Math.min(Math.floor(maximum / ratio.narrowFactor), Math.floor(maximum / ratio.wideFactor))
  if (maximumScale !== null && minimumScale > maximumScale) return undefined
  const preferredScale = Math.max(minimumScale, Math.round(preferredNarrow / ratio.narrowFactor))
  const scale = maximumScale === null ? preferredScale : Math.min(preferredScale, maximumScale)
  return { narrow: ratio.narrowFactor * scale, wide: ratio.wideFactor * scale }
}

/** 将 TSPL 比例映射到旧 Ratio 字段，显式模块宽度仍是 TSPL 的实际来源。 */
export function legacyRatioForTsplBarcodeRatio(value: TsplBarcodeRatioKey): number {
  return tsplVariableBarcodeRatios.find((ratio) => ratio.value === value)?.legacyRatio ?? 1
}

/** 识别旧 Ratio 中仍能无损表达的 TSPL 比例，供导入旧模板时恢复选择器状态。 */
export function tsplBarcodeRatioFromLegacyRatio(value: number | undefined): TsplBarcodeRatioKey | undefined {
  if (value === 1 || value === 20) return '1:2'
  if (value === 2 || value === 25) return '2:5'
  if (value === 3 || value === 30) return '1:3'
  return undefined
}

/** 返回要求固定宽条比例的 TSPL 条码倍数；其他制式由 Ratio 或协议决定。 */
export function fixedBarcodeWideMultiplier(type: number): number | undefined {
  if (type === 17) return 2
  if (type === 19) return 1
  if (type === 21) return 1
  return undefined
}

/** 根据窄条点宽计算固定比例制式的合法宽条点宽。 */
export function fixedBarcodeWideWidth(type: number, narrow: number): number | undefined {
  const multiplier = fixedBarcodeWideMultiplier(type)
  if (multiplier === undefined || !Number.isInteger(narrow) || narrow < 1) return undefined
  return narrow * multiplier
}
