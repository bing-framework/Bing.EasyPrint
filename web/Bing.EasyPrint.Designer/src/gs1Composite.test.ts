import { afterEach, describe, expect, it, vi } from 'vitest'
import { loadDesignerCapabilities, previewTemplate, renderTemplate } from './api'
import { exportTemplateBundle, importTemplateBundle } from './archive'
import { canCreateElement, rotationSupported } from './capabilities'
import { parseDesignerProject, serializeDesignerProject } from './designerProject'
import { buildLocalLayout } from './layout'
import { assertTemplate, cloneTemplate, type BpTemplate, type Gs1CompositeElement } from './model'
import { elementLabel } from './tree'

afterEach(() => vi.unstubAllGlobals())

function template(): BpTemplate {
  return { Version: 3, Width: 400, Height: 300, Rows: [{ Cells: [{ Elements: [{
    Type: 11, LinearType: 27, Mode: 2, ModuleWidth: 2, LinearBarHeight: 40, Rotation: 90,
    Field: { Name: 'Gtin' }, SecondaryField: { Name: 'Lot' }, QuietZone: { Left: 3, Top: 5, Right: 7, Bottom: 11 },
  }] }] }] }
}

function caps(): Record<string, unknown> {
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
    gs1Composite: { elementType: 11, linearTypes: ['Gs1DataBarOmnidirectional', 'Gs1DataBarLimited'],
      modes: ['Auto', 'CcA', 'CcB'], rotations: [0, 90, 180, 270], includesHumanReadable: false, dimensionsFromEncoding: true },
  }
}

function headers(composite = true): Record<string, string> {
  return { 'Content-Type': 'application/json', 'X-EasyPrint-Contract-Version': '1',
    'X-EasyPrint-Features': `structured-errors,strict-options,binding-diagnostics${composite ? ',gs1-composite' : ''}` }
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

  it('uses Host preview dimensions without local estimates', async () => {
    const page = { width: 400, height: 300, elements: [{ sourcePath: 'Rows[0].Cells[0].Elements[0]', type: 11,
      rotation: 90, x: 0, y: 0, width: 81, height: 186 }] }
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ pages: [page] }), { headers: headers() })))
    expect((await previewTemplate(template(), {}, [])).pages[0]).toEqual(page)
  })
})
