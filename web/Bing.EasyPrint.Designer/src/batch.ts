import JSZip from 'jszip'
import { parseJsonPreservingPrecision } from './losslessJson'

export type BatchInputFormat = 'csv' | 'json' | 'jsonl'
export type BatchDataRecord = { index: number; data: Record<string, unknown>; copies?: number }
export type BatchParseResult = {
  format: BatchInputFormat
  headers: string[]
  rows: string[][]
  records: BatchDataRecord[]
  mapping: Record<string, string>
}
export type BatchFileResult = {
  file: string
  pageIndex?: number | null
  segmentIndex: number
  kind: string
  sourcePageIndex?: number | null
  copyIndex?: number | null
  placement?: string | null
  format?: 'pdf' | 'svg'
  elementIndex?: number | null
  pageCount?: number | null
  copies?: number | null
}
export type BatchRecordResult = {
  index: number
  status: 'completed' | 'failed'
  copies: number
  pageCount: number
  byteLength: number
  copiedByProtocol: boolean
  files: BatchFileResult[]
}
export type BatchErrorRow = {
  recordIndex: number
  code?: string | null
  phase?: string | null
  fieldPath?: string | null
  sourcePath?: string | null
  message?: string | null
}
export type BatchManifest = {
  status: 'completed' | 'partial' | 'all_failed'
  recordOffset: number
  totalRecords: number
  succeeded: number
  failed: number
  physicalPages: number
  byteLength: number
  remainingPages: number
  remainingBytes: number
  records: BatchRecordResult[]
  errors: BatchErrorRow[]
}

export function detectBatchFormat(fileName: string, text: string): BatchInputFormat {
  const lower = fileName.toLowerCase()
  if (lower.endsWith('.csv')) return 'csv'
  if (lower.endsWith('.jsonl') || lower.endsWith('.ndjson')) return 'jsonl'
  if (lower.endsWith('.json')) return 'json'
  const trimmed = text.trimStart()
  if (trimmed.startsWith('[')) return 'json'
  const firstLine = trimmed.split(/\r?\n/, 1)[0]?.trim() ?? ''
  try {
    if (firstLine && typeof JSON.parse(firstLine) === 'object') return 'jsonl'
  } catch {
    // Not JSONL; fall through to CSV.
  }
  return 'csv'
}

export function parseBatchInput(text: string, format: BatchInputFormat, delimiter?: string): BatchParseResult {
  const source = stripBom(text)
  if (!source.trim()) throw new Error('批量数据为空。')
  if (format === 'json') return parseJsonArray(source)
  if (format === 'jsonl') return parseJsonLines(source)
  return parseCsv(source, delimiter)
}

export function applyCsvMapping(headers: string[], rows: string[][], mapping: Record<string, string>): BatchDataRecord[] {
  const targets = headers.map((header) => mapping[header] ?? header)
  const duplicate = targets.find((target, index) => targets.indexOf(target) !== index)
  if (duplicate) throw new Error(`字段映射目标重复：${duplicate}`)
  return rows.map((row, index) => {
    const data: Record<string, unknown> = {}
    for (let column = 0; column < headers.length; column++) {
      setPath(data, targets[column]!, row[column] ?? '')
    }
    return { index, data }
  })
}

const pakoStateBytes = 267160
const pakoOutputChunkBytes = 64 * 1024
const pakoInflateStateBytes = 12768

export function jsonUtf8Length(value: unknown, space: string | number = 2): number {
  const gap = typeof space === 'number'
    ? ' '.repeat(Math.max(0, Math.min(10, Math.trunc(space))))
    : space.slice(0, 10)
  return jsonValueLength(value, 0, gap)
}

