import type { BpTemplate, Cell, ComponentDefinition, GroupElement, Row, TemplateElement } from './model'

export type TemplateTraversalKind = 'row' | 'cell' | 'element' | 'component'

export type TemplateTraversalContext = {
  kind: TemplateTraversalKind
  /** 路径指向当前组合实例展开后的节点位置。 */
  path: string
  /** 路径指向模板或组件定义中的可编辑节点。 */
  sourcePath: string
  componentStack: string[]
  componentInstancePaths: string[]
  rowCollection?: Row[]
  rowIndex?: number
  topLevelRow?: boolean
}

export type TemplateTraversalOptions = {
  /** false 时只报告组件引用，不展开引用的定义。 */
  expandComponentReferences?: boolean
  /** false 时不遍历未被模板内容引用的组件定义。 */
  includeUnreferencedComponents?: boolean
}

type ComponentEntry = { definition: ComponentDefinition; index: number }

/** 遍历模板行列、元素组合及其引用的组件定义。 */
export function walkTemplateNodes(
  template: BpTemplate,
  visit: (node: Row | Cell | TemplateElement | ComponentDefinition, context: TemplateTraversalContext) => void,
  options: TemplateTraversalOptions = {},
): void {
  const definitions = new Map<string, ComponentEntry>()
  for (const [index, definition] of (template.Components ?? []).entries()) {
    if (definition && typeof definition.Id === 'string' && definition.Id && !definitions.has(definition.Id)) {
      definitions.set(definition.Id, { definition, index })
    }
  }

  const expandReferences = options.expandComponentReferences !== false
  const includeUnreferenced = options.includeUnreferencedComponents !== false
  const referencedDefinitions = new Set<string>()
  let nodeCount = 0

  const emit = (node: Row | Cell | TemplateElement | ComponentDefinition, context: TemplateTraversalContext, depth: number): void => {
    if (depth > 16) throw new Error(`${context.path}: 模板嵌套深度不能超过 16 层。`)
    if (++nodeCount > 20000) throw new Error(`${context.path}: 模板结构节点不能超过 20,000 个。`)
    visit(node, context)
  }

  const context = (
    kind: TemplateTraversalKind,
    path: string,
    sourcePath: string,
    componentStack: string[],
    componentInstancePaths: string[],
  ): TemplateTraversalContext => ({ kind, path, sourcePath, componentStack, componentInstancePaths })

  const walkRows = (
    rows: Row[] | undefined,
    path: string,
    sourcePath: string,
    componentStack: string[],
    componentInstancePaths: string[],
    activeComponents: string[],
    depth: number,
    topLevel = false,
  ): void => {
    if (!Array.isArray(rows)) return
    rows.forEach((row, rowIndex) => {
      const rowPath = `${path}[${rowIndex}]`
      const rowSourcePath = `${sourcePath}[${rowIndex}]`
      const rowContext = context('row', rowPath, rowSourcePath, componentStack, componentInstancePaths)
      rowContext.rowCollection = rows
      rowContext.rowIndex = rowIndex
      rowContext.topLevelRow = topLevel
      emit(row, rowContext, depth)
      const cells = row && typeof row === 'object' && Array.isArray(row.Cells) ? row.Cells : []
      cells.forEach((cell, cellIndex) => {
        const cellPath = `${rowPath}.Cells[${cellIndex}]`
        const cellSourcePath = `${rowSourcePath}.Cells[${cellIndex}]`
        emit(cell, context('cell', cellPath, cellSourcePath, componentStack, componentInstancePaths), depth + 1)
        if (cell && typeof cell === 'object') {
          walkRows(cell.Rows, `${cellPath}.Rows`, `${cellSourcePath}.Rows`, componentStack, componentInstancePaths, activeComponents, depth + 2)
          walkElements(cell.Elements, `${cellPath}.Elements`, `${cellSourcePath}.Elements`, componentStack, componentInstancePaths, activeComponents, depth + 2)
        }
      })
    })
  }

  const walkComponent = (
    id: string,
    entry: ComponentEntry,
    occurrencePath: string,
    componentStack: string[],
    componentInstancePaths: string[],
    activeComponents: string[],
    depth: number,
  ): void => {
    if (activeComponents.includes(id)) throw new Error(`${occurrencePath}: 组件存在循环引用：${id}`)
    referencedDefinitions.add(id)
    const definitionPath = `Components[${entry.index}]`
    const nextStack = [...componentStack, id]
    const nextInstances = [...componentInstancePaths, occurrencePath]
    const nextActive = [...activeComponents, id]
    emit(entry.definition, context('component', definitionPath, definitionPath, nextStack, nextInstances), depth)
    if (Array.isArray(entry.definition.Elements))
      walkElements(entry.definition.Elements, `${occurrencePath}.Elements`, `${definitionPath}.Elements`, nextStack, nextInstances, nextActive, depth + 1)
    if (Array.isArray(entry.definition.Rows))
      walkRows(entry.definition.Rows, `${occurrencePath}.Rows`, `${definitionPath}.Rows`, nextStack, nextInstances, nextActive, depth + 1)
  }

  const walkElements = (
    elements: TemplateElement[] | undefined,
    path: string,
    sourcePath: string,
    componentStack: string[],
    componentInstancePaths: string[],
    activeComponents: string[],
    depth: number,
  ): void => {
    if (!Array.isArray(elements)) return
    elements.forEach((element, index) => {
      const elementPath = `${path}[${index}]`
      const elementSourcePath = `${sourcePath}[${index}]`
      emit(element, context('element', elementPath, elementSourcePath, componentStack, componentInstancePaths), depth)
      if (!element || typeof element !== 'object' || element.Type !== 6) return

      const group = element as GroupElement
      if (group.ComponentId !== undefined) {
        if (!expandReferences) return
        const entry = definitions.get(group.ComponentId)
        if (!entry) throw new Error(`${elementPath}: 组件不存在：${group.ComponentId}`)
        walkComponent(group.ComponentId, entry, elementPath, componentStack, componentInstancePaths, activeComponents, depth + 1)
        return
      }

      walkElements(group.Elements, `${elementPath}.Elements`, `${elementSourcePath}.Elements`, componentStack, componentInstancePaths, activeComponents, depth + 1)
      walkRows(group.Rows, `${elementPath}.Rows`, `${elementSourcePath}.Rows`, componentStack, componentInstancePaths, activeComponents, depth + 1)
    })
  }

  walkRows(template.Rows, 'Rows', 'Rows', [], [], [], 0, true)
  if (includeUnreferenced) {
    for (const [id, entry] of definitions) {
      if (referencedDefinitions.has(id)) continue
      walkComponent(id, entry, `Components[${entry.index}]`, [], [], [], 0)
    }
  }
}
