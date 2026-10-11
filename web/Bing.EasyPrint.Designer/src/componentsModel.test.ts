import { describe, expect, it } from 'vitest'
import { appendComponent, appendDataTable, appendStaticTable, componentLibraryTargetVersion, defaultGroupPagination, detachComponent, groupCellElements, groupConsecutiveRows, removeComponent, renameComponent, saveGroupComponent, ungroupElement } from './componentsModel'
import { resolvePath } from './binding'
import { findElement, findRow, listTemplateTree } from './tree'
import { assertTemplate, type BpTemplate } from './model'

function template(): BpTemplate {
  return { Version: 1, Width: 300, Height: 180, Rows: [{ Height: 180, Cells: [{ Width: 300, Elements: [
    { Type: 1, FontSize: 24, Field: { Value: 'A' }, StartCoordinate: { X: 20, Y: 20 }, EndCoordinate: { X: 80, Y: 50 } },
    { Type: 7, Width: 50, Height: 30, StartCoordinate: { X: 100, Y: 20 } },
  ] }] }] }
}

describe('V2 composition', () => {
  it('groups elements with relative coordinates and reuses a named definition', () => {
    const value = template()
    const groupPath = groupCellElements(value, 'Rows[0].Cells[0]')
    expect(value.Version).toBe(2)
    const group = findElement(value, groupPath)
    expect(group?.Type).toBe(6)
    if (group?.Type !== 6) return
    expect(group.StartCoordinate).toEqual({ X: 20, Y: 20 })
    expect(group.Elements?.[0].StartCoordinate).toEqual({ X: 0, Y: 0 })
    saveGroupComponent(value, groupPath, 'badge')
    expect(group.ComponentId).toBe('badge')
    expect(group.Elements).toBeUndefined()
    expect(findElement(value, `${groupPath}.Elements[0]`)?.Type).toBe(1)
    const second = appendComponent(value, 'Rows[0].Cells[0]', 'badge')
    expect(findElement(value, second)?.Type).toBe(6)
    expect(listTemplateTree(value).filter((entry) => entry.kind === 'element')).toHaveLength(8)
  })

  it('keeps V3 context and declares supplied parameters when saving a group component', () => {
    const value = template()
    const path = groupCellElements(value, 'Rows[0].Cells[0]', [0])
    const group = findElement(value, path)
    if (group?.Type !== 6) throw new Error('组合不存在。')
    group.DataPath = 'Product'
    group.Parameters = { caption: { Path: 'Name' } }
    group.Elements![0].Field = { Name: '$param.caption' }
    saveGroupComponent(value, path, 'productName')

    expect(value.Version).toBe(3)
    expect(group).toMatchObject({ ComponentId: 'productName', Parameters: { caption: { Path: 'Name' } } })
    expect(value.Components?.[0].Parameters).toEqual({ caption: { Required: true } })
  })

  it('initializes inserted component instances with defaults and explicit required values', () => {
    const value = template()
    value.Version = 3
    value.Components = [{ Id: 'badge', Width: 100, Height: 40, Elements: [], Parameters: {
      currency: { Required: true }, unit: { Required: true, DefaultValue: 'kg' }, optional: { DefaultValue: 'x' },
    } }]
    const path = appendComponent(value, 'Rows[0].Cells[0]', 'badge')
    const instance = findElement(value, path)
    expect(instance?.Type).toBe(6)
    expect(instance).toMatchObject({ Parameters: {
      currency: { Value: '' }, unit: { Value: 'kg' }, optional: { Value: 'x' },
    } })
    expect(() => assertTemplate(value)).not.toThrow()
  })

  it('rejects inserting a component that would create a definition cycle', () => {
    const value: BpTemplate = {
      Version: 3,
      Width: 200,
      Height: 120,
      Rows: [],
      Components: [
        { Id: 'outer', Width: 160, Height: 60, Rows: [{ Cells: [{ Elements: [] }] }] },
        { Id: 'inner', Width: 160, Height: 60, Elements: [{ Type: 6, ComponentId: 'outer' }] },
      ],
    }

    expect(() => appendComponent(value, 'Components[0].Rows[0].Cells[0]', 'inner')).toThrow('循环引用')
  })

  it('upgrades imported component libraries for declared and nested V3 features', () => {
    const automatic = [{ Id: 'automatic', Width: 0, Height: 0, Elements: [] }]
    const automaticVersion = componentLibraryTargetVersion(1, 3, automatic)
    expect(automaticVersion).toBe(3)
    expect(() => assertTemplate({ Version: automaticVersion, Width: 100, Height: 100, Rows: [], Components: automatic })).not.toThrow()

    const nested = [{ Id: 'nested', Width: 100, Height: 60,
      Elements: [{ Type: 6 as const, DataPath: 'Product', Rows: [] }] }]
    expect(componentLibraryTargetVersion(1, 2, nested)).toBe(3)
    expect(componentLibraryTargetVersion(1, 2, [{ Id: 'legacy', Width: 100, Height: 60, Elements: [] }])).toBe(2)
  })

  it('creates repeatable and pasted tables as editable rows', () => {
    const value = template()
    appendDataTable(value, 'Items', [{ title: 'Name', path: 'Name' }, { title: 'Qty', path: 'Qty' }])
    expect(value.Rows.at(-2)?.Section).toBe('TableHeader')
    expect(value.Rows.at(-1)?.RepeatSource).toBe('Items')
    appendStaticTable(value, ['Code', 'Name'], [['1', 'Tea']])
    expect(value.Rows.at(-1)?.Cells?.[1].Elements?.[0].Field?.Value).toBe('Tea')
  })

  it('resolves declared variables against root data', () => {
    expect(resolvePath('$var.shop', { root: { Store: { Name: 'North' } }, item: { Name: 'Tea' }, index: 2,
      variables: { shop: 'Store.Name' } })).toBe('North')
    expect(() => resolvePath('$var.missing', { root: {}, item: {}, index: 0, variables: {} })).toThrow('变量不存在')
  })

  it('groups only selected elements and detaches instances before independent edits', () => {
    const value = template()
    const path = groupCellElements(value, 'Rows[0].Cells[0]', [1])
    expect(value.Rows[0].Cells?.[0].Elements).toHaveLength(2)
    saveGroupComponent(value, path, 'badge')
    const second = appendComponent(value, 'Rows[0].Cells[0]', 'badge')
    detachComponent(value, second)
    const detached = findElement(value, second)
    expect(detached?.Type).toBe(6)
    if (detached?.Type !== 6) return
    detached.Elements![0].StartCoordinate!.X = 12
    expect(value.Components![0].Elements![0].StartCoordinate?.X).toBe(0)
    renameComponent(value, 'badge', 'renamed')
    expect(() => removeComponent(value, 'renamed')).toThrow('仍被模板引用')
    ungroupElement(value, path)
    removeComponent(value, 'renamed')
    expect(value.Components).toHaveLength(0)
  })

  it('uses rendered bounds for automatic text and preserves group conditions when ungrouping', () => {
    const value: BpTemplate = { Version: 2, Width: 120, Height: 120, Rows: [{ Cells: [{ Width: 120,
      Elements: [{ Type: 1, FontSize: 24, Field: { Value: 'FIRST' } },
        { Type: 1, FontSize: 24, Field: { Value: 'SECOND' } }] }] }] }
    const measured = new Map([
      [0, { left: 0, top: 0, right: 60, bottom: 24 }],
      [1, { left: 0, top: 24, right: 72, bottom: 48 }],
    ])
    const path = groupCellElements(value, 'Rows[0].Cells[0]', undefined, measured, 120)
    const group = findElement(value, path)
    expect(group).toMatchObject({ Type: 6, Width: 120, Height: 48 })
    if (group?.Type !== 6) return
    expect(group.Elements?.map((child) => child.StartCoordinate?.Y)).toEqual([0, 24])
    group.Condition = { Path: 'Show', Operator: 'eq', Value: true }
    group.Elements![1].Condition = { Path: 'Name', Operator: 'notNull' }
    ungroupElement(value, path)
    const children = value.Rows[0].Cells![0].Elements!
    expect(children[0].Condition).toEqual(group.Condition)
    expect(children[1].Condition?.Operator).toBe('and')
    expect(children[1].Condition?.Conditions).toHaveLength(2)
  })

  it('keeps inherited dimensions when detaching and supports nested ungrouping', () => {
    const value = template()
    const path = groupCellElements(value, 'Rows[0].Cells[0]')
    saveGroupComponent(value, path, 'outer')
    const group = findElement(value, path)
    if (group?.Type !== 6) return
    group.Width = 0
    group.Height = 0
    detachComponent(value, path)
    expect(group.Width).toBe(value.Components![0].Width)
    expect(group.Height).toBe(value.Components![0].Height)
    group.Elements = [{ Type: 6, Width: 30, Height: 30, StartCoordinate: { X: 0, Y: 0 },
      Elements: [{ Type: 7, Width: 20, Height: 20, StartCoordinate: { X: 0, Y: 0 } }] }]
    ungroupElement(value, `${path}.Elements[0]`)
    expect(findElement(value, `${path}.Elements[0]`)?.Type).toBe(7)
  })

  it('does not flatten automatic group children onto the same coordinate', () => {
    const value: BpTemplate = { Version: 2, Width: 200, Height: 200, Rows: [{ Cells: [{ Elements: [
      { Type: 6, Width: 100, Height: 80, StartCoordinate: { X: 20, Y: 20 }, Elements: [
        { Type: 1, FontSize: 24, Field: { Value: 'ONE' } },
        { Type: 1, FontSize: 24, Field: { Value: 'TWO' } },
      ] },
    ] }] }] }
    const path = 'Rows[0].Cells[0].Elements[0]'
    expect(() => ungroupElement(value, path)).toThrow('预览')
    ungroupElement(value, path, new Map([
      [0, { left: 0, top: 0, right: 48, bottom: 24 }],
      [1, { left: 0, top: 24, right: 48, bottom: 48 }],
    ]))
    const children = value.Rows[0].Cells![0].Elements!
    expect(children.map((child) => child.StartCoordinate)).toEqual([{ X: 20, Y: 20 }, { X: 20, Y: 44 }])
    expect(children.map((child) => child.EndCoordinate?.X)).toEqual([120, 120])
    expect(children[1].Margin?.Bottom).toBe(32)
  })

  it('refuses to ungroup a context-bound component without mutating the template', () => {
    const value: BpTemplate = { Version: 3, Width: 200, Height: 100, Rows: [{ Cells: [{ Elements: [
      { Type: 6, Width: 100, Height: 40, StartCoordinate: { X: 10, Y: 12 }, DataPath: 'Product',
        Parameters: { caption: { Path: 'Name' } },
        Elements: [{ Type: 1, Field: { Name: '$param.caption' }, StartCoordinate: { X: 0, Y: 0 } }] },
    ] }] }] }
    const before = JSON.stringify(value)

    expect(() => ungroupElement(value, 'Rows[0].Cells[0].Elements[0]')).toThrow('无法安全拆组')
    expect(JSON.stringify(value)).toBe(before)
  })

  it('retains the original available width when grouping centered automatic text', () => {
    const value: BpTemplate = { Version: 2, Width: 200, Height: 100, Rows: [{ Cells: [{ Width: 200,
      Elements: [{ Type: 1, FontSize: 24, TextAlignment: 1, Field: { Value: 'ONE' } },
        { Type: 1, FontSize: 24, Field: { Value: 'TWO' } }] }] }] }
    const groupPath = groupCellElements(value, 'Rows[0].Cells[0]', undefined, new Map([
      [0, { left: 82, top: 0, right: 118, bottom: 24 }],
      [1, { left: 0, top: 24, right: 48, bottom: 48 }],
    ]), 200)
    const group = findElement(value, groupPath)
    expect(group?.Type).toBe(6)
    if (group?.Type !== 6) return
    expect(group.StartCoordinate?.X).toBe(0)
    expect(group.Width).toBe(200)
    expect(group.Elements?.[0].StartCoordinate?.X).toBe(0)
    expect(group.Elements?.[0].Type === 1 && group.Elements[0].TextAlignment).toBe(1)
  })

  it('uses the rendered size to detach a group with automatic dimensions', () => {
    const value: BpTemplate = { Version: 2, Width: 160, Height: 120, Rows: [{ Cells: [{ Width: 160, Elements: [
      { Type: 6, Elements: [{ Type: 1, FontSize: 24, Field: { Value: 'AUTO' } }] },
    ] }] }] }
    ungroupElement(value, 'Rows[0].Cells[0].Elements[0]', new Map([
      [0, { left: 0, top: 0, right: 48, bottom: 24 }],
    ]), { width: 160, height: 24 })
    expect(value.Rows[0].Cells?.[0].Elements?.[0]).toMatchObject({ Type: 1,
      StartCoordinate: { X: 0, Y: 0 }, EndCoordinate: { X: 160, Y: 24 } })
  })

  it('keeps the rendered position of an automatically placed parent group', () => {
    const value: BpTemplate = { Version: 2, Width: 200, Height: 200, Rows: [{ Cells: [{ Elements: [
      { Type: 1, FontSize: 24, Field: { Value: 'BEFORE' } },
      { Type: 6, Width: 100, Height: 80, Elements: [
        { Type: 1, FontSize: 24, Field: { Value: 'GROUP' }, StartCoordinate: { X: 0, Y: 0 } },
      ] },
    ] }] }] }
    ungroupElement(value, 'Rows[0].Cells[0].Elements[1]', new Map([
      [0, { left: 0, top: 0, right: 60, bottom: 24 }],
    ]), { width: 100, height: 80, position: { X: 0, Y: 24 } })
    const child = value.Rows[0].Cells![0].Elements![1]
    expect(child.StartCoordinate).toEqual({ X: 0, Y: 24 })
    expect(child.Margin?.Bottom).toBe(56)
  })

  it('preserves the reserved height of a group with explicit children', () => {
    const value: BpTemplate = { Version: 2, Width: 200, Height: 200, Rows: [{ Cells: [{ Elements: [
      { Type: 6, Width: 100, Height: 100, StartCoordinate: { X: 0, Y: 24 }, Elements: [
        { Type: 1, FontSize: 24, Field: { Value: 'GROUP' }, StartCoordinate: { X: 0, Y: 0 } },
      ] },
    ] }] }] }
    ungroupElement(value, 'Rows[0].Cells[0].Elements[0]', new Map([
      [0, { left: 0, top: 0, right: 60, bottom: 24 }],
    ]), { width: 100, height: 100, position: { X: 0, Y: 24 } })
    const child = value.Rows[0].Cells![0].Elements![0]
    expect(child.StartCoordinate).toEqual({ X: 0, Y: 24 })
    expect(child.Margin?.Bottom).toBe(76)
  })
})

