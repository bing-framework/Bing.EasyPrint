import { describe, expect, it } from 'vitest'
import { assertTemplate, type BpTemplate } from './model'

function baseTemplate(): BpTemplate {
  return { Version: 1, Width: 100, Height: 100, Rows: [] }
}

describe('template validation', () => {
  it('rejects excessive nesting before tree rendering', () => {
    const template = baseTemplate() as BpTemplate & Record<string, unknown>
    let root: Record<string, unknown> = {}
    const nested = root
    for (let depth = 0; depth < 18; depth += 1) {
      const child: Record<string, unknown> = {}
      root.child = child
      root = child
    }
    template.extra = nested
    expect(() => assertTemplate(template)).toThrow('嵌套深度')
  })

  it('rejects unknown element types and cyclic objects', () => {
    const invalid = baseTemplate()
    invalid.Rows.push({ Cells: [{ Elements: [{ Type: 11 } as never] }] })
    expect(() => assertTemplate(invalid)).toThrow('不支持的元素类型')

    const cyclic = baseTemplate() as BpTemplate & Record<string, unknown>
    cyclic.self = cyclic
    expect(() => assertTemplate(cyclic)).toThrow('循环引用')
  })

  it('accepts V2 groups but keeps V1 extensions invalid', () => {
    const extended = baseTemplate()
    extended.Version = 2
    extended.Components = [{ Id: 'badge', Width: 20, Height: 20, Elements: [{ Type: 7, Width: 20, Height: 20 }] }]
    extended.Rows = [{ Section: 'Body', Cells: [{ Elements: [{ Type: 6, ComponentId: 'badge' }] }] }]
    expect(() => assertTemplate(extended)).not.toThrow()
    extended.Version = 1
    expect(() => assertTemplate(extended)).toThrow('Version: 1')
  })

  it('rejects component reference cycles before the structure tree expands', () => {
    const template: BpTemplate = { Version: 2, Width: 100, Height: 100,
      Components: [{ Id: 'loop', Width: 20, Height: 20, Elements: [{ Type: 6, ComponentId: 'loop' }] }],
      Rows: [{ Cells: [{ Elements: [{ Type: 6, ComponentId: 'loop' }] }] }] }
    expect(() => assertTemplate(template)).toThrow('循环引用')
    template.Components![0].Elements = [{ Type: 6, ComponentId: 'missing' }]
    expect(() => assertTemplate(template)).toThrow('组件不存在')
  })

  it('requires a table header to precede a repeated body row', () => {
    const template: BpTemplate = { Version: 2, Width: 100, Height: 100,
      Rows: [{ Section: 'TableHeader', Cells: [] }] }
    expect(() => assertTemplate(template)).toThrow('Rows[0]')
    template.Rows.push({ Section: 'PageFooter', RepeatSource: 'Items', Cells: [] })
    expect(() => assertTemplate(template)).toThrow('正文重复明细')
    template.Rows[1].Section = 'Body'
    expect(() => assertTemplate(template)).not.toThrow()
  })
})
