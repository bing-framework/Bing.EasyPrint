import { describe, expect, it } from 'vitest'
import { ref } from 'vue'
import {
  canCreateElement, createCapabilityRequestRunner, cutCapabilitiesKnown, filledRectangleSupported, legacyDeviceFontSupported, normalizeDesignerCapabilities,
  rotationSupported, textSizeSupported, valueInRange, type DesignerCapabilities,
} from './capabilities'
import type { FontElement } from './model'

function capabilities(overrides: Partial<DesignerCapabilities> = {}): DesignerCapabilities {
  return {
    protocol: 'Cpcl', escPosLayoutMode: null, nativeFlow: null, nativeText: null, rasterFonts: null, jobFeedUnits: [],
    cutModes: [], cutFeedUnits: [], cutFeedMotionUnits: null, cutFeedDots: null, tsplCutter: null, dpi: 200,
    elementTypes: [1, 2, 3, 4, 5, 7, 8, 9, 10], fontKinds: [0, 1],
    deviceFonts: [
      { name: '24', size: { minimum: 1, maximum: null }, sizes: [24, 48] },
      { name: '55', size: { minimum: 1, maximum: null }, sizes: [16, 32] },
    ],
    textSizes: [16, 24, 32, 48], textSize: { minimum: 1, maximum: null },
    textRotations: [0, 90, 180, 270], imageRotations: [0, 90, 180, 270], barcodeTypes: [0], barcodeRotations: [0, 270],
    qrModuleSize: { minimum: 1, maximum: 32 }, barcodeModuleWidth: { minimum: 1, maximum: null },
    barcodeHeight: { minimum: 1, maximum: null },
    supportsExplicitBarcodeModuleWidth: false, supportsWideModuleWidth: false,
    supportsBoldText: true, supportsUnderlineText: true, supportsRotatedUnderlineText: false,
    supportsHostFontFamily: false, supportsLegacyDeviceFont: false, supportsBarcodeHumanReadable: false,
    supportsCut: false, supportsExtraFeed: false, supportsFilledRectangles: true,
    supportsCoordinates: true, supportsExplicitQuietZone: true,
    ...overrides,
  }
}

