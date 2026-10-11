import { describe, expect, it } from 'vitest'
import { walkTemplateNodes } from './templateTraversal'
import type { BpTemplate } from './model'
import { listTemplateTree } from './tree'

describe('template node traversal', () => {
  it('walks template rows, inline rows, and nested component references with editable source paths', () => {
    const template: BpTemplate = {
      Version: 3,
      Width: 100,
      Height: 100,
      Components: [
        { Id: 'inner', Width: 0, Height: 0, Rows: [{ Cells: [{ Elements: [{ Type: 1 }] }] }] },
        { Id: 'outer', Width: 0, Height: 0, Elements: [{ Type: 6, ComponentId: 'inner' }] },
        { Id: 'unused', Width: 0, Height: 0, Elements: [{ Type: 5 }] },
      ],
      Rows: [{ Cells: [{ Elements: [
        { Type: 6, ComponentId: 'outer' },
        { Type: 6, Rows: [{ Cells: [{ Elements: [{ Type: 6, ComponentId: 'inner' }] }] }] },
      ] }] }],
    }
    const entries: Array<{ kind: string; path: string; sourcePath: string; stack: string[] }> = []

    walkTemplateNodes(template, (_node, context) => entries.push({
      kind: context.kind,
      path: context.path,
      sourcePath: context.sourcePath,
      stack: context.componentStack,
    }))

    expect(entries).toContainEqual({
      kind: 'row',
      path: 'Rows[0].Cells[0].Elements[0].Elements[0].Rows[0]',
      sourcePath: 'Components[0].Rows[0]',
      stack: ['outer', 'inner'],
    })
    expect(entries.some((entry) => entry.sourcePath === 'Components[1].Elements[0]')).toBe(true)
    expect(entries.some((entry) => entry.sourcePath === 'Components[2].Elements[0]')).toBe(true)
    expect(entries.some((entry) => entry.kind === 'element' && entry.sourcePath === 'Rows[0].Cells[0].Elements[1].Rows[0].Cells[0].Elements[0]')).toBe(true)
  })

  it('can omit unused definitions and component expansion', () => {
    const template: BpTemplate = {
      Version: 3,
      Width: 100,
      Height: 100,
      Components: [{ Id: 'unused', Width: 0, Height: 0, Elements: [{ Type: 1 }] }],
      Rows: [{ Cells: [{ Elements: [{ Type: 6, ComponentId: 'unused' }] }] }],
    }
    const nodes: string[] = []

    walkTemplateNodes(template, (_node, context) => nodes.push(context.path), {
      includeUnreferencedComponents: false,
      expandComponentReferences: false,
    })

    expect(nodes).not.toContain('Components[0]')
    expect(nodes).not.toContain('Rows[0].Cells[0].Elements[0].Component.Elements[0]')
    expect(nodes).toContain('Rows[0].Cells[0].Elements[0]')
  })

  it('reports missing and cyclic references at the component occurrence path', () => {
    const template: BpTemplate = {
      Version: 3,
      Width: 100,
      Height: 100,
      Components: [{ Id: 'loop', Width: 0, Height: 0, Elements: [{ Type: 6, ComponentId: 'loop' }] }],
      Rows: [{ Cells: [{ Elements: [{ Type: 6, ComponentId: 'loop' }] }] }],
    }
    expect(() => walkTemplateNodes(template, () => undefined)).toThrow('组件存在循环引用')

    template.Components![0].Elements = [{ Type: 6, ComponentId: 'missing' }]
    expect(() => walkTemplateNodes(template, () => undefined)).toThrow('组件不存在')
  })

  it('lists a shared component definition once while retaining each occurrence', () => {
    const template: BpTemplate = {
      Version: 3,
      Width: 100,
      Height: 100,
      Components: [{ Id: 'badge', Width: 20, Height: 20, Elements: [{ Type: 1 }] }],
      Rows: [{ Cells: [{ Elements: [
        { Type: 6, ComponentId: 'badge' }, { Type: 6, ComponentId: 'badge' },
      ] }] }],
    }
    const tree = listTemplateTree(template)
    expect(tree.filter((entry) => entry.kind === 'component' && entry.sourcePath === 'Components[0]')).toHaveLength(1)
    expect(tree.filter((entry) => entry.kind === 'element' && entry.sourcePath === 'Components[0].Elements[0]')).toHaveLength(3)
    expect(tree.filter((entry) => entry.kind === 'element' && entry.sourcePath === 'Components[0].Elements[0]' && entry.path.startsWith('Rows'))).toHaveLength(2)
  })
})
