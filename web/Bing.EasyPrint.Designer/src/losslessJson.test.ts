import { describe, expect, it } from 'vitest'
import {
  cloneJsonPreservingPrecision, isJsonNumber, parseJsonPreservingPrecision, stringifyJsonPreservingPrecision,
} from './losslessJson'

describe('lossless JSON numbers', () => {
  it('keeps high precision numbers as raw numbers and ordinary values as JSON types', () => {
    const value = parseJsonPreservingPrecision(
      '{"Value":123456789012345.6789,"Small":1.5,"Text":"123456789012345.6789","When":"2024-01-02T03:04:05-05:00"}',
    ) as Record<string, unknown>
    expect(isJsonNumber(value.Value)).toBe(true)
    expect(value.Small).toBe(1.5)
    expect(value.Text).toBe('123456789012345.6789')
    expect(value.When).toBe('2024-01-02T03:04:05-05:00')
    expect(stringifyJsonPreservingPrecision(value)).toBe(
      '{"Value":123456789012345.6789,"Small":1.5,"Text":"123456789012345.6789","When":"2024-01-02T03:04:05-05:00"}',
    )
  })

  it.each([
    '{"Value":1234567890123456e}',
    '{"Value":-1234567890123456.}',
    '{"Value":1.}',
    '{"Value":1e+}',
    '{"Value":.5}',
    '{"Value":01}',
  ])('rejects invalid JSON number grammar: %s', (text) => {
    expect(() => parseJsonPreservingPrecision(text)).toThrow(SyntaxError)
  })

  it('clones raw number wrappers without turning them into strings or plain objects', () => {
    const value = parseJsonPreservingPrecision('{"Value":123456789012345.6789}') as { Value: unknown }
    const clone = cloneJsonPreservingPrecision(value)
    expect(isJsonNumber(clone.Value)).toBe(true)
    expect(stringifyJsonPreservingPrecision(clone)).toBe('{"Value":123456789012345.6789}')
  })

  it('preserves nested paths and rejects every invalid numeric tail', () => {
    expect(stringifyJsonPreservingPrecision(parseJsonPreservingPrecision('[{"A":123456789012345.6789}]')))
      .toBe('[{"A":123456789012345.6789}]')
    expect(() => parseJsonPreservingPrecision('1234567890123456e')).toThrow(SyntaxError)
    expect(() => parseJsonPreservingPrecision('-1234567890123456.')).toThrow(SyntaxError)
  })
})
