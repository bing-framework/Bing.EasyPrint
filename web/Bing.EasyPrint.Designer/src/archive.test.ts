import { describe, expect, it } from 'vitest'
import JSZip from 'jszip'
import { collectResourceKeys, createComponentTemplate, exportTemplateBundle, importTemplateBundle } from './archive'
import { createProductSample } from './samples'
import { MAX_ARCHIVE_BYTES, MAX_EXPANDED_BYTES, MAX_TEMPLATE_JSON_BYTES } from './archive'
import { defaultDesignerOutputSettings } from './designerProject'
import type { BpTemplate } from './model'

async function createArchive(entries: Array<[string, string]>): Promise<Blob> {
  const zip = new JSZip()
  for (const [path, contents] of entries) zip.file(path, contents)
  return zip.generateAsync({ type: 'blob' })
}

function asFile(blob: Blob): File {
  return new File([blob], 'template.zip', { type: 'application/zip' })
}

describe('template resource package', () => {
  it('exports only referenced resources and a selected component dependency closure', async () => {
    const template = {
      Version: 3 as const,
      Width: 200,
      Height: 100,
      Rows: [],
      Components: [
        { Id: 'badge', Width: 100, Height: 40, Elements: [{ Type: 6 as const, ComponentId: 'picture' }] },
        { Id: 'picture', Width: 40, Height: 40, Elements: [{ Type: 2 as const, Width: 40, Height: 40, Field: { Value: 'used' } }] },
        { Id: 'unused', Width: 20, Height: 20, Elements: [{ Type: 2 as const, Width: 20, Height: 20, Field: { Value: 'unused-image' } }] },
      ],
    }
    const resources = ['used', 'unused-image', 'orphan'].map((key) => ({
      key, fileName: `${key}.png`, mimeType: 'image/png', dataUrl: 'data:image/png;base64,aA==',
    }))
    const bundleTemplate = createComponentTemplate(template, 'badge')
    expect(bundleTemplate.Components?.map((item) => item.Id)).toEqual(['badge', 'picture'])
    expect([...collectResourceKeys(bundleTemplate).keys]).toEqual(['used'])
    const zip = await exportTemplateBundle(bundleTemplate, resources)
    const imported = await importTemplateBundle(asFile(zip))
    expect(imported.resources.map((item) => item.key)).toEqual(['used'])
  })

  it('keeps the lowest template version for a component package', () => {
    const v2Template: BpTemplate = {
      Version: 2,
      Width: 200,
      Height: 100,
      Rows: [],
      Components: [{
        Id: 'badge',
        Width: 100,
        Height: 40,
        Elements: [{ Type: 1, Field: { Value: 'V2' } }],
      }],
    }

    const result = createComponentTemplate(v2Template, 'badge')

    expect(result.Version).toBe(2)
    expect(result.Components?.[0].Elements?.[0].Field?.Value).toBe('V2')
  })

  it('rejects cyclic component references while collecting resources', () => {
    const template: BpTemplate = {
      Version: 3,
      Width: 200,
      Height: 100,
      Rows: [{ Cells: [{ Elements: [{ Type: 6, ComponentId: 'outer' }] }] }],
      Components: [
        { Id: 'outer', Width: 100, Height: 40, Elements: [{ Type: 6, ComponentId: 'inner' }] },
        { Id: 'inner', Width: 100, Height: 40, Elements: [
          { Type: 2, Width: 20, Height: 20, Field: { Value: 'used' } },
          { Type: 6, ComponentId: 'outer' },
        ] },
      ],
    }

    expect(() => collectResourceKeys(template)).toThrow(/循环/)
  })

  it('round-trips a template and image resource key', async () => {
    const template = createProductSample()
    const image = template.Rows[0].Cells![0].Elements!.find((element) => element.Type === 2)
    if (!image || image.Type !== 2) throw new Error('样例缺少图片元素。')
    image.Crop = { X: 6, Y: 4, Width: 48, Height: 36 }
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

  it('round-trips output settings through the v1 manifest without changing template.json', async () => {
    const template = createProductSample()
    const outputSettings = {
      ...defaultDesignerOutputSettings(),
      outputTarget: 'Tspl' as const,
      encoding: 'Utf8' as const,
      imageThreshold: 96,
      tsplMedia: { dpi: 300, density: 8 },
    }

    const zip = await exportTemplateBundle(template, [], outputSettings)
    const imported = await importTemplateBundle(asFile(zip))

    expect(imported.template).toEqual(template)
    expect(imported.outputSettings).toEqual(outputSettings)
  })

  it('round-trips the ESC/POS Native sequential flow through the archive manifest', async () => {
    const template = createProductSample()
    const outputSettings = {
      ...defaultDesignerOutputSettings(),
      outputTarget: 'EscPosNative' as const,
      encoding: 'Device' as const,
      escPosDevice: { nativeFlow: 'Sequential' as const, profile: {
        name: 'native-text', imageCommand: 'RasterGsV0' as const, bitImageMode: 'DoubleDensity24' as const,
        horizontalDpi: 203, verticalDpi: 203, motionUnitsPerInch: 203,
        supportsCut: true, supportsNativeBarcode: true, supportsNativeQrCode: true,
        supportsPrintAreaWidth: true, supportsTextSize: true, unidirectionalBitImage: false,
        text: { encodingCodePage: 936, characterMode: 'FsKanji' as const,
          characterRanges: [{ first: 0, last: 127 }, { first: 19968, last: 40959 }], lineHeightDots: 26 as const },
      } },
    }
    const zip = await exportTemplateBundle(template, [], outputSettings)
    const imported = await importTemplateBundle(asFile(zip))
    expect(imported.outputSettings?.outputTarget).toBe('EscPosNative')
    expect(imported.outputSettings?.encoding).toBe('Device')
    expect(imported.outputSettings?.escPosDevice.nativeFlow).toBe('Sequential')
    expect(imported.outputSettings?.escPosDevice.profile?.text).toEqual(outputSettings.escPosDevice.profile.text)
  })

  it('round-trips fixed Raster font bytes and alias metadata through the archive manifest', async () => {
    const template = createProductSample()
    const outputSettings = {
      ...defaultDesignerOutputSettings(),
      outputTarget: 'EscPosRaster' as const,
      escPosDevice: {
        rasterFonts: {
          defaultFont: 'Regular', fallbackFonts: ['Fallback'],
          fonts: [
            { name: 'Regular', version: '1.0', regularData: 'dGVzdA==', boldData: 'Ym9sZA==' },
            { name: 'Fallback', version: '2.0', regularData: 'ZmFsbA==' },
          ],
        },
      },
    }
    const zip = await exportTemplateBundle(template, [], outputSettings)
    const imported = await importTemplateBundle(asFile(zip))
    expect(imported.outputSettings?.escPosDevice.rasterFonts).toEqual(outputSettings.escPosDevice.rasterFonts)
    expect(imported.outputSettings?.escPosDevice.rasterFonts?.fonts[0].regularData).toBe('dGVzdA==')
    expect(imported.outputSettings?.escPosDevice.rasterFonts?.fonts[0].boldData).toBe('Ym9sZA==')
  })

  it('round-trips normalized task settings through the archive manifest', async () => {
    const template = createProductSample()
    const outputSettings = {
      ...defaultDesignerOutputSettings(),
      job: {
        copies: 2, copyOrder: 'Collated' as const, feedScope: 'EachPage' as const,
        feedUnit: 'Dots' as const, feedAmount: 12, cutScope: 'JobEnd' as const,
      },
    }
    const zip = await exportTemplateBundle(template, [], outputSettings)
    const imported = await importTemplateBundle(asFile(zip))
    expect(imported.outputSettings?.job).toEqual(outputSettings.job)
  })

  it('rejects positive task feed without a scope when exporting or importing an archive', async () => {
    const template = createProductSample()
    const invalidSettings = { ...defaultDesignerOutputSettings(), job: { feedAmount: 1 } }

    await expect(exportTemplateBundle(template, [], invalidSettings)).rejects.toThrow('进纸数量为正数时必须指定进纸范围')

    const manifest = JSON.stringify({
      format: 'Bing.EasyPrint.TemplateBundle', version: 1, resources: [], outputSettings: invalidSettings,
    })
    const zip = await createArchive([
      ['template.json', JSON.stringify(template)],
      ['manifest.json', manifest],
    ])
    await expect(importTemplateBundle(asFile(zip))).rejects.toThrow('进纸数量为正数时必须指定进纸范围')
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

  it('rejects invalid output settings in a package manifest', async () => {
    const invalidSettings = await createArchive([
      ['template.json', JSON.stringify(createProductSample())],
      ['manifest.json', JSON.stringify({
        format: 'Bing.EasyPrint.TemplateBundle', version: 1, resources: [],
        outputSettings: { outputTarget: 'Zpl', encoding: 'Gbk' },
      })],
    ])

    await expect(importTemplateBundle(asFile(invalidSettings))).rejects.toThrow('ZPL 不支持 Gbk')
  })

  it('rejects unsupported template versions and element types', async () => {
    const unsupportedVersion = { ...createProductSample(), Version: 4 }
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
