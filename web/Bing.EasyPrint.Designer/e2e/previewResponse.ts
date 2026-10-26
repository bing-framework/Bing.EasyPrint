import JSZip from 'jszip'

type PreviewLayout = {
  pageIndex?: number
  pageCount?: number
  pages: Array<{ width: number; height: number; elements: Array<Record<string, unknown>> }>
}

export async function readPreviewLayout(response: {
  url(): string
  json(): Promise<unknown>
  body(): Promise<Buffer>
}): Promise<PreviewLayout> {
  if (!response.url().includes('/api/designer/preview-bundle')) {
    return await response.json() as PreviewLayout
  }
  const archive = await JSZip.loadAsync(await response.body())
  const entry = archive.file('manifest.json')
  if (!entry) throw new Error('预览包缺少 manifest.json。')
  const manifest = JSON.parse(await entry.async('string')) as { layout?: PreviewLayout }
  if (!manifest.layout || !Array.isArray(manifest.layout.pages)) {
    throw new Error('预览包布局清单格式无效。')
  }
  return manifest.layout
}

export function isPreviewBundleResponse(response: { url(): string }): boolean {
  return response.url().includes('/api/designer/preview-bundle')
}
