import { afterEach, describe, expect, it, vi } from 'vitest'
import { convertGs1Input, DesignerValidationError, loadGs1AiCatalog, validateGs1Input } from './api'
import type { BpTemplate } from './model'

afterEach(() => vi.unstubAllGlobals())

function responseHeaders(features: string[]): Record<string, string> {
  return {
    'Content-Type': 'application/json',
    'X-EasyPrint-Contract-Version': '1',
    'X-EasyPrint-Features': ['structured-errors', 'strict-options', 'binding-diagnostics', ...features].join(','),
  }
}

const template: BpTemplate = {
  Version: 2,
  Width: 200,
  Height: 100,
  Rows: [{ Cells: [{ Elements: [{ Type: 3, Gs1: false, Field: { Gs1Input: {
    Mode: 1, BaseUri: 'https://id.gs1.org', Items: [{ Ai: '01', Value: '09520123456788' }],
  } } }] }] }],
}

describe('GS1 Host APIs', () => {
  it('loads the AI catalog only after negotiating its feature', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
      dictionaryCommit: 'd1702ffb2fa5060f8460f7d581b01e377deaa1d6',
      entries: [{ ai: '01', role: 'PrimaryKey' }],
    }), { headers: responseHeaders(['gs1-ai-catalog', 'gs1-structured-input', 'gs1-digital-link']) }))
    vi.stubGlobal('fetch', fetchMock)

    const catalog = await loadGs1AiCatalog()

    expect(catalog.entries[0]?.ai).toBe('01')
    expect(fetchMock.mock.calls[0]![0]).toBe('/api/designer/gs1-catalog')
    expect((fetchMock.mock.calls[0]![1]!.headers as Record<string, string>)['X-EasyPrint-Required-Features'])
      .toBe('gs1-ai-catalog,gs1-structured-input,gs1-digital-link')
  })

  it('keeps the editor disabled when an older Host lacks the catalog feature', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response('{}', {
      headers: responseHeaders(['gs1-structured-input', 'gs1-digital-link']),
    }))
    vi.stubGlobal('fetch', fetchMock)

    await expect(loadGs1AiCatalog()).rejects.toMatchObject({
      name: 'DesignerValidationError',
      diagnostics: [expect.objectContaining({ code: 'ContractFeatureUnavailable', phase: 'Contract' })],
    } satisfies Partial<DesignerValidationError>)
  })

  it('sends the current template and data to authoritative validation', async () => {
    const headers = responseHeaders(['gs1-ai-catalog', 'gs1-structured-input', 'gs1-digital-link'])
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
      status: 'valid', dataProvided: true, items: [], errors: [],
    }), { headers }))
    vi.stubGlobal('fetch', fetchMock)

    const result = await validateGs1Input(template, { Product: { Gtin: '09520123456788' } })

    expect(result.status).toBe('valid')
    expect(fetchMock.mock.calls[0]![0]).toBe('/api/designer/gs1-validate')
    expect(fetchMock.mock.calls[0]![1]!.body).toContain('"Product"')
    expect((fetchMock.mock.calls[0]![1]!.headers as Record<string, string>)['X-EasyPrint-Required-Features'])
      .toContain('gs1-digital-link')
  })

  it('converts without dropping the extension array and returns structured input', async () => {
    const headers = responseHeaders(['gs1-ai-catalog', 'gs1-structured-input', 'gs1-digital-link'])
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
      status: 'valid', sourceFormat: 'elements', targetFormat: 'digital-link', output: 'https://id.gs1.org/01/09520123456788',
      structuredInput: { mode: 1, items: [{ ai: '01', value: '09520123456788', placement: 0 }], queryParameters: [] },
    }), { headers }))
    vi.stubGlobal('fetch', fetchMock)

    const result = await convertGs1Input({
      input: '[01]09520123456788', targetFormat: 'digital-link', baseUri: 'https://id.gs1.org',
      queryParameters: [{ key: 'source', value: 'label' }, { key: 'source', value: 'scan' }],
    })
    const body = JSON.parse(fetchMock.mock.calls[0]![1]!.body as string)

    expect(result.structuredInput.items).toHaveLength(1)
    expect(body.gs1Conversion.queryParameters).toEqual([
      { key: 'source', value: 'label' }, { key: 'source', value: 'scan' },
    ])
    expect(fetchMock.mock.calls[0]![0]).toBe('/api/designer/gs1-convert')
  })
})
