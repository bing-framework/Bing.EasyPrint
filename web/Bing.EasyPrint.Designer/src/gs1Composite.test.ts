import { afterEach, describe, expect, it, vi } from 'vitest'
import { loadDesignerCapabilities, previewTemplate, renderTemplate } from './api'
import { exportTemplateBundle, importTemplateBundle } from './archive'
import { canCreateElement, normalizeDesignerCapabilities, rotationSupported } from './capabilities'
import { parseDesignerProject, serializeDesignerProject } from './designerProject'
import {
  DEFAULT_GS1_EAN_UPC_COMPOSITE_PRIMARY, EAN_8_LINEAR_TYPE, EAN_13_LINEAR_TYPE,
  DATABAR_TRUNCATED_LINEAR_TYPE, DATABAR_EXPANDED_LINEAR_TYPE, DATABAR_STACKED_LINEAR_TYPE,
  DATABAR_EXPANDED_STACKED_LINEAR_TYPE, DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE,
  GS1_COMPOSITE_EAN_UPC_FEATURE, GS1_COMPOSITE_EAN_UPC_ADDONS_FEATURE,
  DEFAULT_GS1_128_COMPOSITE_PRIMARY,
  DEFAULT_GS1_EXPANDED_COMPOSITE_PRIMARY, GS1_128_LINEAR_TYPE, GS1_COMPOSITE_CC_C_MODE,
  UPC_A_LINEAR_TYPE, UPC_E_LINEAR_TYPE,
  GS1_COMPOSITE_GS1_128_CC_AB_FEATURE,
  GS1_COMPOSITE_TRUNCATED_FEATURE, GS1_COMPOSITE_STACKED_OMNIDIRECTIONAL_FEATURE,
  gs1CompositeModeChoices, gs1CompositeRetailPrimaryError, isValidGs1CompositeCombination,
  supportsGs1CompositeEanUpcAddons,
  minimumGs1DataBarTruncatedLinearHeight, minimumGs1DataBarStackedOmnidirectionalLinearHeight,
  setGs1CompositeLinearType, setGs1CompositeMode,
  supportsGs1CompositeCcC, supportsGs1CompositeExpanded, supportsGs1CompositeGs1128CcAb,
  supportsGs1CompositeStacked, supportsGs1CompositeExpandedStacked, supportsGs1CompositeEanUpc,
  supportsGs1CompositeTruncated, supportsGs1CompositeStackedOmnidirectional,
} from './gs1Composite'
import { buildLocalLayout } from './layout'
import { assertTemplate, cloneTemplate, type BpTemplate, type Gs1CompositeElement } from './model'
import { createBlankSample } from './samples'
import { elementLabel } from './tree'

afterEach(() => vi.unstubAllGlobals())

it.each(['12345670+12\n', '12345670+12345\n', '123456\n+12', '123456\n'])(
  'rejects final newlines without JavaScript end-anchor leniency: %s', (value) => {
    expect(gs1CompositeRetailPrimaryError(EAN_8_LINEAR_TYPE, value)).toBeDefined()
  })

function template(linearType = 27, mode = 2): BpTemplate {
  return { Version: 3, Width: 400, Height: 300, Rows: [{ Cells: [{ Elements: [{
    Type: 11, LinearType: linearType as 4 | 5 | 6 | 7 | 11 | 25 | 26 | 27 | 28 | 30 | 31 | 32, Mode: mode as 0 | 1 | 2 | 3, ModuleWidth: 2, LinearBarHeight: 40, Rotation: 90,
    Field: { Name: 'Gtin' }, SecondaryField: { Name: 'Lot' }, QuietZone: { Left: 3, Top: 5, Right: 7, Bottom: 11 },
  }] }] }] }
}

function caps(ccC = false, expanded = false, stacked = false, expandedStacked = false,
  eanUpc = false, truncatedStackedOmnidirectional = false, eanUpcAddons = false): Record<string, unknown> {
  return {
    protocol: 'Cpcl', escPosLayoutMode: null, nativeFlow: null, jobFeedUnits: [], dpi: 200,
    elementTypes: [1, 2, 11], fontKinds: [0], deviceFonts: [], textSizes: null, textSize: { minimum: 1, maximum: null },
    textRotations: [0], imageRotations: [0, 90, 180, 270], barcodeTypes: [], barcodeRotations: [0],
    qrModuleSize: null, barcodeModuleWidth: null, barcodeHeight: null,
    supportsExplicitBarcodeModuleWidth: false, supportsWideModuleWidth: false,
    supportsBoldText: false, supportsUnderlineText: false, supportsRotatedUnderlineText: false,
    supportsHostFontFamily: false, supportsLegacyDeviceFont: false, supportsBarcodeHumanReadable: false,
    supportsCut: false, supportsExtraFeed: false, supportsFilledRectangles: true,
    supportsCoordinates: true, supportsExplicitQuietZone: true, supportsGs1Composite: true,
    gs1Composite: { elementType: 11,
      linearTypes: truncatedStackedOmnidirectional
        ? ['Gs1DataBarOmnidirectional', 'Gs1DataBarLimited', 'UccEan128', 'Gs1DataBarExpanded', 'Gs1DataBarStacked', 'Gs1DataBarExpandedStacked', 'Ean8', 'Ean13', 'UpcA', 'UpcE', 'Gs1DataBarTruncated', 'Gs1DataBarStackedOmnidirectional']
        : eanUpc
        ? ['Gs1DataBarOmnidirectional', 'Gs1DataBarLimited', 'UccEan128', 'Gs1DataBarExpanded', 'Gs1DataBarStacked', 'Gs1DataBarExpandedStacked', 'Ean8', 'Ean13', 'UpcA', 'UpcE']
        : expandedStacked
        ? ['Gs1DataBarOmnidirectional', 'Gs1DataBarLimited', 'UccEan128', 'Gs1DataBarExpanded', 'Gs1DataBarStacked', 'Gs1DataBarExpandedStacked']
        : stacked
        ? ['Gs1DataBarOmnidirectional', 'Gs1DataBarLimited', 'UccEan128', 'Gs1DataBarExpanded', 'Gs1DataBarStacked']
        : expanded
          ? ['Gs1DataBarOmnidirectional', 'Gs1DataBarLimited', 'UccEan128', 'Gs1DataBarExpanded']
        : ccC ? ['Gs1DataBarOmnidirectional', 'Gs1DataBarLimited', 'UccEan128'] : ['Gs1DataBarOmnidirectional', 'Gs1DataBarLimited'],
      modes: ccC || expanded || stacked || expandedStacked ? ['Auto', 'CcA', 'CcB', 'CcC'] : ['Auto', 'CcA', 'CcB'],
      rotations: [0, 90, 180, 270], primaryFormats: ['GTIN-13-data', 'GTIN-14', 'GS1-AI-01',
        'bracketed-gs1-ai', 'ean-upc-digits', ...(eanUpcAddons ? ['ean-upc-digits-with-addon'] : [])],
      includesHumanReadable: false, dimensionsFromEncoding: true },
  }
}

function headers(composite = true, ccC = false, expanded = false, stacked = false, gs1128CcAb = false,
  expandedStacked = false, eanUpc = false, truncated = false, stackedOmnidirectional = false,
  eanUpcAddons = false): Record<string, string> {
  return { 'Content-Type': 'application/json', 'X-EasyPrint-Contract-Version': '1',
    'X-EasyPrint-Features': `structured-errors,strict-options,binding-diagnostics${composite ? ',gs1-composite' : ''}${ccC ? ',gs1-composite-cc-c' : ''}${expanded ? ',gs1-composite-expanded' : ''}${stacked ? ',gs1-composite-stacked' : ''}${gs1128CcAb ? `,${GS1_COMPOSITE_GS1_128_CC_AB_FEATURE}` : ''}${expandedStacked ? ',gs1-composite-expanded-stacked' : ''}${eanUpc ? `,${GS1_COMPOSITE_EAN_UPC_FEATURE}` : ''}${truncated ? `,${GS1_COMPOSITE_TRUNCATED_FEATURE}` : ''}${stackedOmnidirectional ? `,${GS1_COMPOSITE_STACKED_OMNIDIRECTIONAL_FEATURE}` : ''}${eanUpcAddons ? `,${GS1_COMPOSITE_EAN_UPC_ADDONS_FEATURE}` : ''}` }
}