function jsonValueLength(value: unknown, depth: number, gap: string): number {
  if (value === null) return 4
  if (typeof value === 'string') return jsonStringUtf8Length(value)
  if (typeof value === 'boolean') return value ? 4 : 5
  if (typeof value === 'number') return JSON.stringify(value).length
  if (typeof value === 'undefined' || typeof value === 'function' || typeof value === 'symbol') return 0
  if (Array.isArray(value)) {
    if (!value.length) return 2
    const childIndent = gap.repeat(depth + 1)
    let total = 1 + (gap ? 1 + childIndent.length : 0)
    for (let index = 0; index < value.length; index++) {
      total += jsonValueLength(value[index], depth + 1, gap)
      if (index < value.length - 1) total += 1 + (gap ? 1 + childIndent.length : 0)
    }
    return total + 1 + (gap ? 1 + gap.repeat(depth).length : 0)
  }
  if (typeof value === 'object') {
    const object = value as Record<string, unknown>
    const keys = Object.keys(object).filter((key) => {
      const item = object[key]
      return item !== undefined && typeof item !== 'function' && typeof item !== 'symbol'
    })
    if (!keys.length) return 2
    const childIndent = gap.repeat(depth + 1)
    let total = 1 + (gap ? 1 + childIndent.length : 0)
    for (let index = 0; index < keys.length; index++) {
      total += jsonStringUtf8Length(keys[index]!) + (gap ? 2 : 1) +
        jsonValueLength(object[keys[index]!], depth + 1, gap)
      if (index < keys.length - 1) total += 1 + (gap ? 1 + childIndent.length : 0)
    }
    return total + 1 + (gap ? 1 + gap.repeat(depth).length : 0)
  }
  return 0
}

function jsonStringUtf8Length(value: string): number {
  let total = 2
  for (let index = 0; index < value.length; index++) {
    const code = value.charCodeAt(index)
    if (code === 0x22 || code === 0x5c) total += 2
    else if (code === 0x08 || code === 0x09 || code === 0x0a || code === 0x0c || code === 0x0d) total += 2
    else if (code < 0x20) total += 6
    else if (code >= 0xd800 && code <= 0xdbff) {
      if (index + 1 < value.length && value.charCodeAt(index + 1) >= 0xdc00 && value.charCodeAt(index + 1) <= 0xdfff) {
        total += 4
        index++
      } else total += 6
    } else if (code >= 0xdc00 && code <= 0xdfff) total += 6
    else if (code < 0x80) total += 1
    else if (code < 0x800) total += 2
    else total += 3
  }
  return total
}

export function jsonUtf16Length(value: unknown, space: string | number = 2): number {
  const gap = typeof space === 'number'
    ? ' '.repeat(Math.max(0, Math.min(10, Math.trunc(space))))
    : space.slice(0, 10)
  return jsonValueUtf16Length(value, 0, gap)
}

function jsonValueUtf16Length(value: unknown, depth: number, gap: string): number {
  if (value === null) return 4
  if (typeof value === 'string') return jsonStringUtf16Length(value)
  if (typeof value === 'boolean') return value ? 4 : 5
  if (typeof value === 'number') return JSON.stringify(value).length
  if (typeof value === 'undefined' || typeof value === 'function' || typeof value === 'symbol') return 0
  if (Array.isArray(value)) {
    if (!value.length) return 2
    const childIndent = gap.repeat(depth + 1)
    let total = 1 + (gap ? 1 + childIndent.length : 0)
    for (let index = 0; index < value.length; index++) {
      total += jsonValueUtf16Length(value[index], depth + 1, gap)
      if (index < value.length - 1) total += 1 + (gap ? 1 + childIndent.length : 0)
    }
    return total + 1 + (gap ? 1 + gap.repeat(depth).length : 0)
  }
  if (typeof value === 'object') {
    const object = value as Record<string, unknown>
    const keys = Object.keys(object).filter((key) => {
      const item = object[key]
      return item !== undefined && typeof item !== 'function' && typeof item !== 'symbol'
    })
    if (!keys.length) return 2
    const childIndent = gap.repeat(depth + 1)
    let total = 1 + (gap ? 1 + childIndent.length : 0)
    for (let index = 0; index < keys.length; index++) {
      total += jsonStringUtf16Length(keys[index]!) + (gap ? 2 : 1) +
        jsonValueUtf16Length(object[keys[index]!], depth + 1, gap)
      if (index < keys.length - 1) total += 1 + (gap ? 1 + childIndent.length : 0)
    }
    return total + 1 + (gap ? 1 + gap.repeat(depth).length : 0)
  }
  return 0
}

