export type TextTableDelimiter = 'auto' | ',' | '\t'

export interface ParseTextTableOptions {
  delimiter?: TextTableDelimiter
  firstRowHeader?: boolean
}

export interface ParsedTextTable {
  delimiter: ',' | '\t'
  headers: string[]
  rows: string[][]
  hasHeader: boolean
}

export const MAX_TEXT_TABLE_BYTES = 1024 * 1024
export const MAX_TEXT_TABLE_ROWS = 10_000
export const MAX_TEXT_TABLE_COLUMNS = 256

type Delimiter = ',' | '\t'

interface Candidate {
  delimiter: Delimiter
  records: string[][]
}

function countDelimiters(text: string): Record<Delimiter, number> {
  let commas = 0
  let tabs = 0
  let inQuotes = false

  for (let index = 0; index < text.length; index += 1) {
    const character = text[index]
    if (character === '"') {
      if (inQuotes && text[index + 1] === '"') {
        index += 1
      } else {
        inQuotes = !inQuotes
      }
    } else if (!inQuotes && character === ',') {
      commas += 1
    } else if (!inQuotes && character === '\t') {
      tabs += 1
    }
  }

  return { ',': commas, '\t': tabs }
}

function parseRecords(text: string, delimiter: Delimiter): string[][] {
  const records: string[][] = []
  let record: string[] = []
  let field = ''
  let inQuotes = false
  let afterQuote = false

  const location = (): string => `${records.length + 1}行第${record.length + 1}列`

  const pushField = (): void => {
    if (record.length >= MAX_TEXT_TABLE_COLUMNS) {
      throw new Error(`每行最多支持 ${MAX_TEXT_TABLE_COLUMNS} 列（${location()}）`)
    }
    record.push(field)
    field = ''
    afterQuote = false
  }

  const pushRecord = (): void => {
    pushField()
    if (records.length >= MAX_TEXT_TABLE_ROWS) {
      throw new Error(`粘贴内容最多支持 ${MAX_TEXT_TABLE_ROWS} 行`)
    }
    records.push(record)
    record = []
  }

  for (let index = 0; index < text.length;) {
    const character = text[index]

    if (inQuotes) {
      if (character === '"') {
        if (text[index + 1] === '"') {
          field += '"'
          index += 2
          continue
        }
        inQuotes = false
        afterQuote = true
        index += 1
        continue
      }

      if (character === '\r' && text[index + 1] === '\n') {
        field += '\r\n'
        index += 2
      } else {
        field += character
        index += 1
      }
      continue
    }

    if (afterQuote) {
      if (character === delimiter) {
        pushField()
        index += 1
        continue
      }
      if (character === '\r' || character === '\n') {
        pushRecord()
        index += character === '\r' && text[index + 1] === '\n' ? 2 : 1
        continue
      }
      throw new Error(`${location()}的闭合引号后只能是分隔符或换行`)
    }

    if (character === '"') {
      if (field.length !== 0) {
        throw new Error(`${location()}包含未转义的双引号`)
      }
      inQuotes = true
      index += 1
      continue
    }
    if (character === delimiter) {
      pushField()
      index += 1
      continue
    }
    if (character === '\r' || character === '\n') {
      pushRecord()
      index += character === '\r' && text[index + 1] === '\n' ? 2 : 1
      continue
    }

    field += character
    index += 1
  }

  if (inQuotes) {
    throw new Error(`${location()}缺少结束引号`)
  }
  if (afterQuote || field.length > 0 || record.length > 0) {
    pushRecord()
  }

  return records
}

function candidateQuality(candidate: Candidate): readonly [number, number, number, number] {
  const widths = new Map<number, number>()
  for (const record of candidate.records) {
    widths.set(record.length, (widths.get(record.length) ?? 0) + 1)
  }

  const modalCount = Math.max(...widths.values())
  const multiColumnRecords = candidate.records.filter((record) => record.length > 1).length
  return [
    modalCount === candidate.records.length ? 1 : 0,
    modalCount / candidate.records.length,
    multiColumnRecords,
    candidate.delimiter === '\t' ? 1 : 0,
  ]
}

function isBetterCandidate(left: Candidate, right: Candidate): boolean {
  const leftQuality = candidateQuality(left)
  const rightQuality = candidateQuality(right)
  for (let index = 0; index < leftQuality.length; index += 1) {
    if (leftQuality[index] !== rightQuality[index]) {
      return leftQuality[index] > rightQuality[index]
    }
  }
  return false
}

function detectDelimiter(text: string): Delimiter {
  const counts = countDelimiters(text)
  const candidates: Delimiter[] = []
  if (counts['\t'] > 0) candidates.push('\t')
  if (counts[','] > 0) candidates.push(',')
  if (candidates.length === 0) return ','

  const parsed: Candidate[] = []
  const errors: Array<{ delimiter: Delimiter; error: unknown }> = []
  for (const delimiter of candidates) {
    try {
      parsed.push({ delimiter, records: parseRecords(text, delimiter) })
    } catch (error) {
      errors.push({ delimiter, error })
    }
  }

  if (parsed.length === 0) {
    const tabError = errors.find((candidate) => candidate.delimiter === '\t')
    const preferredError = counts['\t'] > 0 ? tabError : errors[0]
    throw preferredError?.error ?? new Error('无法识别表格分隔符')
  }

  return parsed.reduce((best, candidate) => isBetterCandidate(candidate, best) ? candidate : best).delimiter
}

/**
 * 解析用户粘贴的 CSV 或 TSV 文本，不会修改输入。
 * 自动识别比较候选格式的矩形度；同样匹配时优先使用 TSV。
 */
export function parseTextTable(input: string, options: ParseTextTableOptions = {}): ParsedTextTable {
  if (typeof input !== 'string') {
    throw new Error('表格内容必须是文本')
  }
  if (input.length > MAX_TEXT_TABLE_BYTES) {
    throw new Error(`粘贴内容超过 ${MAX_TEXT_TABLE_BYTES} 字节上限`)
  }
  if (new TextEncoder().encode(input).byteLength > MAX_TEXT_TABLE_BYTES) {
    throw new Error(`粘贴内容超过 ${MAX_TEXT_TABLE_BYTES} 字节上限`)
  }

  const text = input.startsWith('\uFEFF') ? input.slice(1) : input
  if (text.length === 0) {
    throw new Error('请先粘贴表格内容')
  }

  const requestedDelimiter = options.delimiter ?? 'auto'
  if (requestedDelimiter !== 'auto' && requestedDelimiter !== ',' && requestedDelimiter !== '\t') {
    throw new Error('分隔符仅支持自动识别、逗号或制表符')
  }

  const delimiter = requestedDelimiter === 'auto' ? detectDelimiter(text) : requestedDelimiter
  const records = parseRecords(text, delimiter)

  const width = records[0]?.length ?? 0
  if (width === 0) {
    throw new Error('表格至少需要一列')
  }
  const raggedIndex = records.findIndex((record) => record.length !== width)
  if (raggedIndex >= 0) {
    throw new Error(`第${raggedIndex + 1}行有 ${records[raggedIndex].length} 列，预期 ${width} 列；请补齐或删除缺失单元格`)
  }

  const hasHeader = options.firstRowHeader ?? false
  const headers = hasHeader
    ? records[0]
    : Array.from({ length: width }, (_, index) => `列 ${index + 1}`)

  return {
    delimiter,
    headers: [...headers],
    rows: (hasHeader ? records.slice(1) : records).map((record) => [...record]),
    hasHeader,
  }
}
