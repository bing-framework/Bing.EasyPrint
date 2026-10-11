import { afterEach, describe, expect, it, vi } from 'vitest'
import { defaultEscPosPrinterProfile, DesignerValidationError, loadDesignerCapabilities, previewTemplate, renderTemplate } from './api'
import { createBlankSample } from './samples'

afterEach(() => vi.unstubAllGlobals())

function validContractHeaders(contentType = 'application/json'): Record<string, string> {
  return {
    'Content-Type': contentType,
    'X-EasyPrint-Contract-Version': '1',
    'X-EasyPrint-Required-Features': 'structured-errors,strict-options,binding-diagnostics',
    'X-EasyPrint-Features': 'structured-errors,strict-options,binding-diagnostics,multi-protocol,ordered-output-segments',
  }
}

function validLabelCapabilitiesResponse(overrides: Record<string, unknown> = {}): Record<string, unknown> {
  return {
    protocol: 'Tspl', escPosLayoutMode: null, nativeFlow: null, jobFeedUnits: [], dpi: 203,
    elementTypes: [1], fontKinds: [0], deviceFonts: [], textSizes: null,
    textSize: { minimum: 24, maximum: 24 }, textRotations: [0], barcodeTypes: [0],
    barcodeRotations: [0], qrModuleSize: null, barcodeModuleWidth: null, barcodeHeight: null,
    supportsExplicitBarcodeModuleWidth: false, supportsWideModuleWidth: false,
    supportsBoldText: false, supportsUnderlineText: false, supportsRotatedUnderlineText: false,
    supportsHostFontFamily: false, supportsLegacyDeviceFont: false, supportsBarcodeHumanReadable: false,
    supportsCut: true, supportsExtraFeed: true, supportsFilledRectangles: true,
    supportsCoordinates: true, supportsExplicitQuietZone: false,
    ...overrides,
  }
}

function validEscPosCapabilitiesResponse(overrides: Record<string, unknown> = {}): Record<string, unknown> {
  return validLabelCapabilitiesResponse({
    protocol: 'EscPos', escPosLayoutMode: 'Native', nativeFlow: 'Rows',
    jobFeedUnits: ['Lines', 'MotionUnits'],
    cutModes: ['Full', 'Partial'], cutFeedUnits: ['MotionUnits', 'Dots'],
    cutFeedMotionUnits: { minimum: 0, maximum: 255 },
    ...overrides,
  })
}

function validTsplCutterCapabilitiesResponse(overrides: Record<string, unknown> = {}): Record<string, unknown> {
  return validLabelCapabilitiesResponse({
    labelProfile: {
      name: 'TSC-CUT', firmwareVersion: null, printableWidthDots: 384,
      maxLabelHeightDots: null, maxImageDataBytes: null,
      supportsNativeBarcodeHumanReadable: true,
      encodingCodePages: [20127, 65001, 936], characterRanges: null,
    },
    supportsCut: true,
    cutModes: ['Full'], cutFeedUnits: [], cutFeedMotionUnits: null,
    tsplCutter: { strategy: 'Batch', supportedCutModes: ['Full'], supportsEndOfJob: true, cutPositionFeedDots: null },
    cutFeedDots: null,
    ...overrides,
  })
}

