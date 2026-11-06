import { describe, expect, it, vi } from 'vitest'
import JSZip from 'jszip'
import { applyCsvMapping, errorsCsv, errorsCsvUtf16Bytes, errorsCsvUtf8Bytes, jsonUtf16Length, jsonUtf8Length, mergeBatchArchives, parseBatchInput as parse } from './batch'
import { isJsonNumber, stringifyJsonPreservingPrecision } from './losslessJson'

describe('batch input parsing', () => {
  it('preserves leading zero strings and parses quoted multiline fields with BOM', () => {
    const parsed = parse('\uFEFFCode,Name\n00123,"Tea ""A""\nsecond line"', 'csv')
    expect(parsed.headers).toEqual(['Code', 'Name'])
    expect(parsed.rows).toEqual([['00123', 'Tea "A"\nsecond line']])
  })

  it('supports semicolon and tab delimiters outside quotes', () => {
    expect(parse('Code;Name\n001;Tea', 'csv').rows).toEqual([['001', 'Tea']])
    expect(parse('Code\tName\n001\tTea', 'csv').rows).toEqual([['001', 'Tea']])
  })

  it('preserves high precision JSON without changing ordinary numbers or date strings', () => {
    const parsed = parse('[{"Value":123456789012345.6789,"Small":1.5,"When":"2024-01-02T03:04:05-05:00"}]', 'json')
    expect(isJsonNumber(parsed.records[0]!.data.Value)).toBe(true)
    expect(stringifyJsonPreservingPrecision(parsed.records)).toContain('"Value":123456789012345.6789')
    expect(parsed.records[0]!.data.Small).toBe(1.5)
    expect(parsed.records[0]!.data.When).toBe('2024-01-02T03:04:05-05:00')
  })
  it('rejects duplicate headers and malformed row widths', () => {
    expect(() => parse('Code,Code\n1,2', 'csv')).toThrow('CSV 表头重复')
    expect(() => parse('Code,Name\n1', 'csv')).toThrow('列数与表头不一致')
    expect(() => parse('a,b\n"x"oops,y', 'csv')).toThrow('引号关闭')
    expect(() => parse('a,b\nx"oops,y', 'csv')).toThrow('未转义引号')
    expect(parse('a,b\n,\nx,y', 'csv').rows).toEqual([['', ''], ['x', 'y']])
    expect(parse('a,b\n\nx,y\n', 'csv').rows).toEqual([['x', 'y']])
  })

  it('rejects prototype paths without polluting global objects', () => {
    expect(() => applyCsvMapping(['a'], [['polluted']], { a: '__proto__.easyPrintReviewProbe' })).toThrow('危险原型')
    expect(({} as Record<string, unknown>).easyPrintReviewProbe).toBeUndefined()
  })

  it('maps CSV strings into nested paths without changing leading zero values', () => {
    const parsed = parse('Code,Name\n00123,Tea', 'csv')
    const records = applyCsvMapping(parsed.headers, parsed.rows, { Code: 'Product.Code', Name: 'Product.Name' })
    expect(records[0]!.data).toEqual({ Product: { Code: '00123', Name: 'Tea' } })
  })

  it('parses JSON arrays and JSONL while rejecting scalar or array records', () => {
    expect(parse('[{"Code":"001"}]', 'json').records).toEqual([{ index: 0, data: { Code: '001' } }])
    expect(parse('\r\n{"A":1}\r\n \r\n{"A":2}\n', 'jsonl').records.map((record) => record.index)).toEqual([0, 1])
    expect(() => parse('{}', 'json')).toThrow('对象数组')
    expect(() => parse('[1]', 'json')).toThrow('必须是对象')
    expect(() => parse('[[]]', 'json')).toThrow('必须是对象')
    expect(() => parse('{"A":1}\n2\n', 'jsonl')).toThrow('必须是对象')
  })

  it.each([
    ['{"A":1}\n\nnot-json\n', 'JSONL 第 3 行的 JSON 无效'],
    ['{"A":1}\r\n \r\n1\r\n', 'JSONL 第 3 行必须是对象'],
  ])('reports physical JSONL lines after blank lines: %j', (input, expected) => {
    expect(() => parse(input, 'jsonl')).toThrow(expected)
  })
})

