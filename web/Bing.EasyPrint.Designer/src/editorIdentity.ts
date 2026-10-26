import type { BpTemplate } from './model'
import { walkTemplateNodes } from './templateTraversal'

export type NodeIdentityState = { id: string; locked: boolean }
export type NodeIdentityMap = Record<string, NodeIdentityState>

type NodeVisit = { node: object; sourcePath: string; path: string }

function collectNodes(template: BpTemplate): NodeVisit[] {
  const nodes: NodeVisit[] = []
  const seen = new Set<string>()
  walkTemplateNodes(template, (node, context) => {
    if (seen.has(context.sourcePath)) return
    seen.add(context.sourcePath)
    nodes.push({ node, sourcePath: context.sourcePath, path: context.path })
  }, { expandComponentReferences: true, includeUnreferencedComponents: true })
  return nodes
}

function pathPrefixes(path: string): string[] {
  const matches = path.match(/(?:^|\.)(?:Rows|Cells|Elements|Components)\[\d+\]/g) ?? []
  const prefixes: string[] = []
  let current = ''
  for (const match of matches) {
    current += match
    prefixes.push(current)
  }
  return prefixes
}

function createId(): string {
  return typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
    ? crypto.randomUUID()
    : `node-${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`
}

export class NodeIdentityStore {
  private readonly idByObject = new WeakMap<object, string>()
  private readonly lockedById = new Map<string, boolean>()
  private readonly idFactory: () => string
  private state: NodeIdentityMap = {}
  private structureTransactionDepth = 0

  constructor(idFactory: () => string = createId) {
    this.idFactory = idFactory
  }

  reconcile(template: BpTemplate): NodeIdentityMap {
    if (this.structureTransactionDepth > 0) return this.snapshot()
    const next: NodeIdentityMap = {}
    const liveIds = new Set<string>()
    for (const { node, sourcePath } of collectNodes(template)) {
      let id = this.idByObject.get(node)
      if (!id || liveIds.has(id)) id = this.uniqueId(liveIds)
      this.idByObject.set(node, id)
      liveIds.add(id)
      const locked = this.lockedById.get(id) ?? false
      this.lockedById.set(id, locked)
      next[sourcePath] = { id, locked }
    }
    for (const id of this.lockedById.keys()) if (!liveIds.has(id)) this.lockedById.delete(id)
    this.state = next
    return this.snapshot()
  }

  beginStructureTransaction(): (template: BpTemplate) => NodeIdentityMap {
    this.structureTransactionDepth += 1
    let ended = false
    return (template) => {
      if (ended) return this.snapshot()
      ended = true
      this.structureTransactionDepth -= 1
      return this.structureTransactionDepth === 0 ? this.reconcile(template) : this.snapshot()
    }
  }

  restore(template: BpTemplate, value: unknown): NodeIdentityMap {
    if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error('editorState.nodeStates 必须为对象。')
    const visits = collectNodes(template)
    const validPaths = new Set(visits.map((item) => item.sourcePath))
    const source = value as Record<string, unknown>
    const seenIds = new Set<string>()
    const restored: NodeIdentityMap = {}
    for (const [path, entry] of Object.entries(source)) {
      if (!validPaths.has(path)) throw new Error(`editorState.nodeStates.${path} 不对应模板节点。`)
      if (!entry || typeof entry !== 'object' || Array.isArray(entry)) throw new Error(`editorState.nodeStates.${path} 必须为对象。`)
      const state = entry as Record<string, unknown>
      if (typeof state.id !== 'string' || !state.id.trim() || state.id.length > 128 || seenIds.has(state.id))
        throw new Error(`editorState.nodeStates.${path}.id 无效或重复。`)
      if (typeof state.locked !== 'boolean') throw new Error(`editorState.nodeStates.${path}.locked 必须为布尔值。`)
      seenIds.add(state.id)
      restored[path] = { id: state.id, locked: state.locked }
    }
    if (Object.keys(restored).length !== validPaths.size || [...validPaths].some((path) => !restored[path]))
      throw new Error('editorState.nodeStates 必须为每个模板节点提供身份映射。')
    this.lockedById.clear()
    for (const visit of visits) {
      const state = restored[visit.sourcePath]!
      this.idByObject.set(visit.node, state.id)
      this.lockedById.set(state.id, state.locked)
    }
    this.state = restored
    return this.snapshot()
  }

  setLocked(template: BpTemplate, path: string, locked: boolean): NodeIdentityMap {
    if (!this.state[path]) this.reconcile(template)
    const state = this.state[path]
    if (!state) throw new Error(`模板节点不存在：${path}`)
    this.lockedById.set(state.id, locked)
    this.state = { ...this.state, [path]: { ...state, locked } }
    return this.snapshot()
  }

  isLocked(template: BpTemplate, sourcePath: string, occurrencePath?: string): boolean {
    if (!Object.keys(this.state).length) this.reconcile(template)
    const protectedPaths = new Set(pathPrefixes(sourcePath))
    if (occurrencePath) {
      const sourceByPath = new Map<string, string>()
      walkTemplateNodes(template, (_node, context) => sourceByPath.set(context.path, context.sourcePath),
        { expandComponentReferences: true, includeUnreferencedComponents: true })
      for (const occurrence of pathPrefixes(occurrencePath)) {
        const resolvedSource = sourceByPath.get(occurrence)
        if (resolvedSource) for (const path of pathPrefixes(resolvedSource)) protectedPaths.add(path)
      }
    }
    for (const path of protectedPaths) if (this.state[path]?.locked) return true
    return false
  }

  snapshot(): NodeIdentityMap {
    return Object.fromEntries(Object.entries(this.state).map(([path, state]) => [path, { ...state }]))
  }

  private uniqueId(liveIds: Set<string>): string {
    for (let attempt = 0; attempt < 1000; attempt += 1) {
      const id = this.idFactory()
      if (id && !liveIds.has(id) && ![...Object.values(this.state)].some((state) => state.id === id)) return id
    }
    throw new Error('无法生成唯一的编辑器节点标识。')
  }
}
