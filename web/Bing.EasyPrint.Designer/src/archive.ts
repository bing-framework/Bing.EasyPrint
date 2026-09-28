import JSZip from 'jszip'
import { assertTemplate, type BpTemplate, type TemplateResource } from './model'

export const MAX_TEMPLATE_JSON_BYTES = 1024 * 1024
export const MAX_ARCHIVE_BYTES = 20 * 1024 * 1024
export const MAX_EXPANDED_BYTES = 32 * 1024 * 1024
const MAX_ARCHIVE_ENTRIES = 100

type Manifest = { format: 'Bing.EasyPrint.TemplateBundle'; version: 1; resources: Array<{ key: string; path: string; fileName: string; mimeType: string }> }

function bytesFromDataUrl(dataUrl: string): Uint8Array {
  const separator = dataUrl.indexOf(',')
  if (separator < 0 || !dataUrl.slice(0, separator).includes(';base64')) throw new Error('图片资源必须为 base64 Data URL。')
  const binary = atob(dataUrl.slice(separator + 1))
  const bytes = new Uint8Array(binary.length)
  for (let i = 0; i < binary.length; i += 1) bytes[i] = binary.charCodeAt(i)
  return bytes
}

function dataUrlFromBytes(bytes: Uint8Array, mimeType: string): string {
  let binary = ''
  const chunkSize = 0x8000
  for (let offset = 0; offset < bytes.length; offset += chunkSize) {
    const chunk = bytes.subarray(offset, Math.min(offset + chunkSize, bytes.length))
    binary += String.fromCharCode(...chunk)
  }
  return `data:${mimeType};base64,${btoa(binary)}`
}

function declaredSize(entry: JSZip.JSZipObject): number | undefined {
  return (entry as unknown as { _data?: { uncompressedSize?: number } })._data?.uncompressedSize
}

function safePath(path: string): boolean {
  return !path.startsWith('/') && !path.includes('\\') && !path.includes(':') && !path.split('/').some((part) => part === '..' || part === '.')
}