describe('V3 flowing composition', () => {
  it('groups adjacent table rows and keeps row content after detach', () => {
    const value = template()
    appendDataTable(value, 'Items', [{ title: 'Name', path: 'Name' }])
    const path = groupConsecutiveRows(value, [1, 2])
    expect(value.Version).toBe(3)
    expect(findElement(value, path)?.Type).toBe(6)
    expect(findRow(value, `${path}.Rows[1]`)?.RepeatSource).toBe('Items')
    saveGroupComponent(value, path, 'itemsTable')
    const copy = appendComponent(value, 'Rows[0].Cells[0]', 'itemsTable')
    expect(findRow(value, `${copy}.Rows[1]`)?.RepeatSource).toBe('Items')
    renameComponent(value, 'itemsTable', 'productsTable')
    expect(findElement(value, copy)).toMatchObject({ ComponentId: 'productsTable' })
    detachComponent(value, copy)
    expect(findElement(value, copy)).toMatchObject({ Pagination: 'Split' })
    const detached = findElement(value, copy)
    if (detached?.Type !== 6) throw new Error('流式组合不存在。')
    expect(detached.ComponentId).toBeUndefined()
    expect(findRow(value, `${copy}.Rows[1]`)?.RepeatSource).toBe('Items')
    expect(() => removeComponent(value, 'productsTable')).toThrow('仍被模板引用')
  })

  it('does not mutate an instance when detach fails on a missing required parameter', () => {
    const value = template()
    value.Version = 3
    value.Components = [{ Id: 'pricedItem', Width: 120, Height: 80,
      Elements: [{ Type: 1, Field: { Value: 'Price' } }], Parameters: { currency: { Required: true } } }]
    value.Rows[0].Cells![0].Elements![0] = { Type: 6, ComponentId: 'pricedItem', Width: 0, Height: 0 }
    const before = JSON.stringify(value)

    expect(() => detachComponent(value, 'Rows[0].Cells[0].Elements[0]')).toThrow('缺少组件参数：currency')
    expect(JSON.stringify(value)).toBe(before)
  })

  it('derives split pagination from inline or referenced row content', () => {
    const value = template()
    value.Version = 3
    value.Components = [
      { Id: 'table', Width: 120, Height: 0, Rows: [{ Cells: [] }] },
      { Id: 'badge', Width: 120, Height: 80, Elements: [] },
    ]
    expect(defaultGroupPagination(value, { Type: 6, ComponentId: 'table' })).toBe('Split')
    expect(defaultGroupPagination(value, { Type: 6, Rows: [] })).toBe('Split')
    expect(defaultGroupPagination(value, { Type: 6, ComponentId: 'badge' })).toBe('KeepTogether')
    expect(defaultGroupPagination(value, { Type: 6, ComponentId: 'table', Pagination: 'KeepTogether' })).toBe('KeepTogether')
  })
})
