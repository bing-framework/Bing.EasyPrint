import { afterEach, describe, expect, it, vi } from 'vitest'
import { loadDesignerCapabilities, previewTemplate, renderTemplate, type ZplCutterProfile } from './api'
import { cutCapabilitiesKnown, normalizeDesignerCapabilities } from './capabilities'
import { defaultDesignerOutputSettings, normalizeLabelPrinterProfile, parseDesignerProject, serializeDesignerProject } from './designerProject'
import { createBlankSample } from './samples'

afterEach(() => vi.unstubAllGlobals())

const delayed: ZplCutterProfile = { strategy: 'Delayed', supportsTearOff: true, supportedCutModes: ['Full'] }
function response(overrides: Record<string, unknown> = {}) {
  return {
    protocol: 'Zpl', escPosLayoutMode: null, nativeFlow: null, dpi: 203, jobFeedUnits: [],
    labelProfile: { name: 'ZPL-CUT', firmwareVersion: null, printableWidthDots: null, maxLabelHeightDots: null,
      maxImageDataBytes: null, encodingCodePages: [20127, 65001], characterRanges: null },
    cutModes: ['Full'], cutFeedUnits: [], cutFeedMotionUnits: null, cutFeedDots: null,
    zplCutter: { ...delayed, kioskPartialUncutMillimeters: null, kioskCutMarginMillimeters: null },
    supportsIndependentOutputSegments: true,
    elementTypes: [1, 6], fontKinds: [0], deviceFonts: [], textSizes: null,
    textSize: { minimum: 1, maximum: null }, textRotations: [0], barcodeTypes: [0], barcodeRotations: [0],
    qrModuleSize: null, barcodeModuleWidth: null, barcodeHeight: null,
    supportsExplicitBarcodeModuleWidth: false, supportsWideModuleWidth: false,
    supportsBoldText: false, supportsUnderlineText: false, supportsRotatedUnderlineText: false,
    supportsHostFontFamily: false, supportsLegacyDeviceFont: true, supportsBarcodeHumanReadable: true,
    supportsCut: true, supportsExtraFeed: false, supportsFilledRectangles: false,
    supportsCoordinates: true, supportsExplicitQuietZone: true,
    ...overrides,
  }
}
function json(body: unknown) {
  return new Response(JSON.stringify(body), { headers: {
    'Content-Type': 'application/json',
    'X-EasyPrint-Contract-Version': '1',
    'X-EasyPrint-Features': 'structured-errors,strict-options,binding-diagnostics',
  } })
}

