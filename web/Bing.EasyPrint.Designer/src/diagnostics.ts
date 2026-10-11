import type { BpTemplate, DesignerDiagnostic, PreviewElement, PreviewPage } from './model'
import { listTemplateTree, type TreeEntry } from './tree'

export type DiagnosticTarget = { path: string; occurrencePath: string }

type DiagnosticRecord = Record<string, unknown>

function isRecord(value: unknown): value is DiagnosticRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function validPath(value: unknown): string | null | undefined {
  if (value === null) return null
  return typeof value === 'string' && value.length > 0 ? value : undefined
}

function validByteCount(value: unknown): number | null | undefined {
  if (value === null) return null
  return typeof value === 'number' && Number.isFinite(value) && Number.isInteger(value) && value >= 0
    ? value : undefined
}

function validIndex(value: unknown): number | null | undefined {
  if (value === null) return null
  return typeof value === 'number' && Number.isInteger(value) && value >= 0 ? value : undefined
}

function validInstanceIndices(value: unknown): number[] | null | undefined {
  if (value === null) return null
  if (!Array.isArray(value) || !value.every((item) => typeof item === 'number' && Number.isInteger(item) && item >= 0))
    return undefined
  return [...value]
}

/** 将接口返回的原始错误数组收敛为可安全消费的诊断对象。 */
export function normalizeDesignerDiagnostics(value: unknown): DesignerDiagnostic[] {
  if (!Array.isArray(value)) return []
  return value.flatMap((item): DesignerDiagnostic[] => {
    if (!isRecord(item) || typeof item.message !== 'string' || item.message.trim().length === 0) return []
    const diagnostic: DesignerDiagnostic = { message: item.message }
    for (const field of ['sourcePath', 'definitionPath', 'componentInstancePath'] as const) {
      const normalized = validPath(item[field])
      if (normalized !== undefined) diagnostic[field] = normalized
    }
    for (const field of ['code', 'reason', 'phase', 'fieldPath', 'resourceName'] as const) {
      const normalized = validPath(item[field])
      if (normalized !== undefined) diagnostic[field] = normalized
    }
    const instanceIndices = validInstanceIndices(item.instanceIndices)
    if (instanceIndices !== undefined) diagnostic.instanceIndices = instanceIndices
    const pageIndex = validIndex(item.pageIndex)
    if (pageIndex !== undefined) diagnostic.pageIndex = pageIndex
    const elementIndex = validIndex(item.elementIndex)
    if (elementIndex !== undefined) diagnostic.elementIndex = elementIndex
    for (const field of [
      'sourcePixelWidth', 'sourcePixelHeight', 'renderedPixelWidth', 'renderedPixelHeight',
      'cropX', 'cropY', 'cropWidth', 'cropHeight',
      'requestedBytes', 'currentBytes', 'maxBytes',
    ] as const) {
      const normalized = validByteCount(item[field])
      if (normalized !== undefined) diagnostic[field] = normalized
    }
    return [diagnostic]
  })
}

function formatByteCount(value: number): string {
  const display = String(value)
  return Number.isSafeInteger(value) ? `${display} 字节` : `约 ${display} 字节`
}

