import { describe, expect, it } from 'vitest'
import type { BpTemplate, PreviewPage } from './model'
import {
  findDiagnosticPage,
  formatDesignerDiagnostic,
  normalizeDesignerDiagnostics,
  resolveDiagnosticTarget,
} from './diagnostics'
import { listTemplateTree } from './tree'

function createSharedTemplate(): BpTemplate {
  return {
    Version: 3,
    Width: 240,
    Height: 120,
    Components: [{
      Id: 'badge',
      Width: 80,
      Height: 24,
      Elements: [{ Type: 6, Elements: [{ Type: 1, Field: { Value: 'shared' } }] }],
    }],
    Rows: [{ Cells: [{ Elements: [
      { Type: 6, ComponentId: 'badge' },
      { Type: 6, ComponentId: 'badge' },
    ] }] }],
  }
}

function expandedSharedEntries(template: BpTemplate) {
  return listTemplateTree(template).filter((entry) => entry.sourcePath === 'Components[0].Elements[0].Elements[0]'
    && entry.path !== entry.sourcePath)
}

describe('designer diagnostics', () => {
  it('normalizes messages and snapshots valid metadata', () => {
    const indices = [1, 3]
    const result = normalizeDesignerDiagnostics([
      { message: '  原因  ', sourcePath: 'Rows[0]', definitionPath: null, instanceIndices: indices, pageIndex: 2, elementIndex: 0 },
      { message: '', sourcePath: 'Rows[1]' },
      { message: '坏元数据', sourcePath: 1, instanceIndices: [0, -1], pageIndex: 1.5, elementIndex: -1 },
      { message: '保留', componentInstancePath: null },
    ])

    indices[0] = 99
    expect(result).toEqual([
      { message: '  原因  ', sourcePath: 'Rows[0]', definitionPath: null, instanceIndices: [1, 3], pageIndex: 2, elementIndex: 0 },
      { message: '坏元数据' },
      { message: '保留', componentInstancePath: null },
    ])
  })

  it('preserves stable error metadata and rejects invalid resource counters', () => {
    const approximate = Number.MAX_SAFE_INTEGER + 2
    expect(normalizeDesignerDiagnostics([
      {
        message: '预算不足',
        code: 'ResourceLimitExceeded',
        reason: null,
        phase: 'Response',
        fieldPath: 'Order.Total',
        resourceName: 'Host.ZipOutput',
        requestedBytes: approximate,
        currentBytes: 0,
        maxBytes: null,
      },
      {
        message: '坏计数',
        code: 1,
        reason: '',
        phase: false,
        fieldPath: 0,
        resourceName: '',
        requestedBytes: Number.NaN,
        currentBytes: Number.POSITIVE_INFINITY,
        maxBytes: -1.5,
      },
      {
        message: '拒绝字符串计数',
        requestedBytes: '1024',
        currentBytes: true,
        maxBytes: {},
      },
    ])).toEqual([
      {
        message: '预算不足',
        code: 'ResourceLimitExceeded',
        reason: null,
        phase: 'Response',
        fieldPath: 'Order.Total',
        resourceName: 'Host.ZipOutput',
        requestedBytes: approximate,
        currentBytes: 0,
        maxBytes: null,
      },
      { message: '坏计数' },
      { message: '拒绝字符串计数' },
    ])
  })

  it('normalizes image pixel metadata without turning missing dimensions into zero', () => {
    expect(normalizeDesignerDiagnostics([{
      message: '裁切区域超出源图',
      resourceName: 'photo',
      sourcePixelWidth: 640,
      sourcePixelHeight: 480,
      renderedPixelWidth: null,
      renderedPixelHeight: null,
      cropX: 600,
      cropY: 10,
      cropWidth: 80,
      cropHeight: 80,
    }, {
      message: '损坏资源',
      resourceName: 'broken',
      sourcePixelWidth: '640',
      sourcePixelHeight: -1,
      cropX: Number.NaN,
    }])).toEqual([{
      message: '裁切区域超出源图',
      resourceName: 'photo',
      sourcePixelWidth: 640,
      sourcePixelHeight: 480,
      renderedPixelWidth: null,
      renderedPixelHeight: null,
      cropX: 600,
      cropY: 10,
      cropWidth: 80,
      cropHeight: 80,
    }, {
      message: '损坏资源',
      resourceName: 'broken',
    }])
  })

  it('formats paths, repeated items and page numbers as one-based Chinese details', () => {
    expect(formatDesignerDiagnostic({
      message: '字体不支持',
      sourcePath: 'Rows[0].Cells[0].Elements[0]',
      definitionPath: 'Components[0].Elements[0]',
      componentInstancePath: 'Rows[0].Cells[0].Elements[1]',
      instanceIndices: [2, 4],
      pageIndex: 2,
    })).toBe('Rows[0].Cells[0].Elements[0]: 字体不支持 · 定义：Components[0].Elements[0] · 实例：Rows[0].Cells[0].Elements[1] · 重复项：第 3 项 / 第 5 项 · 页号：第 3 页')
  })

  it('formats business field and resource metadata without turning null into zero', () => {
    const approximate = Number.MAX_SAFE_INTEGER + 2
    const formatted = formatDesignerDiagnostic({
      message: '预算不足',
      code: 'ResourceLimitExceeded',
      reason: 'Budget',
      phase: 'Response',
      fieldPath: 'Order.Total',
      resourceName: 'Host.ZipOutput',
      requestedBytes: null,
      currentBytes: approximate,
      maxBytes: null,
    })

    expect(formatted).toBe('预算不足 · 代码：ResourceLimitExceeded · 原因：Budget · 阶段：Response · 字段：Order.Total · 资源：Host.ZipOutput · 当前：约 9007199254740992 字节')
    expect(formatted).not.toContain('申请：0 字节')
    expect(formatted).not.toContain('上限：0 字节')
  })

  it('formats image source, crop and rendered pixel dimensions', () => {
    expect(formatDesignerDiagnostic({
      message: '裁切区域超出源图',
      resourceName: 'photo',
      sourcePixelWidth: 640,
      sourcePixelHeight: 480,
      cropX: 600,
      cropY: 10,
      cropWidth: 80,
      cropHeight: 80,
      renderedPixelWidth: 160,
      renderedPixelHeight: 160,
    })).toBe('裁切区域超出源图 · 资源：photo · 源图：640×480 像素 · 裁切：600,10 80×80 像素 · 输出：160×160 像素')
  })

  it('resolves the second shared reference and preserves its occurrence', () => {
    const template = createSharedTemplate()
    const entries = expandedSharedEntries(template)
    expect(entries).toHaveLength(2)
    const second = entries[1]
    expect(resolveDiagnosticTarget(template, {
      message: '错误',
      sourcePath: second.sourcePath,
      definitionPath: second.sourcePath,
      componentInstancePath: 'Rows[0].Cells[0].Elements[1]',
    })).toEqual({ path: second.sourcePath, occurrencePath: second.path })
  })

  it('keeps static definition diagnostics on the definition instead of choosing a reference', () => {
    const template = createSharedTemplate()
    expect(resolveDiagnosticTarget(template, {
      message: '定义错误', sourcePath: 'Components[0].Elements[0].Elements[0]',
    })).toEqual({ path: 'Components[0].Elements[0].Elements[0]', occurrencePath: 'Components[0].Elements[0].Elements[0]' })
    expect(resolveDiagnosticTarget(template, {
      message: '定义字段错误', definitionPath: 'Components[0].Elements[0].Elements[0].Field.Value',
    }, 'definition')).toEqual({ path: 'Components[0].Elements[0].Elements[0]', occurrencePath: 'Components[0].Elements[0].Elements[0]' })
  })

  it('uses the nearest existing ancestor for field paths and falls back to instance then definition', () => {
    const template = createSharedTemplate()
    const second = expandedSharedEntries(template)[1]
    expect(resolveDiagnosticTarget(template, {
      message: '字段错误', sourcePath: `${second.path}.Field.Value`,
    })).toEqual({ path: second.sourcePath, occurrencePath: second.path })
    expect(resolveDiagnosticTarget(template, {
      message: '实例错误', sourcePath: 'Rows[9].Cells[9].Elements[9].Field.Value',
      componentInstancePath: 'Rows[0].Cells[0].Elements[1]',
    })).toEqual({ path: 'Rows[0].Cells[0].Elements[1]', occurrencePath: 'Rows[0].Cells[0].Elements[1]' })
    expect(resolveDiagnosticTarget(template, {
      message: '定义错误', sourcePath: 'Rows[9].Field.Value', definitionPath: 'Components[0].Elements[0].Elements[0].Field.Value',
    })).toEqual({ path: 'Components[0].Elements[0].Elements[0]', occurrencePath: 'Components[0].Elements[0].Elements[0]' })
  })

  it('keeps definition and instance modes scoped to their corresponding path', () => {
    const template = createSharedTemplate()
    expect(resolveDiagnosticTarget(template, {
      message: '仅定义', sourcePath: 'Rows[0].Cells[0].Elements[1]', definitionPath: 'Components[0].Elements[0]',
    }, 'definition')).toEqual({ path: 'Components[0].Elements[0]', occurrencePath: 'Components[0].Elements[0]' })
    expect(resolveDiagnosticTarget(template, {
      message: '仅实例', sourcePath: 'Components[0].Elements[0]', componentInstancePath: 'Rows[0].Cells[0].Elements[1]',
    }, 'instance')).toEqual({ path: 'Rows[0].Cells[0].Elements[1]', occurrencePath: 'Rows[0].Cells[0].Elements[1]' })
  })

  it('finds only an exact, uniquely provable preview page', () => {
    const pages: PreviewPage[] = [
      { width: 240, height: 120, elements: [
        { sourcePath: 'Rows[0]', instanceIndices: [0], type: 0, x: 0, y: 0, width: 240, height: 20, kind: 'row' },
        { sourcePath: 'Rows[0].Cells[0]', instanceIndices: [0], type: 0, x: 0, y: 0, width: 240, height: 20, kind: 'cell' },
        { sourcePath: 'Rows[0].Cells[0].Elements[0]', instanceIndices: [0], type: 1, x: 0, y: 0, width: 20, height: 10 },
      ] },
      { width: 240, height: 120, elements: [{ sourcePath: 'Rows[0].Cells[0].Elements[0]', instanceIndices: [1], type: 1, x: 0, y: 0, width: 20, height: 10 }] },
    ]
    expect(findDiagnosticPage(pages, { message: '第二项', sourcePath: 'Rows[0].Cells[0].Elements[0]', instanceIndices: [1] })).toBe(1)
    expect(findDiagnosticPage(pages, { message: '未知项', sourcePath: 'Rows[0].Cells[0].Elements[0]' })).toBeUndefined()
    expect(findDiagnosticPage(pages, { message: '指定页', sourcePath: 'does.not.exist', pageIndex: 1 })).toBe(1)
    expect(findDiagnosticPage(pages, { message: '字段', sourcePath: 'Rows[0].Cells[0].Elements[0].Field.Value', instanceIndices: [1] })).toBeUndefined()
    expect(findDiagnosticPage(pages, { message: '命令索引仅供展示', sourcePath: 'Rows[0].Cells[0].Elements[0]', elementIndex: 0, instanceIndices: [0] })).toBe(0)
  })

  it('requires complete nested repeat indices before locating a page', () => {
    const pages: PreviewPage[] = [{ width: 240, height: 120, elements: [
      { sourcePath: 'Rows[0].Cells[0].Elements[0]', instanceIndices: [2, 5], type: 1, x: 0, y: 0, width: 20, height: 10 },
      { sourcePath: 'Rows[0].Cells[0].Elements[0]', instanceIndices: [2, 6], type: 1, x: 0, y: 0, width: 20, height: 10 },
    ] }]
    expect(findDiagnosticPage(pages, { message: '嵌套项', sourcePath: 'Rows[0].Cells[0].Elements[0]', instanceIndices: [2, 6] })).toBe(0)
    expect(findDiagnosticPage(pages, { message: '不完整索引', sourcePath: 'Rows[0].Cells[0].Elements[0]', instanceIndices: [2] })).toBeUndefined()
  })

  it('does not guess a repeated reference for a static definition page match', () => {
    const pages: PreviewPage[] = [{ width: 240, height: 120, elements: [
      { sourcePath: 'Components[0].Elements[0]', definitionPath: 'Components[0].Elements[0]', componentInstancePath: 'Rows[0].Cells[0].Elements[0]', instanceIndices: [0], type: 1, x: 0, y: 0, width: 20, height: 10 },
      { sourcePath: 'Components[0].Elements[0]', definitionPath: 'Components[0].Elements[0]', componentInstancePath: 'Rows[0].Cells[0].Elements[1]', instanceIndices: [1], type: 1, x: 0, y: 0, width: 20, height: 10 },
    ] }]
    expect(findDiagnosticPage(pages, { message: '定义错误', definitionPath: 'Components[0].Elements[0]' })).toBeUndefined()
    expect(findDiagnosticPage(pages, { message: '实例错误', componentInstancePath: 'Rows[0].Cells[0].Elements[1]', instanceIndices: [1] })).toBe(0)
  })

  it('uses source path before definition path and keeps both children on one instance distinct', () => {
    const pages: PreviewPage[] = [{ width: 240, height: 120, elements: [
      { sourcePath: 'Rows[0].Cells[0].Elements[0].Elements[0]', definitionPath: 'Components[0].Elements[0]', componentInstancePath: 'Rows[0].Cells[0].Elements[0]', instanceIndices: [], type: 1, x: 0, y: 0, width: 20, height: 10 },
      { sourcePath: 'Rows[0].Cells[0].Elements[0].Elements[1]', definitionPath: 'Components[0].Elements[0]', componentInstancePath: 'Rows[0].Cells[0].Elements[0]', instanceIndices: [], type: 1, x: 0, y: 0, width: 20, height: 10 },
    ] }]
    expect(findDiagnosticPage(pages, {
      message: '第一个子元素', sourcePath: 'Rows[0].Cells[0].Elements[0].Elements[0]',
      definitionPath: 'Components[0].Elements[0]', componentInstancePath: 'Rows[0].Cells[0].Elements[0]', instanceIndices: [],
    })).toBe(0)
  })
})
