import type { Condition, Field } from './model'

export type BindingContext = { root: unknown; item: unknown; index: number; variables?: Record<string, string> }

export function collectFieldPaths(root: unknown, maxDepth = 8, maxPaths = 500): string[] {
  const paths = new Set<string>()
  const seen = new WeakSet<object>()
  const visit = (value: unknown, prefix: string, depth: number): void => {
    if (depth > maxDepth || value == null || typeof value !== 'object') return
    if (seen.has(value)) return
    seen.add(value)
    if (Array.isArray(value)) {
      if (value.length) visit(value[0], '', depth + 1)
      return
    }
    for (const [key, child] of Object.entries(value)) {
      const path = prefix ? `${prefix}.${key}` : key
      if (child == null || typeof child !== 'object') paths.add(path)
      else visit(child, path, depth + 1)
      if (paths.size >= maxPaths) return
    }
  }
  visit(root, '', 0)
  return [...paths].sort((left, right) => left.localeCompare(right))
}

export function collectRootFieldPaths(root: unknown, maxDepth = 8, maxPaths = 500): string[] {
  const paths: string[] = []
  const visit = (value: unknown, prefix: string, depth: number): void => {
    if (!value || typeof value !== 'object' || Array.isArray(value) || depth > maxDepth) return
    for (const [key, child] of Object.entries(value)) {
      if (paths.length >= maxPaths) return
      const path = prefix ? `${prefix}.${key}` : key
      if (Array.isArray(child)) continue
      if (child !== null && typeof child === 'object') visit(child, path, depth + 1)
      else paths.push(path)
    }
  }
  visit(root, '', 0)
  return paths.sort((left, right) => left.localeCompare(right))
}

export function collectArrayPaths(root: unknown, maxDepth = 8): string[] {
  const paths: string[] = []
  const visit = (value: unknown, prefix: string, depth: number): void => {
    if (!value || typeof value !== 'object' || Array.isArray(value) || depth > maxDepth) return
    for (const [key, child] of Object.entries(value)) {
      const path = prefix ? `${prefix}.${key}` : key
      if (Array.isArray(child)) paths.push(path)
      else if (child && typeof child === 'object') visit(child, path, depth + 1)
    }
  }
  visit(root, '', 0)
  return paths
}

export function collectBindingPaths(root: unknown, repeatSource?: string | string[], variables?: Record<string, string>): string[] {
  const rootPaths = collectRootFieldPaths(root)
  const sources = typeof repeatSource === 'string' ? [repeatSource] : repeatSource ?? []
  if (!sources.length) return [...rootPaths, ...rootPaths.map((path) => `$root.${path}`)]
  let item: unknown = root
  for (const sourcePath of sources) {
    let source: unknown
    try { source = resolvePath(sourcePath, { root, item, index: -1, variables }) }
    catch { source = undefined }
    item = Array.isArray(source) ? source.find((value) => value && typeof value === 'object') : undefined
  }
  const itemPaths = collectRootFieldPaths(item)
  return [...itemPaths, ...itemPaths.map((path) => `$item.${path}`),
    ...rootPaths.map((path) => `$root.${path}`), '$index']
}

function hasOwn(value: object, key: string): boolean {
  return Object.prototype.hasOwnProperty.call(value, key)
}

export function resolvePath(path: string, context: BindingContext): unknown {
  if (!path.trim()) throw new Error('字段路径不能为空。')
  if (path.startsWith('$var.')) {
    const name = path.slice(5)
    const source = context.variables?.[name]
    if (!source || name.includes('.')) throw new Error(`变量不存在：${name}`)
    return resolvePath(`$root.${source}`, { ...context, variables: undefined })
  }
  if (path === '$root') return context.root
  if (path === '$item') return context.item
  if (path === '$index') return context.index
  let value: unknown = context.item
  let lookup = path
  if (path.startsWith('$root.')) { value = context.root; lookup = path.slice(6) }
  else if (path.startsWith('$item.')) lookup = path.slice(6)
  for (const key of lookup.split('.')) {
    if (!key) throw new Error('字段路径包含空段。')
    if (value == null) return null
    if (typeof value !== 'object' || !hasOwn(value, key)) throw new Error(`字段不存在：${lookup}`)
    value = (value as Record<string, unknown>)[key]
  }
  return value
}

export function valueForField(field: Field | undefined, context: BindingContext): string {
  if (!field) return ''
  const value = field.Name ? resolvePath(field.Name, context) : field.Value
  return value == null ? '' : String(value)
}

export function bindImageResource(field: Field | undefined, resourceKey: string): Field {
  const result = { ...field }
  delete result.Name
  if (resourceKey) result.Value = resourceKey
  else delete result.Value
  return result
}

function compare(left: unknown, right: unknown, op: string): boolean {
  if (op === 'isNull') return left == null
  if (op === 'notNull') return left != null
  if (op === 'eq') return left === right
  if (op === 'ne') return left !== right
  if (left == null || right == null || typeof left !== typeof right) return false
  if (typeof left !== 'number' && typeof left !== 'string') return false
  if (typeof right !== 'number' && typeof right !== 'string') return false
  const order = left < right ? -1 : left > right ? 1 : 0
  switch (op) {
    case 'gt': return order > 0
    case 'ge': return order >= 0
    case 'lt': return order < 0
    case 'le': return order <= 0
    default: return false
  }
}

export function conditionMatches(condition: Condition | undefined, context: BindingContext): boolean {
  if (!condition) return true
  if (condition.Operator === 'and') return (condition.Conditions ?? []).every((child) => conditionMatches(child, context))
  if (condition.Operator === 'or') return (condition.Conditions ?? []).some((child) => conditionMatches(child, context))
  return compare(resolvePath(condition.Path ?? '', context), condition.Value, condition.Operator)
}

export function setPathValue(root: Record<string, unknown>, path: string, value: unknown): void {
  const segments = path.split('.')
  if (segments.some((segment) => !segment)) throw new Error('字段路径包含空段。')
  let target = root
  for (const segment of segments.slice(0, -1)) {
    const current = target[segment]
    if (!current || typeof current !== 'object' || Array.isArray(current)) target[segment] = {}
    target = target[segment] as Record<string, unknown>
  }
  target[segments[segments.length - 1]] = value
}