/** 将诊断中的路径和位置整理成界面可直接展示的简短文本。 */
export function formatDesignerDiagnostic(diagnostic: DesignerDiagnostic): string {
  const result = `${diagnostic.sourcePath ? `${diagnostic.sourcePath}: ` : ''}${diagnostic.message}`
  const details: string[] = []
  if (diagnostic.code) details.push(`代码：${diagnostic.code}`)
  if (diagnostic.reason) details.push(`原因：${diagnostic.reason}`)
  if (diagnostic.phase) details.push(`阶段：${diagnostic.phase}`)
  if (diagnostic.fieldPath) details.push(`字段：${diagnostic.fieldPath}`)
  if (diagnostic.definitionPath) details.push(`定义：${diagnostic.definitionPath}`)
  if (diagnostic.componentInstancePath) details.push(`实例：${diagnostic.componentInstancePath}`)
  if (diagnostic.instanceIndices && diagnostic.instanceIndices.length > 0)
    details.push(`重复项：${diagnostic.instanceIndices.map((index) => `第 ${index + 1} 项`).join(' / ')}`)
  if (diagnostic.pageIndex !== null && diagnostic.pageIndex !== undefined)
    details.push(`页号：第 ${diagnostic.pageIndex + 1} 页`)
  const resourceDetails: string[] = []
  if (diagnostic.resourceName) resourceDetails.push(`资源：${diagnostic.resourceName}`)
  if (diagnostic.sourcePixelWidth !== null && diagnostic.sourcePixelWidth !== undefined &&
      diagnostic.sourcePixelHeight !== null && diagnostic.sourcePixelHeight !== undefined)
    resourceDetails.push(`源图：${diagnostic.sourcePixelWidth}×${diagnostic.sourcePixelHeight} 像素`)
  if (diagnostic.cropX !== null && diagnostic.cropX !== undefined &&
      diagnostic.cropY !== null && diagnostic.cropY !== undefined &&
      diagnostic.cropWidth !== null && diagnostic.cropWidth !== undefined &&
      diagnostic.cropHeight !== null && diagnostic.cropHeight !== undefined)
    resourceDetails.push(`裁切：${diagnostic.cropX},${diagnostic.cropY} ${diagnostic.cropWidth}×${diagnostic.cropHeight} 像素`)
  if (diagnostic.renderedPixelWidth !== null && diagnostic.renderedPixelWidth !== undefined &&
      diagnostic.renderedPixelHeight !== null && diagnostic.renderedPixelHeight !== undefined)
    resourceDetails.push(`输出：${diagnostic.renderedPixelWidth}×${diagnostic.renderedPixelHeight} 像素`)
  if (diagnostic.requestedBytes !== null && diagnostic.requestedBytes !== undefined)
    resourceDetails.push(`申请：${formatByteCount(diagnostic.requestedBytes)}`)
  if (diagnostic.currentBytes !== null && diagnostic.currentBytes !== undefined)
    resourceDetails.push(`当前：${formatByteCount(diagnostic.currentBytes)}`)
  if (diagnostic.maxBytes !== null && diagnostic.maxBytes !== undefined)
    resourceDetails.push(`上限：${formatByteCount(diagnostic.maxBytes)}`)
  details.push(...resourceDetails)
  return details.length > 0 ? `${result} · ${details.join(' · ')}` : result
}

function ancestorPaths(path: string): string[] {
  const result = [path]
  let current = path
  while (true) {
    const separator = current.lastIndexOf('.')
    if (separator < 0) break
    current = current.slice(0, separator)
    if (current.length === 0) break
    result.push(current)
  }
  return result
}

function isWithin(path: string, root: string): boolean {
  return path === root || path.startsWith(`${root}.`)
}

function mapEntry(entry: TreeEntry): DiagnosticTarget {
  return { path: entry.sourcePath, occurrencePath: entry.path }
}

function isStaticComponentPath(path: string): boolean {
  return path.startsWith('Components[')
}

function resolveSourcePath(entries: TreeEntry[], path: string, instancePath?: string | null): DiagnosticTarget | undefined {
  for (const candidate of ancestorPaths(path)) {
    const exact = entries.filter((entry) => entry.path === candidate)
    if (instancePath) {
      const scopedExact = exact.filter((entry) => isWithin(entry.path, instancePath))
      if (scopedExact.length === 1) return mapEntry(scopedExact[0])
      const scopedSource = entries.filter((entry) => entry.sourcePath === candidate && isWithin(entry.path, instancePath))
      if (scopedSource.length === 1) return mapEntry(scopedSource[0])
      continue
    }
    if (exact.length === 1) return mapEntry(exact[0])
    const bySource = entries.filter((entry) => entry.sourcePath === candidate)
    if (bySource.length === 1) return mapEntry(bySource[0])
    if (isStaticComponentPath(candidate) && bySource.length > 0) return { path: candidate, occurrencePath: candidate }
  }
  return undefined
}

function resolveDefinitionPath(entries: TreeEntry[], path: string): DiagnosticTarget | undefined {
  for (const candidate of ancestorPaths(path)) {
    const exact = entries.filter((entry) => entry.path === candidate)
    const staticEntry = exact.find((entry) => entry.sourcePath === candidate && isStaticComponentPath(candidate))
    if (staticEntry) return mapEntry(staticEntry)
    const bySource = entries.filter((entry) => entry.sourcePath === candidate)
    const staticSource = bySource.find((entry) => entry.path === candidate && entry.sourcePath === candidate)
    if (staticSource) return mapEntry(staticSource)
    if (isStaticComponentPath(candidate) && bySource.length > 0) return { path: candidate, occurrencePath: candidate }
    if (exact.length === 1) return mapEntry(exact[0])
    if (bySource.length === 1) return mapEntry(bySource[0])
  }
  return undefined
}

