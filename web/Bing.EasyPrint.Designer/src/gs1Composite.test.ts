import { afterEach, describe, expect, it, vi } from 'vitest'
import { loadDesignerCapabilities, previewTemplate, renderTemplate } from './api'
import { exportTemplateBundle, importTemplateBundle } from './archive'
import { canCreateElement, normalizeDesignerCapabilities, rotationSupported } from './capabilities'
import { parseDesignerProject, serializeDesignerProject } from './designerProject'
import {
  DEFAULT_GS1_128_COMPOSITE_PRIMARY, GS1_128_LINEAR_TYPE, GS1_COMPOSITE_CC_C_MODE, gs1CompositeModeChoices,
  isValidGs1CompositeCombination, setGs1CompositeLinearType, supportsGs1CompositeCcC,
} from './gs1Composite'
import { buildLocalLayout } from './layout'
import { assertTemplate, cloneTemplate, type BpTemplate, type Gs1CompositeElement } from './model'
import { elementLabel } from './tree'

afterEach(() => vi.unstubAllGlobals())

function template(linearType = 27, mode = 2): BpTemplate {
  return { Version: 3, Width: 400, Height: 300, Rows: [{ Cells: [{ Elements: [{
    Type: 11, LinearType: linearType as 11 | 25 | 27, Mode: mode as 0 | 1 | 2 | 3, ModuleWidth: 2, LinearBarHeight: 40, Rotation: 90,
    Field: { Name: 'Gtin' }, SecondaryField: { Name: 'Lot' }, QuietZone: { Left: 3, Top: 5, Right: 7, Bottom: 11 },
  }] }] }] }
}

function caps(ccC = false): Record<string, unknown> {
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
      linearTypes: ccC ? ['Gs1DataBarOmnidirectional', 'Gs1DataBarLimited', 'UccEan128'] : ['Gs1DataBarOmnidirectional', 'Gs1DataBarLimited'],
      modes: ccC ? ['Auto', 'CcA', 'CcB', 'CcC'] : ['Auto', 'CcA', 'CcB'],
      rotations: [0, 90, 180, 270], includesHumanReadable: false, dimensionsFromEncoding: true },
  }
}

function headers(composite = true, ccC = false): Record<string, string> {
  return { 'Content-Type': 'application/json', 'X-EasyPrint-Contract-Version': '1',
    'X-EasyPrint-Features': `structured-errors,strict-options,binding-diagnostics${composite ? ',gs1-composite' : ''}${ccC ? ',gs1-composite-cc-c' : ''}` }
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
      [GS1_128_LINEAR_TYPE, 0], [GS1_128_LINEAR_TYPE, GS1_COMPOSITE_CC_C_MODE]] as const) {
      expect(() => assertTemplate(template(linearType, mode))).not.toThrow()
      expect(isValidGs1CompositeCombination(linearType, mode)).toBe(true)
    }
    for (const [linearType, mode] of [[25, 3], [27, 3], [GS1_128_LINEAR_TYPE, 1], [GS1_128_LINEAR_TYPE, 2], [99, 0]] as const) {
      expect(() => assertTemplate(template(linearType, mode))).toThrow('LinearType 与 Mode 组合无效')
      expect(isValidGs1CompositeCombination(linearType, mode)).toBe(false)
    }
  })

  it.each([0, GS1_COMPOSITE_CC_C_MODE])('roundtrips valid GS1-128 Composite mode %s without changing JSON fields', (mode) => {
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
    expect(supportsGs1CompositeCcC(result)).toBe(false)
    expect(gs1CompositeModeChoices(25, result).map((mode) => mode.disabled)).toEqual([false, false, false])
    expect(gs1CompositeModeChoices(GS1_128_LINEAR_TYPE, result).map((mode) => mode.disabled)).toEqual([true, true])
    const existing = template().Rows[0]!.Cells![0]!.Elements![0]! as Gs1CompositeElement
    expect(setGs1CompositeLinearType(existing, GS1_128_LINEAR_TYPE, result)).toBe(false)
    expect(existing.LinearType).toBe(27)
  })

  it('enables GS1-128 CC-C only after the new feature and capabilities are negotiated', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(caps(true)), { headers: headers(true, true) })))
    const result = await loadDesignerCapabilities({ protocol: 'Cpcl' })
    expect(supportsGs1CompositeCcC(result)).toBe(true)
    expect(gs1CompositeModeChoices(GS1_128_LINEAR_TYPE, result).map((mode) => mode.disabled)).toEqual([false, false])

    const element = template(25, 1).Rows[0]!.Cells![0]!.Elements![0]! as Gs1CompositeElement
    element.Field = { Value: '' }
    expect(setGs1CompositeLinearType(element, GS1_128_LINEAR_TYPE, result)).toBe(true)
    expect(element.LinearType).toBe(GS1_128_LINEAR_TYPE)
    expect(element.Mode).toBe(GS1_COMPOSITE_CC_C_MODE)
    expect(element.Field?.Value).toBe(DEFAULT_GS1_128_COMPOSITE_PRIMARY)
    expect(() => assertTemplate(template(GS1_128_LINEAR_TYPE, 0))).not.toThrow()
  })

  it('rejects a capability response that advertises CC-C without its GS1-128 linear type', () => {
    const response = caps(true)
    const composite = response.gs1Composite as Record<string, unknown>
    composite.linearTypes = ['Gs1DataBarOmnidirectional', 'Gs1DataBarLimited']
    expect(() => normalizeDesignerCapabilities(response)).toThrow('gs1Composite 无效')
  })

  it('preserves Composite parameters and both bindings through a project ZIP package', async () => {
    const original = template()
    const zip = await exportTemplateBundle(original, [])
    const restored = await importTemplateBundle(new File([zip], 'composite.zip', { type: 'application/zip' }))
    expect(restored.template).toEqual(original)
    expect(restored.resources).toEqual([])
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

  it('rejects invalid GS1-128 mode combinations before preview or render requests', async () => {
    const fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
    await expect(previewTemplate(template(GS1_128_LINEAR_TYPE, 1), {}, []))
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
