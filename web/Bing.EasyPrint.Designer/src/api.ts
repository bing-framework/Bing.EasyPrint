import type { BpTemplate, PreviewResponse, TemplateResource } from './model'

export type DesignerSample = { id: string; name: string; template: BpTemplate; data: unknown }
export type PreviewRequest = { template: BpTemplate; data: unknown; resources?: Record<string, string> }

export class DesignerValidationError extends Error {}

function resourcesAsBase64(resources: TemplateResource[]): Record<string, string> | undefined {
  if (!resources.length) return undefined
  return Object.fromEntries(resources.map((resource) => [resource.key, resource.dataUrl.split(',')[1] ?? '']))
}

async function responseError(response: Response): Promise<string> {
  try {
    const body = await response.json() as { errors?: Array<{ sourcePath?: string; message?: string }>; message?: string; title?: string }
    const messages = body.errors?.map((item) => `${item.sourcePath ? `${item.sourcePath}: ` : ''}${item.message ?? ''}`).filter(Boolean)
    return messages?.join('\n') || body.message || body.title || `服务返回 HTTP ${response.status}`
  } catch {
    return `服务返回 HTTP ${response.status}`
  }
}

export async function loadDesignerSamples(signal?: AbortSignal): Promise<DesignerSample[]> {
  const response = await fetch('/api/designer/samples', { signal })
  if (!response.ok) throw new Error(await responseError(response))
  const body = await response.json() as { samples?: DesignerSample[] }
  if (!Array.isArray(body.samples)) throw new Error('样例服务返回的数据格式无效。')
  return body.samples
}

export async function previewTemplate(template: BpTemplate, data: unknown, resources: TemplateResource[], signal?: AbortSignal): Promise<PreviewResponse> {
  const body: PreviewRequest = { template, data, resources: resourcesAsBase64(resources) }
  const response = await fetch('/api/designer/preview', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
    signal,
  })
  if (!response.ok) throw new DesignerValidationError(await responseError(response))
  const result = await response.json() as PreviewResponse
  if (!Array.isArray(result.pages)) throw new Error('预览服务返回的数据格式无效。')
  return result
}

export async function renderTemplate(template: BpTemplate, data: unknown, resources: TemplateResource[]): Promise<Blob> {
  const body: PreviewRequest = { template, data, resources: resourcesAsBase64(resources) }
  const response = await fetch('/api/designer/render', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!response.ok) throw new Error(await responseError(response))
  return response.blob()
}