function jsonStringUtf16Length(value: string): number {
  let total = 2
  for (let index = 0; index < value.length; index++) {
    const code = value.charCodeAt(index)
    if (code === 0x22 || code === 0x5c) total += 2
    else if (code === 0x08 || code === 0x09 || code === 0x0a || code === 0x0c || code === 0x0d) total += 2
    else if (code < 0x20) total += 6
    else if (code >= 0xd800 && code <= 0xdbff) {
      if (index + 1 < value.length && value.charCodeAt(index + 1) >= 0xdc00 && value.charCodeAt(index + 1) <= 0xdfff) {
        total += 2
        index++
      } else total += 6
    } else if (code >= 0xdc00 && code <= 0xdfff) total += 6
    else total += 1
  }
  return total
}

export function errorsCsvUtf8Bytes(errors: BatchErrorRow[]): number {
  let total = 3 + 'recordIndex,code,phase,fieldPath,sourcePath,message'.length + 2
  for (const error of errors) {
    total += String(error.recordIndex).length + 1
    total += csvCellUtf8Length(error.code) + 1
    total += csvCellUtf8Length(error.phase) + 1
    total += csvCellUtf8Length(error.fieldPath) + 1
    total += csvCellUtf8Length(error.sourcePath) + 1
    total += csvCellUtf8Length(error.message) + 2
  }
  return total
}

export function errorsCsvUtf16Bytes(errors: BatchErrorRow[]): number {
  let total = 1 + 'recordIndex,code,phase,fieldPath,sourcePath,message'.length + 2
  for (const error of errors) {
    total += String(error.recordIndex).length + 1
    total += csvCellUtf16Length(error.code) + 1
    total += csvCellUtf16Length(error.phase) + 1
    total += csvCellUtf16Length(error.fieldPath) + 1
    total += csvCellUtf16Length(error.sourcePath) + 1
    total += csvCellUtf16Length(error.message) + 2
  }
  return total
}

function csvCellUtf16Length(value: string | null | undefined): number {
  const text = value ?? ''
  let total = 2
  if (text.length > 0 && '=+-@\t\r'.includes(text[0]!)) total++
  for (let index = 0; index < text.length; index++) {
    const code = text.charCodeAt(index)
    if (code === 0x22) total += 2
    else if (code >= 0xd800 && code <= 0xdbff) {
      if (index + 1 < text.length && text.charCodeAt(index + 1) >= 0xdc00 && text.charCodeAt(index + 1) <= 0xdfff) {
        total += 2
        index++
      } else total += 1
    } else total++
  }
  return total
}

function csvCellUtf8Length(value: string | null | undefined): number {
  const text = value ?? ''
  let total = 2
  if (text.length > 0 && '=+-@\t\r'.includes(text[0]!)) total++
  for (let index = 0; index < text.length; index++) {
    const code = text.charCodeAt(index)
    if (code === 0x22) total += 2
    else if (code >= 0xd800 && code <= 0xdbff) {
      if (index + 1 < text.length && text.charCodeAt(index + 1) >= 0xdc00 && text.charCodeAt(index + 1) <= 0xdfff) {
        total += 4
        index++
      } else total += 3
    } else if (code >= 0xdc00 && code <= 0xdfff) total += 3
    else if (code < 0x80) total += 1
    else if (code < 0x800) total += 2
    else total += 3
  }
  return total
}