describe('designer capabilities rules', () => {
  it('does not enable new entries until a confirmed snapshot exists, while keeping composites in the capability model', () => {
    const caps = capabilities({ elementTypes: [1, 6] })
    expect(canCreateElement(undefined, 'loading', 1)).toBe(false)
    expect(canCreateElement(caps, 'ready', 1)).toBe(true)
    expect(canCreateElement(caps, 'ready', 3)).toBe(false)
    expect(canCreateElement(caps, 'ready', 6)).toBe(true)
  })

  it('keeps outline rectangles creatable when the protocol cannot fill them', () => {
    const caps = capabilities({ elementTypes: [1, 5, 7], supportsFilledRectangles: false })
    expect(canCreateElement(caps, 'ready', 7)).toBe(true)
    expect(filledRectangleSupported(caps)).toBe(false)
    expect(filledRectangleSupported({ ...caps, supportsFilledRectangles: true })).toBe(true)
  })

  it('uses exact device names and device-specific discrete sizes for retained legacy values', () => {
    const caps = capabilities()
    const deviceFont = { Type: 1, Font: { Kind: 1 as const, Name: '24' }, FontSize: 24 } as FontElement
    const wrongCase = { Type: 1, Font: { Kind: 1 as const, Name: '24 ' }, FontSize: 24 } as FontElement
    expect(textSizeSupported(caps, deviceFont, 24)).toBe(true)
    expect(textSizeSupported(caps, deviceFont, 32)).toBe(false)
    expect(textSizeSupported(caps, wrongCase, 24)).toBe(false)
    expect(legacyDeviceFontSupported(caps, 24, 24)).toBe(true)
    expect(legacyDeviceFontSupported(caps, 24, 32)).toBe(false)
    expect(legacyDeviceFontSupported(caps, 24, 24.5)).toBe(false)
  })

  it('accepts only integer module values within declared ranges', () => {
    const range = { minimum: 1, maximum: 6 }
    expect(valueInRange(2, range)).toBe(true)
    expect(valueInRange(6, range)).toBe(true)
    expect(valueInRange(6.5, range)).toBe(false)
    expect(valueInRange(7, range)).toBe(false)
  })

  it('keeps image rotation conservative when an old Host omits the field', () => {
    const response = capabilities()
    delete (response as unknown as Record<string, unknown>).imageRotations
    expect(normalizeDesignerCapabilities(response).imageRotations).toEqual([0])
    expect(normalizeDesignerCapabilities({ ...response, imageRotations: [0, 90, 180, 270] }).imageRotations)
      .toEqual([0, 90, 180, 270])
    expect(() => normalizeDesignerCapabilities({ ...response, imageRotations: [0, 45] })).toThrow('imageRotations')
    expect(() => normalizeDesignerCapabilities({ ...response, imageRotations: [90] })).toThrow('包含 0 度')
  })

  it('normalizes QR rotation capability and remains conservative for old Hosts', () => {
    const response = capabilities({ qrCodeRotations: [0, 90, 180, 270] })
    const normalized = normalizeDesignerCapabilities(response)
    expect(normalized.qrCodeRotations).toEqual([0, 90, 180, 270])
    expect(rotationSupported(normalized, 3, 90)).toBe(true)
    const legacy = { ...response }
    delete (legacy as unknown as Record<string, unknown>).qrCodeRotations
    expect(normalizeDesignerCapabilities(legacy).qrCodeRotations).toEqual([0])
    expect(() => normalizeDesignerCapabilities({ ...response, qrCodeRotations: [0, 45] })).toThrow('qrCodeRotations')
  })

  it('does not expose images from ESC/POS Native capabilities', () => {
    const response = capabilities({ protocol: 'EscPos', escPosLayoutMode: 'Native', nativeFlow: 'Sequential', elementTypes: [1, 2], imageRotations: [0] })
    const normalized = normalizeDesignerCapabilities(response)
    expect(normalized.imageRotations).toEqual([0])
    expect(canCreateElement(normalized, 'ready', 2)).toBe(false)
    expect(() => normalizeDesignerCapabilities({ ...response, imageRotations: [0, 90] })).toThrow('Native')
  })

  it('accepts the ESC/POS Raster HRI font-size capability and rejects invalid scope', () => {
    const response = capabilities({ protocol: 'EscPos', escPosLayoutMode: 'Raster', nativeFlow: null,
      rasterBarcodeHumanReadableFontSize: 16 })
    expect(normalizeDesignerCapabilities(response).rasterBarcodeHumanReadableFontSize).toBe(16)
    expect(normalizeDesignerCapabilities({ ...response, rasterBarcodeHumanReadableFontSize: undefined })
      .rasterBarcodeHumanReadableFontSize).toBeNull()
    expect(() => normalizeDesignerCapabilities({ ...response, rasterBarcodeHumanReadableFontSize: 0 })).toThrow('1–255')
    expect(() => normalizeDesignerCapabilities({ ...response, protocol: 'Cpcl', escPosLayoutMode: null,
      rasterBarcodeHumanReadableFontSize: 16 })).toThrow('仅能用于 ESC/POS Raster')
  })
})

describe('capability request races', () => {
  it('marks an older response stale after a newer configuration starts', async () => {
    const pending: Array<{ resolve: (value: string) => void; signal: AbortSignal }> = []
    const runner = createCapabilityRequestRunner(async (request: string, signal) => {
      const result = await new Promise<string>((resolve) => pending.push({ resolve, signal }))
      return `${request}:${result}`
    })

    const first = runner.run('old')
    const second = runner.run('new')
    pending[0].resolve('response')
    pending[1].resolve('response')

    await expect(first).resolves.toEqual({ status: 'stale' })
    await expect(second).resolves.toEqual({ status: 'ready', value: 'new:response' })
    expect(pending[0].signal.aborted).toBe(true)
  })
})