function resolveInstancePath(entries: TreeEntry[], path: string): DiagnosticTarget | undefined {
  for (const candidate of ancestorPaths(path)) {
    const exact = entries.filter((entry) => entry.path === candidate)
    if (exact.length === 1) return mapEntry(exact[0])
  }
  return undefined
}

/** 将诊断路径解析为可编辑的定义路径和用于保留发生位置的树路径。 */
export function resolveDiagnosticTarget(
  template: BpTemplate,
  diagnostic: DesignerDiagnostic,
  kind: 'source' | 'definition' | 'instance' = 'source',
): DiagnosticTarget | undefined {
  const entries = listTemplateTree(template)
  if (kind === 'definition')
    return diagnostic.definitionPath ? resolveDefinitionPath(entries, diagnostic.definitionPath) : undefined
  if (kind === 'instance')
    return diagnostic.componentInstancePath ? resolveInstancePath(entries, diagnostic.componentInstancePath) : undefined

  if (diagnostic.sourcePath) {
    const source = resolveSourcePath(entries, diagnostic.sourcePath, diagnostic.componentInstancePath)
    if (source) return source
  }
  if (diagnostic.componentInstancePath) {
    const instance = resolveInstancePath(entries, diagnostic.componentInstancePath)
    if (instance) return instance
  }
  if (diagnostic.definitionPath) return resolveDefinitionPath(entries, diagnostic.definitionPath)
  return undefined
}

function sameIndices(expected: number[] | null | undefined, actual: number[] | undefined): boolean {
  const expectedValues = expected ?? []
  const actualValues = actual ?? []
  return expectedValues.length === actualValues.length && expectedValues.every((value, index) => value === actualValues[index])
}

function matchesDiagnosticElement(element: PreviewElement, diagnostic: DesignerDiagnostic): boolean {
  let pathMatches = false
  if (typeof diagnostic.sourcePath === 'string' && diagnostic.sourcePath.length > 0)
    pathMatches = element.sourcePath === diagnostic.sourcePath
  else if (typeof diagnostic.definitionPath === 'string' && diagnostic.definitionPath.length > 0)
    pathMatches = element.definitionPath === diagnostic.definitionPath
  else if (typeof diagnostic.componentInstancePath === 'string' && diagnostic.componentInstancePath.length > 0)
    pathMatches = element.componentInstancePath === diagnostic.componentInstancePath
  if (!pathMatches) return false
  if (typeof diagnostic.componentInstancePath === 'string' && diagnostic.componentInstancePath.length > 0
    && element.componentInstancePath !== diagnostic.componentInstancePath)
    return false
  return sameIndices(diagnostic.instanceIndices, element.instanceIndices)
}

/** 在预览页中只接受唯一的精确路径匹配，避免把共享定义错误指向第一个实例。 */
export function findDiagnosticPage(pages: PreviewPage[], diagnostic: DesignerDiagnostic): number | undefined {
  if (!Array.isArray(pages)) return undefined
  if (diagnostic.pageIndex !== null && diagnostic.pageIndex !== undefined
    && Number.isInteger(diagnostic.pageIndex) && diagnostic.pageIndex >= 0 && diagnostic.pageIndex < pages.length)
    return diagnostic.pageIndex

  if (!diagnostic.sourcePath && !diagnostic.definitionPath && !diagnostic.componentInstancePath) return undefined
  const matchingPages: number[] = []
  for (const [pageIndex, page] of pages.entries()) {
    const matches = page.elements.filter((element) => matchesDiagnosticElement(element, diagnostic))
    if (matches.length > 0) matchingPages.push(pageIndex)
  }
  if (matchingPages.length !== 1) return undefined
  const page = pages[matchingPages[0]]
  const totalMatches = page.elements.filter((element) => matchesDiagnosticElement(element, diagnostic)).length
  return totalMatches === 1 ? matchingPages[0] : undefined
}
