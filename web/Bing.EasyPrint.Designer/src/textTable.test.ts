import { describe, expect, it } from 'vitest'
import {
  MAX_TEXT_TABLE_BYTES,
  MAX_TEXT_TABLE_COLUMNS,
  MAX_TEXT_TABLE_ROWS,
  parseTextTable,
} from './textTable'

describe('parseTextTable', () => {
  it('parses CSV quoting, escaped quotes, embedded line breaks, and empty cells', () => {
    const result = parseTextTable('Name,Note,Empty\r\nAda,"said ""hello""\r\nagain",\r\nLin,"line 1\nline 2",x', {
      firstRowHeader: true,
    })

    expect(result).toEqual({
      delimiter: ',',
      headers: ['Name', 'Note', 'Empty'],
      rows: [
        ['Ada', 'said "hello"\r\nagain', ''],
        ['Lin', 'line 1\nline 2', 'x'],
      ],
      hasHeader: true,
    })
  })

  it('detects pasted TSV even when cell values contain commas', () => {
    const result = parseTextTable('Name\tAddress\tPrice\nTea, green\t"Shop, North"\t1.25\nMilk\tShop South\t2.00')

    expect(result.delimiter).toBe('\t')
    expect(result.headers).toEqual(['列 1', '列 2', '列 3'])
    expect(result.rows).toEqual([
      ['Name', 'Address', 'Price'],
      ['Tea, green', 'Shop, North', '1.25'],
      ['Milk', 'Shop South', '2.00'],
    ])
    expect(result.hasHeader).toBe(false)
  })

  it('uses the first row as headers only when requested and supports a BOM', () => {
    const result = parseTextTable('\uFEFFField,Value\na,1', { firstRowHeader: true })

    expect(result.headers).toEqual(['Field', 'Value'])
    expect(result.rows).toEqual([['a', '1']])
    expect(result.hasHeader).toBe(true)
  })

  it('allows callers to force a delimiter', () => {
    const result = parseTextTable('one,two\tthree,four', { delimiter: '\t' })

    expect(result.delimiter).toBe('\t')
    expect(result.rows).toEqual([['one,two', 'three,four']])
  })

  it('rejects non-rectangular input with a row-specific message', () => {
    expect(() => parseTextTable('a,b\nc', { delimiter: ',' })).toThrow('第2行有 1 列，预期 2 列')
  })

  it.each([
    ['a,"b', '缺少结束引号'],
    ['a,b"c', '未转义的双引号'],
    ['"a" x,b', '闭合引号后只能是分隔符或换行'],
  ])('rejects malformed CSV %j', (input, message) => {
    expect(() => parseTextTable(input, { delimiter: ',' })).toThrow(message)
  })

  it('rejects empty text and input over the UTF-8 byte limit', () => {
    expect(() => parseTextTable('')).toThrow('请先粘贴表格内容')
    expect(() => parseTextTable('中'.repeat(Math.floor(MAX_TEXT_TABLE_BYTES / 2) + 1))).toThrow('字节上限')
  })

  it('rejects tables over the row and column limits', () => {
    const tooManyRows = Array.from({ length: MAX_TEXT_TABLE_ROWS + 1 }, () => 'x').join('\n')
    const tooManyColumns = Array.from({ length: MAX_TEXT_TABLE_COLUMNS + 1 }, () => 'x').join(',')

    expect(() => parseTextTable(tooManyRows, { delimiter: '\t' })).toThrow(`${MAX_TEXT_TABLE_ROWS} 行`)
    expect(() => parseTextTable(tooManyColumns, { delimiter: ',' })).toThrow(`${MAX_TEXT_TABLE_COLUMNS} 列`)
  })
})