describe('designer preview errors', () => {
  it('sends only output configuration to the capabilities endpoint', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
      protocol: 'EscPos', escPosLayoutMode: 'Native', nativeFlow: 'Rows', jobFeedUnits: ['Lines', 'MotionUnits'], dpi: 203,
      elementTypes: [1, 3, 4, 6], fontKinds: [0], deviceFonts: [], textSizes: [24],
      textSize: { minimum: 24, maximum: 24 }, textRotations: [0], barcodeTypes: [0],
      barcodeRotations: [0], qrModuleSize: null, barcodeModuleWidth: { minimum: 2, maximum: 6 },
      barcodeHeight: { minimum: 1, maximum: 255 }, supportsExplicitBarcodeModuleWidth: false,
      supportsWideModuleWidth: false, supportsBoldText: false, supportsUnderlineText: true,
      supportsRotatedUnderlineText: false, supportsHostFontFamily: false, supportsLegacyDeviceFont: false,
      supportsBarcodeHumanReadable: true, supportsCut: false, supportsExtraFeed: true,
      supportsFilledRectangles: false, supportsCoordinates: true, supportsExplicitQuietZone: false,
    }), { headers: validContractHeaders() }))
    vi.stubGlobal('fetch', fetchMock)

    const capabilities = await loadDesignerCapabilities({
      protocol: 'EscPos', escPosLayoutMode: 'Native', encoding: 'Gbk', allowRasterizedLines: false,
      escPosDevice: { dpi: 180, profile: { name: 'impact', imageCommand: 'BitImageEscStar', bitImageMode: 'DoubleDensity8',
        horizontalDpi: 180, verticalDpi: 60, motionUnitsPerInch: 144, supportsCut: false,
        supportsNativeBarcode: false, supportsNativeQrCode: false, supportsPrintAreaWidth: false,
        supportsTextSize: false, unidirectionalBitImage: true } },
      tsplMedia: {}, zplMedia: {},
    })
    expect(capabilities.supportsExplicitQuietZone).toBe(false)
    expect(capabilities.nativeFlow).toBe('Rows')

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(url).toBe('/api/designer/capabilities')
    expect(init.method).toBe('POST')
    expect(init.headers).toMatchObject({
      'X-EasyPrint-Contract-Version': '1',
      'X-EasyPrint-Required-Features': 'structured-errors,strict-options,binding-diagnostics',
    })
    expect(init.headers).not.toHaveProperty('X-EasyPrint-Features')
    const body = JSON.parse(init.body as string) as Record<string, unknown>
    expect(body).toMatchObject({ protocol: 'EscPos', escPosLayoutMode: 'Native', encoding: 'Gbk', allowRasterizedLines: false })
    expect(body).toHaveProperty('escPosDevice')
    expect(body).not.toHaveProperty('template')
    expect(body).not.toHaveProperty('data')
    expect(body).not.toHaveProperty('resources')
    expect(body).not.toHaveProperty('tsplMedia')
    expect(body).not.toHaveProperty('zplMedia')
  })

  it.each([
    ['capabilities', () => loadDesignerCapabilities({})],
    ['preview', () => previewTemplate(createBlankSample(), {}, [])],
    ['render', () => renderTemplate(createBlankSample(), {}, [])],
  ] as const)('rejects a successful %s response without a matching contract version', async (_operation, request) => {
    const headers = validContractHeaders()
    delete headers['X-EasyPrint-Contract-Version']
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('{}', { headers })))

    const error = await request().catch((value: unknown) => value)
    expect(error).toBeInstanceOf(DesignerValidationError)
    expect((error as DesignerValidationError).diagnostics[0]).toMatchObject({
      code: 'ContractVersionMismatch', phase: 'Contract',
    })
    expect((error as Error).message).toContain('请更新 Host')
  })

  it('rejects a successful POST response with a wrong contract version', async () => {
    const headers = validContractHeaders()
    headers['X-EasyPrint-Contract-Version'] = '2'
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('{}', { headers })))

    const error = await loadDesignerCapabilities({}).catch((value: unknown) => value)
    expect(error).toBeInstanceOf(DesignerValidationError)
    expect((error as DesignerValidationError).diagnostics[0]).toMatchObject({
      code: 'ContractVersionMismatch', phase: 'Contract',
    })
  })

  it('rejects a successful POST response missing a required contract feature', async () => {
    const headers = validContractHeaders()
    headers['X-EasyPrint-Features'] = 'structured-errors,strict-options'
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('{}', { headers })))

    const error = await loadDesignerCapabilities({}).catch((value: unknown) => value)
    expect(error).toBeInstanceOf(DesignerValidationError)
    expect((error as DesignerValidationError).diagnostics[0]).toMatchObject({
      code: 'ContractFeatureUnavailable', phase: 'Contract',
    })
    expect((error as Error).message).toContain('请更新 Host')
  })

  it('maps a network TypeError to a transport diagnostic', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))

    const error = await previewTemplate(createBlankSample(), {}, []).catch((value: unknown) => value)
    expect(error).toBeInstanceOf(DesignerValidationError)
    expect((error as DesignerValidationError).diagnostics[0]).toMatchObject({
      code: 'ServiceUnavailable', phase: 'Transport',
    })
    expect((error as Error).message).toContain('无法连接打印服务')
  })

  it('passes AbortError through without wrapping it', async () => {
    const abortError = new DOMException('The operation was aborted', 'AbortError')
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(abortError))

    await expect(renderTemplate(createBlankSample(), {}, [])).rejects.toBe(abortError)
  })

  it('rejects a label profile request when the Host omits label profile capabilities', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(validLabelCapabilitiesResponse()), {
      headers: validContractHeaders(),
    })))

    await expect(loadDesignerCapabilities({ protocol: 'Tspl', tsplMedia: { profile: {} } }))
      .rejects.toThrow('能力服务未返回标签机型配置，请更新 Host')
  })

  it('accepts label profile capabilities for a profile request', async () => {
    const labelProfile = {
      name: 'TSC-203', firmwareVersion: null, printableWidthDots: 384,
      maxLabelHeightDots: null, maxImageDataBytes: 4096,
      supportsNativeBarcodeHumanReadable: false,
      encodingCodePages: [20127, 65001, 936], characterRanges: null,
    }
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(validLabelCapabilitiesResponse({ labelProfile })), {
      headers: validContractHeaders(),
    })))

    const capabilities = await loadDesignerCapabilities({ protocol: 'Tspl', tsplMedia: { profile: {} } })
    expect(capabilities.labelProfile).toEqual(labelProfile)
  })

  it('requires the new cut capability fields without sending a client-only marker to an old Host', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(validLabelCapabilitiesResponse()), {
      headers: validContractHeaders(),
    }))
    vi.stubGlobal('fetch', fetchMock)

    await expect(loadDesignerCapabilities({ protocol: 'EscPos', requireCutContract: true }))
      .rejects.toThrow('ESC/POS 裁切能力字段')
    const body = JSON.parse(fetchMock.mock.calls[0][1].body as string) as Record<string, unknown>
    expect(body).not.toHaveProperty('requireCutContract')
  })

  it('guards TSPL cutter preview and render against an old Host, then forwards the new contract', async () => {
    const profile = { name: 'TSC-CUT', cutter: { supportsEndOfJob: true } }
    const oldFetch = vi.fn().mockImplementation(() => Promise.resolve(new Response(JSON.stringify(validLabelCapabilitiesResponse()), {
      headers: validContractHeaders(),
    })))
    vi.stubGlobal('fetch', oldFetch)
    const template = createBlankSample()
    const previewArgs: Parameters<typeof previewTemplate> = [
      template, {}, [], undefined, 'Tspl', 'Native', false,
      undefined, undefined, undefined, undefined, undefined, undefined,
      { profile }, undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined,
    ]
    const renderArgs: Parameters<typeof renderTemplate> = [
      template, {}, [], 'Tspl', 'Native', false,
      undefined, undefined, undefined, undefined, undefined, undefined,
      { profile }, undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined,
    ]
    await expect(previewTemplate(...previewArgs)).rejects.toThrow('TSPL 裁切能力字段')
    await expect(renderTemplate(...renderArgs)).rejects.toThrow('TSPL 裁切能力字段')
    expect(oldFetch).toHaveBeenCalledTimes(2)
    expect(oldFetch.mock.calls.every(([url]) => url === '/api/designer/capabilities')).toBe(true)

    const newFetch = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify(validTsplCutterCapabilitiesResponse()), {
        headers: validContractHeaders(),
      }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ pages: [] }), {
        headers: validContractHeaders(),
      }))
      .mockResolvedValueOnce(new Response(JSON.stringify(validTsplCutterCapabilitiesResponse()), {
        headers: validContractHeaders(),
      }))
      .mockResolvedValueOnce(new Response(new Blob(['TSPL']), {
        headers: validContractHeaders('application/zip'),
      }))
    vi.stubGlobal('fetch', newFetch)
    await previewTemplate(...previewArgs)
    await renderTemplate(...renderArgs)
    expect(newFetch.mock.calls.map(([url]) => url)).toEqual([
      '/api/designer/capabilities', '/api/designer/preview', '/api/designer/capabilities', '/api/designer/render',
    ])
    const previewBody = JSON.parse(newFetch.mock.calls[1][1].body as string) as Record<string, unknown>
    const renderBody = JSON.parse(newFetch.mock.calls[3][1].body as string) as Record<string, unknown>
    expect(previewBody).toMatchObject({ protocol: 'Tspl', tsplMedia: { profile } })
    expect(renderBody).toMatchObject({ protocol: 'Tspl', tsplMedia: { profile } })
    expect(previewBody).not.toHaveProperty('cutOptions')
    expect(renderBody).not.toHaveProperty('cutOptions')
  })

  it('rejects a TSPL capability response whose cutter strategy or position differs from the request', async () => {
    const profile = {
      name: 'TSC-CUT',
      cutter: { strategy: 'Immediate' as const, supportedCutModes: ['Partial' as const], supportsEndOfJob: true, cutPositionFeedDots: 12 },
    }
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(validTsplCutterCapabilitiesResponse({
      tsplCutter: { strategy: 'Batch', supportedCutModes: ['Full'], supportsEndOfJob: true, cutPositionFeedDots: null },
      cutModes: ['Full'], cutFeedUnits: [], cutFeedDots: null,
    })), { headers: validContractHeaders() }))
    vi.stubGlobal('fetch', fetchMock)
    const args: Parameters<typeof previewTemplate> = [
      createBlankSample(), {}, [], undefined, 'Tspl', 'Native', false,
      undefined, undefined, undefined, undefined, undefined, undefined,
      { profile }, undefined, undefined, undefined, undefined, undefined, undefined, undefined,
    ]
    await expect(previewTemplate(...args)).rejects.toThrow('TSPL cutter 与请求配置不一致')
    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(fetchMock.mock.calls[0][0]).toBe('/api/designer/capabilities')
  })

  it('rejects direct preview and render cut requests before an old Host receives output', async () => {
    const oldHost = validEscPosCapabilitiesResponse()
    delete oldHost.cutModes
    delete oldHost.cutFeedUnits
    delete oldHost.cutFeedMotionUnits
    const fetchMock = vi.fn().mockImplementation(() => Promise.resolve(new Response(JSON.stringify(oldHost), {
      headers: validContractHeaders(),
    })))
    vi.stubGlobal('fetch', fetchMock)

    const template = createBlankSample()
    const rootCutOptions = { mode: 'Partial' as const }
    const signal = new AbortController().signal
    const previewArgs: Parameters<typeof previewTemplate> = [
      template, {}, [], signal, 'EscPos', 'Native', false,
      undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined,
      undefined, undefined, undefined, undefined, undefined, undefined, rootCutOptions,
    ]
    const renderArgs: Parameters<typeof renderTemplate> = [
      template, {}, [], 'EscPos', 'Native', false,
      undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined,
      undefined, undefined, undefined, undefined, undefined, undefined, rootCutOptions,
    ]
    await expect(previewTemplate(...previewArgs)).rejects.toThrow('ESC/POS 裁切能力字段')
    await expect(renderTemplate(...renderArgs)).rejects.toThrow('ESC/POS 裁切能力字段')

    const profileArgs: Parameters<typeof previewTemplate> = [...previewArgs]
    profileArgs[16] = { profile: { ...defaultEscPosPrinterProfile(), supportsFeedAndCut: false } }
    profileArgs[21] = undefined
    await expect(previewTemplate(...profileArgs)).rejects.toThrow('ESC/POS 裁切能力字段')

    expect(fetchMock).toHaveBeenCalledTimes(3)
    expect((fetchMock.mock.calls[0][1] as RequestInit).signal).toBe(signal)
    for (const [url, init] of fetchMock.mock.calls as [string, RequestInit][]) {
      expect(url).toBe('/api/designer/capabilities')
      expect(JSON.parse(init.body as string)).not.toHaveProperty('requireCutContract')
    }
  })

  it('forwards fixed Raster font resources without dropping their Base64 bytes', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ pages: [] }), { headers: validContractHeaders() }))
      .mockResolvedValueOnce(new Response(new Blob(['ZIP']), { headers: validContractHeaders('application/zip') }))
    vi.stubGlobal('fetch', fetchMock)
    const device = {
      rasterFonts: {
        defaultFont: 'Regular', fallbackFonts: ['Fallback'],
        fonts: [
          { name: 'Regular', version: '1.0', regularData: 'dGVzdA==', boldData: 'Ym9sZA==' },
          { name: 'Fallback', version: '2.0', regularData: 'ZmFsbA==' },
        ],
      },
    }
    const template = createBlankSample()
    await previewTemplate(template, {}, [], undefined, 'EscPos', 'Raster', false,
      undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined, device)
    await renderTemplate(template, {}, [], 'EscPos', 'Raster', false,
      undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined, device)
    const previewBody = JSON.parse(fetchMock.mock.calls[0][1].body as string) as Record<string, unknown>
    const renderBody = JSON.parse(fetchMock.mock.calls[1][1].body as string) as Record<string, unknown>
    expect(previewBody).toMatchObject({ escPosDevice: device })
    expect(renderBody).toMatchObject({ escPosDevice: device })
  })

  it('preserves the server node path and reason for an invalid layout', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      errors: [{ sourcePath: 'Rows[0].Cells[0].Elements[0]', message: '元素超出单元格。' }],
    }), { status: 400, headers: validContractHeaders() })))

    const preview = previewTemplate(createBlankSample(), {}, [])
    await expect(preview).rejects.toThrow(DesignerValidationError)
    await expect(preview).rejects.toThrow('Rows[0].Cells[0].Elements[0]: 元素超出单元格。')
  })

  it('preserves the same repeated component diagnostics in preview and export failures', async () => {
    const diagnostic = {
      sourcePath: 'Rows[0].Cells[0].Elements[1].Elements[0]',
      definitionPath: 'Components[0].Elements[0]',
      componentInstancePath: 'Rows[0].Cells[0].Elements[1]',
      instanceIndices: [2, 4], pageIndex: 2, elementIndex: 0, message: '文本包含非法字符。',
    }
    vi.stubGlobal('fetch', vi.fn().mockImplementation(() => Promise.resolve(new Response(JSON.stringify({
      errors: [diagnostic],
    }), { status: 400, headers: validContractHeaders() }))))

    for (const request of [previewTemplate(createBlankSample(), {}, []), renderTemplate(createBlankSample(), {}, [])]) {
      const error = await request.catch((value: unknown) => value)
      expect(error).toBeInstanceOf(DesignerValidationError)
      expect((error as DesignerValidationError).diagnostics).toEqual([diagnostic])
      expect((error as DesignerValidationError).status).toBe(400)
      expect((error as Error).message).toContain(diagnostic.sourcePath)
      expect((error as Error).message).toContain(diagnostic.definitionPath)
    }
  })

  it('keeps structured diagnostics returned with a successful preview response', async () => {
    const diagnostic = { sourcePath: 'Components[1].Elements[0]', definitionPath: 'Components[1].Elements[0]',
      componentInstancePath: null, instanceIndices: null, pageIndex: null, elementIndex: null, message: '字体不支持。' }
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ pages: [], errors: [diagnostic] }), {
      headers: validContractHeaders(),
    })))
    expect((await previewTemplate(createBlankSample(), {}, [])).errors).toEqual([diagnostic])
  })

  it('reports an HTTP failure without inventing a node from an unstructured message', async () => {
    vi.stubGlobal('fetch', vi.fn().mockImplementation(() => Promise.resolve(new Response('Unavailable', { status: 503 }))))
    const error = await renderTemplate(createBlankSample(), {}, []).catch((value: unknown) => value)
    expect(error).toBeInstanceOf(DesignerValidationError)
    expect((error as DesignerValidationError).diagnostics).toEqual([])
    expect((error as Error).message).toBe('服务返回 HTTP 503')
  })

  it('forwards image and device font calibration to preview and render requests', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ pages: [] }), {
        headers: validContractHeaders(),
      }))
      .mockResolvedValueOnce(new Response(new Blob(['CPCL']), {
        headers: validContractHeaders('application/zip'),
      }))
    vi.stubGlobal('fetch', fetchMock)

    const template = createBlankSample()
    await previewTemplate(template, {}, [], undefined, 'Tspl', 'Native', true, 96, 17.5, 35, 2, 5, false)
    await renderTemplate(template, {}, [], 'Tspl', 'Native', true, 96, 17.5, 35, 2, 5, false)

    const previewBody = JSON.parse(fetchMock.mock.calls[0][1].body as string)
    const renderBody = JSON.parse(fetchMock.mock.calls[1][1].body as string)
    expect(previewBody).toMatchObject({ imageThreshold: 96, deviceHalfCellWidthAt24: 17.5, deviceFullCellWidthAt24: 35, copies: 2, feedLines: 5, cutAfterPrint: false })
    expect(renderBody).toMatchObject({ imageThreshold: 96, deviceHalfCellWidthAt24: 17.5, deviceFullCellWidthAt24: 35, copies: 2, feedLines: 5, cutAfterPrint: false })
  })

  it('forwards native bounds calibration to preview and render requests', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ pages: [] }), {
        headers: validContractHeaders(),
      }))
      .mockResolvedValueOnce(new Response(new Blob(['ZPL']), {
        headers: validContractHeaders('application/zip'),
      }))
    vi.stubGlobal('fetch', fetchMock)

    const template = createBlankSample()
    await previewTemplate(template, {}, [], undefined, 'Zpl', 'Native', false,
      undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined,
      undefined, undefined, 28.5, true)
    await renderTemplate(template, {}, [], 'Zpl', 'Native', false,
      undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined,
      undefined, undefined, 28.5, true)

    const previewBody = JSON.parse(fetchMock.mock.calls[0][1].body as string) as Record<string, unknown>
    const renderBody = JSON.parse(fetchMock.mock.calls[1][1].body as string) as Record<string, unknown>
    expect(previewBody).toMatchObject({ deviceLineHeightAt24: 28.5, requireDeclaredNativeBounds: true })
    expect(renderBody).toMatchObject({ deviceLineHeightAt24: 28.5, requireDeclaredNativeBounds: true })
  })

  it('forwards per-device-font calibration to preview and render requests', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ pages: [] }), {
        headers: validContractHeaders(),
      }))
      .mockResolvedValueOnce(new Response(new Blob(['TSPL']), {
        headers: validContractHeaders('application/zip'),
      }))
    vi.stubGlobal('fetch', fetchMock)

    const template = createBlankSample()
    const calibrations = {
      '1': { halfCellWidthAt24: 16.5, fullCellWidthAt24: 33, lineHeightAt24: 48 },
    }
    await previewTemplate(template, {}, [], undefined, 'Tspl', 'Native', false,
      undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined,
      undefined, undefined, undefined, undefined, calibrations)
    await renderTemplate(template, {}, [], 'Tspl', 'Native', false,
      undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined,
      undefined, undefined, undefined, undefined, calibrations)

    const previewBody = JSON.parse(fetchMock.mock.calls[0][1].body as string) as Record<string, unknown>
    const renderBody = JSON.parse(fetchMock.mock.calls[1][1].body as string) as Record<string, unknown>
    expect(previewBody.deviceFontCalibrations).toEqual(calibrations)
    expect(renderBody.deviceFontCalibrations).toEqual(calibrations)
  })

  it('omits page settings when they use protocol defaults', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ pages: [] }), {
        headers: validContractHeaders(),
      }))
      .mockResolvedValueOnce(new Response(new Blob(['CPCL']), {
        headers: validContractHeaders('application/zip'),
      }))
    vi.stubGlobal('fetch', fetchMock)

    const template = createBlankSample()
    await previewTemplate(template, {}, [])
    await renderTemplate(template, {}, [])

    const previewBody = JSON.parse(fetchMock.mock.calls[0][1].body as string) as Record<string, unknown>
    const renderBody = JSON.parse(fetchMock.mock.calls[1][1].body as string) as Record<string, unknown>
    expect(previewBody).not.toHaveProperty('copies')
    expect(previewBody).not.toHaveProperty('feedLines')
    expect(previewBody).not.toHaveProperty('cutAfterPrint')
    expect(renderBody).not.toHaveProperty('copies')
    expect(renderBody).not.toHaveProperty('feedLines')
    expect(renderBody).not.toHaveProperty('cutAfterPrint')
  })

  it('forwards protocol media settings and omits empty nested options', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ pages: [] }), {
        headers: validContractHeaders(),
      }))
      .mockResolvedValueOnce(new Response(new Blob(['TSPL']), {
        headers: validContractHeaders('application/zip'),
      }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ pages: [] }), {
        headers: validContractHeaders(),
      }))
      .mockResolvedValueOnce(new Response(new Blob(['ZPL']), {
        headers: validContractHeaders('application/zip'),
      }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ pages: [] }), {
        headers: validContractHeaders(),
      }))
    vi.stubGlobal('fetch', fetchMock)

    const template = createBlankSample()
    const tsplMedia = {
      dpi: 300, gapDistance: 2, gapOffset: 1, direction: 1,
      referenceX: 3, referenceY: 4, speed: 5, density: 8,
    }
    const zplMedia = { dpi: 300, printSpeed: 6, orientation: 'Right' as const, mediaTracking: 'M' as const, darkness: -4 }
    await previewTemplate(template, {}, [], undefined, 'Tspl', 'Native', false,
      undefined, undefined, undefined, undefined, undefined, undefined, tsplMedia)
    await renderTemplate(template, {}, [], 'Tspl', 'Native', false,
      undefined, undefined, undefined, undefined, undefined, undefined, tsplMedia)
    await previewTemplate(template, {}, [], undefined, 'Zpl', 'Native', false,
      undefined, undefined, undefined, undefined, undefined, undefined, undefined, zplMedia)
    await renderTemplate(template, {}, [], 'Zpl', 'Native', false,
      undefined, undefined, undefined, undefined, undefined, undefined, undefined, zplMedia)

    const previewTsplBody = JSON.parse(fetchMock.mock.calls[0][1].body as string) as Record<string, unknown>
    const renderTsplBody = JSON.parse(fetchMock.mock.calls[1][1].body as string) as Record<string, unknown>
    const previewZplBody = JSON.parse(fetchMock.mock.calls[2][1].body as string) as Record<string, unknown>
    const renderZplBody = JSON.parse(fetchMock.mock.calls[3][1].body as string) as Record<string, unknown>
    expect(previewTsplBody.tsplMedia).toEqual(tsplMedia)
    expect(renderTsplBody.tsplMedia).toEqual(tsplMedia)
    expect(previewZplBody.zplMedia).toEqual(zplMedia)
    expect(renderZplBody.zplMedia).toEqual(zplMedia)

    await previewTemplate(template, {}, [], undefined, 'Tspl', 'Native', false,
      undefined, undefined, undefined, undefined, undefined, undefined, {})
    const emptyBody = JSON.parse(fetchMock.mock.calls[4][1].body as string) as Record<string, unknown>
    expect(emptyBody).not.toHaveProperty('tsplMedia')
    expect(emptyBody).not.toHaveProperty('zplMedia')
  })

  it('forwards TSPL and ZPL printer profiles in preview and render requests', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ pages: [] }), {
        headers: validContractHeaders(),
      }))
      .mockResolvedValueOnce(new Response(new Blob(['ZPL']), {
        headers: validContractHeaders('application/zip'),
      }))
    vi.stubGlobal('fetch', fetchMock)

    const template = createBlankSample()
    const tsplProfile = {
      name: 'TSC-203', printableWidthDots: 384, supportsImages: false,
      deviceFontNames: ['0', '8'], encodingCodePages: [20127, 65001, 936],
      characterRanges: [{ first: 0x20, last: 0x7E }],
    }
    const zplProfile = {
      name: 'ZQ-320', supportsCut: false, supportsNativeQrCode: false,
      deviceFontNames: ['0', 'A'], encodingCodePages: [20127, 65001],
      characterRanges: [{ first: 0x20, last: 0x7E }],
    }
    await previewTemplate(template, {}, [], undefined, 'Tspl', 'Native', false,
      undefined, undefined, undefined, undefined, undefined, undefined,
      { profile: tsplProfile })
    await renderTemplate(template, {}, [], 'Zpl', 'Native', false,
      undefined, undefined, undefined, undefined, undefined, undefined,
      undefined, { profile: zplProfile })

    const previewBody = JSON.parse(fetchMock.mock.calls[0][1].body as string) as Record<string, unknown>
    const renderBody = JSON.parse(fetchMock.mock.calls[1][1].body as string) as Record<string, unknown>
    expect(previewBody.tsplMedia).toMatchObject({ profile: tsplProfile })
    expect(renderBody.zplMedia).toMatchObject({ profile: zplProfile })
  })

  it('forwards encoding and protocol-specific ESC/POS device settings consistently', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ pages: [] }), {
        headers: validContractHeaders(),
      }))
      .mockResolvedValueOnce(new Response(new Blob(['ESC/POS']), {
        headers: validContractHeaders('application/zip'),
      }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ pages: [] }), {
        headers: validContractHeaders(),
      }))
      .mockResolvedValueOnce(new Response(new Blob(['TSPL']), {
        headers: validContractHeaders('application/zip'),
      }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ pages: [] }), {
        headers: validContractHeaders(),
      }))
    vi.stubGlobal('fetch', fetchMock)

    const template = createBlankSample()
    const device = {
      dpi: 180, codePageIndex: 0, printAreaWidthUnits: 576, nativeTabWidthDots: 32, nativeFlow: 'Sequential',
      profile: {
        name: '8-dot impact', imageCommand: 'BitImageEscStar', bitImageMode: 'DoubleDensity8',
        horizontalDpi: 180, verticalDpi: 60, motionUnitsPerInch: 144, printableWidthDots: 576,
        maxImageBlockBytes: 180, supportsCut: false, supportsNativeBarcode: false,
        supportsNativeQrCode: false, supportsPrintAreaWidth: false, supportsTextSize: false,
        unidirectionalBitImage: true,
        text: { encodingCodePage: 936, codePageIndex: 255, characterMode: 'FsKanji',
          characterRanges: [{ first: 0, last: 127 }, { first: 19968, last: 40959 }],
          halfCellWidthDots: 12, fullCellWidthDots: 24, lineHeightDots: 24 },
      },
    } as const
    await previewTemplate(template, {}, [], undefined, 'EscPos', 'Native', false,
      undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined,
      'Device', device)
    await renderTemplate(template, {}, [], 'EscPos', 'Native', false,
      undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined,
      'Device', device)
    await previewTemplate(template, {}, [], undefined, 'EscPos', 'Raster', false,
      undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined,
      'Utf8', device)
    await renderTemplate(template, {}, [], 'Tspl', 'Native', false,
      undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined,
      'Ascii', device)
    await previewTemplate(template, {}, [], undefined, 'Zpl', 'Native', false,
      undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined,
      'Gbk', device)

    const previewNative = JSON.parse(fetchMock.mock.calls[0][1].body as string) as Record<string, unknown>
    const renderNative = JSON.parse(fetchMock.mock.calls[1][1].body as string) as Record<string, unknown>
    const previewRaster = JSON.parse(fetchMock.mock.calls[2][1].body as string) as Record<string, unknown>
    const renderTspl = JSON.parse(fetchMock.mock.calls[3][1].body as string) as Record<string, unknown>
    const previewZpl = JSON.parse(fetchMock.mock.calls[4][1].body as string) as Record<string, unknown>
    expect(previewNative).toMatchObject({ encoding: 'Device', escPosDevice: device })
    expect(renderNative).toMatchObject({ encoding: 'Device', escPosDevice: device })
    expect(previewRaster).toMatchObject({ encoding: 'Utf8', escPosDevice: device })
    expect((previewRaster.escPosDevice as Record<string, unknown>).nativeFlow).toBe('Sequential')
    expect(renderTspl).toMatchObject({ encoding: 'Ascii', escPosDevice: device })
    expect(previewZpl).toMatchObject({ encoding: 'Gbk', escPosDevice: device })
  })

  it('forwards an explicit job as the final optional argument and preserves physical page mapping', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ pages: [{ width: 200, height: 100, elements: [], sourcePageIndex: 1, copyIndex: 0 }] }), {
        headers: validContractHeaders(),
      }))
      .mockResolvedValueOnce(new Response(new Blob(['JOB']), {
        headers: validContractHeaders('application/zip'),
      }))
    vi.stubGlobal('fetch', fetchMock)

    const job = { copies: 2, copyOrder: 'Collated' as const, feedScope: 'EachPage' as const,
      feedUnit: 'Dots' as const, feedAmount: 8, cutScope: 'JobEnd' as const }
    const template = createBlankSample()
    const preview = await previewTemplate(template, {}, [], undefined, 'Tspl', 'Native', false,
      undefined, undefined, undefined, undefined, undefined, undefined, undefined,
      undefined, undefined, undefined, undefined, undefined, undefined, job)
    await renderTemplate(template, {}, [], 'Tspl', 'Native', false,
      undefined, undefined, undefined, undefined, undefined, undefined, undefined,
      undefined, undefined, undefined, undefined, undefined, undefined, job)

    expect(preview.pages[0]).toMatchObject({ sourcePageIndex: 1, copyIndex: 0 })
    const previewBody = JSON.parse(fetchMock.mock.calls[0][1].body as string) as Record<string, unknown>
    const renderBody = JSON.parse(fetchMock.mock.calls[1][1].body as string) as Record<string, unknown>
    expect(previewBody.job).toEqual(job)
    expect(renderBody.job).toEqual(job)
  })

  it('forwards root and job cut options without rewriting their values', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify(validEscPosCapabilitiesResponse()), {
        headers: validContractHeaders(),
      }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ pages: [] }), {
        headers: validContractHeaders(),
      }))
      .mockResolvedValueOnce(new Response(JSON.stringify(validEscPosCapabilitiesResponse()), {
        headers: validContractHeaders(),
      }))
      .mockResolvedValueOnce(new Response(new Blob(['JOB']), { headers: validContractHeaders('application/zip') }))
    vi.stubGlobal('fetch', fetchMock)

    const rootCutOptions = { mode: 'Partial' as const, feedToCutPosition: true, feedBeforeCutAmount: 7, feedBeforeCutUnit: 'Dots' as const }
    const job = { copies: 2, cutScope: 'JobEnd' as const,
      cutOptions: { mode: 'Full' as const, feedToCutPosition: false, feedBeforeCutAmount: 0, feedBeforeCutUnit: 'Lines' as const } }
    const device = { profile: { ...defaultEscPosPrinterProfile(), supportsFeedAndCut: false } }
    const template = createBlankSample()
    const previewArgs: Parameters<typeof previewTemplate> = [
      template, {}, [], undefined, 'EscPos', 'Native', false,
      undefined, undefined, undefined, undefined, undefined, true,
      undefined, undefined, 'Utf8', device, undefined, undefined, undefined, undefined, rootCutOptions,
    ]
    const renderArgs: Parameters<typeof renderTemplate> = [
      template, {}, [], 'EscPos', 'Native', false,
      undefined, undefined, undefined, undefined, undefined, undefined, undefined, undefined,
      'Utf8', device, undefined, undefined, undefined, job,
    ]
    await previewTemplate(...previewArgs)
    await renderTemplate(...renderArgs)

    expect(fetchMock).toHaveBeenCalledTimes(4)
    const previewCapabilitiesBody = JSON.parse(fetchMock.mock.calls[0][1].body as string) as Record<string, unknown>
    const renderCapabilitiesBody = JSON.parse(fetchMock.mock.calls[2][1].body as string) as Record<string, unknown>
    expect(previewCapabilitiesBody).toMatchObject({ protocol: 'EscPos', escPosLayoutMode: 'Native', encoding: 'Utf8', escPosDevice: device })
    expect(renderCapabilitiesBody).toEqual(previewCapabilitiesBody)
    expect(previewCapabilitiesBody).not.toHaveProperty('requireCutContract')
    const previewBody = JSON.parse(fetchMock.mock.calls[1][1].body as string) as Record<string, unknown>
    const renderBody = JSON.parse(fetchMock.mock.calls[3][1].body as string) as Record<string, unknown>
    expect(previewBody.cutOptions).toEqual(rootCutOptions)
    expect(renderBody.job).toEqual(job)
  })
})