describe('ZPL cutter project and capability contract', () => {
  it('round trips a Kiosk profile and preserves its independent configuration', () => {
    const settings = defaultDesignerOutputSettings()
    settings.outputTarget = 'Zpl'
    settings.cutAfterPrint = true
    settings.cutOptions = { mode: 'Partial' }
    settings.zplMedia.profile = { name: 'Kiosk', cutter: { strategy: 'Kiosk', supportsTearOff: true,
      supportedCutModes: ['Full', 'Partial'], kioskPartialUncutMillimeters: 20, kioskCutMarginMillimeters: 7 } }
    const restored = parseDesignerProject(JSON.parse(serializeDesignerProject(createBlankSample(), settings)))
    expect(restored.outputSettings?.zplMedia.profile?.cutter).toEqual(settings.zplMedia.profile.cutter)
    expect(restored.outputSettings?.cutOptions).toEqual({ mode: 'Partial', feedToCutPosition: false,
      feedBeforeCutAmount: 0, feedBeforeCutUnit: 'MotionUnits' })
    expect(restored.outputSettings?.zplMedia.profile?.cutter).not.toBe(settings.zplMedia.profile.cutter)
  })

  it.each([
    { supportsTearOff: false }, { supportsTearOff: true, strategy: 'Wrong' },
    { supportsTearOff: true, supportedCutModes: [] }, { supportsTearOff: true, supportedCutModes: ['Full', 'Full'] },
    { supportsTearOff: true, strategy: 'Delayed', supportedCutModes: ['Partial'] },
    { supportsTearOff: true, kioskCutMarginMillimeters: 9 },
    { supportsTearOff: true, strategy: 'Kiosk' },
    { supportsTearOff: true, strategy: 'Kiosk', kioskCutMarginMillimeters: 1 },
    { supportsTearOff: true, strategy: 'Kiosk', kioskCutMarginMillimeters: 10 },
    { supportsTearOff: true, strategy: 'Kiosk', kioskCutMarginMillimeters: 9, supportedCutModes: ['Partial'] },
    { supportsTearOff: true, strategy: 'Kiosk', kioskCutMarginMillimeters: 9, supportedCutModes: ['Partial'], kioskPartialUncutMillimeters: 61 },
    { supportsTearOff: true, supportsEndOfJob: true },
  ])('rejects invalid or cross protocol cutter declaration %#', (cutter) => {
    expect(() => normalizeLabelPrinterProfile({ cutter }, 'Zpl')).toThrow()
  })

  it('rejects disabled cutter contradictions and ZPL fields in a TSPL declaration', () => {
    expect(() => normalizeLabelPrinterProfile({ supportsCut: false, cutter: delayed }, 'Zpl')).toThrow()
    expect(() => normalizeLabelPrinterProfile({ cutter: delayed }, 'Tspl')).toThrow()
  })

  it('parses delayed capabilities and marks explicit cut support', () => {
    const caps = normalizeDesignerCapabilities(response())
    expect(caps.zplCutter?.strategy).toBe('Delayed')
    expect(caps.supportsIndependentOutputSegments).toBe(true)
    expect(cutCapabilitiesKnown(caps)).toBe(true)
  })

  it.each([
    { supportsIndependentOutputSegments: false }, { cutModes: ['Partial'] }, { supportsCut: false },
    { zplCutter: null }, { zplCutter: { ...delayed, supportsTearOff: false } },
    { zplCutter: { ...delayed, kioskPartialUncutMillimeters: null, kioskCutMarginMillimeters: null, extra: true } },
  ])('rejects inconsistent capabilities %#', (overrides) => {
    expect(() => normalizeDesignerCapabilities(response(overrides))).toThrow()
  })

  it('keeps legacy ZPL capabilities usable without claiming explicit cutter support', () => {
    const legacy = response({ cutModes: [], zplCutter: undefined, supportsIndependentOutputSegments: undefined })
    const caps = normalizeDesignerCapabilities(JSON.parse(JSON.stringify(legacy)))
    expect(caps.zplCutter).toBeNull()
    expect(caps.supportsIndependentOutputSegments).toBe(false)
    expect(cutCapabilitiesKnown(caps)).toBe(false)
  })

  it.each(['preview', 'render'] as const)('blocks direct %s on an old Host even without custom cut options', async (operation) => {
    const old = response({ zplCutter: undefined, supportsIndependentOutputSegments: undefined })
    const fetchMock = vi.fn().mockResolvedValue(json(old))
    vi.stubGlobal('fetch', fetchMock)
    const call = operation === 'preview'
      ? previewTemplate(createBlankSample(), {}, [], undefined, 'Zpl', 'Native', false, undefined, undefined, undefined, undefined, undefined, true,
        undefined, { profile: { cutter: delayed } })
      : renderTemplate(createBlankSample(), {}, [], 'Zpl', 'Native', false, undefined, undefined, undefined, undefined, undefined, true,
        undefined, { profile: { cutter: delayed } })
    await expect(call).rejects.toThrow('更新 Host')
    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/designer/capabilities')
  })

  it('rejects a Host that accepts the request but returns a different strategy', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(json(response({ supportsIndependentOutputSegments: false,
      zplCutter: { ...delayed, strategy: 'Cutter', kioskPartialUncutMillimeters: null, kioskCutMarginMillimeters: null } }))))
    await expect(loadDesignerCapabilities({ protocol: 'Zpl', zplMedia: { profile: { cutter: delayed } } })).rejects.toThrow('不一致')
  })
})
