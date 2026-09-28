import { describe, expect, it } from 'vitest'
import JSZip from 'jszip'
import { exportTemplateBundle, importTemplateBundle } from './archive'
import { createProductSample } from './samples'
import { MAX_ARCHIVE_BYTES, MAX_EXPANDED_BYTES, MAX_TEMPLATE_JSON_BYTES } from './archive'

async function createArchive(entries: Array<[string, string]>): Promise<Blob> {
  const zip = new JSZip()
  for (const [path, contents] of entries) zip.file(path, contents)
  return zip.generateAsync({ type: 'blob' })
}

function asFile(blob: Blob): File {
  return new File([blob], 'template.zip', { type: 'application/zip' })
}

describe('template resource package', () => {
  it('round-trips a template and image resource key', async () => {
    const template = createProductSample()
    const resource = {
      key: 'product/photo.jpg',
      fileName: 'photo.jpg',
      mimeType: 'image/png',
      dataUrl: 'data:image/png;base64,aGVsbG8=',
    }
    const zip = await exportTemplateBundle(template, [resource])
    const imported = await importTemplateBundle(asFile(zip))
    expect(imported.template).toEqual(template)
    expect(imported.resources).toEqual([resource])
  })

  it('exports JSON-only templates as an importable package', async () => {
    const template = createProductSample()
    const zip = await exportTemplateBundle(template, [])
    const result = await importTemplateBundle(asFile(zip))
    expect(result.template).toEqual(template)
    expect(result.resources).toEqual([])
  })

  it('rejects malformed ZIP data', async () => {
    await expect(importTemplateBundle(new File(['not a zip'], 'broken.zip'))).rejects.toThrow()
  })

  it('rejects unsafe archive entry paths', async () => {
    const traversalPackage = await createArchive([
      ['template.json', JSON.stringify(createProductSample())],
      ['../outside.txt', 'unexpected'],
    ])

    await expect(importTemplateBundle(asFile(traversalPackage))).rejects.toThrow()
  })

  it('rejects path traversal in the resource manifest', async () => {
    const template = JSON.stringify(createProductSample())
    const manifest = JSON.stringify({
      format: 'Bing.EasyPrint.TemplateBundle',
      version: 1,
      resources: [{ key: 'photo', path: 'resources/../outside.png', fileName: 'outside.png', mimeType: 'image/png' }],
    })
    const traversalPackage = await createArchive([
      ['template.json', template],
      ['manifest.json', manifest],
      ['resources/photo.png', 'image-bytes'],
    ])

    await expect(importTemplateBundle(asFile(traversalPackage))).rejects.toThrow('模板包资源清单无效')
  })

  it('rejects unsupported manifest versions, malformed JSON, and invalid structure', async () => {
    const template = JSON.stringify(createProductSample())
    const unsupportedVersion = await createArchive([
      ['template.json', template],
      ['manifest.json', JSON.stringify({ format: 'Bing.EasyPrint.TemplateBundle', version: 2, resources: [] })],
    ])
    const malformedManifest = await createArchive([
      ['template.json', template],
      ['manifest.json', '{'],
    ])
    const invalidStructure = await createArchive([
      ['template.json', template],
      ['manifest.json', JSON.stringify({ format: 'Bing.EasyPrint.TemplateBundle', version: 1, resources: {} })],
    ])

    await expect(importTemplateBundle(asFile(unsupportedVersion))).rejects.toThrow('模板包清单版本不受支持')
    await expect(importTemplateBundle(asFile(malformedManifest))).rejects.toThrow()
    await expect(importTemplateBundle(asFile(invalidStructure))).rejects.toThrow('模板包清单版本不受支持')
  })

  it('rejects unsupported template versions and element types', async () => {
    const unsupportedVersion = { ...createProductSample(), Version: 3 }
    const unsupportedType = createProductSample()
    unsupportedType.Rows[0].Cells![0].Elements![0].Type = 11 as never

    for (const template of [unsupportedVersion, unsupportedType]) {
      const zip = await createArchive([['template.json', JSON.stringify(template)]])
      await expect(importTemplateBundle(asFile(zip))).rejects.toThrow()
    }
  })

  it('rejects archive entry counts over the import limit', async () => {
    const entries: Array<[string, string]> = Array.from({ length: 101 }, (_, index) => [`resources/${index}.bin`, 'x'])
    const zip = await createArchive(entries)

    await expect(importTemplateBundle(asFile(zip))).rejects.toThrow('模板包文件数量超过限制')
  })

  it('rejects export resource counts over the package limit', async () => {
    const resources = Array.from({ length: 99 }, (_, index) => ({
      key: `image-${index}`,
      fileName: `${index}.png`,
      mimeType: 'image/png',
      dataUrl: 'data:image/png;base64,',
    }))

    await expect(exportTemplateBundle(createProductSample(), resources)).rejects.toThrow('模板资源数量超过限制')
  })

  it('rejects an archive whose declared file size exceeds the import limit without allocating the payload', async () => {
    const oversizedFile = { size: MAX_ARCHIVE_BYTES + 1 } as File

    await expect(importTemplateBundle(oversizedFile)).rejects.toThrow('模板包超过 20 MiB 上限')
  })

  it('rejects declared expanded entry sizes before inflating a highly-compressed payload', async () => {
    const zip = new JSZip()
    zip.file('resources/large.bin', new Uint8Array(MAX_EXPANDED_BYTES + 1).fill(0x41))
    const compressedPackage = await zip.generateAsync({ type: 'blob', compression: 'DEFLATE' })

    expect(compressedPackage.size).toBeLessThan(MAX_ARCHIVE_BYTES)
    await expect(importTemplateBundle(asFile(compressedPackage))).rejects.toThrow('模板包解压数据超过 32 MiB 上限')
  })

  it('rejects oversized template JSON on import and export', async () => {
    const template = createProductSample()
    template.Rows[0].Cells![0].Elements![0].Field!.Value = 'x'.repeat(MAX_TEMPLATE_JSON_BYTES)
    const oversizedTemplate = JSON.stringify(template)
    const zip = await createArchive([['template.json', oversizedTemplate]])

    await expect(importTemplateBundle(asFile(zip))).rejects.toThrow('模板 JSON 超过 1 MiB 上限')
    await expect(exportTemplateBundle(template, [])).rejects.toThrow('模板 JSON 超过 1 MiB 上限')
  })
})
