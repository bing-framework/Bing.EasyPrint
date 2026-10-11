import { describe, expect, it } from 'vitest'
import { createProductSample } from './samples'
import {
  defaultDesignerOutputSettings,
  defaultEscPosPrinterProfile,
  normalizeLabelPrinterProfile,
  parseDesignerProject,
  serializeDesignerProject,
  validateDesignerOutputSettings,
  type DesignerOutputSettings,
} from './designerProject'

describe('designer project serialization', () => {
  it('returns independent default settings objects', () => {
    const first = defaultDesignerOutputSettings()
    const second = defaultDesignerOutputSettings()

    expect(first).toEqual(second)
    expect(first).not.toBe(second)
    expect(first.escPosDevice).not.toBe(second.escPosDevice)
    expect(first.tsplMedia).not.toBe(second.tsplMedia)
    expect(first.zplMedia).not.toBe(second.zplMedia)
  })

  it('round-trips a versioned envelope and normalizes nested settings', () => {
    const template = createProductSample()
    const settings: DesignerOutputSettings = {
      ...defaultDesignerOutputSettings(),
      outputTarget: 'Tspl',
      allowRasterizedLines: true,
      imageThreshold: 96,
      deviceHalfCellWidthAt24: 12.5,
      deviceFullCellWidthAt24: 25,
      deviceLineHeightAt24: 24,
      deviceFontCalibrations: {
        Tspl: { '1': { halfCellWidthAt24: 16.5, fullCellWidthAt24: 33, lineHeightAt24: 48 } },
        Zpl: {},
      },
      requireDeclaredNativeBounds: true,
      copies: 2,
      feedLines: 3,
      cutAfterPrint: false,
      encoding: 'Utf8',
      escPosDevice: {
        dpi: 203, codePageIndex: 17,
        profile: { ...defaultEscPosPrinterProfile(), printableWidthDots: 576, maxImageBlockBytes: 1024,
          text: { encodingCodePage: 936, codePageIndex: 255, characterMode: 'FsKanji' as const,
            characterRanges: [{ first: 0, last: 127 }, { first: 19968, last: 40959 }],
            halfCellWidthDots: 12, fullCellWidthDots: 24, lineHeightDots: 24 as const } },
      },
      tsplMedia: { allowRasterizedTextStyles: true, dpi: 300, density: 8 },
      zplMedia: { allowRasterizedTextStyles: true, dpi: 203, darkness: -2 },
    }

    const serialized = serializeDesignerProject(template, settings)
    const envelope = JSON.parse(serialized) as Record<string, unknown>
    expect(envelope.format).toBe('Bing.EasyPrint.DesignerProject')
    expect(envelope.version).toBe(1)

    const parsed = parseDesignerProject(envelope)
    expect(parsed.template).toEqual(template)
    expect(parsed.outputSettings).toEqual(settings)
    expect(parsed.outputSettings).not.toBe(settings)
    expect(parsed.outputSettings?.tsplMedia).not.toBe(settings.tsplMedia)
    expect(parsed.outputSettings?.escPosDevice).not.toBe(settings.escPosDevice)
    expect(parsed.outputSettings?.escPosDevice.profile).not.toBe(settings.escPosDevice.profile)
    expect(parsed.outputSettings?.escPosDevice.profile?.text).not.toBe(settings.escPosDevice.profile?.text)
    expect(parsed.outputSettings?.escPosDevice.profile?.text?.characterRanges)
      .not.toBe(settings.escPosDevice.profile?.text?.characterRanges)
  })

  it('accepts the historical bare template JSON without output settings', () => {
    const template = createProductSample()
    const parsed = parseDesignerProject(template)

    expect(parsed.template).toEqual(template)
    expect(parsed.template).not.toBe(template)
    expect(parsed.outputSettings).toBeUndefined()
  })

  it('rejects unsupported envelope versions and malformed envelopes', () => {
    const template = createProductSample()

    expect(() => parseDesignerProject({
      format: 'Bing.EasyPrint.DesignerProject', version: 2, template,
    })).toThrow('设计器项目版本不受支持')
    expect(() => parseDesignerProject({
      format: 'other', version: 1, template,
    })).toThrow('设计器项目版本不受支持')
    expect(() => parseDesignerProject({
      format: 'Bing.EasyPrint.DesignerProject', version: 1,
    })).toThrow('设计器项目缺少模板')
  })

  it('rejects invalid settings, unknown fields, and ZPL GBK', () => {
    const defaults = defaultDesignerOutputSettings()

    expect(() => validateDesignerOutputSettings({ ...defaults, imageThreshold: Number.NaN })).toThrow()
    expect(() => validateDesignerOutputSettings({ ...defaults, outputTarget: 'NoSuchProtocol' })).toThrow()
    expect(() => validateDesignerOutputSettings({ ...defaults, unknown: true })).toThrow('不支持的字段')
    expect(() => validateDesignerOutputSettings({ ...defaults, outputTarget: 'Zpl', encoding: 'Gbk' })).toThrow('ZPL 不支持 Gbk')
    expect(() => validateDesignerOutputSettings({ ...defaults, encoding: 'Device' })).toThrow('Device 编码')
    expect(() => validateDesignerOutputSettings({ ...defaults, outputTarget: 'EscPosRaster', encoding: 'Device', escPosDevice: {
      profile: { ...defaultEscPosPrinterProfile(), text: { encodingCodePage: 936, characterRanges: [{ first: 0, last: 127 }] } },
    } })).toThrow('Device 编码')
    expect(() => validateDesignerOutputSettings({ ...defaults, tsplMedia: { density: 16 } })).toThrow()
    expect(() => validateDesignerOutputSettings({ ...defaults, escPosDevice: {
      profile: { ...defaultEscPosPrinterProfile(), imageCommand: 'invalid' },
    } })).toThrow('imageCommand')
    expect(() => validateDesignerOutputSettings({ ...defaults, escPosDevice: {
      profile: { ...defaultEscPosPrinterProfile(), imageCommand: 0 },
    } })).toThrow('imageCommand')
    expect(() => validateDesignerOutputSettings({ ...defaults, escPosDevice: {
      profile: { ...defaultEscPosPrinterProfile(), bitImageMode: 'DoubleDensity8' },
    } })).toThrow('GS v 0')
    expect(() => validateDesignerOutputSettings({ ...defaults, escPosDevice: {
      profile: { ...defaultEscPosPrinterProfile(), maxImageBlockBytes: 0 },
    } })).toThrow('maxImageBlockBytes')
    expect(() => validateDesignerOutputSettings({ ...defaults, escPosDevice: {
      profile: { ...defaultEscPosPrinterProfile(), text: { encodingCodePage: 936, characterRanges: [] } },
    } })).toThrow('characterRanges')
    expect(() => validateDesignerOutputSettings({ ...defaults, escPosDevice: {
      profile: { ...defaultEscPosPrinterProfile(), text: { encodingCodePage: 936, characterRanges: [{ first: 0xD800, last: 0xDFFF }] } },
    } })).toThrow('代理项')
    expect(() => validateDesignerOutputSettings({ ...defaults, escPosDevice: {
      profile: { ...defaultEscPosPrinterProfile(), text: { encodingCodePage: 437, characterMode: 'FsKanji', characterRanges: [{ first: 0, last: 127 }] } },
    } })).toThrow('FsKanji')
    expect(() => validateDesignerOutputSettings({ ...defaults, escPosDevice: {
      profile: { ...defaultEscPosPrinterProfile(), text: { encodingCodePage: 936, characterRanges: [{ first: 0, last: 127 }], lineHeightDots: 25 } },
    } })).toThrow('lineHeightDots')
    expect(() => validateDesignerOutputSettings({ ...defaults, deviceFontCalibrations: {
      Tspl: { '1': { halfCellWidthAt24: 16, fullCellWidthAt24: 32 } }, Zpl: {},
    } })).toThrow('必须同时提供')
    expect(() => validateDesignerOutputSettings({ ...defaults, deviceFontCalibrations: {
      Tspl: { lower: { halfCellWidthAt24: 16, fullCellWidthAt24: 32, lineHeightAt24: 48 } }, Zpl: {},
    } })).toThrow('字体编号')
    expect(() => validateDesignerOutputSettings({ ...defaults, deviceFontCalibrations: {
      '1': { halfCellWidthAt24: 16, fullCellWidthAt24: 32, lineHeightAt24: 48 },
    } })).toThrow('不支持的字段')
  })

  it('keeps TSPL and ZPL font number calibration isolated', () => {
    const parsed = parseDesignerProject({
      format: 'Bing.EasyPrint.DesignerProject', version: 1, template: createProductSample(),
      outputSettings: {
        ...defaultDesignerOutputSettings(),
        outputTarget: 'Zpl',
        deviceFontCalibrations: {
          Tspl: { '1': { halfCellWidthAt24: 16, fullCellWidthAt24: 32, lineHeightAt24: 48 } },
          Zpl: { '1': { halfCellWidthAt24: 18, fullCellWidthAt24: 36, lineHeightAt24: 24 } },
        },
      },
    })

    expect(parsed.outputSettings?.deviceFontCalibrations.Tspl['1'].halfCellWidthAt24).toBe(16)
    expect(parsed.outputSettings?.deviceFontCalibrations.Zpl['1'].halfCellWidthAt24).toBe(18)
  })

  it('keeps the legacy Rows default compact and rejects Sequential for non-Native targets', () => {
    const defaults = defaultDesignerOutputSettings()
    expect(defaults.escPosDevice).toEqual({})
    expect(validateDesignerOutputSettings({
      ...defaults, outputTarget: 'EscPosNative', escPosDevice: { nativeFlow: 'Rows' },
    }).escPosDevice.nativeFlow).toBe('Rows')
    expect(() => validateDesignerOutputSettings({
      ...defaults, outputTarget: 'EscPosRaster', escPosDevice: { nativeFlow: 'Sequential' },
    })).toThrow('Sequential 仅适用于 ESC/POS Native')
    expect(() => validateDesignerOutputSettings({
      ...defaults, outputTarget: 'Tspl', escPosDevice: { nativeFlow: 'Sequential' },
    })).toThrow('Sequential 仅适用于 ESC/POS Native')
    expect(() => validateDesignerOutputSettings({
      ...defaults, outputTarget: 'EscPosNative', escPosDevice: { nativeFlow: 'Invalid' },
    })).toThrow('nativeFlow')
  })

  it('keeps a disabled Native text contract absent and clones its ranges on normalization', () => {
    const defaults = defaultDesignerOutputSettings()
    expect(validateDesignerOutputSettings(defaults).escPosDevice.profile).toBeUndefined()
    const ranges = [{ first: 0, last: 127 }]
    const normalized = validateDesignerOutputSettings({
      ...defaults,
      outputTarget: 'EscPosNative',
      escPosDevice: { profile: { ...defaultEscPosPrinterProfile(), text: { encodingCodePage: 437, characterRanges: ranges } } },
    })
    expect(normalized.escPosDevice.profile?.text?.characterRanges).toEqual(ranges)
    expect(normalized.escPosDevice.profile?.text?.characterRanges).not.toBe(ranges)
    expect(validateDesignerOutputSettings({
      ...defaults,
      outputTarget: 'EscPosNative', encoding: 'Device',
      escPosDevice: { profile: { ...defaultEscPosPrinterProfile(), text: { encodingCodePage: 437, characterRanges: ranges } } },
    }).encoding).toBe('Device')
  })

  it('round-trips TSPL and ZPL profiles with defaults and independent nested data', () => {
    const template = createProductSample()
    const tsplProfile = {
      name: 'TSC-203', firmwareVersion: '1.7', printableWidthDots: 384,
      maxLabelHeightDots: 1024, maxImageDataBytes: 4096,
      supportsImages: false, supportsGraphics: false, supportsNativeBarcode: false,
      supportsNativeBarcodeHumanReadable: false,
      supportsNativeQrCode: true, supportsRotation: false,
      deviceFontNames: ['0', '8'], encodingCodePages: [20127, 65001, 936],
      characterRanges: [{ first: 0x20, last: 0x7E }, { first: 0x4E00, last: 0x9FFF }],
    }
    const zplProfile = {
      name: 'ZQ-320', firmwareVersion: '3.2', printableWidthDots: 832,
      maxLabelHeightDots: 1600, maxImageDataBytes: 8192,
      supportsImages: true, supportsGraphics: true, supportsNativeBarcode: false,
      supportsNativeBarcodeHumanReadable: true,
      supportsNativeQrCode: false, supportsRotation: true, supportsCut: false,
      deviceFontNames: ['0', 'A'], encodingCodePages: [20127, 65001],
      characterRanges: [{ first: 0x20, last: 0x7E }],
    }
    const defaults = defaultDesignerOutputSettings()
    const settings = validateDesignerOutputSettings({
      ...defaults,
      outputTarget: 'Tspl',
      tsplMedia: { profile: tsplProfile },
      zplMedia: { profile: zplProfile },
    })

    expect(settings.tsplMedia.profile).toEqual(tsplProfile)
    expect(settings.zplMedia.profile).toEqual(zplProfile)
    expect(settings.tsplMedia.profile?.deviceFontNames).not.toBe(tsplProfile.deviceFontNames)
    expect(settings.tsplMedia.profile?.characterRanges).not.toBe(tsplProfile.characterRanges)
    expect(settings.tsplMedia.profile?.characterRanges?.[0]).not.toBe(tsplProfile.characterRanges[0])

    tsplProfile.deviceFontNames[0] = '1'
    tsplProfile.characterRanges[0].first = 0x30
    expect(settings.tsplMedia.profile?.deviceFontNames?.[0]).toBe('0')
    expect(settings.tsplMedia.profile?.characterRanges?.[0].first).toBe(0x20)

    const serialized = JSON.parse(serializeDesignerProject(template, settings)) as Record<string, unknown>
    expect(serialized.version).toBe(1)
    const parsed = parseDesignerProject(serialized)
    expect(parsed.outputSettings?.tsplMedia.profile).toEqual(settings.tsplMedia.profile)
    expect(parsed.outputSettings?.zplMedia.profile).toEqual(settings.zplMedia.profile)
    expect(parsed.outputSettings?.zplMedia.profile).not.toBe(settings.zplMedia.profile)
    expect(parsed.outputSettings?.zplMedia.profile?.characterRanges).not.toBe(settings.zplMedia.profile?.characterRanges)

    const importedDefault = normalizeLabelPrinterProfile({}, 'Tspl')
    expect(importedDefault).toEqual({ name: 'Custom' })
    expect(parseDesignerProject({
      format: 'Bing.EasyPrint.DesignerProject', version: 1, template,
      outputSettings: { ...defaults, tsplMedia: { profile: {} }, zplMedia: { profile: {} } },
    }).outputSettings?.tsplMedia.profile).toEqual({ name: 'Custom' })
  })

  it('rejects invalid label profile fields and protocol-specific keys', () => {
    const defaults = defaultDesignerOutputSettings()
    const invalid = (tsplMedia: unknown, zplMedia: unknown = defaults.zplMedia) => ({
      ...defaults, tsplMedia, zplMedia,
    })

    expect(() => validateDesignerOutputSettings(invalid({ profile: { unknown: true } }))).toThrow('不支持的字段')
    expect(() => validateDesignerOutputSettings(invalid({ profile: { supportsCut: false } }))).toThrow('不支持的字段')
    expect(() => validateDesignerOutputSettings(invalid({ profile: { name: '  ' } }))).toThrow('非空')
    expect(() => validateDesignerOutputSettings(invalid({ profile: { deviceFontNames: ['9'] } }))).toThrow('字体标识')
    expect(() => validateDesignerOutputSettings(invalid({ profile: { deviceFontNames: ['0', '0'] } }))).toThrow('不能重复')
    expect(() => validateDesignerOutputSettings(invalid({ profile: { encodingCodePages: [] } }))).toThrow('非空')
    expect(() => validateDesignerOutputSettings(invalid({ profile: { encodingCodePages: [1252] } }))).toThrow('代码页')
    expect(() => validateDesignerOutputSettings(invalid({ profile: {
      characterRanges: [{ first: 0x20, last: 0x7E }, { first: 0x7E, last: 0x80 }],
    } }))).toThrow('重叠')
    expect(() => validateDesignerOutputSettings(invalid({ profile: {
      characterRanges: [{ first: 0xD800, last: 0xDFFF }],
    } }))).toThrow('代理项')
    expect(() => validateDesignerOutputSettings({
      ...defaults,
      zplMedia: { profile: { encodingCodePages: [936] } },
    })).toThrow('代码页')
    expect(() => validateDesignerOutputSettings({
      ...defaults,
      zplMedia: { profile: { deviceFontNames: ['a'] } },
    })).toThrow('字体标识')
  })

  it('preserves omitted ESC/POS feed-and-cut profile fields across defaults and project round trips', () => {
    const template = createProductSample()
    const defaults = defaultDesignerOutputSettings()
    const historical = validateDesignerOutputSettings({
      ...defaults, outputTarget: 'EscPosNative',
      escPosDevice: { profile: { ...defaultEscPosPrinterProfile() } },
    })
    expect(defaultEscPosPrinterProfile()).not.toHaveProperty('supportsFeedAndCut')
    expect(historical.escPosDevice.profile).not.toHaveProperty('supportsFeedAndCut')

    const serialized = JSON.parse(serializeDesignerProject(template, historical)) as {
      outputSettings?: { escPosDevice?: { profile?: Record<string, unknown> } }
    }
    expect(serialized.outputSettings?.escPosDevice?.profile).not.toHaveProperty('supportsFeedAndCut')
    const parsed = parseDesignerProject(serialized)
    expect(parsed.outputSettings?.escPosDevice.profile).not.toHaveProperty('supportsFeedAndCut')

    const explicitFalse = validateDesignerOutputSettings({
      ...defaults, outputTarget: 'EscPosNative',
      escPosDevice: { profile: { ...defaultEscPosPrinterProfile(), supportsFeedAndCut: false } },
    })
    expect(explicitFalse.escPosDevice.profile).toHaveProperty('supportsFeedAndCut', false)
    expect(() => validateDesignerOutputSettings({
      ...defaults, outputTarget: 'EscPosNative',
      escPosDevice: { profile: { ...defaultEscPosPrinterProfile(), supportsFeedAndCut: 'false' } },
    })).toThrow('supportsFeedAndCut')
  })

  it('accepts and bounds label profile positive int limits at Int32.MaxValue', () => {
    const defaults = defaultDesignerOutputSettings()
    const maximum = 2147483647
    const fields = ['printableWidthDots', 'maxLabelHeightDots', 'maxImageDataBytes'] as const
    const profile = {
      printableWidthDots: maximum,
      maxLabelHeightDots: maximum,
      maxImageDataBytes: maximum,
    }
    expect(validateDesignerOutputSettings({ ...defaults, tsplMedia: { profile } }).tsplMedia.profile)
      .toMatchObject(profile)
    expect(validateDesignerOutputSettings({ ...defaults, zplMedia: { profile } }).zplMedia.profile)
      .toMatchObject(profile)

    for (const field of fields) {
      expect(() => validateDesignerOutputSettings({
        ...defaults, tsplMedia: { profile: { [field]: maximum + 1 } },
      })).toThrow(field)
      expect(() => validateDesignerOutputSettings({
        ...defaults, zplMedia: { profile: { [field]: maximum + 1 } },
      })).toThrow(field)
    }
  })

  it('validates native QR rotation allowlists for label profiles', () => {
    const normalized = normalizeLabelPrinterProfile({
      supportedNativeQrCodeRotations: ['Rotate90', 'Rotate270'],
    }, 'Tspl')
    expect(normalized?.supportedNativeQrCodeRotations).toEqual(['Rotate90', 'Rotate270'])
    expect(normalizeLabelPrinterProfile({ supportedNativeQrCodeRotations: [] }, 'Tspl')
      ?.supportedNativeQrCodeRotations).toEqual([])
    expect(() => normalizeLabelPrinterProfile({
      supportedNativeQrCodeRotations: ['Rotate90', 'Rotate90'],
    }, 'Zpl')).toThrow('不能重复')
    expect(() => normalizeLabelPrinterProfile({
      supportedNativeQrCodeRotations: ['None'],
    }, 'Tspl')).toThrow('无效')
  })

  it('round-trips and snapshots a TSPL cutter profile while preserving legacy profiles without cutter', () => {
    const template = createProductSample()
    const defaults = defaultDesignerOutputSettings()
    const source: { name: string; cutter: { strategy: 'Immediate'; supportedCutModes: Array<'Full' | 'Partial'>; supportsEndOfJob: true; cutPositionFeedDots: number } } = {
      name: 'TSC-CUT',
      cutter: { strategy: 'Immediate', supportedCutModes: ['Partial'], supportsEndOfJob: true, cutPositionFeedDots: 36 },
    }
    const settings = validateDesignerOutputSettings({ ...defaults, outputTarget: 'Tspl', tsplMedia: { profile: source } })
    expect(settings.tsplMedia.profile?.cutter).toEqual(source.cutter)
    expect(settings.tsplMedia.profile?.cutter).not.toBe(source.cutter)
    expect(settings.tsplMedia.profile?.cutter?.supportedCutModes).not.toBe(source.cutter.supportedCutModes)

    source.cutter.supportedCutModes[0] = 'Full'
    expect(settings.tsplMedia.profile?.cutter?.supportedCutModes).toEqual(['Partial'])

    const parsed = parseDesignerProject(JSON.parse(serializeDesignerProject(template, settings)) as unknown)
    expect(parsed.outputSettings?.tsplMedia.profile?.cutter).toEqual({
      strategy: 'Immediate', supportedCutModes: ['Partial'], supportsEndOfJob: true, cutPositionFeedDots: 36,
    })
    expect(parsed.outputSettings?.tsplMedia.profile?.cutter).not.toBe(settings.tsplMedia.profile?.cutter)
    expect(parsed.outputSettings?.tsplMedia.profile?.cutter?.supportedCutModes)
      .not.toBe(settings.tsplMedia.profile?.cutter?.supportedCutModes)

    const old = validateDesignerOutputSettings({ ...defaults, outputTarget: 'Tspl', tsplMedia: { profile: { name: 'Legacy' } } })
    expect(old.tsplMedia.profile).not.toHaveProperty('cutter')
  })

  it('rejects unsafe TSPL cutter combinations and preserves defaults for omitted optional fields', () => {
    const defaults = defaultDesignerOutputSettings()
    const validDefault = validateDesignerOutputSettings({ ...defaults, outputTarget: 'Tspl', tsplMedia: {
      profile: { cutter: { supportsEndOfJob: true } },
    } })
    expect(validDefault.tsplMedia.profile?.cutter).toEqual({
      strategy: 'Batch', supportedCutModes: ['Full'], supportsEndOfJob: true,
    })

    const invalid = (cutter: unknown) => validateDesignerOutputSettings({ ...defaults, outputTarget: 'Tspl', tsplMedia: { profile: { cutter } } })
    expect(() => invalid({ supportsEndOfJob: false })).toThrow('supportsEndOfJob')
    expect(() => invalid({ supportsEndOfJob: true, supportedCutModes: [] })).toThrow('非空')
    expect(() => invalid({ supportsEndOfJob: true, supportedCutModes: ['Full', 'Full'] })).toThrow('不能重复')
    expect(() => invalid({ strategy: 'Immediate', supportsEndOfJob: true, supportedCutModes: ['Full', 'Partial'] })).toThrow('一种固定切法')
    expect(() => invalid({ strategy: 'Batch', supportsEndOfJob: true, cutPositionFeedDots: 1 })).toThrow('Batch')
    expect(() => invalid({ strategy: 'Immediate', supportsEndOfJob: true, cutPositionFeedDots: -1 })).toThrow('cutPositionFeedDots')
    expect(() => invalid({ strategy: 'Immediate', supportsEndOfJob: true, cutPositionFeedDots: 10000 })).toThrow('cutPositionFeedDots')
    expect(() => invalid({ supportsEndOfJob: true, unknown: true })).toThrow('不支持的字段')
  })

  it('round-trips fixed Raster fonts with independent resource bytes and references', () => {
    const defaults = defaultDesignerOutputSettings()
    const settings = validateDesignerOutputSettings({
      ...defaults,
      outputTarget: 'EscPosRaster',
      escPosDevice: {
        rasterFonts: {
          defaultFont: 'Regular', fallbackFonts: ['Fallback'],
          fonts: [
            { name: 'Regular', version: '1.0', regularData: 'dGVzdA==', boldData: 'Ym9sZA==' },
            { name: 'Fallback', version: '2.0', regularData: 'ZmFsbA==' },
          ],
        },
      },
    })
    const rasterFonts = settings.escPosDevice.rasterFonts!
    expect(rasterFonts.defaultFont).toBe('Regular')
    expect(rasterFonts.fallbackFonts).toEqual(['Fallback'])
    expect(rasterFonts.fonts[0].regularData).toBe('dGVzdA==')
    expect(rasterFonts.fonts[0].boldData).toBe('Ym9sZA==')

    const source = {
      defaultFont: 'Regular', fallbackFonts: ['Fallback'],
      fonts: [
        { name: 'Regular', version: '1.0', regularData: 'dGVzdA==', boldData: 'Ym9sZA==' },
        { name: 'Fallback', version: '2.0', regularData: 'ZmFsbA==' },
      ],
    }
    const normalized = validateDesignerOutputSettings({ ...defaults, outputTarget: 'EscPosRaster', escPosDevice: { rasterFonts: source } })
    expect(normalized.escPosDevice.rasterFonts).not.toBe(source)
    expect(normalized.escPosDevice.rasterFonts?.fonts).not.toBe(source.fonts)
    expect(normalized.escPosDevice.rasterFonts?.fonts[0]).not.toBe(source.fonts[0])
    source.fonts[0].regularData = 'changed'
    source.fallbackFonts[0] = 'changed'
    expect(normalized.escPosDevice.rasterFonts?.fonts[0].regularData).toBe('dGVzdA==')
    expect(normalized.escPosDevice.rasterFonts?.fallbackFonts?.[0]).toBe('Fallback')
    const parsed = parseDesignerProject(JSON.parse(serializeDesignerProject(createProductSample(), settings)) as unknown)
    expect(parsed.outputSettings?.escPosDevice.rasterFonts).toEqual(settings.escPosDevice.rasterFonts)
  })

  it('rejects fixed Raster fonts outside Raster and invalid resources', () => {
    const defaults = defaultDesignerOutputSettings()
    const valid = {
      defaultFont: 'Regular', fallbackFonts: [],
      fonts: [{ name: 'Regular', version: '1.0', regularData: 'dGVzdA==' }],
    }
    expect(() => validateDesignerOutputSettings({ ...defaults, outputTarget: 'EscPosNative', escPosDevice: { rasterFonts: valid } })).toThrow('仅适用于 ESC/POS Raster')
    expect(() => validateDesignerOutputSettings({ ...defaults, outputTarget: 'EscPosRaster', escPosDevice: { rasterFonts: { ...valid, defaultFont: 'Missing' } } })).toThrow('defaultFont')
    expect(() => validateDesignerOutputSettings({ ...defaults, outputTarget: 'EscPosRaster', escPosDevice: { rasterFonts: { ...valid, fallbackFonts: ['Missing'] } } })).toThrow('fallbackFonts')
    expect(() => validateDesignerOutputSettings({ ...defaults, outputTarget: 'EscPosRaster', escPosDevice: { rasterFonts: { ...valid, fonts: [{ ...valid.fonts[0], name: 'Regular' }, { ...valid.fonts[0], name: 'Regular' }] } } })).toThrow('不能重复')
    expect(() => validateDesignerOutputSettings({ ...defaults, outputTarget: 'EscPosRaster', escPosDevice: { rasterFonts: { ...valid, fonts: [{ ...valid.fonts[0], regularData: 'invalid' }] } } })).toThrow('Base64')
    const tooMany = Array.from({ length: 65 }, (_, index) => ({ name: `Font${index}`, version: '1.0', regularData: 'dA==' }))
    expect(() => validateDesignerOutputSettings({ ...defaults, outputTarget: 'EscPosRaster', escPosDevice: { rasterFonts: { defaultFont: 'Font0', fonts: tooMany } } })).toThrow('64')
    const oversized = 'A'.repeat(4 * Math.ceil((32 * 1024 * 1024 + 1) / 3))
    expect(() => validateDesignerOutputSettings({ ...defaults, outputTarget: 'EscPosRaster', escPosDevice: { rasterFonts: {
      defaultFont: 'Regular', fonts: [{ name: 'Regular', version: '1.0', regularData: oversized }],
    } } })).toThrow('32 MiB')
  })

  it('round-trips the ESC/POS Raster barcode human-readable font size and validates its scope', () => {
    const defaults = defaultDesignerOutputSettings()
    const settings = validateDesignerOutputSettings({
      ...defaults,
      outputTarget: 'EscPosRaster',
      escPosDevice: { rasterBarcodeHumanReadableFontSize: 16 },
    })
    expect(settings.escPosDevice.rasterBarcodeHumanReadableFontSize).toBe(16)
    expect(parseDesignerProject(JSON.parse(serializeDesignerProject(createProductSample(), settings))).outputSettings?.escPosDevice)
      .toMatchObject({ rasterBarcodeHumanReadableFontSize: 16 })
    expect(() => validateDesignerOutputSettings({ ...defaults, outputTarget: 'EscPosRaster', escPosDevice: {
      rasterBarcodeHumanReadableFontSize: 0,
    } })).toThrow('rasterBarcodeHumanReadableFontSize')
    expect(() => validateDesignerOutputSettings({ ...defaults, outputTarget: 'EscPosNative', escPosDevice: {
      rasterBarcodeHumanReadableFontSize: 16,
    } })).toThrow('仅适用于 ESC/POS Raster')
  })

  it('round-trips normalized task settings and rejects legacy conflicts or unknown job fields', () => {
    const defaults = defaultDesignerOutputSettings()
    const settings = validateDesignerOutputSettings({
      ...defaults,
      job: { copies: 3, copyOrder: 'Uncollated', feedScope: 'EachCopy', feedUnit: 'MotionUnits', feedAmount: 4, cutScope: 'JobEnd' },
    })
    expect(settings.job).toEqual({
      copies: 3, copyOrder: 'Uncollated', feedScope: 'EachCopy', feedUnit: 'MotionUnits', feedAmount: 4, cutScope: 'JobEnd',
    })
    expect(parseDesignerProject(JSON.parse(serializeDesignerProject(createProductSample(), settings))).outputSettings?.job)
      .toEqual(settings.job)
    expect(() => validateDesignerOutputSettings({ ...defaults, job: {}, copies: 2 })).toThrow('旧 copies')
    expect(() => validateDesignerOutputSettings({ ...defaults, job: {}, feedLines: 1 })).toThrow('旧 feedLines')
    expect(() => validateDesignerOutputSettings({ ...defaults, job: {}, cutAfterPrint: true })).toThrow('旧 cutAfterPrint')
    expect(() => validateDesignerOutputSettings({ ...defaults, job: { feedUnit: 'Pixels' } })).toThrow('feedUnit')
    expect(() => validateDesignerOutputSettings({ ...defaults, job: { unknown: true } })).toThrow('不支持的字段')
  })

  it('normalizes and deep-copies page and job cut options while preserving old jobs without the field', () => {
    const template = createProductSample()
    const defaults = defaultDesignerOutputSettings()
    const pageSource = { mode: 'Partial' as const, feedToCutPosition: true, feedBeforeCutAmount: 7, feedBeforeCutUnit: 'Dots' as const }
    const pageSettings = validateDesignerOutputSettings({ ...defaults, outputTarget: 'EscPosNative', cutAfterPrint: true, cutOptions: pageSource })
    expect(pageSettings.cutOptions).toEqual(pageSource)
    expect(pageSettings.cutOptions).not.toBe(pageSource)

    const jobSource = { copies: 2, cutScope: 'JobEnd' as const,
      cutOptions: { mode: 'Full' as const, feedToCutPosition: false, feedBeforeCutAmount: 0, feedBeforeCutUnit: 'MotionUnits' as const } }
    const jobSettings = validateDesignerOutputSettings({ ...defaults, outputTarget: 'EscPosNative', job: jobSource })
    expect(jobSettings.job?.cutOptions).toEqual(jobSource.cutOptions)
    expect(jobSettings.job?.cutOptions).not.toBe(jobSource.cutOptions)
    const parsed = parseDesignerProject(JSON.parse(serializeDesignerProject(template, jobSettings)) as unknown)
    expect(parsed.outputSettings?.job?.cutOptions).toEqual(jobSettings.job?.cutOptions)
    expect(parsed.outputSettings?.job?.cutOptions).not.toBe(jobSettings.job?.cutOptions)

    const oldJob = validateDesignerOutputSettings({ ...defaults, job: { cutScope: 'JobEnd' } }).job!
    expect(Object.prototype.hasOwnProperty.call(oldJob, 'cutOptions')).toBe(false)
  })

  it('rejects invalid cut option combinations and profile mode declarations', () => {
    const defaults = defaultDesignerOutputSettings()
    expect(() => validateDesignerOutputSettings({ ...defaults, cutAfterPrint: false,
      cutOptions: { mode: 'Full' } })).toThrow('cutAfterPrint')
    expect(() => validateDesignerOutputSettings({ ...defaults, job: { cutScope: 'None', cutOptions: { mode: 'Full' } } }))
      .toThrow('cutScope')
    expect(() => validateDesignerOutputSettings({ ...defaults, cutOptions: { feedBeforeCutAmount: 1 } }))
      .toThrow('feedToCutPosition')
    expect(() => validateDesignerOutputSettings({ ...defaults, cutOptions: { feedBeforeCutAmount: 2147483648, feedToCutPosition: true } }))
      .toThrow('feedBeforeCutAmount')
    expect(() => validateDesignerOutputSettings({ ...defaults, escPosDevice: { profile: {
      ...defaultEscPosPrinterProfile(), supportedCutModes: ['Full', 'Full'],
    } } })).toThrow('不能重复')
    expect(() => validateDesignerOutputSettings({ ...defaults, escPosDevice: { profile: {
      ...defaultEscPosPrinterProfile(), supportedCutModes: ['Diagonal'],
    } } })).toThrow('supportedCutModes')
    const profileSettings = validateDesignerOutputSettings({ ...defaults, escPosDevice: { profile: {
      ...defaultEscPosPrinterProfile(), supportedCutModes: [], supportsFeedAndCut: true,
    } } })
    expect(profileSettings.escPosDevice.profile?.supportedCutModes).toEqual([])
    expect(profileSettings.escPosDevice.profile?.supportsFeedAndCut).toBe(true)
  })

  it('round-trips and validates ESC/POS Native HRI position allowlists', () => {
    const defaults = defaultDesignerOutputSettings()
    const positions = ['Below', 'Both'] as const
    const source = { ...defaultEscPosPrinterProfile(), supportedNativeBarcodeHumanReadablePositions: [...positions] }
    const settings = validateDesignerOutputSettings({ ...defaults, outputTarget: 'EscPosNative',
      escPosDevice: { profile: source } })
    expect(settings.escPosDevice.profile?.supportedNativeBarcodeHumanReadablePositions).toEqual(positions)
    expect(settings.escPosDevice.profile?.supportedNativeBarcodeHumanReadablePositions).not.toBe(source.supportedNativeBarcodeHumanReadablePositions)
    const parsed = parseDesignerProject(JSON.parse(serializeDesignerProject(createProductSample(), settings)) as unknown)
    expect(parsed.outputSettings?.escPosDevice.profile?.supportedNativeBarcodeHumanReadablePositions).toEqual(positions)
    expect(() => validateDesignerOutputSettings({ ...defaults, outputTarget: 'EscPosNative',
      escPosDevice: { profile: { ...source, supportedNativeBarcodeHumanReadablePositions: ['Below', 'Below'] } } })).toThrow('不能重复')
    expect(() => validateDesignerOutputSettings({ ...defaults, outputTarget: 'EscPosNative',
      escPosDevice: { profile: { ...source, supportedNativeBarcodeHumanReadablePositions: ['None'] } } })).toThrow('HRI 位置')
  })

  it('rejects positive task feed without an execution scope for validation, save, and import', () => {
    const template = createProductSample()
    const defaults = defaultDesignerOutputSettings()
    const invalidSettings = { ...defaults, job: { feedAmount: 1 } }

    expect(() => validateDesignerOutputSettings(invalidSettings)).toThrow('outputSettings.job.feedScope')
    expect(() => serializeDesignerProject(template, invalidSettings)).toThrow('进纸数量为正数时必须指定进纸范围')
    expect(() => parseDesignerProject({
      format: 'Bing.EasyPrint.DesignerProject', version: 1, template, outputSettings: invalidSettings,
    })).toThrow('进纸数量为正数时必须指定进纸范围')
  })
})
