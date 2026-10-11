import { describe, expect, it } from 'vitest'
import {
  barcodeOptions, fixedBarcodeWideMultiplier, fixedBarcodeWideWidth, isTsplVariableBarcodeType,
  legacyRatioForTsplBarcodeRatio, tsplBarcodeModulePairForRatio, tsplBarcodeRatioForModules,
  tsplBarcodeRatioFromLegacyRatio, tsplVariableBarcodeRatios,
} from './barcodeOptions'

describe('designer barcode options', () => {
  it('exposes the TSPL barcode types added after the original enum range', () => {
    expect(barcodeOptions).toEqual(expect.arrayContaining([
      ['PLESSEY', 17], ['ITF-14', 18], ['EAN-14', 19], ['Code 11', 20], ['China Post', 21],
      ['TELEPEN', 22], ['TELEPENN', 23], ['CODE49', 24],
      ['GS1 DataBar Omnidirectional', 25], ['GS1 DataBar Truncated', 26],
      ['GS1 DataBar Limited', 27], ['GS1 DataBar Expanded', 28], ['CODE128 auto', 29],
    ]))
  })

  it('keeps fixed-ratio TSPL defaults valid when the narrow width changes', () => {
    expect(fixedBarcodeWideMultiplier(17)).toBe(2)
    expect(fixedBarcodeWideMultiplier(19)).toBe(1)
    expect(fixedBarcodeWideMultiplier(21)).toBe(1)
    expect(fixedBarcodeWideMultiplier(18)).toBeUndefined()
    expect(fixedBarcodeWideWidth(17, 3)).toBe(6)
    expect(fixedBarcodeWideWidth(19, 3)).toBe(3)
    expect(fixedBarcodeWideWidth(21, 3)).toBe(3)
    expect(fixedBarcodeWideWidth(17, 0)).toBeUndefined()
  })

  it('exposes the five TSPL variable ratios for Telepen and Code 49', () => {
    expect(tsplVariableBarcodeRatios.map((ratio) => ratio.value)).toEqual(['1:1', '1:2', '1:3', '2:5', '3:7'])
    expect(isTsplVariableBarcodeType(22)).toBe(true)
    expect(isTsplVariableBarcodeType(23)).toBe(true)
    expect(isTsplVariableBarcodeType(24)).toBe(true)
    expect(isTsplVariableBarcodeType(21)).toBe(false)
  })

  it('scales TSPL ratios to the nearest valid module pair in the capability range', () => {
    expect(tsplBarcodeModulePairForRatio('1:2', 3, { minimum: 1, maximum: 6 })).toEqual({ narrow: 3, wide: 6 })
    expect(tsplBarcodeModulePairForRatio('2:5', 3, { minimum: 1, maximum: 10 })).toEqual({ narrow: 4, wide: 10 })
    expect(tsplBarcodeModulePairForRatio('3:7', 3, { minimum: 1, maximum: 6 })).toBeUndefined()
    expect(tsplBarcodeModulePairForRatio('1:1', 5, { minimum: 2, maximum: 6 })).toEqual({ narrow: 5, wide: 5 })
    expect(tsplBarcodeRatioForModules(4, 10)).toBe('2:5')
    expect(tsplBarcodeRatioForModules(3, 8)).toBeUndefined()
  })

  it('keeps legacy Ratio compatibility explicit while importing valid choices', () => {
    expect(legacyRatioForTsplBarcodeRatio('1:1')).toBe(0)
    expect(legacyRatioForTsplBarcodeRatio('3:7')).toBe(2)
    expect(tsplBarcodeRatioFromLegacyRatio(1)).toBe('1:2')
    expect(tsplBarcodeRatioFromLegacyRatio(25)).toBe('2:5')
    expect(tsplBarcodeRatioFromLegacyRatio(0)).toBeUndefined()
  })
})