describe('capability response contract', () => {
  it('recognizes confirmed cut metadata through Vue proxies without upgrading old Host responses', () => {
    const response = capabilities({ protocol: 'EscPos', escPosLayoutMode: 'Raster', supportsCut: true,
      cutModes: ['Partial'], cutFeedUnits: ['MotionUnits'], cutFeedMotionUnits: { minimum: 0, maximum: 255 } })
    const normalized = normalizeDesignerCapabilities(response)
    const reactiveCapabilities = ref(normalized)
    expect(reactiveCapabilities.value).not.toBe(normalized)
    expect(cutCapabilitiesKnown(reactiveCapabilities.value)).toBe(true)
    expect(JSON.parse(JSON.stringify(normalized))).toEqual(normalized)

    const oldResponse = { ...response } as Record<string, unknown>
    delete oldResponse.cutModes
    delete oldResponse.cutFeedUnits
    delete oldResponse.cutFeedMotionUnits
    expect(cutCapabilitiesKnown(ref(normalizeDesignerCapabilities(oldResponse)).value)).toBe(false)
  })

  it('validates and snapshots label profile limits without assuming older Hosts provide them', () => {
    const base = capabilities({ protocol: 'Tspl' })
    expect(normalizeDesignerCapabilities(base).labelProfile).toBeNull()
    const profile = {
      name: 'Label-A', firmwareVersion: 'recorded-1', printableWidthDots: 400,
      maxLabelHeightDots: 800, maxImageDataBytes: 4096,
      supportedNativeBarcodeHumanReadableTypes: ['Code128', 'Ean13'],
      supportedNativeQrCodeRotations: ['Rotate90', 'Rotate270'],
      encodingCodePages: [20127, 936], characterRanges: [{ first: 32, last: 126 }],
    }
    const snapshot = normalizeDesignerCapabilities({ ...base, labelProfile: profile }).labelProfile!
    expect(snapshot).toEqual(profile)
    profile.encodingCodePages[0] = 65001
    profile.characterRanges[0].last = 127
    expect(snapshot.encodingCodePages).toEqual([20127, 936])
    expect(snapshot.characterRanges).toEqual([{ first: 32, last: 126 }])
    expect(snapshot.supportedNativeBarcodeHumanReadableTypes).toEqual(['Code128', 'Ean13'])
    expect(snapshot.supportedNativeQrCodeRotations).toEqual(['Rotate90', 'Rotate270'])
    expect(() => normalizeDesignerCapabilities({ ...base, protocol: 'Cpcl', labelProfile: profile })).toThrow('labelProfile')
    expect(() => normalizeDesignerCapabilities({ ...base, labelProfile: { ...profile, maxImageDataBytes: 0 } })).toThrow('maxImageDataBytes')
    expect(() => normalizeDesignerCapabilities({ ...base, labelProfile: { ...profile, encodingCodePages: [936, 936] } })).toThrow('encodingCodePages')
    expect(() => normalizeDesignerCapabilities({ ...base, protocol: 'Zpl', labelProfile: { ...profile, encodingCodePages: [936] } })).toThrow('encodingCodePages')
    expect(() => normalizeDesignerCapabilities({ ...base, labelProfile: { ...profile, characterRanges: [{ first: 32, last: 64 }, { first: 64, last: 126 }] } })).toThrow('重叠')
    expect(() => normalizeDesignerCapabilities({ ...base, labelProfile: { ...profile, unknown: true } })).toThrow('unknown')
    expect(() => normalizeDesignerCapabilities({ ...base, labelProfile: { ...profile, supportedNativeBarcodeHumanReadableTypes: ['Code128', 'Code128'] } })).toThrow('重复制式')
    expect(() => normalizeDesignerCapabilities({ ...base, labelProfile: { ...profile, supportedNativeBarcodeHumanReadableTypes: ['Unknown'] } })).toThrow('supportedNativeBarcodeHumanReadableTypes')
    expect(() => normalizeDesignerCapabilities({ ...base, labelProfile: { ...profile, supportedNativeQrCodeRotations: ['Rotate90', 'Rotate90'] } })).toThrow('不能重复')
    expect(() => normalizeDesignerCapabilities({ ...base, labelProfile: { ...profile, supportedNativeQrCodeRotations: ['None'] } })).toThrow('supportedNativeQrCodeRotations')
    const deviceOnly = normalizeDesignerCapabilities({ ...base, fontKinds: [1], textSize: null, textSizes: null,
      deviceFonts: [{ name: '1', size: { minimum: 1, maximum: 125 }, sizes: null }] })
    expect(textSizeSupported(deviceOnly, { Type: 1, Font: { Kind: 1, Name: '1' } } as FontElement, 24)).toBe(true)
    expect(textSizeSupported(deviceOnly, { Type: 1, Font: { Kind: 0 } } as FontElement, 24)).toBe(false)
    expect(() => normalizeDesignerCapabilities({ ...base, textSize: null })).toThrow('默认字体')
  })

  it('validates ESC/POS Native HRI position capabilities and rejects them for other layouts', () => {
    const base = capabilities({ protocol: 'EscPos', escPosLayoutMode: 'Native', nativeFlow: 'Rows', imageRotations: [0] })
    const response = normalizeDesignerCapabilities({ ...base,
      supportedNativeBarcodeHumanReadablePositions: ['Above', 'Below'] })
    expect(response.supportedNativeBarcodeHumanReadablePositions).toEqual(['Above', 'Below'])
    expect(() => normalizeDesignerCapabilities({ ...base,
      supportedNativeBarcodeHumanReadablePositions: ['Below', 'Below'] })).toThrow('不能重复')
    expect(() => normalizeDesignerCapabilities({ ...base,
      supportedNativeBarcodeHumanReadablePositions: ['None'] })).toThrow('无效')
    expect(() => normalizeDesignerCapabilities({ ...base, escPosLayoutMode: 'Raster', nativeFlow: null,
      supportedNativeBarcodeHumanReadablePositions: ['Below'] })).toThrow('仅能用于 ESC/POS Native')
  })

  it('rejects incomplete responses instead of inferring unsupported fields', () => {
    expect(() => normalizeDesignerCapabilities({ protocol: 'Cpcl' })).toThrow('能力响应')
  })

  it('strictly validates the Native flow response and its protocol/layout pairing', () => {
    const base = {
      protocol: 'EscPos', escPosLayoutMode: 'Native', nativeFlow: 'Sequential', jobFeedUnits: ['Lines', 'MotionUnits'], dpi: 203,
      elementTypes: [1], fontKinds: [0], deviceFonts: [], textSizes: [24],
      textSize: { minimum: 24, maximum: 24 }, textRotations: [0], barcodeTypes: [], barcodeRotations: [0],
      qrModuleSize: null, barcodeModuleWidth: null, barcodeHeight: null,
      supportsExplicitBarcodeModuleWidth: false, supportsWideModuleWidth: false,
      supportsBoldText: false, supportsUnderlineText: false, supportsRotatedUnderlineText: false,
      supportsHostFontFamily: false, supportsLegacyDeviceFont: false, supportsBarcodeHumanReadable: false,
      supportsCut: false, supportsExtraFeed: true, supportsFilledRectangles: false,
      supportsCoordinates: true, supportsExplicitQuietZone: false,
    }
    expect(normalizeDesignerCapabilities(base).nativeFlow).toBe('Sequential')
    expect(normalizeDesignerCapabilities(base).nativeText).toBeNull()
    const nativeText = {
      encodingCodePage: 936, codePageIndex: 255, characterMode: 'FsKanji',
      characterRanges: [{ first: 0, last: 127 }, { first: 19968, last: 40959 }],
      halfCellWidthDots: 12, fullCellWidthDots: 24, lineHeightDots: 24,
    }
    expect(normalizeDesignerCapabilities({ ...base, nativeText }).nativeText).toEqual(nativeText)
    expect(() => normalizeDesignerCapabilities({ ...base, nativeText: { ...nativeText, characterRanges: [{ first: 0xD800, last: 0xDFFF }] } })).toThrow('nativeText')
    expect(() => normalizeDesignerCapabilities({ ...base, nativeText: { ...nativeText, encodingCodePage: 437 } })).toThrow('FsKanji')
    expect(() => normalizeDesignerCapabilities({ ...base, protocol: 'Tspl', escPosLayoutMode: null, nativeFlow: null, nativeText })).toThrow('nativeText')
    expect(() => normalizeDesignerCapabilities({ ...base, nativeFlow: null })).toThrow('nativeFlow')
    expect(() => normalizeDesignerCapabilities({ ...base, protocol: 'Tspl', escPosLayoutMode: null, nativeFlow: 'Rows' })).toThrow('nativeFlow')
    expect(() => normalizeDesignerCapabilities({ ...base, nativeFlow: 'Other' })).toThrow('nativeFlow')
  })

  it('normalizes Raster font metadata and rejects unknown or dangling aliases', () => {
    const base = {
      protocol: 'EscPos', escPosLayoutMode: 'Raster', nativeFlow: null, jobFeedUnits: ['Lines', 'MotionUnits'], dpi: 203,
      elementTypes: [1], fontKinds: [0], deviceFonts: [], textSizes: [24],
      textSize: { minimum: 24, maximum: 24 }, textRotations: [0], barcodeTypes: [], barcodeRotations: [0],
      qrModuleSize: null, barcodeModuleWidth: null, barcodeHeight: null,
      supportsExplicitBarcodeModuleWidth: false, supportsWideModuleWidth: false,
      supportsBoldText: false, supportsUnderlineText: false, supportsRotatedUnderlineText: false,
      supportsHostFontFamily: true, supportsLegacyDeviceFont: false, supportsBarcodeHumanReadable: false,
      supportsCut: false, supportsExtraFeed: false, supportsFilledRectangles: false,
      supportsCoordinates: true, supportsExplicitQuietZone: false,
    }
    const rasterFonts = {
      defaultFont: 'Regular', fallbackFonts: ['Fallback'],
      fonts: [
        { name: 'Regular', version: '1.0', hasBold: true },
        { name: 'Fallback', version: '2.0', hasBold: false },
      ],
    }
    expect(normalizeDesignerCapabilities({ ...base, rasterFonts }).rasterFonts).toEqual(rasterFonts)
    expect(normalizeDesignerCapabilities({ ...base }).rasterFonts).toBeNull()
    expect(() => normalizeDesignerCapabilities({ ...base, rasterFonts: { ...rasterFonts, unknown: true } })).toThrow('rasterFonts.unknown')
    expect(() => normalizeDesignerCapabilities({ ...base, rasterFonts: { ...rasterFonts, defaultFont: 'Missing' } })).toThrow('defaultFont')
    expect(() => normalizeDesignerCapabilities({ ...base, rasterFonts: { ...rasterFonts, fallbackFonts: ['Fallback', 'Fallback'] } })).toThrow('fallbackFonts')
    expect(() => normalizeDesignerCapabilities({ ...base, rasterFonts: { ...rasterFonts, fonts: [{ ...rasterFonts.fonts[0], name: 'Regular' }, { ...rasterFonts.fonts[1], name: 'Regular' }] } })).toThrow('name')
    expect(() => normalizeDesignerCapabilities({ ...base, protocol: 'EscPos', escPosLayoutMode: 'Native', nativeFlow: 'Rows', rasterFonts })).toThrow('仅能用于 ESC/POS Raster')
  })

  it('normalizes ESC/POS cut capabilities and keeps old Host responses compatible', () => {
    const base = capabilities({ protocol: 'EscPos', escPosLayoutMode: 'Raster', nativeFlow: null, supportsCut: true,
      cutModes: ['Full', 'Partial'], cutFeedUnits: ['MotionUnits', 'Dots'], cutFeedMotionUnits: { minimum: 0, maximum: 255 } })
    expect(normalizeDesignerCapabilities(base).cutModes).toEqual(['Full', 'Partial'])
    expect(normalizeDesignerCapabilities(base).cutFeedUnits).toEqual(['MotionUnits', 'Dots'])
    expect(normalizeDesignerCapabilities(base).cutFeedMotionUnits).toEqual({ minimum: 0, maximum: 255 })

    const oldHost = { ...base }
    delete (oldHost as Record<string, unknown>).cutModes
    delete (oldHost as Record<string, unknown>).cutFeedUnits
    delete (oldHost as Record<string, unknown>).cutFeedMotionUnits
    expect(normalizeDesignerCapabilities(oldHost).cutModes).toEqual([])
    expect(normalizeDesignerCapabilities(oldHost).cutFeedUnits).toEqual([])
    expect(normalizeDesignerCapabilities(oldHost).cutFeedMotionUnits).toBeNull()
  })

  it('normalizes the TSPL cutter contract and derives the bounded dot feed range', () => {
    const base = capabilities({ protocol: 'Tspl', escPosLayoutMode: null, nativeFlow: null, supportsCut: true,
      cutModes: ['Partial'], cutFeedUnits: ['Dots'], cutFeedDots: { minimum: 0, maximum: 9963 },
      tsplCutter: { strategy: 'Immediate', supportedCutModes: ['Partial'], supportsEndOfJob: true, cutPositionFeedDots: 36 } })
    const normalized = normalizeDesignerCapabilities(base)
    expect(normalized.tsplCutter).toEqual({
      strategy: 'Immediate', supportedCutModes: ['Partial'], supportsEndOfJob: true, cutPositionFeedDots: 36,
    })
    expect(normalized.cutFeedDots).toEqual({ minimum: 0, maximum: 9963 })
    expect(normalized.cutFeedUnits).toEqual(['Dots'])
    expect(cutCapabilitiesKnown(normalized)).toBe(true)

    const batch = normalizeDesignerCapabilities({ ...base,
      tsplCutter: { strategy: 'Batch', supportedCutModes: ['Full', 'Partial'], supportsEndOfJob: true, cutPositionFeedDots: null },
      cutModes: ['Full', 'Partial'], cutFeedUnits: [], cutFeedDots: null,
    })
    expect(batch.tsplCutter?.strategy).toBe('Batch')
    expect(batch.cutFeedDots).toBeNull()
  })

  it('rejects malformed TSPL cutter capability combinations', () => {
    const base = capabilities({ protocol: 'Tspl', escPosLayoutMode: null, nativeFlow: null, supportsCut: true,
      cutModes: ['Full'], cutFeedUnits: [], cutFeedDots: null,
      tsplCutter: { strategy: 'Batch', supportedCutModes: ['Full'], supportsEndOfJob: true, cutPositionFeedDots: null } })
    expect(() => normalizeDesignerCapabilities({ ...base,
      tsplCutter: { strategy: 'Immediate', supportedCutModes: ['Full', 'Partial'], supportsEndOfJob: true, cutPositionFeedDots: null },
      cutModes: ['Full', 'Partial'], cutFeedDots: { minimum: 0, maximum: 9999 },
    })).toThrow('一种固定切法')
    expect(() => normalizeDesignerCapabilities({ ...base,
      tsplCutter: { strategy: 'Batch', supportedCutModes: ['Full'], supportsEndOfJob: true, cutPositionFeedDots: 1 },
    })).toThrow('Batch')
    expect(() => normalizeDesignerCapabilities({ ...base,
      tsplCutter: { strategy: 'Batch', supportedCutModes: ['Full'], supportsEndOfJob: false, cutPositionFeedDots: null },
    })).toThrow('supportsEndOfJob')
    expect(() => normalizeDesignerCapabilities({ ...base, cutFeedUnits: ['MotionUnits'] })).toThrow('TSPL')
    expect(() => normalizeDesignerCapabilities({ ...base,
      tsplCutter: { strategy: 'Immediate', supportedCutModes: ['Full'], supportsEndOfJob: true, cutPositionFeedDots: 1 },
      cutModes: ['Full'], cutFeedUnits: ['Dots'], cutFeedDots: { minimum: 0, maximum: 9997 },
    })).toThrow('9999')
    expect(() => normalizeDesignerCapabilities({ ...base,
      tsplCutter: { strategy: 'Immediate', supportedCutModes: ['Full'], supportsEndOfJob: true, cutPositionFeedDots: 1 },
      cutModes: ['Full'], cutFeedUnits: ['Dots'], cutFeedDots: { minimum: 0, maximum: 10000 },
    })).toThrow('0–9999')
    expect(() => normalizeDesignerCapabilities({ ...base, cutModes: ['Partial'] })).toThrow('不一致')
  })

  it('strictly rejects invalid cut capability combinations', () => {
    const base = capabilities({ protocol: 'EscPos', escPosLayoutMode: 'Raster', nativeFlow: null, supportsCut: true,
      cutModes: ['Full'], cutFeedUnits: ['MotionUnits'], cutFeedMotionUnits: { minimum: 0, maximum: 255 } })
    expect(() => normalizeDesignerCapabilities({ ...base, cutModes: ['Full', 'Full'] })).toThrow('不能重复')
    expect(() => normalizeDesignerCapabilities({ ...base, cutFeedUnits: ['Lines'] })).toThrow('不支持 Lines')
    expect(() => normalizeDesignerCapabilities({ ...base, cutFeedMotionUnits: { minimum: 0, maximum: 254 } })).toThrow('0–255')
    expect(() => normalizeDesignerCapabilities({ ...base, supportsCut: false })).toThrow('supportsCut')
    expect(() => normalizeDesignerCapabilities({ ...base, protocol: 'Tspl', escPosLayoutMode: null, nativeFlow: null })).toThrow('TSPL')
  })
})