export function mergeBatchArchives(chunks: Blob[], maxBytes = 32 * 1024 * 1024, signal?: AbortSignal): Promise<{ blob: Blob; manifest: BatchManifest }> {
  if (!chunks.length) throw new Error('没有可合并的批量结果。')
  return (async () => {
    const output = new JSZip()
    let workingBytes = chunks.reduce((total, chunk) => total + chunk.size, 0)
    const records: BatchRecordResult[] = []
    const errors: BatchErrorRow[] = []
    let physicalPages = 0
    let byteLength = 0
    let remainingPages = 0
    let remainingBytes = 0
    for (const chunk of chunks) {
      signal?.throwIfAborted()
      if (workingBytes > maxBytes) throw new Error("批量合并工作空间超过 32 MiB 上限。")
      const temporaryBytes = chunk.size
      if (workingBytes + temporaryBytes > maxBytes)
        throw new Error("批量合并工作空间超过 32 MiB 上限。")
      let buffer = await chunk.arrayBuffer()
      const archive = await JSZip.loadAsync(buffer, { checkCRC32: false, createFolders: false })
      buffer = new ArrayBuffer(0)
      const manifestEntry = archive.file('manifest.json')
      if (!manifestEntry) throw new Error('批量结果缺少 manifest.json。')
      let projectedLowerBound = 0
      for (const [name, entry] of Object.entries(archive.files))
        if (!entry.dir && name !== 'manifest.json') projectedLowerBound += uncompressedSize(entry, chunk.size)
      projectedLowerBound += uncompressedSize(manifestEntry, chunk.size)
      if (workingBytes + temporaryBytes + projectedLowerBound > maxBytes)
        throw new Error("批量合并工作空间超过 32 MiB 上限。")
      const manifestSize = entrySize(manifestEntry)
      if (manifestSize === null) throw new Error("无法确定 manifest.json 解压大小。")
      const manifestWorking = manifestSize * 4
      if (workingBytes + temporaryBytes + manifestWorking + pakoInflateStateBytes + pakoOutputChunkBytes > maxBytes)
        throw new Error("批量合并工作空间超过 32 MiB 上限。")
      const manifestBytes = await readEntry(manifestEntry, manifestSize, signal)
      workingBytes += manifestWorking
      if (workingBytes + temporaryBytes > maxBytes)
        throw new Error("批量合并工作空间超过 32 MiB 上限。")
      const manifest = JSON.parse(new TextDecoder().decode(manifestBytes)) as BatchManifest
      records.push(...(manifest.records ?? []))
      errors.push(...(manifest.errors ?? []))
      physicalPages += manifest.physicalPages
      byteLength += manifest.byteLength
      remainingPages = manifest.remainingPages
      remainingBytes = manifest.remainingBytes
      for (const [name, entry] of Object.entries(archive.files)) {
        signal?.throwIfAborted()
        if (entry.dir || name === 'manifest.json' || name === 'errors.csv') continue
        const size = entrySize(entry)
        if (size === null) throw new Error("无法确定 ZIP 条目解压大小。")
        if (workingBytes + size + pakoInflateStateBytes + pakoOutputChunkBytes > maxBytes)
          throw new Error("批量合并工作空间超过 32 MiB 上限。")
        const bytes = await readEntry(entry, size, signal)
        workingBytes += bytes.byteLength
        if (workingBytes > maxBytes) throw new Error("批量合并工作空间超过 32 MiB 上限。")
        output.file(name, bytes)
      }
    }
    const succeeded = records.filter((record) => record.status === 'completed').length
    const failed = records.filter((record) => record.status === 'failed').length
    const status: BatchManifest['status'] = failed === 0 ? 'completed' : succeeded === 0 ? 'all_failed' : 'partial'
    const manifest: BatchManifest = {
      status,
      recordOffset: 0,
      totalRecords: records.length,
      succeeded,
      failed,
      physicalPages,
      byteLength,
      remainingPages,
      remainingBytes,
      records,
      errors,
    }
    signal?.throwIfAborted()
    const manifestUtf8Bytes = jsonUtf8Length(manifest, 2)
    const manifestUtf16Bytes = jsonUtf16Length(manifest, 2) * 2
    const errorsUtf8Bytes = errorsCsvUtf8Bytes(errors)
    const errorsUtf16Bytes = errorsCsvUtf16Bytes(errors) * 2
    const metadataUtf8Bytes = manifestUtf8Bytes + errorsUtf8Bytes
    const metadataConstructionBytes = manifestUtf16Bytes + errorsUtf16Bytes * 2 + metadataUtf8Bytes
    if (byteLength + metadataUtf8Bytes > maxBytes)
      throw new Error("批量结果超过 32 MiB 输出上限。")
    const compressionBytes = pakoStateBytes + pakoOutputChunkBytes
    const streamWorkingBytes = workingBytes + metadataConstructionBytes + compressionBytes
    if (streamWorkingBytes > maxBytes)
      throw new Error("批量最终元数据或压缩工作空间超过 32 MiB 上限。")
    signal?.throwIfAborted()
    const manifestText = JSON.stringify(manifest, null, 2)
    const errorsText = '\uFEFF' + errorsCsv(errors)
    output.file('manifest.json', manifestText)
    output.file('errors.csv', errorsText)
    const blob = await collectZipOutput(output, maxBytes, streamWorkingBytes, signal)
    signal?.throwIfAborted()
    if (blob.size > maxBytes) throw new Error("批量结果 ZIP 超过 32 MiB 输出上限。")
    return { blob, manifest }
  })()
}