async function loadWithCompositeShape(body: Record<string, unknown>, responseHeaders: Record<string, string>) {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(body), { headers: responseHeaders })))
  return loadDesignerCapabilities({ protocol: 'Cpcl' })
}

describe('GS1 Composite designer contract', () => {
  it.each([2, 3] as const)('preserves both bindings and all parameters in V%s JSON and project round trips', (version) => {
    const original = template()
    original.Version = version
    assertTemplate(original)
    expect(parseDesignerProject(JSON.parse(serializeDesignerProject(original))).template).toEqual(original)
    expect(parseDesignerProject(JSON.parse(JSON.stringify(original))).template).toEqual(original)
    const edited = cloneTemplate(original)
    const element = edited.Rows[0]!.Cells![0]!.Elements![0]! as Gs1CompositeElement
    element.Field = { Value: '12345678901231' }
    element.SecondaryField = { Value: '(17)010615(10)A123456' }
    element.Mode = 0
    expect(parseDesignerProject(JSON.parse(serializeDesignerProject(edited))).template).toEqual(edited)
    expect(elementLabel(element)).toContain('GS1 Composite')
  })

  it.each(['SecondaryField', 'LinearType', 'Mode', 'ModuleWidth', 'LinearBarHeight', 'Rotation', 'QuietZone'])('rejects malformed %s without silently replacing it', (key) => {
    const value = template()
    const element = value.Rows[0]!.Cells![0]!.Elements![0]! as unknown as Record<string, unknown>
    element[key] = key === 'SecondaryField' ? null : key === 'QuietZone' ? { Left: -1 } : -1
    expect(() => assertTemplate(value)).toThrow()
  })

  it('requires a V1 template upgrade', () => {
    const value = template()
    value.Version = 1
    expect(() => assertTemplate(value)).toThrow('Version: 1')
  })

  it('accepts only the declared GS1 Composite linear-type and mode pairs', () => {
    for (const [linearType, mode] of [[25, 0], [25, 1], [25, 2], [27, 0], [27, 1], [27, 2],
      [DATABAR_EXPANDED_LINEAR_TYPE, 0], [DATABAR_EXPANDED_LINEAR_TYPE, 1], [DATABAR_EXPANDED_LINEAR_TYPE, 2],
      [DATABAR_STACKED_LINEAR_TYPE, 0], [DATABAR_STACKED_LINEAR_TYPE, 1], [DATABAR_STACKED_LINEAR_TYPE, 2],
      [GS1_128_LINEAR_TYPE, 0], [GS1_128_LINEAR_TYPE, 1], [GS1_128_LINEAR_TYPE, 2],
      [GS1_128_LINEAR_TYPE, GS1_COMPOSITE_CC_C_MODE]] as const) {
      expect(() => assertTemplate(template(linearType, mode))).not.toThrow()
      expect(isValidGs1CompositeCombination(linearType, mode)).toBe(true)
    }
    for (const [linearType, mode] of [[25, 3], [27, 3], [DATABAR_EXPANDED_LINEAR_TYPE, 3], [DATABAR_STACKED_LINEAR_TYPE, 3],
      [GS1_128_LINEAR_TYPE, 4], [99, 0]] as const) {
      expect(() => assertTemplate(template(linearType, mode))).toThrow('LinearType 与 Mode 组合无效')
      expect(isValidGs1CompositeCombination(linearType, mode)).toBe(false)
    }
  })

  it('requires Stacked total row height to be at least twelve modules', () => {
    const valid = template(DATABAR_STACKED_LINEAR_TYPE, 2)
    assertTemplate(valid)
    const invalid = template(DATABAR_STACKED_LINEAR_TYPE, 2)
    ;(invalid.Rows[0]!.Cells![0]!.Elements![0]! as Gs1CompositeElement).LinearBarHeight = 23
    expect(() => assertTemplate(invalid)).toThrow('LinearBarHeight 对 Stacked 必须至少为模块宽度的 12 倍')
  })

  it.each([0, 1, 2, GS1_COMPOSITE_CC_C_MODE])('roundtrips valid GS1-128 Composite mode %s without changing JSON fields', (mode) => {
    const original = template(GS1_128_LINEAR_TYPE, mode)
    assertTemplate(original)
    expect(parseDesignerProject(JSON.parse(serializeDesignerProject(original))).template).toEqual(original)
    expect(parseDesignerProject(JSON.parse(JSON.stringify(original))).template).toEqual(original)
  })

  it('retains legacy DataBar Composite while withholding GS1-128 and CC-C from old Hosts', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(caps()), { headers: headers(true) })))
    const result = await loadDesignerCapabilities({ protocol: 'Cpcl' })
    expect(canCreateElement(result, 'ready', 11)).toBe(true)
    expect(result.gs1CompositeCcCFeature).toBe(false)
    expect(result.gs1CompositeExpandedFeature).toBe(false)
    expect(result.gs1CompositeStackedFeature).toBe(false)
    expect(result.gs1CompositeGs1128CcAbFeature).toBe(false)
    expect(result.gs1CompositeEanUpcFeature).toBe(false)
    expect(supportsGs1CompositeCcC(result)).toBe(false)
    expect(supportsGs1CompositeExpanded(result)).toBe(false)
    expect(supportsGs1CompositeStacked(result)).toBe(false)
    expect(gs1CompositeModeChoices(25, result).map((mode) => mode.disabled)).toEqual([false, false, false])
    expect(gs1CompositeModeChoices(GS1_128_LINEAR_TYPE, result).map((mode) => mode.disabled)).toEqual([true, true, true, true])
    expect(gs1CompositeModeChoices(DATABAR_STACKED_LINEAR_TYPE, result).map((mode) => mode.disabled)).toEqual([true, true, true])
    expect(gs1CompositeModeChoices(EAN_8_LINEAR_TYPE, result).map((mode) => mode.disabled)).toEqual([true, true, true])
    const existing = template().Rows[0]!.Cells![0]!.Elements![0]! as Gs1CompositeElement
    expect(setGs1CompositeLinearType(existing, GS1_128_LINEAR_TYPE, result)).toBe(false)
    expect(setGs1CompositeLinearType(existing, EAN_8_LINEAR_TYPE, result)).toBe(false)
    expect(existing.LinearType).toBe(27)
  })

  it('enables GS1-128 CC-C only after the new feature and capabilities are negotiated', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(caps(true)), { headers: headers(true, true) })))
    const result = await loadDesignerCapabilities({ protocol: 'Cpcl' })
    expect(supportsGs1CompositeCcC(result)).toBe(true)
    expect(gs1CompositeModeChoices(GS1_128_LINEAR_TYPE, result).map((mode) => mode.disabled)).toEqual([false, true, true, false])
    expect(gs1CompositeModeChoices(GS1_128_LINEAR_TYPE, result).slice(1, 3).map((mode) => mode.reason))
      .toEqual([
        '当前 Host 未协商 GS1-128 + CC-A/CC-B Composite 功能。',
        '当前 Host 未协商 GS1-128 + CC-A/CC-B Composite 功能。',
      ])

    const element = template(25, 1).Rows[0]!.Cells![0]!.Elements![0]! as Gs1CompositeElement
    element.Field = { Value: '' }
    expect(setGs1CompositeLinearType(element, GS1_128_LINEAR_TYPE, result)).toBe(true)
    expect(element.LinearType).toBe(GS1_128_LINEAR_TYPE)
    expect(element.Mode).toBe(GS1_COMPOSITE_CC_C_MODE)
    expect(element.Field?.Value).toBe(DEFAULT_GS1_128_COMPOSITE_PRIMARY)
    expect(() => assertTemplate(template(GS1_128_LINEAR_TYPE, 0))).not.toThrow()

    const oldHostElement = template(GS1_128_LINEAR_TYPE, GS1_COMPOSITE_CC_C_MODE).Rows[0]!.Cells![0]!.Elements![0]! as Gs1CompositeElement
    expect(setGs1CompositeMode(oldHostElement, 1, result)).toBe(false)
    expect(oldHostElement.Mode).toBe(GS1_COMPOSITE_CC_C_MODE)
  })

  it('enables GS1-128 CC-A/CC-B only when the feature, base capability and pair are present', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(caps(true)), {
      headers: headers(true, true, false, false, true),
    })))
    const result = await loadDesignerCapabilities({ protocol: 'Cpcl' })
    expect(result.gs1CompositeGs1128CcAbFeature).toBe(true)
    expect(supportsGs1CompositeGs1128CcAb(result)).toBe(true)
    expect(gs1CompositeModeChoices(GS1_128_LINEAR_TYPE, result).map((mode) => mode.disabled))
      .toEqual([false, false, false, false])

    const element = template(25, 2).Rows[0]!.Cells![0]!.Elements![0]! as Gs1CompositeElement
    expect(setGs1CompositeLinearType(element, GS1_128_LINEAR_TYPE, result)).toBe(true)
    expect(element.Mode).toBe(GS1_COMPOSITE_CC_C_MODE)
    for (const mode of [1, 2] as const) {
      expect(setGs1CompositeMode(element, mode, result)).toBe(true)
      expect(element.Mode).toBe(mode)
    }

    expect(supportsGs1CompositeGs1128CcAb({ ...result, gs1CompositeGs1128CcAbFeature: false })).toBe(false)
    expect(supportsGs1CompositeGs1128CcAb({ ...result, gs1CompositeFeature: false })).toBe(false)
    expect(supportsGs1CompositeGs1128CcAb({ ...result, supportsGs1Composite: false })).toBe(false)
    expect(supportsGs1CompositeGs1128CcAb({ ...result, elementTypes: [1, 2] })).toBe(false)
    expect(supportsGs1CompositeGs1128CcAb({ ...result,
      gs1Composite: { ...result.gs1Composite!, linearTypes: ['Gs1DataBarOmnidirectional', 'Gs1DataBarLimited'] } })).toBe(false)
    expect(supportsGs1CompositeGs1128CcAb({ ...result,
      gs1Composite: { ...result.gs1Composite!, modes: ['Auto', 'CcA', 'CcC'] } })).toBe(false)
  })

  it('rejects a capability response that advertises CC-C without its GS1-128 linear type', () => {
    const response = caps(true)
    const composite = response.gs1Composite as Record<string, unknown>
    composite.linearTypes = ['Gs1DataBarOmnidirectional', 'Gs1DataBarLimited']
    expect(() => normalizeDesignerCapabilities(response)).toThrow('gs1Composite 无效')
  })

  it('accepts Expanded capabilities only with the complete CC-C-era shape', () => {
    const response = caps(true, true)
    expect(normalizeDesignerCapabilities(response).gs1Composite?.linearTypes)
      .toEqual(['Gs1DataBarOmnidirectional', 'Gs1DataBarLimited', 'UccEan128', 'Gs1DataBarExpanded'])
    const incomplete = caps(true, true)
    ;(incomplete.gs1Composite as Record<string, unknown>).linearTypes = ['Gs1DataBarOmnidirectional', 'Gs1DataBarLimited', 'Gs1DataBarExpanded']
    expect(() => normalizeDesignerCapabilities(incomplete)).toThrow('gs1Composite 无效')
  })

  it('accepts Stacked only as the appended post-Expanded capability shape', () => {
    const response = caps(true, true, true)
    expect(normalizeDesignerCapabilities(response).gs1Composite?.linearTypes)
      .toEqual(['Gs1DataBarOmnidirectional', 'Gs1DataBarLimited', 'UccEan128', 'Gs1DataBarExpanded', 'Gs1DataBarStacked'])
    const incomplete = caps(true, true, true)
    ;(incomplete.gs1Composite as Record<string, unknown>).linearTypes = [
      'Gs1DataBarOmnidirectional', 'Gs1DataBarLimited', 'UccEan128', 'Gs1DataBarStacked',
    ]
    expect(() => normalizeDesignerCapabilities(incomplete)).toThrow('gs1Composite 无效')
  })

  it('accepts Expanded Stacked only as the appended post-Stacked capability shape', () => {
    const response = caps(true, true, true, true)
    expect(normalizeDesignerCapabilities(response).gs1Composite?.linearTypes)
      .toEqual(['Gs1DataBarOmnidirectional', 'Gs1DataBarLimited', 'UccEan128', 'Gs1DataBarExpanded',
        'Gs1DataBarStacked', 'Gs1DataBarExpandedStacked'])
    const incomplete = caps(true, true, true, true)
    ;(incomplete.gs1Composite as Record<string, unknown>).linearTypes = [
      'Gs1DataBarOmnidirectional', 'Gs1DataBarLimited', 'UccEan128', 'Gs1DataBarExpanded',
      'Gs1DataBarExpandedStacked',
    ]
    expect(() => normalizeDesignerCapabilities(incomplete)).toThrow('gs1Composite 无效')
  })

  it('accepts EAN/UPC only as the four appended retail capability types', () => {
    const response = caps(true, true, true, true, true)
    expect(normalizeDesignerCapabilities(response).gs1Composite?.linearTypes).toEqual([
      'Gs1DataBarOmnidirectional', 'Gs1DataBarLimited', 'UccEan128', 'Gs1DataBarExpanded',
      'Gs1DataBarStacked', 'Gs1DataBarExpandedStacked', 'Ean8', 'Ean13', 'UpcA', 'UpcE',
    ])
    const reordered = caps(true, true, true, true, true)
    ;(reordered.gs1Composite as Record<string, unknown>).linearTypes = [
      'Gs1DataBarOmnidirectional', 'Gs1DataBarLimited', 'UccEan128', 'Gs1DataBarExpanded',
      'Gs1DataBarStacked', 'Ean8', 'Gs1DataBarExpandedStacked', 'Ean13', 'UpcA', 'UpcE',
    ]
    expect(() => normalizeDesignerCapabilities(reordered)).toThrow('gs1Composite 无效')
  })

  it('accepts the old EAN/UPC primary format list and the one appended addon format', () => {
    const legacy = normalizeDesignerCapabilities(caps(true, true, true, true, true, true))
    expect(legacy.gs1Composite?.primaryFormats).toEqual([
      'GTIN-13-data', 'GTIN-14', 'GS1-AI-01', 'bracketed-gs1-ai', 'ean-upc-digits',
    ])
    const withAddon = normalizeDesignerCapabilities(caps(true, true, true, true, true, true, true))
    expect(withAddon.gs1Composite?.primaryFormats).toEqual([
      'GTIN-13-data', 'GTIN-14', 'GS1-AI-01', 'bracketed-gs1-ai', 'ean-upc-digits',
      'ean-upc-digits-with-addon',
    ])
    const invalid = caps(true, true, true, true, true, true, true)
    ;(invalid.gs1Composite as Record<string, unknown>).primaryFormats = [
      'GTIN-13-data', 'GTIN-14', 'GS1-AI-01', 'bracketed-gs1-ai', 'ean-upc-digits-with-addon', 'ean-upc-digits',
    ]
    expect(() => normalizeDesignerCapabilities(invalid)).toThrow('gs1Composite 无效')
  })

  it.each([
    ['GTIN-13-data', 'GTIN-14', 'GS1-AI-01'],
    ['GTIN-13-data', 'GTIN-14', 'GS1-AI-01', 'bracketed-gs1-ai'],
  ])('preserves pre-retail Host primary formats: %s', (...formats) => {
    const legacy = caps(formats.length === 4)
    ;(legacy.gs1Composite as Record<string, unknown>).primaryFormats = formats
    expect(normalizeDesignerCapabilities(legacy).gs1Composite?.primaryFormats).toEqual(formats)
  })

  it('negotiates Truncated and Stacked Omnidirectional independently with their row-height limits', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(
      JSON.stringify(caps(true, true, true, true, true, true)),
      { headers: headers(true, true, true, true, false, true, true, true, true) },
    )))
    const result = await loadDesignerCapabilities({ protocol: 'Cpcl' })
    expect(result.gs1CompositeTruncatedFeature).toBe(true)
    expect(result.gs1CompositeStackedOmnidirectionalFeature).toBe(true)
    expect(result.gs1Composite?.linearTypes?.slice(-2)).toEqual([
      'Gs1DataBarTruncated', 'Gs1DataBarStackedOmnidirectional',
    ])
    expect(supportsGs1CompositeTruncated(result)).toBe(true)
    expect(supportsGs1CompositeStackedOmnidirectional(result)).toBe(true)

    for (const [linearType, minimumHeight] of [
      [DATABAR_TRUNCATED_LINEAR_TYPE, minimumGs1DataBarTruncatedLinearHeight(2)],
      [DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE, minimumGs1DataBarStackedOmnidirectionalLinearHeight(2)],
    ] as const) {
      expect([0, 1, 2].every((mode) => isValidGs1CompositeCombination(linearType, mode))).toBe(true)
      expect(isValidGs1CompositeCombination(linearType, GS1_COMPOSITE_CC_C_MODE)).toBe(false)
      expect(gs1CompositeModeChoices(linearType, result).map((choice) => choice.disabled)).toEqual([false, false, false])

      const element = template(25, GS1_COMPOSITE_CC_C_MODE).Rows[0]!.Cells![0]!.Elements![0]! as Gs1CompositeElement
      const retainedPrimary = element.Field
      element.LinearBarHeight = linearType === DATABAR_TRUNCATED_LINEAR_TYPE ? 25 : 40
      expect(setGs1CompositeLinearType(element, linearType, result)).toBe(true)
      expect(element.LinearType).toBe(linearType)
      expect(element.Mode).toBe(0)
      expect(element.Field).toEqual(retainedPrimary)
      expect(element.LinearBarHeight).toBe(minimumHeight)
      expect(setGs1CompositeMode(element, 2, result)).toBe(true)
      expect(() => assertTemplate({ ...template(linearType, 2),
        Rows: [{ Cells: [{ Elements: [element] }] }] })).not.toThrow()

      element.LinearBarHeight = minimumHeight - 1
      expect(() => assertTemplate({ ...template(linearType, 2),
        Rows: [{ Cells: [{ Elements: [element] }] }] })).toThrow('LinearBarHeight')
      expect(() => assertTemplate(template(linearType, GS1_COMPOSITE_CC_C_MODE))).toThrow('组合无效')
    }

    const oldFeatures = await loadWithCompositeShape(caps(true, true, true, true, true, true),
      headers(true, true, true, true, false, true, true))
    expect(supportsGs1CompositeTruncated(oldFeatures)).toBe(false)
    expect(supportsGs1CompositeStackedOmnidirectional(oldFeatures)).toBe(false)
    expect(gs1CompositeModeChoices(DATABAR_TRUNCATED_LINEAR_TYPE, oldFeatures).every((choice) => choice.disabled)).toBe(true)
    expect(gs1CompositeModeChoices(DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE, oldFeatures)
      .every((choice) => choice.disabled)).toBe(true)

    const truncatedOnly = await loadWithCompositeShape(caps(true, true, true, true, true, true),
      headers(true, true, true, true, false, true, true, true, false))
    expect(truncatedOnly.gs1CompositeTruncatedFeature).toBe(true)
    expect(truncatedOnly.gs1CompositeStackedOmnidirectionalFeature).toBe(false)
    expect(supportsGs1CompositeTruncated(truncatedOnly)).toBe(true)
    expect(supportsGs1CompositeStackedOmnidirectional(truncatedOnly)).toBe(false)

    const stackedOmnidirectionalOnly = await loadWithCompositeShape(caps(true, true, true, true, true, true),
      headers(true, true, true, true, false, true, true, false, true))
    expect(stackedOmnidirectionalOnly.gs1CompositeTruncatedFeature).toBe(false)
    expect(stackedOmnidirectionalOnly.gs1CompositeStackedOmnidirectionalFeature).toBe(true)
    expect(supportsGs1CompositeTruncated(stackedOmnidirectionalOnly)).toBe(false)
    expect(supportsGs1CompositeStackedOmnidirectional(stackedOmnidirectionalOnly)).toBe(true)

    const oldTypes = await loadWithCompositeShape(caps(true, true, true, true, true),
      headers(true, true, true, true, false, true, true, true, true))
    expect(supportsGs1CompositeTruncated(oldTypes)).toBe(false)
    expect(supportsGs1CompositeStackedOmnidirectional(oldTypes)).toBe(false)
  })

  it('gates EAN/UPC modes on the feature and fills only empty retail primary data', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(caps(true, true, true, true, true)), {
      headers: headers(true, true, true, true, false, true, true),
    })))
    const result = await loadDesignerCapabilities({ protocol: 'Cpcl' })
    expect(result.gs1CompositeEanUpcFeature).toBe(true)
    expect(supportsGs1CompositeEanUpc(result)).toBe(true)

    for (const linearType of [EAN_8_LINEAR_TYPE, EAN_13_LINEAR_TYPE, UPC_A_LINEAR_TYPE, UPC_E_LINEAR_TYPE] as const) {
      expect(isValidGs1CompositeCombination(linearType, 0)).toBe(true)
      expect(isValidGs1CompositeCombination(linearType, 1)).toBe(true)
      expect(isValidGs1CompositeCombination(linearType, 2)).toBe(true)
      expect(isValidGs1CompositeCombination(linearType, GS1_COMPOSITE_CC_C_MODE)).toBe(false)
      expect(gs1CompositeModeChoices(linearType, result).map((mode) => mode.disabled)).toEqual([false, false, false])

      const empty = template(25, GS1_COMPOSITE_CC_C_MODE).Rows[0]!.Cells![0]!.Elements![0]! as Gs1CompositeElement
      empty.Field = { Value: '' }
      expect(setGs1CompositeLinearType(empty, linearType, result)).toBe(true)
      expect(empty.LinearType).toBe(linearType)
      expect(empty.Mode).toBe(0)
      expect(empty.Field?.Value).toBe(DEFAULT_GS1_EAN_UPC_COMPOSITE_PRIMARY[linearType])
      expect(gs1CompositeRetailPrimaryError(linearType, empty.Field!.Value!)).toBeUndefined()
      expect(setGs1CompositeMode(empty, 2, result)).toBe(true)
      expect(empty.Mode).toBe(2)

      const nonempty = template(25, 0).Rows[0]!.Cells![0]!.Elements![0]! as Gs1CompositeElement
      nonempty.Field = { Value: 'KEEP-THIS-VALUE' }
      expect(setGs1CompositeLinearType(nonempty, linearType, result)).toBe(true)
      expect(nonempty.Field?.Value).toBe('KEEP-THIS-VALUE')
    }
  })

  it('gates EAN/UPC addons on all three features and the appended primary format', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(caps(true, true, true, true, true, true, true)), {
      headers: headers(true, true, true, true, false, true, true, true, true, true),
    })))
    const result = await loadDesignerCapabilities({ protocol: 'Cpcl' })
    expect(result.gs1CompositeEanUpcAddonsFeature).toBe(true)
    expect(supportsGs1CompositeEanUpcAddons(result)).toBe(true)
    expect(supportsGs1CompositeEanUpcAddons({ ...result, gs1CompositeFeature: false })).toBe(false)
    expect(supportsGs1CompositeEanUpcAddons({ ...result, gs1CompositeEanUpcFeature: false })).toBe(false)
    expect(supportsGs1CompositeEanUpcAddons({ ...result, gs1CompositeEanUpcAddonsFeature: false })).toBe(false)
    expect(supportsGs1CompositeEanUpcAddons({ ...result, gs1Composite: {
      ...result.gs1Composite!, primaryFormats: ['GTIN-13-data', 'GTIN-14', 'GS1-AI-01', 'bracketed-gs1-ai', 'ean-upc-digits'],
    } })).toBe(false)
  })

  it.each([
    [EAN_8_LINEAR_TYPE, '1234567'], [EAN_8_LINEAR_TYPE, '12345670'], [EAN_8_LINEAR_TYPE, '96385074'],
    [EAN_13_LINEAR_TYPE, '123456789012'], [EAN_13_LINEAR_TYPE, '1234567890128'], [EAN_13_LINEAR_TYPE, '4006381333931'],
    [UPC_A_LINEAR_TYPE, '01234567890'], [UPC_A_LINEAR_TYPE, '012345678905'], [UPC_A_LINEAR_TYPE, '036000291452'],
    [UPC_E_LINEAR_TYPE, '123456'], [UPC_E_LINEAR_TYPE, '0123456'], [UPC_E_LINEAR_TYPE, '01234565'],
    [UPC_E_LINEAR_TYPE, '04252614'],
  ] as const)('accepts EAN/UPC %s primary %s', (linearType, value) => {
    expect(gs1CompositeRetailPrimaryError(linearType, value)).toBeUndefined()
  })

  it.each([
    [EAN_8_LINEAR_TYPE, '１２３４５６７０'], [EAN_8_LINEAR_TYPE, '(01)12345670'], [EAN_8_LINEAR_TYPE, '12345671'],
    [EAN_13_LINEAR_TYPE, '4006381333932'], [UPC_A_LINEAR_TYPE, '036000291453'],
    [UPC_E_LINEAR_TYPE, '2123456'], [UPC_E_LINEAR_TYPE, '0121453'], [UPC_E_LINEAR_TYPE, '0123014'],
    [UPC_E_LINEAR_TYPE, '0123405'], [UPC_E_LINEAR_TYPE, '01234564'],
    [UPC_E_LINEAR_TYPE, '01234565+1'], [UPC_E_LINEAR_TYPE, '01234565+123'],
    [UPC_E_LINEAR_TYPE, '01234565+1234'], [UPC_E_LINEAR_TYPE, '01234565+123456'],
    [UPC_E_LINEAR_TYPE, '01234565++12'], [UPC_E_LINEAR_TYPE, '01234565+１２'],
    [EAN_8_LINEAR_TYPE, '12345671+12'], [EAN_8_LINEAR_TYPE, '12345670+12 '],
  ] as const)('rejects malformed EAN/UPC %s primary %s', (linearType, value) => {
    expect(gs1CompositeRetailPrimaryError(linearType, value)).toBeDefined()
  })

  it.each([
    [EAN_8_LINEAR_TYPE, '1234567+12'], [EAN_8_LINEAR_TYPE, '12345670+51234'],
    [EAN_13_LINEAR_TYPE, '123456789012+12'], [EAN_13_LINEAR_TYPE, '1234567890128+51234'],
    [UPC_A_LINEAR_TYPE, '01234567890+12'], [UPC_A_LINEAR_TYPE, '012345678905+51234'],
    [UPC_E_LINEAR_TYPE, '0123456+12'], [UPC_E_LINEAR_TYPE, '01234565+51234'],
  ] as const)('accepts EAN/UPC %s primary with addon %s', (linearType, value) => {
    expect(gs1CompositeRetailPrimaryError(linearType, value)).toBeUndefined()
  })

  it('enables Expanded only after its feature and capability are negotiated and supplies a bracketed primary default', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(caps(true, true)), { headers: headers(true, true, true) })))
    const result = await loadDesignerCapabilities({ protocol: 'Cpcl' })
    expect(supportsGs1CompositeExpanded(result)).toBe(true)
    expect(gs1CompositeModeChoices(DATABAR_EXPANDED_LINEAR_TYPE, result).map((mode) => mode.value)).toEqual([0, 1, 2])
    expect(gs1CompositeModeChoices(DATABAR_EXPANDED_LINEAR_TYPE, result).map((mode) => mode.disabled)).toEqual([false, false, false])

    const empty = template(25, 1).Rows[0]!.Cells![0]!.Elements![0]! as Gs1CompositeElement
    empty.Field = { Value: '' }
    expect(setGs1CompositeLinearType(empty, DATABAR_EXPANDED_LINEAR_TYPE, result)).toBe(true)
    expect(empty.LinearType).toBe(DATABAR_EXPANDED_LINEAR_TYPE)
    expect(empty.Mode).toBe(1)
    expect(empty.Field?.Value).toBe(DEFAULT_GS1_EXPANDED_COMPOSITE_PRIMARY)
    expect(() => assertTemplate(template(DATABAR_EXPANDED_LINEAR_TYPE, 2))).not.toThrow()

    const seeded = template(25, 0).Rows[0]!.Cells![0]!.Elements![0]! as Gs1CompositeElement
    seeded.Field = { Value: '12345678901231' }
    expect(setGs1CompositeLinearType(seeded, DATABAR_EXPANDED_LINEAR_TYPE, result)).toBe(true)
    expect(seeded.Field?.Value).toBe('12345678901231')

    const edited = template(25, 0).Rows[0]!.Cells![0]!.Elements![0]! as Gs1CompositeElement
    edited.Field = { Value: '(01)12345678901231(10)KEEP-ME' }
    expect(setGs1CompositeLinearType(edited, DATABAR_EXPANDED_LINEAR_TYPE, result)).toBe(true)
    expect(edited.Field?.Value).toBe('(01)12345678901231(10)KEEP-ME')
  })

  it('keeps Expanded unavailable to legacy and CC-C Hosts without the Expanded feature marker', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(caps(true)), { headers: headers(true, true) })))
    const result = await loadDesignerCapabilities({ protocol: 'Cpcl' })
    expect(supportsGs1CompositeCcC(result)).toBe(true)
    expect(supportsGs1CompositeExpanded(result)).toBe(false)
    const element = template(25, 1).Rows[0]!.Cells![0]!.Elements![0]! as Gs1CompositeElement
    expect(setGs1CompositeLinearType(element, DATABAR_EXPANDED_LINEAR_TYPE, result)).toBe(false)
    expect(element.LinearType).toBe(25)

    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(caps(true, true)), { headers: headers(true, true) })))
    const capabilityOnly = await loadDesignerCapabilities({ protocol: 'Cpcl' })
    expect(capabilityOnly.gs1Composite?.linearTypes).toContain('Gs1DataBarExpanded')
    expect(supportsGs1CompositeExpanded(capabilityOnly)).toBe(false)
  })

  it('enables Stacked only when its feature and appended capability are both present', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(caps(true, true, true)), {
      headers: headers(true, true, true, true),
    })))
    const result = await loadDesignerCapabilities({ protocol: 'Cpcl' })
    expect(supportsGs1CompositeStacked(result)).toBe(true)
    expect(gs1CompositeModeChoices(DATABAR_STACKED_LINEAR_TYPE, result).map((mode) => mode.value)).toEqual([0, 1, 2])
    expect(gs1CompositeModeChoices(DATABAR_STACKED_LINEAR_TYPE, result).map((mode) => mode.disabled)).toEqual([false, false, false])

    const element = template(25, GS1_COMPOSITE_CC_C_MODE).Rows[0]!.Cells![0]!.Elements![0]! as Gs1CompositeElement
    expect(setGs1CompositeLinearType(element, DATABAR_STACKED_LINEAR_TYPE, result)).toBe(true)
    expect(element.LinearType).toBe(DATABAR_STACKED_LINEAR_TYPE)
    expect(element.Mode).toBe(0)
    expect(() => assertTemplate(template(DATABAR_STACKED_LINEAR_TYPE, 2))).not.toThrow()
    expect(() => assertTemplate(template(DATABAR_STACKED_LINEAR_TYPE, GS1_COMPOSITE_CC_C_MODE))).toThrow('组合无效')

    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(caps(true, true, true)), {
      headers: headers(true, true, true),
    })))
    const featureMissing = await loadDesignerCapabilities({ protocol: 'Cpcl' })
    expect(supportsGs1CompositeStacked(featureMissing)).toBe(false)
    expect(setGs1CompositeLinearType(element, DATABAR_STACKED_LINEAR_TYPE, featureMissing)).toBe(false)

    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(caps(true, true)), {
      headers: headers(true, true, true, true),
    })))
    const typeMissing = await loadDesignerCapabilities({ protocol: 'Cpcl' })
    expect(supportsGs1CompositeStacked(typeMissing)).toBe(false)
    expect(setGs1CompositeLinearType(element, DATABAR_STACKED_LINEAR_TYPE, typeMissing)).toBe(false)
  })

  it('enables Expanded Stacked only with the separate feature and preserves Expanded input and row-height semantics', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(caps(true, true, true, true)), {
      headers: headers(true, true, true, true, false, true),
    })))
    const result = await loadDesignerCapabilities({ protocol: 'Cpcl' })
    expect(supportsGs1CompositeExpandedStacked(result)).toBe(true)
    expect(gs1CompositeModeChoices(DATABAR_EXPANDED_STACKED_LINEAR_TYPE, result).map((mode) => mode.value))
      .toEqual([0, 1, 2])
    expect(gs1CompositeModeChoices(DATABAR_EXPANDED_STACKED_LINEAR_TYPE, result).map((mode) => mode.disabled))
      .toEqual([false, false, false])

    const element = template(25, GS1_COMPOSITE_CC_C_MODE).Rows[0]!.Cells![0]!.Elements![0]! as Gs1CompositeElement
    element.Field = { Value: '' }
    expect(setGs1CompositeLinearType(element, DATABAR_EXPANDED_STACKED_LINEAR_TYPE, result)).toBe(true)
    expect(element.LinearType).toBe(DATABAR_EXPANDED_STACKED_LINEAR_TYPE)
    expect(element.Mode).toBe(0)
    expect(element.Field?.Value).toBe(DEFAULT_GS1_EXPANDED_COMPOSITE_PRIMARY)
    element.ModuleWidth = 3
    element.LinearBarHeight = 3
    expect(() => assertTemplate({ ...template(DATABAR_EXPANDED_STACKED_LINEAR_TYPE, 2),
      Rows: [{ Cells: [{ Elements: [element] }] }] })).not.toThrow()
    element.LinearBarHeight = 2
    expect(() => assertTemplate({ ...template(DATABAR_EXPANDED_STACKED_LINEAR_TYPE, 2),
      Rows: [{ Cells: [{ Elements: [element] }] }] })).toThrow('至少为模块宽度')
    expect(() => assertTemplate(template(DATABAR_EXPANDED_STACKED_LINEAR_TYPE, GS1_COMPOSITE_CC_C_MODE)))
      .toThrow('组合无效')

    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(caps(true, true, true, true)), {
      headers: headers(true, true, true, true),
    })))
    const featureMissing = await loadDesignerCapabilities({ protocol: 'Cpcl' })
    expect(supportsGs1CompositeExpandedStacked(featureMissing)).toBe(false)
    expect(setGs1CompositeLinearType(element, DATABAR_EXPANDED_STACKED_LINEAR_TYPE, featureMissing)).toBe(false)
    expect(element.LinearType).toBe(DATABAR_EXPANDED_STACKED_LINEAR_TYPE)

    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(caps(true, true, true)), {
      headers: headers(true, true, true, true, false, true),
    })))
    const typeMissing = await loadDesignerCapabilities({ protocol: 'Cpcl' })
    expect(supportsGs1CompositeExpandedStacked(typeMissing)).toBe(false)
  })

  it('preserves Composite parameters and both bindings through a project ZIP package', async () => {
    const original = template()
    const zip = await exportTemplateBundle(original, [])
    const restored = await importTemplateBundle(new File([zip], 'composite.zip', { type: 'application/zip' }))
    expect(restored.template).toEqual(original)
    expect(restored.resources).toEqual([])
  })

  it('preserves Stacked type and total bar height through JSON and project ZIP round trips', async () => {
    const original = template(DATABAR_STACKED_LINEAR_TYPE, 1)
    const restoredJson = parseDesignerProject(JSON.parse(JSON.stringify(original))).template
    expect(restoredJson).toEqual(original)
    const zip = await exportTemplateBundle(original, [])
    expect((await importTemplateBundle(new File([zip], 'stacked.zip', { type: 'application/zip' }))).template)
      .toEqual(original)
  })

  it('preserves Expanded Stacked type and per-row height through JSON and project ZIP round trips', async () => {
    const original = template(DATABAR_EXPANDED_STACKED_LINEAR_TYPE, 1)
    ;(original.Rows[0]!.Cells![0]!.Elements![0]! as Gs1CompositeElement).LinearBarHeight = 34
    const restoredJson = parseDesignerProject(JSON.parse(JSON.stringify(original))).template
    expect(restoredJson).toEqual(original)
    const zip = await exportTemplateBundle(original, [])
    expect((await importTemplateBundle(new File([zip], 'expanded-stacked.zip', { type: 'application/zip' }))).template)
      .toEqual(original)
  })

  it('uses a pending placeholder offline rather than claiming encoded dimensions', () => {
    const layout = buildLocalLayout(template(), { Gtin: '12345678901231', Lot: '(91)123456' })
    expect(layout.errors.some((error) => error.message.includes('待 Host 验证'))).toBe(true)
    expect(layout.pages[0]!.elements[0]).toMatchObject({ type: 11, rotation: 90, text: 'GS1 Composite（待验证）' })
  })

  it.each([false, true])('requires the negotiated feature as well as the capability (feature=%s)', async (feature) => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(caps()), { headers: headers(feature) })))
    const result = await loadDesignerCapabilities({ protocol: 'Cpcl' })
    expect(canCreateElement(result, 'ready', 11)).toBe(feature)
    expect(canCreateElement(result, 'loading', 11)).toBe(false)
    expect(rotationSupported(result, 11, 270)).toBe(feature)
    expect(canCreateElement({ ...result, gs1Composite: null }, 'ready', 11)).toBe(false)
    expect(canCreateElement({ ...result, supportsGs1Composite: false }, 'ready', 11)).toBe(false)
  })

  it.each(['preview', 'render'])('negotiates the feature for Composite %s and rejects old Hosts', async (operation) => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ pages: [] }), { headers: headers(false) }))
    vi.stubGlobal('fetch', fetchMock)
    const request = operation === 'preview' ? previewTemplate : renderTemplate
    await expect(request(template(), {}, [])).rejects.toMatchObject({ diagnostics: expect.any(Array) })
    const options = fetchMock.mock.calls[0]![1] as RequestInit
    expect((options.headers as Record<string, string>)['X-EasyPrint-Required-Features']).toContain('gs1-composite')
  })

  it.each(['preview', 'render'] as const)('requires the separate CC-C feature for GS1-128 Composite %s', async (operation) => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ pages: [] }), { headers: headers(true) }))
    vi.stubGlobal('fetch', fetchMock)
    const request = operation === 'preview' ? previewTemplate : renderTemplate
    await expect(request(template(GS1_128_LINEAR_TYPE, GS1_COMPOSITE_CC_C_MODE), {}, []))
      .rejects.toMatchObject({ message: '打印服务缺少 GS1-128 + CC-C Composite 功能，请更新 Host。' })
    const options = fetchMock.mock.calls[0]![1] as RequestInit
    expect((options.headers as Record<string, string>)['X-EasyPrint-Required-Features'])
      .toContain('gs1-composite-cc-c')
    expect((options.headers as Record<string, string>)['X-EasyPrint-Required-Features'])
      .toContain('gs1-composite')
  })

  it.each([0, GS1_COMPOSITE_CC_C_MODE])('requires the CC-C feature for GS1-128 Auto/CC-C mode %s', async (mode) => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ pages: [] }), { headers: headers(true) }))
    vi.stubGlobal('fetch', fetchMock)
    await expect(previewTemplate(template(GS1_128_LINEAR_TYPE, mode), {}, []))
      .rejects.toMatchObject({ message: '打印服务缺少 GS1-128 + CC-C Composite 功能，请更新 Host。' })
    expect((fetchMock.mock.calls[0]![1] as RequestInit).headers)
      .toMatchObject({ 'X-EasyPrint-Required-Features': expect.stringContaining('gs1-composite-cc-c') })
  })

  it.each(['preview', 'render'] as const)('requires GS1-128 CC-A/CC-B feature for %s and rejects a Host without it', async (operation) => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ pages: [] }), {
      headers: headers(true, true),
    }))
    vi.stubGlobal('fetch', fetchMock)
    const request = operation === 'preview' ? previewTemplate : renderTemplate
    for (const mode of [1, 2] as const) {
      await expect(request(template(GS1_128_LINEAR_TYPE, mode), {}, []))
        .rejects.toMatchObject({ message: '打印服务缺少 GS1-128 + CC-A/CC-B Composite 功能，请更新 Host。' })
      const requiredFeatures = (fetchMock.mock.calls[fetchMock.mock.calls.length - 1]![1] as RequestInit).headers as Record<string, string>
      expect(requiredFeatures['X-EasyPrint-Required-Features']).toContain(GS1_COMPOSITE_GS1_128_CC_AB_FEATURE)
      expect(requiredFeatures['X-EasyPrint-Required-Features']).toContain('gs1-composite')
      expect(requiredFeatures['X-EasyPrint-Required-Features']).not.toContain('gs1-composite-cc-c')
    }
  })

  it.each([1, 2] as const)('accepts GS1-128 CC-%s preview when the new feature is negotiated', async (mode) => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ pages: [] }), {
      headers: headers(true, true, false, false, true),
    }))
    vi.stubGlobal('fetch', fetchMock)
    await expect(previewTemplate(template(GS1_128_LINEAR_TYPE, mode), {}, [])).resolves.toMatchObject({ pages: [] })
    const requiredFeatures = (fetchMock.mock.calls[0]![1] as RequestInit).headers as Record<string, string>
    expect(requiredFeatures['X-EasyPrint-Required-Features']).toContain(GS1_COMPOSITE_GS1_128_CC_AB_FEATURE)
  })

  it.each(['preview', 'render'] as const)('requires the Expanded feature for Composite %s', async (operation) => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ pages: [] }), { headers: headers(true, true) }))
    vi.stubGlobal('fetch', fetchMock)
    const request = operation === 'preview' ? previewTemplate : renderTemplate
    await expect(request(template(DATABAR_EXPANDED_LINEAR_TYPE, 0), {}, []))
      .rejects.toMatchObject({ message: '打印服务缺少 GS1 DataBar Expanded Composite 功能，请更新 Host。' })
    expect((fetchMock.mock.calls[0]![1] as RequestInit).headers)
      .toMatchObject({ 'X-EasyPrint-Required-Features': expect.stringContaining('gs1-composite-expanded') })
  })

  it.each(['preview', 'render'] as const)('requires the Stacked feature for Composite %s', async (operation) => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ pages: [] }), {
      headers: headers(true, true, true),
    }))
    vi.stubGlobal('fetch', fetchMock)
    const request = operation === 'preview' ? previewTemplate : renderTemplate
    await expect(request(template(DATABAR_STACKED_LINEAR_TYPE, 1), {}, []))
      .rejects.toMatchObject({ message: '打印服务缺少 GS1 DataBar Stacked Composite 功能，请更新 Host。' })
    const requiredFeatures = (fetchMock.mock.calls[0]![1] as RequestInit).headers as Record<string, string>
    expect(requiredFeatures['X-EasyPrint-Required-Features']).toContain('gs1-composite-stacked')
    expect(requiredFeatures['X-EasyPrint-Required-Features']).toContain('gs1-composite')
  })

  it.each(['preview', 'render'] as const)('requires the Expanded Stacked feature for Composite %s', async (operation) => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ pages: [] }), {
      headers: headers(true, true, true, true),
    }))
    vi.stubGlobal('fetch', fetchMock)
    const request = operation === 'preview' ? previewTemplate : renderTemplate
    await expect(request(template(DATABAR_EXPANDED_STACKED_LINEAR_TYPE, 0), {}, []))
      .rejects.toMatchObject({ message: '打印服务缺少 GS1 DataBar Expanded Stacked Composite 功能，请更新 Host。' })
    const requiredFeatures = (fetchMock.mock.calls[0]![1] as RequestInit).headers as Record<string, string>
    expect(requiredFeatures['X-EasyPrint-Required-Features']).toContain('gs1-composite-expanded-stacked')
    expect(requiredFeatures['X-EasyPrint-Required-Features']).toContain('gs1-composite')
  })

  it.each([
    [DATABAR_TRUNCATED_LINEAR_TYPE, GS1_COMPOSITE_TRUNCATED_FEATURE,
      '打印服务缺少 GS1 DataBar Truncated Composite 功能，请更新 Host。'],
    [DATABAR_STACKED_OMNIDIRECTIONAL_LINEAR_TYPE, GS1_COMPOSITE_STACKED_OMNIDIRECTIONAL_FEATURE,
      '打印服务缺少 GS1 DataBar Stacked Omnidirectional Composite 功能，请更新 Host。'],
  ] as const)('requires the independent feature for Composite type %s', async (linearType, feature, message) => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ pages: [] }), {
      headers: headers(true, true, true, true, false, true, true),
    }))
    vi.stubGlobal('fetch', fetchMock)
    const value = template(linearType, 1)
    ;(value.Rows[0]!.Cells![0]!.Elements![0]! as Gs1CompositeElement).LinearBarHeight =
      linearType === DATABAR_TRUNCATED_LINEAR_TYPE ? 26 : 66
    await expect(previewTemplate(value, {}, [])).rejects.toMatchObject({ message })
    const requiredFeatures = (fetchMock.mock.calls[0]![1] as RequestInit).headers as Record<string, string>
    expect(requiredFeatures['X-EasyPrint-Required-Features']).toContain(feature)
    expect(requiredFeatures['X-EasyPrint-Required-Features']).toContain('gs1-composite')
  })

  it.each([
    [EAN_8_LINEAR_TYPE, '12345670'], [EAN_13_LINEAR_TYPE, '1234567890128'],
    [UPC_A_LINEAR_TYPE, '012345678905'], [UPC_E_LINEAR_TYPE, '01234565'],
  ] as const)('requires EAN/UPC Composite feature for preview of type %s', async (linearType, primary) => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ pages: [] }), {
      headers: headers(true, true, true, true, false, true, true),
    }))
    vi.stubGlobal('fetch', fetchMock)
    const value = template(linearType, 2)
    ;(value.Rows[0]!.Cells![0]!.Elements![0]! as Gs1CompositeElement).Field = { Value: primary }
    await previewTemplate(value, {}, [])
    const requiredFeatures = (fetchMock.mock.calls[0]![1] as RequestInit).headers as Record<string, string>
    expect(requiredFeatures['X-EasyPrint-Required-Features']).toContain(GS1_COMPOSITE_EAN_UPC_FEATURE)
    expect(requiredFeatures['X-EasyPrint-Required-Features']).toContain('gs1-composite')
    expect(requiredFeatures['X-EasyPrint-Required-Features']).not.toContain(GS1_COMPOSITE_EAN_UPC_ADDONS_FEATURE)
  })

  it.each([
    [EAN_8_LINEAR_TYPE, '12345670+12'], [EAN_13_LINEAR_TYPE, '1234567890128+51234'],
    [UPC_A_LINEAR_TYPE, '012345678905+12'], [UPC_E_LINEAR_TYPE, '01234565+51234'],
  ] as const)('requires the addon feature only for EAN/UPC Composite addon values of type %s', async (linearType, primary) => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ pages: [] }), {
      headers: headers(true, true, true, true, false, true, true, true, true, true),
    }))
    vi.stubGlobal('fetch', fetchMock)
    const value = template(linearType, 2)
    ;(value.Rows[0]!.Cells![0]!.Elements![0]! as Gs1CompositeElement).Field = { Value: primary }
    await previewTemplate(value, {}, [])
    const requiredFeatures = (fetchMock.mock.calls[0]![1] as RequestInit).headers as Record<string, string>
    expect(requiredFeatures['X-EasyPrint-Required-Features']).toContain('gs1-composite')
    expect(requiredFeatures['X-EasyPrint-Required-Features']).toContain(GS1_COMPOSITE_EAN_UPC_FEATURE)
    expect(requiredFeatures['X-EasyPrint-Required-Features']).toContain(GS1_COMPOSITE_EAN_UPC_ADDONS_FEATURE)
  })

  it.each([EAN_8_LINEAR_TYPE, EAN_13_LINEAR_TYPE, UPC_A_LINEAR_TYPE, UPC_E_LINEAR_TYPE] as const)(
    'rejects EAN/UPC CC-C and malformed literal primary before preview for type %s', async (linearType) => {
      const fetchMock = vi.fn()
      vi.stubGlobal('fetch', fetchMock)
      await expect(previewTemplate(template(linearType, GS1_COMPOSITE_CC_C_MODE), {}, []))
        .rejects.toMatchObject({ diagnostics: [expect.objectContaining({ code: 'InvalidGs1CompositeMode' })] })
      const invalid = template(linearType, 0)
      ;(invalid.Rows[0]!.Cells![0]!.Elements![0]! as Gs1CompositeElement).Field = { Value: 'not digits' }
      await expect(previewTemplate(invalid, {}, []))
        .rejects.toMatchObject({ diagnostics: [expect.objectContaining({ code: 'InvalidGs1CompositePrimaryData' })] })
      expect(fetchMock).not.toHaveBeenCalled()
    })

  it('rejects an EAN/UPC request when the response lacks its negotiated feature', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ pages: [] }), { headers: headers() })))
    const value = template(UPC_A_LINEAR_TYPE, 1)
    ;(value.Rows[0]!.Cells![0]!.Elements![0]! as Gs1CompositeElement).Field = { Value: '012345678905' }
    await expect(previewTemplate(value, {}, [])).rejects.toMatchObject({
      message: '打印服务缺少 EAN/UPC Composite 功能，请更新 Host。',
    })
  })

  it('rejects an addon request when the response lacks its independent feature', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ pages: [] }), {
      headers: headers(true, true, true, true, false, true, true, true, true),
    }))
    vi.stubGlobal('fetch', fetchMock)
    const value = template(EAN_8_LINEAR_TYPE, 1)
    ;(value.Rows[0]!.Cells![0]!.Elements![0]! as Gs1CompositeElement).Field = { Value: '12345670+12' }
    await expect(previewTemplate(value, {}, [])).rejects.toMatchObject({
      message: '打印服务缺少 EAN/UPC Composite 附加码功能，请更新 Host。',
    })
    const requiredFeatures = (fetchMock.mock.calls[0]![1] as RequestInit).headers as Record<string, string>
    expect(requiredFeatures['X-EasyPrint-Required-Features']).toContain(GS1_COMPOSITE_EAN_UPC_ADDONS_FEATURE)
  })

  it('rejects addon output when the response lacks its independent feature', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ pages: [] }), {
      headers: headers(true, true, true, true, false, true, true, true, true),
    })))
    const value = template(EAN_13_LINEAR_TYPE, 2)
    ;(value.Rows[0]!.Cells![0]!.Elements![0]! as Gs1CompositeElement).Field = { Value: '1234567890128+51234' }
    await expect(renderTemplate(value, {}, [])).rejects.toMatchObject({
      message: '打印服务缺少 EAN/UPC Composite 附加码功能，请更新 Host。',
    })
  })

  it('does not apply the Composite addon feature gate to ordinary Type 4 barcodes', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ pages: [] }), {
      headers: headers(true, true, true, true, false, true, true),
    }))
    vi.stubGlobal('fetch', fetchMock)
    const value = createBlankSample()
    value.Rows[0]!.Cells![0]!.Elements!.push({
      Type: 4, BarcodeType: EAN_13_LINEAR_TYPE, Width: 200, Height: 40,
      Field: { Value: '1234567890128+12' },
      StartCoordinate: { X: 10, Y: 10 }, EndCoordinate: { X: 210, Y: 50 },
    })
    await previewTemplate(value, {}, [])
    const request = fetchMock.mock.calls[0]![1] as RequestInit
    const requiredFeatures = request.headers as Record<string, string>
    const body = JSON.parse(String(request.body)) as { template: BpTemplate }
    expect(body.template.Rows[0]!.Cells![0]!.Elements![0]!.Field?.Value).toBe('1234567890128+12')
    expect(requiredFeatures['X-EasyPrint-Required-Features']).not.toContain(GS1_COMPOSITE_EAN_UPC_FEATURE)
    expect(requiredFeatures['X-EasyPrint-Required-Features']).not.toContain(GS1_COMPOSITE_EAN_UPC_ADDONS_FEATURE)
  })

  it('rejects Stacked with CC-C before sending a request', async () => {
    const fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
    await expect(previewTemplate(template(DATABAR_STACKED_LINEAR_TYPE, GS1_COMPOSITE_CC_C_MODE), {}, []))
      .rejects.toMatchObject({ diagnostics: [expect.objectContaining({ code: 'InvalidGs1CompositeMode' })] })
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('rejects Expanded with CC-C before sending a request', async () => {
    const fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
    await expect(previewTemplate(template(DATABAR_EXPANDED_LINEAR_TYPE, GS1_COMPOSITE_CC_C_MODE), {}, []))
      .rejects.toMatchObject({ diagnostics: [expect.objectContaining({ code: 'InvalidGs1CompositeMode' })] })
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('rejects Expanded Stacked with CC-C before sending a request', async () => {
    const fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
    await expect(previewTemplate(template(DATABAR_EXPANDED_STACKED_LINEAR_TYPE, GS1_COMPOSITE_CC_C_MODE), {}, []))
      .rejects.toMatchObject({ diagnostics: [expect.objectContaining({ code: 'InvalidGs1CompositeMode' })] })
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('rejects invalid GS1-128 mode combinations before preview or render requests', async () => {
    const fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
    await expect(previewTemplate(template(GS1_128_LINEAR_TYPE, 4), {}, []))
      .rejects.toMatchObject({ diagnostics: [expect.objectContaining({ code: 'InvalidGs1CompositeMode' })] })
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('uses Host preview dimensions without local estimates', async () => {
    const page = { width: 400, height: 300, elements: [{ sourcePath: 'Rows[0].Cells[0].Elements[0]', type: 11,
      rotation: 90, x: 0, y: 0, width: 81, height: 186 }] }
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ pages: [page] }), { headers: headers() })))
    expect((await previewTemplate(template(), {}, [])).pages[0]).toEqual(page)
  })
})