describe('batch errors CSV', () => {
  it('escapes quotes, newlines and formula-leading text', () => {
    const csv = errorsCsv([{
      recordIndex: 3,
      code: 'BindingFailed',
      phase: 'Binding',
      fieldPath: 'Amount',
      sourcePath: 'Rows[0].Cells[0].Elements[0]',
      message: '=1+1,"quoted"\nnext',
    }])
    expect(csv).toContain('"\'=1+1,""quoted""\nnext"')
  })
})

describe('batch merge budgets', () => {
  it('counts final JSON and CSV UTF-8 exactly without pre-encoding', () => {
    const manifest = {
      status: 'partial', recordOffset: 0, totalRecords: 1, succeeded: 1, failed: 1, physicalPages: 2,
      byteLength: 3, remainingPages: 998, remainingBytes: 10, records: [{ message: '汉字 "quote" \\\\ control \\n' }], errors: [],
    }
    const errors = [{ recordIndex: 1, code: 'BindingFailed', phase: 'Binding', fieldPath: 'Value', sourcePath: 'Rows[0]', message: '汉字 "quote" \\\\n' }]
    expect(jsonUtf8Length(manifest, 2)).toBe(new TextEncoder().encode(JSON.stringify(manifest, null, 2)).byteLength)
    expect(jsonUtf16Length(manifest, 2)).toBe(JSON.stringify(manifest, null, 2).length)
    expect(errorsCsvUtf8Bytes(errors)).toBe(new TextEncoder().encode('\uFEFF' + errorsCsv(errors)).byteLength)
    expect(errorsCsvUtf16Bytes(errors)).toBe(('\uFEFF' + errorsCsv(errors)).length)
  })


  it('checks temporary and final archive size before publishing', async () => {
    const archive = new JSZip()
    archive.file('manifest.json', JSON.stringify({
      status: 'completed', recordOffset: 0, totalRecords: 1, succeeded: 1, failed: 0,
      physicalPages: 1, byteLength: 10, remainingPages: 999, remainingBytes: 100,
      records: [], errors: [],
    }))
    archive.file('records/000000/page-0001.cpcl', new Uint8Array([1, 2, 3, 4]))
    const blob = await archive.generateAsync({ type: 'blob' })
    await expect(mergeBatchArchives([blob], 1)).rejects.toThrow('32 MiB')
  })

  it('counts the final rebuilt manifest and errors CSV before publishing', async () => {
    const chunks: Blob[] = []
    for (let index = 0; index < 2; index++) {
      const error = {
        recordIndex: index, code: 'BindingFailed', phase: 'Binding', fieldPath: 'Value',
        sourcePath: 'Rows[0]', message: 'x'.repeat(2000),
      }
      const manifest = {
        status: 'all_failed', recordOffset: index, totalRecords: 2, succeeded: 0, failed: 1,
        physicalPages: 0, byteLength: 0, remainingPages: 1000, remainingBytes: 10000,
        records: [{ index, status: 'failed', copies: 1, pageCount: 0, byteLength: 0, copiedByProtocol: false, files: [] }],
        errors: [error],
      }
      const archive = new JSZip()
      archive.file('manifest.json', JSON.stringify(manifest))
      chunks.push(await archive.generateAsync({ type: 'blob', compression: 'DEFLATE' }))
    }
    await expect(mergeBatchArchives(chunks, 7000)).rejects.toThrow('32 MiB')
    await expect(mergeBatchArchives(chunks, 200000)).rejects.toThrow(/最终元数据/)

    const merged = await mergeBatchArchives(chunks, 500000)
    const archive = await JSZip.loadAsync(await merged.blob.arrayBuffer())
    let metadata = 0
    for (const name of ['manifest.json', 'errors.csv'])
      metadata += (await archive.file(name)!.async('uint8array')).byteLength
    expect(metadata).toBeGreaterThan(8500)
    expect(metadata).toBeLessThanOrEqual(500000)
    expect(merged.manifest.errors).toHaveLength(2)
  })

  it('cancels during the second merged chunk, not only before the merge starts', async () => {
    const make = async (index: number) => {
      const manifest = {
        status: 'all_failed', recordOffset: index, totalRecords: 2, succeeded: 0, failed: 1,
        physicalPages: 0, byteLength: 0, remainingPages: 1000, remainingBytes: 10000,
        records: [{ index, status: 'failed', copies: 1, pageCount: 0, byteLength: 0, copiedByProtocol: false, files: [] }],
        errors: [{ recordIndex: index, message: 'cancelled' }],
      }
      const archive = new JSZip()
      archive.file('manifest.json', JSON.stringify(manifest))
      return archive.generateAsync({ type: 'blob', compression: 'DEFLATE' })
    }
    const first = await make(0)
    const second = await make(1)
    const controller = new AbortController()
    const secondChunk = {
      size: second.size,
      arrayBuffer: async () => { controller.abort(); return second.arrayBuffer() },
    } as unknown as Blob
    await expect(mergeBatchArchives([first, secondChunk], 32 * 1024 * 1024, controller.signal))
      .rejects.toMatchObject({ name: 'AbortError' })
  })

  it('succeeds with real bounded content against the default 32 MiB limit', async () => {
    const archive = new JSZip()
    archive.file('manifest.json', JSON.stringify({
      status: 'completed', recordOffset: 0, totalRecords: 1, succeeded: 1, failed: 0,
      physicalPages: 1, byteLength: 4096, remainingPages: 999, remainingBytes: 33554432,
      records: [], errors: [],
    }))
    archive.file('records/000000/page-0001.cpcl', new Uint8Array(4096))
    const blob = await archive.generateAsync({ type: 'blob', compression: 'DEFLATE' })
    const merged = await mergeBatchArchives([blob], 32 * 1024 * 1024)
    expect(merged.manifest.status).toBe('completed')
    expect(merged.blob.size).toBeLessThan(32 * 1024 * 1024)
  })
  it('decompresses a page into one preallocated target without async concat copies', async () => {
    const payload = 1024 * 1024
    const archive = new JSZip()
    archive.file('manifest.json', JSON.stringify({
      status: 'completed', recordOffset: 0, totalRecords: 1, succeeded: 1, failed: 0,
      physicalPages: 1, byteLength: payload, remainingPages: 999, remainingBytes: payload,
      records: [], errors: [],
    }))
    archive.file('records/000000/page-0001.cpcl', new Uint8Array(payload))
    const input = await archive.generateAsync({ type: 'blob', compression: 'DEFLATE' })
    const Native = globalThis.Uint8Array
    const largeAllocations: number[] = []
    const tracked = new Proxy(Native, {
      construct(target, args) {
        const value = Reflect.construct(target, args)
        if (value.byteLength >= payload) largeAllocations.push(value.byteLength)
        return value
      },
    })
    vi.stubGlobal('Uint8Array', tracked)
    try {
      const merged = await mergeBatchArchives([input], 1_572_864)
      expect(merged.blob.size).toBeGreaterThan(0)
      expect(largeAllocations).toEqual([payload])
    } finally {
      vi.unstubAllGlobals()
    }
  })

  it('cancels during decompression after an earlier entry without publishing a result', async () => {
    const archive = new JSZip()
    archive.file('manifest.json', JSON.stringify({
      status: 'completed', recordOffset: 0, totalRecords: 1, succeeded: 1, failed: 0,
      physicalPages: 1, byteLength: 64 * 1024 + 1024 * 1024, remainingPages: 999, remainingBytes: 1081344,
      records: [], errors: [],
    }))
    archive.file('records/000000/page-0001.cpcl', new Uint8Array(64 * 1024))
    archive.file('records/000000/page-0002.cpcl', new Uint8Array(1024 * 1024))
    const input = await archive.generateAsync({ type: 'blob', compression: 'DEFLATE' })
    const controller = new AbortController()
    const Native = globalThis.Uint8Array
    const tracked = new Proxy(Native, {
      construct(target, args) {
        const value = Reflect.construct(target, args)
        if (value.byteLength === 1024 * 1024) controller.abort()
        return value
      },
    })
    vi.stubGlobal('Uint8Array', tracked)
    try {
      await expect(mergeBatchArchives([input], 4 * 1024 * 1024, controller.signal))
        .rejects.toMatchObject({ name: 'AbortError' })
    } finally {
      vi.unstubAllGlobals()
    }
  })

  it('uses bounded streamed compression for incompressible content and rejects the final copy over budget', async () => {
    const bytes = new Uint8Array(512 * 1024)
    let seed = 0x12345678
    for (let index = 0; index < bytes.length; index++) {
      seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0
      bytes[index] = seed >>> 24
    }
    const archive = new JSZip()
    archive.file('manifest.json', JSON.stringify({
      status: 'completed', recordOffset: 0, totalRecords: 1, succeeded: 1, failed: 0,
      physicalPages: 1, byteLength: bytes.length, remainingPages: 999, remainingBytes: bytes.length,
      records: [], errors: [],
    }))
    archive.file('records/000000/page-0001.cpcl', bytes)
    const blob = await archive.generateAsync({ type: 'blob', compression: 'DEFLATE' })
    await expect(mergeBatchArchives([blob], 2_000_000)).rejects.toThrow(/最终元数据/)
    const merged = await mergeBatchArchives([blob], 3_000_000)
    expect(merged.blob.size).toBeGreaterThan(400 * 1024)
  })

  it('enforces the configured 32 MiB chunk boundary before reading a ZIP', async () => {
    const blob = new Blob([new Uint8Array([1])])
    Object.defineProperty(blob, 'size', { value: 32 * 1024 * 1024 + 1 })
    await expect(mergeBatchArchives([blob], 32 * 1024 * 1024)).rejects.toThrow('32 MiB')
  })

  it('counts uncompressed metadata before decompressing it', async () => {
    const archive = new JSZip()
    archive.file('manifest.json', JSON.stringify({
      status: 'all_failed', recordOffset: 0, totalRecords: 1, succeeded: 0, failed: 1,
      physicalPages: 0, byteLength: 0, remainingPages: 1000, remainingBytes: 10000,
      records: [], errors: [{ recordIndex: 0, message: 'x'.repeat(5000) }],
    }))
    archive.file('errors.csv', 'x'.repeat(5000))
    const blob = await archive.generateAsync({ type: 'blob' })
    await expect(mergeBatchArchives([blob], 1024)).rejects.toThrow('32 MiB')
  })

  it('counts every chunk already held before processing the first one', async () => {
    const make = async () => {
      const archive = new JSZip()
      archive.file('manifest.json', JSON.stringify({
        status: 'completed', recordOffset: 0, totalRecords: 1, succeeded: 1, failed: 0,
        physicalPages: 1, byteLength: 1, remainingPages: 999, remainingBytes: 100,
        records: [], errors: [],
      }))
      archive.file('records/000000/page-0001.cpcl', new Uint8Array(700))
      return archive.generateAsync({ type: 'blob' })
    }
    await expect(mergeBatchArchives([await make(), await make()], 1000)).rejects.toThrow('32 MiB')
  })

  it('observes cancellation before publishing the merged archive', async () => {
    const archive = new JSZip()
    archive.file('manifest.json', JSON.stringify({
      status: 'completed', recordOffset: 0, totalRecords: 1, succeeded: 1, failed: 0,
      physicalPages: 1, byteLength: 1, remainingPages: 999, remainingBytes: 100,
      records: [], errors: [],
    }))
    archive.file('records/000000/page-0001.cpcl', new Uint8Array([1]))
    const blob = await archive.generateAsync({ type: 'blob' })
    const controller = new AbortController()
    controller.abort()
    await expect(mergeBatchArchives([blob], 32 * 1024 * 1024, controller.signal))
      .rejects.toMatchObject({ name: 'AbortError' })
  })
})