function sanitizeFileName(name: string): string {
  return name.replace(/[\\/:*?"<>|\u0000-\u001f]/g, '_').slice(0, 100) || 'resource.bin'
}

export async function exportTemplateBundle(template: BpTemplate, resources: TemplateResource[]): Promise<Blob> {
  assertTemplate(template)
  if (resources.length + 2 > MAX_ARCHIVE_ENTRIES) throw new Error('模板资源数量超过限制。')
  const templateText = JSON.stringify(template, null, 2)
  const templateBytes = new TextEncoder().encode(templateText)
  if (templateBytes.byteLength > MAX_TEMPLATE_JSON_BYTES) throw new Error('模板 JSON 超过 1 MiB 上限。')
  const zip = new JSZip()
  zip.file('template.json', templateBytes)
  const manifest: Manifest = { format: 'Bing.EasyPrint.TemplateBundle', version: 1, resources: [] }
  let expandedSize = templateBytes.byteLength
  for (const [index, resource] of resources.entries()) {
    if (!resource.key || resource.key.length > 256 || /[\u0000-\u001f]/.test(resource.key)) throw new Error('图片资源键无效。')
    if (!resource.mimeType.startsWith('image/')) throw new Error('模板包仅接受图片资源。')
    const path = `resources/${String(index + 1).padStart(3, '0')}-${sanitizeFileName(resource.fileName)}`
    manifest.resources.push({ key: resource.key, path, fileName: resource.fileName, mimeType: resource.mimeType })
    const bytes = bytesFromDataUrl(resource.dataUrl)
    if (bytes.byteLength > 10 * 1024 * 1024) throw new Error(`图片资源超过 10 MiB：${resource.fileName}`)
    expandedSize += bytes.byteLength
    if (expandedSize > MAX_EXPANDED_BYTES) throw new Error('模板包解压数据超过 32 MiB 上限。')
    zip.file(path, bytes)
  }
  const manifestBytes = new TextEncoder().encode(JSON.stringify(manifest, null, 2))
  expandedSize += manifestBytes.byteLength
  if (expandedSize > MAX_EXPANDED_BYTES) throw new Error('模板包解压数据超过 32 MiB 上限。')
  zip.file('manifest.json', manifestBytes)
  const blob = await zip.generateAsync({ type: 'blob', compression: 'DEFLATE', compressionOptions: { level: 5 } })
  if (blob.size > MAX_ARCHIVE_BYTES) throw new Error('生成的模板包超过 20 MiB 上限。')
  return blob
}

export async function importTemplateBundle(file: File): Promise<{ template: BpTemplate; resources: TemplateResource[] }> {
  if (file.size > MAX_ARCHIVE_BYTES) throw new Error('模板包超过 20 MiB 上限。')
  const zip = await JSZip.loadAsync(file, { checkCRC32: false, createFolders: false })
  const entries = Object.values(zip.files).filter((entry) => !entry.dir)
  if (entries.length > MAX_ARCHIVE_ENTRIES) throw new Error('模板包文件数量超过限制。')
  let declaredTotal = 0
  for (const entry of entries) {
    if (!safePath(entry.name)) throw new Error('模板包包含不安全的条目路径。')
    const uncompressedSize = declaredSize(entry)
    if (typeof uncompressedSize !== 'number') throw new Error('模板包缺少可校验的条目尺寸。')
    declaredTotal += uncompressedSize
    if (declaredTotal > MAX_EXPANDED_BYTES) throw new Error('模板包解压数据超过 32 MiB 上限。')
    if (entry.name !== 'template.json' && entry.name !== 'manifest.json' && !entry.name.startsWith('resources/')) throw new Error('模板包包含不支持的文件。')
  }
  const templateEntry = zip.file('template.json')
  if (!templateEntry) throw new Error('模板包缺少 template.json。')
  let actualExpanded = 0
  const readBytes = async (entry: JSZip.JSZipObject) => {
    const bytes = await entry.async('uint8array')
    actualExpanded += bytes.byteLength
    if (actualExpanded > MAX_EXPANDED_BYTES) throw new Error('模板包实际解压数据超过 32 MiB 上限。')
    const declared = declaredSize(entry)
    if (declared !== undefined && bytes.byteLength !== declared) throw new Error(`模板包条目尺寸不一致：${entry.name}`)
    return bytes
  }
  const templateBytes = await readBytes(templateEntry)
  if (templateBytes.byteLength > MAX_TEMPLATE_JSON_BYTES) throw new Error('模板 JSON 超过 1 MiB 上限。')
  const templateText = new TextDecoder().decode(templateBytes)
  const templateValue: unknown = JSON.parse(templateText)
  assertTemplate(templateValue)
  const manifestEntry = zip.file('manifest.json')
  if (!manifestEntry) {
    if (entries.some((entry) => entry.name.startsWith('resources/'))) throw new Error('含图片资源的模板包必须提供 manifest.json。')
    return { template: templateValue, resources: [] }
  }
  const manifestBytes = await readBytes(manifestEntry)
  const manifest = JSON.parse(new TextDecoder().decode(manifestBytes)) as Partial<Manifest>
  if (manifest.format !== 'Bing.EasyPrint.TemplateBundle' || manifest.version !== 1 || !Array.isArray(manifest.resources)) throw new Error('模板包清单版本不受支持。')
  if (manifest.resources.length + 2 > MAX_ARCHIVE_ENTRIES) throw new Error('模板资源数量超过限制。')
  const resources: TemplateResource[] = []
  const usedPaths = new Set(['template.json', 'manifest.json'])
  const usedKeys = new Set<string>()
  for (const item of manifest.resources) {
    if (!item || typeof item.key !== 'string' || typeof item.path !== 'string' || typeof item.fileName !== 'string' || typeof item.mimeType !== 'string' || !safePath(item.path)) throw new Error('模板包资源清单无效。')
    if (!item.path.startsWith('resources/')) throw new Error('资源必须位于 resources/ 目录。')
    if (item.key.length < 1 || item.key.length > 256 || /[\u0000-\u001f]/.test(item.key) || !item.mimeType.startsWith('image/')) throw new Error('模板包资源键或类型无效。')
    if (usedPaths.has(item.path) || usedKeys.has(item.key)) throw new Error('模板包资源路径和键不能重复。')
    usedPaths.add(item.path)
    usedKeys.add(item.key)
    const entry = zip.file(item.path)
    if (!entry) throw new Error(`模板包资源缺失：${item.path}`)
    const bytes = await readBytes(entry)
    if (bytes.byteLength > 10 * 1024 * 1024) throw new Error(`图片资源超过 10 MiB：${item.fileName}`)
    resources.push({ key: item.key, fileName: item.fileName, mimeType: item.mimeType, dataUrl: dataUrlFromBytes(bytes, item.mimeType) })
  }
  if (entries.some((entry) => !usedPaths.has(entry.name))) throw new Error('模板包包含未列入清单的文件。')
  return { template: templateValue, resources }
}

export function resourceForImage(key: string, file: File): Promise<TemplateResource> {
  return new Promise((resolve, reject) => {
    if (file.size > 10 * 1024 * 1024) { reject(new Error('单张图片不能超过 10 MiB。')); return }
    if (!['image/png', 'image/jpeg', 'image/bmp', 'image/gif', 'image/tiff', 'image/webp'].includes(file.type.toLowerCase())) {
      reject(new Error('仅支持 PNG、JPEG、BMP、GIF、TIFF 和静态 WebP 图片。'))
      return
    }
    const reader = new FileReader()
    reader.onerror = () => reject(new Error('读取图片失败。'))
    reader.onload = () => resolve({ key, fileName: file.name, mimeType: file.type, dataUrl: String(reader.result) })
    reader.readAsDataURL(file)
  })
}