function collectZipOutput(output: JSZip, maxBytes: number, baseBytes: number, signal?: AbortSignal): Promise<Blob> {
  return new Promise<Blob>((resolve, reject) => {
    const chunks: Uint8Array[] = []
    let producedBytes = 0
    let settled = false
    const stream = output.generateInternalStream({ type: 'uint8array', compression: 'DEFLATE', streamFiles: true })
    const fail = (error: unknown) => {
      if (settled) return
      settled = true
      try { stream.pause() } catch { /* no-op */ }
      chunks.length = 0
      reject(error instanceof Error ? error : new Error(String(error)))
    }
    const abortError = () => new DOMException('Aborted', 'AbortError')
    stream.on('data', (data: Uint8Array) => {
      if (settled) return
      if (signal?.aborted) { fail(abortError()); return }
      producedBytes += data.byteLength
      if (baseBytes + producedBytes + pakoOutputChunkBytes > maxBytes) {
        fail(new Error("批量最终元数据或压缩工作空间超过 32 MiB 上限。"))
        return
      }
      chunks.push(data)
    })
    stream.on('error', fail)
    stream.on('end', () => {
      if (settled) return
      if (signal?.aborted) { fail(abortError()); return }
      if (baseBytes + producedBytes * 2 > maxBytes) {
        fail(new Error("批量最终元数据或压缩工作空间超过 32 MiB 上限。"))
        return
      }
      try {
        const blob = new Blob(chunks as unknown as BlobPart[], { type: 'application/zip' })
        chunks.length = 0
        settled = true
        resolve(blob)
      } catch (error) { fail(error) }
    })
    stream.resume()
  })
}

type InternalStreamEntry = JSZip.JSZipObject & {
  internalStream(type: 'uint8array'): JSZip.JSZipStreamHelper<Uint8Array>
}

function entrySize(entry: JSZip.JSZipObject): number | null {
  const size = (entry as unknown as { _data?: { uncompressedSize?: unknown } })._data?.uncompressedSize
  return typeof size === 'number' && Number.isFinite(size) ? size : null
}

