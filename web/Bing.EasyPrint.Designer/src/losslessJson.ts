const numberPattern = /^-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$/

export class JsonNumber {
  readonly raw: string

  constructor(raw: string) {
    if (!numberPattern.test(raw)) throw new SyntaxError(`Invalid JSON number: ${raw}`)
    this.raw = raw
  }

  toJSON(): number {
    return Number(this.raw)
  }
}

export function isJsonNumber(value: unknown): value is JsonNumber {
  return value instanceof JsonNumber
}

export function parseJsonPreservingPrecision(text: string): unknown {
  const parser = new JsonParser(text)
  return parser.parse()
}

export function cloneJsonPreservingPrecision<T>(value: T): T {
  if (value instanceof JsonNumber) return new JsonNumber(value.raw) as T
  if (value instanceof Date) return new Date(value.getTime()) as T
  if (Array.isArray(value)) return value.map((item) => cloneJsonPreservingPrecision(item)) as T
  if (!value || typeof value !== 'object') return value
  if (ArrayBuffer.isView(value)) {
    const bytes = new Uint8Array(value.buffer.slice(value.byteOffset, value.byteOffset + value.byteLength))
    return bytes as T
  }
  const source = value as Record<string, unknown>
  const result: Record<string, unknown> = {}
  for (const key of Object.keys(source))
    Object.defineProperty(result, key, {
      value: cloneJsonPreservingPrecision(source[key]), enumerable: true, configurable: true, writable: true,
    })
  return result as T
}

export function stringifyJsonPreservingPrecision(value: unknown, space?: string | number): string {
  const gap = typeof space === 'number'
    ? ' '.repeat(Math.max(0, Math.min(10, Math.trunc(space))))
    : (space ?? '').slice(0, 10)
  return write(value, gap, '', new Set<object>()) ?? 'undefined'
}

class JsonParser {
  private index = 0

  constructor(private readonly text: string) {}

  parse(): unknown {
    const value = this.value()
    this.whitespace()
    if (this.index !== this.text.length) this.fail('Unexpected trailing content')
    return value
  }

  private value(): unknown {
    this.whitespace()
    const character = this.text[this.index]
    if (character === '{') return this.object()
    if (character === '[') return this.array()
    if (character === '"') return this.string()
    if (character === 't' && this.literal('true')) return true
    if (character === 'f' && this.literal('false')) return false
    if (character === 'n' && this.literal('null')) return null
    if (character === '-' || character !== undefined && character >= '0' && character <= '9')
      return this.number()
    this.fail('Expected a JSON value')
  }

  private object(): Record<string, unknown> {
    this.index++
    const result: Record<string, unknown> = {}
    this.whitespace()
    if (this.text[this.index] === '}') {
      this.index++
      return result
    }
    while (true) {
      this.whitespace()
      if (this.text[this.index] !== '"') this.fail('Expected an object key')
      const key = this.string()
      this.whitespace()
      if (this.text[this.index] !== ':') this.fail('Expected a colon')
      this.index++
      Object.defineProperty(result, key, {
        value: this.value(), enumerable: true, configurable: true, writable: true,
      })
      this.whitespace()
      const delimiter = this.text[this.index++]
      if (delimiter === '}') return result
      if (delimiter !== ',') this.fail('Expected a comma or closing brace')
    }
  }

  private array(): unknown[] {
    this.index++
    const result: unknown[] = []
    this.whitespace()
    if (this.text[this.index] === ']') {
      this.index++
      return result
    }
    while (true) {
      result.push(this.value())
      this.whitespace()
      const delimiter = this.text[this.index++]
      if (delimiter === ']') return result
      if (delimiter !== ',') this.fail('Expected a comma or closing bracket')
    }
  }

  private string(): string {
    const start = this.index
    this.index++
    while (this.index < this.text.length) {
      const character = this.text[this.index]
      if (character === '"') {
        this.index++
        try { return JSON.parse(this.text.slice(start, this.index)) as string }
        catch { this.fail('Invalid JSON string') }
      }
      if (character === '\\') {
        this.index++
        if (this.index >= this.text.length) this.fail('Unterminated JSON string')
        this.index++
        continue
      }
      const code = character!.charCodeAt(0)
      if (code < 0x20) this.fail('Unescaped control character in JSON string')
      this.index++
    }
    this.fail('Unterminated JSON string')
  }

