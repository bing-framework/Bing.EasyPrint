import { describe, expect, it } from 'vitest'
import { reactive, watch } from 'vue'
import { NodeIdentityStore } from './editorIdentity'
import type { BpTemplate } from './model'
import { createProductSample } from './samples'
import { groupCellElements } from './componentsModel'

function createTemplate(): BpTemplate {
  return {
    Version: 1,
    Width: 100,
    Height: 50,
    Rows: [{ Cells: [{ Elements: [
      { Type: 1, FontSize: 12, Field: { Value: 'first' } },
      { Type: 1, FontSize: 12, Field: { Value: 'second' } },
    ] }] }],
  }
}

describe('editor node identity and locks', () => {
  it('keeps identity through reordering and assigns a new identity to a copy', () => {
    let nextId = 0
    const template = createTemplate()
    const store = new NodeIdentityStore(() => `node-${++nextId}`)
    const initial = store.reconcile(template)
    const elements = template.Rows![0]!.Cells![0]!.Elements!
    const first = elements[0]!
    const secondPath = 'Rows[0].Cells[0].Elements[1]'
    const secondId = initial[secondPath]!.id

    elements.splice(0, 1)
    elements.push(first)
    const reordered = store.reconcile(template)
    expect(reordered['Rows[0].Cells[0].Elements[1]']!.id).toBe(initial['Rows[0].Cells[0].Elements[0]']!.id)

    elements.push(structuredClone(elements[0]!))
    const copied = store.reconcile(template)
    expect(copied['Rows[0].Cells[0].Elements[2]']!.id).not.toBe(secondId)
  })

  it('does not transfer a moved node lock to a newly inserted copy at its old path', () => {
    let nextId = 0
    const template = createTemplate()
    const store = new NodeIdentityStore(() => `node-${++nextId}`)
    const initial = store.reconcile(template)
    const elements = template.Rows![0]!.Cells![0]!.Elements!
    const first = elements[0]!
    const locked = elements[1]!
    const firstId = initial['Rows[0].Cells[0].Elements[0]']!.id
    const lockedPath = 'Rows[0].Cells[0].Elements[1]'
    const lockedId = initial[lockedPath]!.id

    store.setLocked(template, lockedPath, true)
    elements.splice(1, 0, structuredClone(first))

    const reconciled = store.reconcile(template)
    expect(reconciled['Rows[0].Cells[0].Elements[0]']!.id).toBe(firstId)
    expect(reconciled['Rows[0].Cells[0].Elements[1]']!.id).not.toBe(lockedId)
    expect(reconciled['Rows[0].Cells[0].Elements[1]']!.locked).toBe(false)
    expect(reconciled['Rows[0].Cells[0].Elements[2]']!.id).toBe(lockedId)
    expect(reconciled['Rows[0].Cells[0].Elements[2]']!.locked).toBe(true)
    expect(locked).toBe(elements[2])
  })

  it('inherits locks from row and cell ancestors and permits explicit unlock', () => {
    const template = createTemplate()
    let nextId = 0
    const store = new NodeIdentityStore(() => `node-${++nextId}`)
    store.reconcile(template)
    const target = 'Rows[0].Cells[0].Elements[0]'

    store.setLocked(template, 'Rows[0]', true)
    expect(store.isLocked(template, target)).toBe(true)
    store.setLocked(template, 'Rows[0]', false)
    expect(store.isLocked(template, target)).toBe(false)
  })

  it('rejects duplicate identities and stale node paths during project restore', () => {
    const template = createTemplate()
    let nextId = 0
    const store = new NodeIdentityStore(() => `node-${++nextId}`)
    const states = store.reconcile(template)
    const duplicate = structuredClone(states)
    const paths = Object.keys(duplicate)
    duplicate[paths[1]!]!.id = duplicate[paths[0]!]!.id

    expect(() => store.restore(template, duplicate)).toThrow('无效或重复')
    expect(() => store.restore(template, { ...states, 'Rows[99]': { id: 'stale', locked: false } }))
      .toThrow('不对应模板节点')
  })

  it('keeps product sample identities attached to objects after sibling reorder', () => {
    const template = createProductSample()
    let nextId = 0
    const store = new NodeIdentityStore(() => `product-${++nextId}`)
    const initial = store.reconcile(template)
    const elements = template.Rows[0]!.Cells![0]!.Elements!
    const moved = elements[0]!
    const movedId = initial['Rows[0].Cells[0].Elements[0]']!.id
    const destination = elements.length - 1

    elements.splice(0, 1)
    elements.push(moved)

    expect(store.reconcile(template)[`Rows[0].Cells[0].Elements[${destination}]`]!.id).toBe(movedId)
  })

  it('preserves nested locks while Vue synchronously observes an element group rewrite', () => {
    const template = reactive(createTemplate())
    let nextId = 0
    const store = new NodeIdentityStore(() => `sync-${++nextId}`)
    let states = store.reconcile(template)
    watch(template, () => { states = store.reconcile(template) }, { deep: true, flush: 'sync' })
    const cellPath = 'Rows[0].Cells[0]'
    const childPath = `${cellPath}.Elements[0]`
    const child = template.Rows[0]!.Cells![0]!.Elements![0]!
    const sibling = template.Rows[0]!.Cells![0]!.Elements![1]!
    if (child.Type !== 1) throw new Error('测试文字缺失。')
    child.StartCoordinate = { X: 10, Y: 10 }
    child.EndCoordinate = { X: 50, Y: 34 }
    if (sibling.Type !== 1) throw new Error('测试文字缺失。')
    sibling.StartCoordinate = { X: 60, Y: 10 }
    sibling.EndCoordinate = { X: 90, Y: 34 }
    states = store.setLocked(template, childPath, true)
    const childId = states[childPath]!.id

    const finishStructureTransaction = store.beginStructureTransaction()
    let groupPath = ''
    try { groupPath = groupCellElements(template, cellPath) }
    finally { states = finishStructureTransaction(template) }

    expect(states[`${groupPath}.Elements[0]`]).toEqual({ id: childId, locked: true })
  })
})