function readEntry(entry: JSZip.JSZipObject, size: number, signal?: AbortSignal): Promise<Uint8Array> {
  return new Promise<Uint8Array>((resolve, reject) => {
    const target = new Uint8Array(size)
    let offset = 0
    let settled = false
    const stream = (entry as InternalStreamEntry).internalStream('uint8array')
    const fail = (error: unknown) => {
      if (settled) return
      settled = true
      try { stream.pause() } catch { /* no-op */ }
      reject(error instanceof Error ? error : new Error(String(error)))
    }
    const abortError = () => new DOMException('Aborted', 'AbortError')
    stream.on('data', (data: Uint8Array) => {
      if (settled) return
      if (signal?.aborted) { fail(abortError()); return }
      if (offset + data.byteLength > target.length) {
        fail(new Error("解压结果超过声明的未压缩大小。"))
        return
      }
      target.set(data, offset)
      offset += data.byteLength
    })
    stream.on('error', fail)
    stream.on('end', () => {
      if (settled) return
      if (signal?.aborted) { fail(abortError()); return }
      if (offset !== target.length) {
        fail(new Error("解压结果与声明的未压缩大小不一致。"))
        return
      }
      settled = true
      resolve(target)
    })
    stream.resume()
  })
}

function uncompressedSize(entry: JSZip.JSZipObject, fallback: number): number {
  const size = (entry as unknown as { _data?: { uncompressedSize?: unknown } })._data?.uncompressedSize
  return typeof size === 'number' && Number.isFinite(size) ? size : fallback
}

export function errorsCsv(errors: BatchErrorRow[]): string {
  const lines = ['recordIndex,code,phase,fieldPath,sourcePath,message']
  for (const error of errors) {
    lines.push([
      String(error.recordIndex),
      csv(error.code),
      csv(error.phase),
      csv(error.fieldPath),
      csv(error.sourcePath),
      csv(error.message),
    ].join(','))
  }
  return lines.join('\r\n') + '\r\n'
}

function parseJsonArray(text: string): BatchParseResult {
  let value: unknown
  try { value = parseJsonPreservingPrecision(text) } catch (error) {
    throw new Error(`JSON 数据无效：${error instanceof Error ? error.message : '解析失败'}`)
  }
  if (!Array.isArray(value)) throw new Error('JSON 顶层必须是对象数组。')
  const records = value.map((item, index) => {
    if (!item || typeof item !== 'object' || Array.isArray(item))
      throw new Error(`JSON 第 ${index + 1} 条记录必须是对象。`)
    return { index, data: item as Record<string, unknown> }
  })
  return { format: 'json', headers: [], rows: [], records, mapping: {} }
}

function parseJsonLines(text: string): BatchParseResult {
  const records: BatchDataRecord[] = []
  const lines = text.split(/\r?\n/)
  for (const [lineIndex, line] of lines.entries()) {
    const trimmed = line.trim()
    if (!trimmed) continue
    let item: unknown
    try { item = parseJsonPreservingPrecision(trimmed) } catch (error) {
      throw new Error(`JSONL 第 ${lineIndex + 1} 行的 JSON 无效：${error instanceof Error ? error.message : '解析失败'}`)
    }
    if (!item || typeof item !== 'object' || Array.isArray(item))
      throw new Error(`JSONL 第 ${lineIndex + 1} 行必须是对象。`)
    records.push({ index: records.length, data: item as Record<string, unknown> })
  }
  if (!records.length) throw new Error('JSONL 没有有效记录。')
  return { format: 'jsonl', headers: [], rows: [], records, mapping: {} }
}