  private number(): JsonNumber | number {
    const start = this.index
    if (this.text[this.index] === '-') this.index++
    if (this.text[this.index] === '0') this.index++
    else if (this.text[this.index] !== undefined && this.text[this.index]! >= '1' && this.text[this.index]! <= '9') {
      while (this.text[this.index] !== undefined && this.text[this.index]! >= '0' && this.text[this.index]! <= '9') this.index++
    } else this.fail('Invalid JSON number')
    if (this.text[this.index] === '.') {
      this.index++
      const fractionStart = this.index
      while (this.text[this.index] !== undefined && this.text[this.index]! >= '0' && this.text[this.index]! <= '9') this.index++
      if (this.index === fractionStart) this.fail('Invalid JSON number fraction')
    }
    if (this.text[this.index] === 'e' || this.text[this.index] === 'E') {
      this.index++
      if (this.text[this.index] === '+' || this.text[this.index] === '-') this.index++
      const exponentStart = this.index
      while (this.text[this.index] !== undefined && this.text[this.index]! >= '0' && this.text[this.index]! <= '9') this.index++
      if (this.index === exponentStart) this.fail('Invalid JSON number exponent')
    }
    const token = this.text.slice(start, this.index)
    return shouldPreserveNumber(token) ? new JsonNumber(token) : Number(token)
  }

  private literal(value: string): boolean {
    if (this.text.slice(this.index, this.index + value.length) !== value) return false
    this.index += value.length
    return true
  }

  private whitespace(): void {
    while (this.text[this.index] === ' ' || this.text[this.index] === '\t' ||
      this.text[this.index] === '\r' || this.text[this.index] === '\n') this.index++
  }

  private fail(message: string): never {
    throw new SyntaxError(`${message} at position ${this.index}`)
  }
}

function shouldPreserveNumber(token: string): boolean {
  const value = token.startsWith('-') ? token.slice(1) : token
  const exponentIndex = value.search(/[eE]/)
  const mantissa = exponentIndex < 0 ? value : value.slice(0, exponentIndex)
  const exponent = exponentIndex < 0 ? 0 : Number(value.slice(exponentIndex + 1))
  const digits = mantissa.replace('.', '').replace(/^0+/, '')
  if (!digits) return false
  const significant = mantissa.includes('.') ? digits.replace(/0+$/, '') : digits
  return significant.length > 15 || Math.abs(exponent) > 308
}

function write(value: unknown, gap: string, indent: string, stack: Set<object>): string | undefined {
  if (value instanceof JsonNumber) return value.raw
  if (value && typeof value === 'object' && typeof (value as { toJSON?: unknown }).toJSON === 'function') {
    value = (value as { toJSON: () => unknown }).toJSON()
  }
  if (value === null) return 'null'
  if (typeof value === 'string') return JSON.stringify(value)
  if (typeof value === 'boolean') return value ? 'true' : 'false'
  if (typeof value === 'number') return JSON.stringify(value)
  if (typeof value === 'bigint') throw new TypeError('Do not know how to serialize a BigInt')
  if (typeof value !== 'object') return undefined
  if (stack.has(value)) throw new TypeError('Converting circular structure to JSON')
  stack.add(value)
  try {
    const next = indent + gap
    if (Array.isArray(value)) {
      if (!value.length) return '[]'
      const entries = value.map((item) => write(item, gap, next, stack) ?? 'null')
      return gap ? `[\n${next}${entries.join(`,\n${next}`)}\n${indent}]` : `[${entries.join(',')}]`
    }
    const object = value as Record<string, unknown>
    const entries: string[] = []
    for (const key of Object.keys(object)) {
      const item = write(object[key], gap, next, stack)
      if (item !== undefined) entries.push(`${JSON.stringify(key)}:${gap ? ' ' : ''}${item}`)
    }
    if (!entries.length) return '{}'
    return gap ? `{\n${next}${entries.join(`,\n${next}`)}\n${indent}}` : `{${entries.join(',')}}`
  } finally {
    stack.delete(value)
  }
}
