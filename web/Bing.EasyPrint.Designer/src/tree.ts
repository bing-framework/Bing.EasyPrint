import type { BpTemplate, Cell, ComponentDefinition, Row, TemplateElement } from './model'
import { walkTemplateNodes } from './templateTraversal'

export type TreeEntry = { key: string; path: string; sourcePath: string; kind: 'row' | 'cell' | 'element' | 'component'; type?: number; label: string; depth: number }

export function listTemplateTree(template: BpTemplate): TreeEntry[] {
  const result: TreeEntry[] = []
  const collect = (definitionOnly: boolean) => {
    const emittedDefinitions = new Set<string>()
    walkTemplateNodes(template, (node, context) => {
      if (context.path.startsWith('Components[') !== definitionOnly) return
      if (!definitionOnly && context.kind === 'component') {
        if (emittedDefinitions.has(context.sourcePath)) return
        emittedDefinitions.add(context.sourcePath)
      }
      if (context.kind === 'component' && !definitionOnly) return
      const depth = context.path.split('.').length - 1
      const row = context.kind === 'row' ? node as Row : undefined
      const cell = context.kind === 'cell' ? node as Cell : undefined
      const element = context.kind === 'element' ? node as TemplateElement : undefined
      const label = row ? row.RepeatSource ? `明细 · ${row.RepeatSource}` : `行 ${(context.rowIndex ?? 0) + 1}`
        : cell ? `列${cell.Width ? ` · ${cell.Width} 点` : ''}`
          : element ? elementLabel(element) : `组件 · ${(node as ComponentDefinition).Id}`
      result.push({ key: context.path, path: context.path, sourcePath: context.sourcePath,
        kind: context.kind, type: element?.Type,
        label: context.path !== context.sourcePath ? `共享 · ${label}` : label, depth })
    }, { expandComponentReferences: !definitionOnly, includeUnreferencedComponents: definitionOnly })
  }
  collect(false)
  collect(true)
  return result
}

export function elementLabel(element: TemplateElement): string {
  const labels: Record<number, string> = { 1: '文字', 2: '图片', 3: '二维码', 4: '条码', 5: '线段', 6: '组合', 7: '矩形', 8: '椭圆', 9: '图标', 10: '二维点阵码' }
  if (element.Type === 6 && element.ComponentId) return `组件 · ${element.ComponentId}`
  return `${labels[element.Type] ?? '元素'}${element.Field?.Name ? ` · ${element.Field.Name}` : element.Field?.Value ? ` · ${element.Field.Value}` : ''}`
}

function findNode(template: BpTemplate, path: string): unknown {
  const parts = path.match(/(?:^|\.)(?:Rows|Cells|Elements|Components)\[\d+\]/g)
  if (!parts || parts.join('') !== path) return undefined
  let current: unknown = template
  for (const part of parts) {
    const match = /(?:^|\.)(Rows|Cells|Elements|Components)\[(\d+)\]/.exec(part)
    if (!match || !current || typeof current !== 'object') return undefined
    const key = match[1] as 'Rows' | 'Cells' | 'Elements' | 'Components'
    if ('Type' in current && current.Type === 6 && 'ComponentId' in current && current.ComponentId && (key === 'Rows' || key === 'Elements'))
      current = template.Components?.find((item) => item.Id === (current as { ComponentId: string }).ComponentId)
    const list = (current as Record<string, unknown> | undefined)?.[key]
    current = Array.isArray(list) ? list[Number(match[2])] : undefined
  }
  return current
}

export function findElement(template: BpTemplate, path: string): TemplateElement | undefined {
  if (!/\.Elements\[\d+\]$/.test(path)) return undefined
  return findNode(template, path) as TemplateElement | undefined
}

export function findRow(template: BpTemplate, path: string): Row | undefined {
  if (!/(?:^|\.)Rows\[\d+\]$/.test(path)) return undefined
  return findNode(template, path) as Row | undefined
}

export function findCell(template: BpTemplate, path: string): Cell | undefined {
  if (!/\.Cells\[\d+\]$/.test(path)) return undefined
  return findNode(template, path) as Cell | undefined
}

export function findComponent(template: BpTemplate, path: string): ComponentDefinition | undefined {
  if (!/^Components\[\d+\]$/.test(path)) return undefined
  return findNode(template, path) as ComponentDefinition | undefined
}