function parseCsv(text: string, delimiter?: string): BatchParseResult {
  const separator = delimiter && delimiter.length === 1 ? delimiter : detectDelimiter(text)
  const rows: string[][] = []
  let row: string[] = []
  let field = ''
  let quoted = false
  let quoteClosed = false
  for (let index = 0; index < text.length; index++) {
    const character = text[index]!
    if (quoted) {
      if (character === '"') {
        if (text[index + 1] === '"') { field += '"'; index++ }
        else { quoted = false; quoteClosed = true }
      } else field += character
      continue
    }
    if (quoteClosed) {
      if (character === separator) { row.push(field); field = ''; quoteClosed = false; continue }
      if (character === '\r' || character === '\n') {
        if (character === '\r' && text[index + 1] === '\n') index++
        row.push(field)
        rows.push(row)
        row = []
        field = ''
        quoteClosed = false
        continue
      }
      throw new Error('CSV 引号关闭后只能出现分隔符或换行。')
    }
    if (character === '"') {
      if (field.length !== 0) throw new Error('CSV 未转义引号只能出现在字段开头。')
      quoted = true
      continue
    }
    if (character === separator) { row.push(field); field = ''; continue }
    if (character === '\r' || character === '\n') {
      if (character === '\r' && text[index + 1] === '\n') index++
      if (row.length === 0 && field.length === 0) continue
      row.push(field)
      rows.push(row)
      row = []
      field = ''
      continue
    }
    field += character
  }
  if (quoted) throw new Error('CSV 存在未闭合的引号。')
  if (quoteClosed || row.length > 0 || field.length > 0) {
    row.push(field)
    rows.push(row)
  }
  if (rows.length < 2) throw new Error('CSV 必须包含表头和至少一条记录。')
  const headers = rows[0]!
  if (headers.some((header) => !header)) throw new Error('CSV 表头不能为空。')
  const duplicate = headers.find((header, index) => headers.indexOf(header) !== index)
  if (duplicate) throw new Error(`CSV 表头重复：${duplicate}`)
  for (let index = 1; index < rows.length; index++) {
    if (rows[index]!.length !== headers.length)
      throw new Error(`CSV 第 ${index} 条记录的列数与表头不一致。`)
  }
  return {
    format: 'csv',
    headers,
    rows: rows.slice(1),
    records: [],
    mapping: Object.fromEntries(headers.map((header) => [header, header])),
  }
}

function detectDelimiter(text: string): string {
  const line = text.split(/\r?\n/, 1)[0] ?? ''
  const candidates = [',', ';', '\t']
  let selected = ','
  let best = -1
  for (const candidate of candidates) {
    let count = 0
    let quoted = false
    for (let index = 0; index < line.length; index++) {
      if (line[index] === '"') quoted = !quoted
      else if (!quoted && line[index] === candidate) count++
    }
    if (count > best) { best = count; selected = candidate }
  }
  return selected
}

function stripBom(text: string): string {
  return text.charCodeAt(0) === 0xFEFF ? text.slice(1) : text
}

function csv(value: string | null | undefined): string {
  let text = value ?? ''
  if (text && '=+-@\t\r'.includes(text[0]!)) text = "'" + text
  return '"' + text.replace(/"/g, '""') + '"'
}

function setPath(target: Record<string, unknown>, path: string, value: unknown): void {
  const segments = path.split('.').map((segment) => segment.trim()).filter(Boolean)
  if (!segments.length) throw new Error('字段映射路径不能为空。')
  if (segments.some((segment) => segment === '__proto__' || segment === 'prototype' || segment === 'constructor'))
    throw new Error('字段映射路径包含危险原型字段。')
  let current = target
  for (let index = 0; index < segments.length - 1; index++) {
    const segment = segments[index]!
    const next = current[segment]
    if (next === undefined) {
      const created: Record<string, unknown> = {}
      current[segment] = created
      current = created
    } else if (next && typeof next === 'object' && !Array.isArray(next)) {
      current = next as Record<string, unknown>
    } else {
      throw new Error(`字段映射路径冲突：${path}`)
    }
  }
  const leaf = segments[segments.length - 1]!
  if (Object.hasOwn(current, leaf)) throw new Error(`字段映射路径重复：${path}`)
  current[leaf] = value
}
