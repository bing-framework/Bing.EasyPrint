import { describe, expect, it } from 'vitest'
import { appendComponent, appendDataTable, appendStaticTable, detachComponent, groupCellElements, removeComponent, renameComponent, saveGroupComponent, ungroupElement } from './componentsModel'
import { resolvePath } from './binding'
import { findElement, listTemplateTree } from './tree'
import type { BpTemplate } from './model'

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
    expect(listTemplateTree(value).filter((entry) => entry.kind === 'element')).toHaveLength(6)
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
    expect(value.Components![0].Elements[0].StartCoordinate?.X).toBe(0)
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
