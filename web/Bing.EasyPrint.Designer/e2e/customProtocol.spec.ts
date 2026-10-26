import { readFile } from 'node:fs/promises'
import { expect, test, type Page, type Response } from '@playwright/test'
import JSZip from 'jszip'

const contractVersionHeader = 'X-EasyPrint-Contract-Version'
const featuresHeader = 'X-EasyPrint-Features'
const features = 'structured-errors,strict-options,binding-diagnostics,protocol-registry,raster-element-png'
const customConfig = { characterWidth: 6, lineHeight: 24, maxPageHeight: 512, supportsFinishing: true }

function headers(): Record<string, string> {
  return {
    'Content-Type': 'application/json',
    [contractVersionHeader]: '1',
    [featuresHeader]: features,
  }
}

function customCapabilities(): Record<string, unknown> {
  return {
    protocol: 'Cpcl', protocolId: 'demo.protocol', escPosLayoutMode: null, nativeFlow: null,
    nativeText: null, rasterFonts: null, dpi: 203, elementTypes: [1, 5, 7], fontKinds: [0],
    deviceFonts: [], textSizes: [], textSize: { minimum: 1, maximum: 512 }, textRotations: [0],
    barcodeTypes: [], barcodeRotations: [0], qrCodeRotations: [0], imageRotations: [0],
    qrModuleSize: null, barcodeModuleWidth: null, barcodeHeight: null,
    supportsExplicitBarcodeModuleWidth: false, supportsWideModuleWidth: false,
    supportsBoldText: false, supportsUnderlineText: false, supportsRotatedUnderlineText: false,
    supportsHostFontFamily: false, supportsLegacyDeviceFont: false, supportsBarcodeHumanReadable: false,
    supportsCut: false, supportsExtraFeed: true, supportsFilledRectangles: true,
    supportsCoordinates: true, supportsExplicitQuietZone: false, jobFeedUnits: [],
    cutModes: [], cutFeedUnits: [], cutFeedMotionUnits: null, cutFeedDots: null,
    tsplCutter: null, zplCutter: null, supportsIndependentOutputSegments: false,
    rasterPreview: {
      softwareBitmap: false, elementPng: false, elementTypes: [], pagePng: false,
      pagePngReason: '示例协议仅演示文本、线段和矩形指令，不提供像素级软件位图。',
      dpiX: 203, dpiY: 203,
    },
    gs1TwoDimensional: { qr: false, dataMatrix: false, native: false, associationValidation: false },
  }
}

async function installRoutes(page: Page, previewBodies: unknown[]) {
  await page.route('**/api/designer/samples', route => route.fulfill({
    status: 200, headers: headers(), body: JSON.stringify({ samples: [] }),
  }))
  await page.route('**/api/designer/protocol-registry', route => route.fulfill({
    status: 200, headers: headers(), body: JSON.stringify({
      contract: '1',
      protocols: [
        { id: 'cpcl', displayName: 'CPCL', isBuiltIn: true, configSchema: {}, constraints: {}, capabilities: {} },
        {
          id: 'demo.protocol', displayName: 'Demo Protocol', isBuiltIn: false,
          configSchema: {
            type: 'object',
            properties: {
              characterWidth: { type: 'integer', minimum: 1, maximum: 128, default: 6, title: '字符宽度' },
              lineHeight: { type: 'integer', minimum: 1, maximum: 512, default: 24, title: '行高' },
              maxPageHeight: { type: 'integer', minimum: 1, maximum: 100000, default: 512, title: '页高上限' },
              supportsFinishing: { type: 'boolean', default: true, title: '收尾能力' },
            },
            required: ['characterWidth', 'lineHeight', 'maxPageHeight'],
          },
          constraints: { maxPageHeight: 100000, elementTypes: [1, 5, 7] },
          capabilities: { dpi: 203, elementTypes: [1, 5, 7] },
        },
      ],
    }),
  }))
  await page.route('**/api/designer/capabilities', async route => {
    const request = route.request().postDataJSON() as Record<string, unknown>
    const body = request.protocolId === 'demo.protocol' ? customCapabilities() : {
      ...customCapabilities(), protocolId: undefined, protocol: 'Cpcl', elementTypes: [1, 5, 7],
    }
    await route.fulfill({ status: 200, headers: headers(), body: JSON.stringify(body) })
  })
  await page.route('**/api/designer/preview', async route => {
    previewBodies.push(route.request().postDataJSON())
    await route.fulfill({
      status: 200,
      headers: headers(),
      body: JSON.stringify({
        protocolId: 'demo.protocol',
        pages: [{
          width: 576, height: 360,
          elements: [{
            type: 1, sourcePath: 'Rows[0].Cells[0].Elements[0]', x: 20, y: 20, width: 180, height: 24,
            text: 'DEMO CUSTOM PROTOCOL',
          }],
        }],
      }),
    })
  })
}

test('discovers a custom protocol, generates its config form, previews and saves project v2', async ({ page }) => {
  const previewBodies: unknown[] = []
  await installRoutes(page, previewBodies)
  await page.goto('/')

  await page.getByLabel('输出协议').selectOption('custom:demo.protocol')
  await page.getByTitle('输出校准设置').click()
  await expect(page.getByText('扩展协议')).toBeVisible()
  await expect(page.getByLabel('字符宽度')).toBeVisible()
  await page.getByLabel('字符宽度').fill('8')

  await expect.poll(() => previewBodies.some(body => {
    const request = body as Record<string, unknown>
    const config = request.protocolConfig as Record<string, unknown> | undefined
    return request.protocolId === 'demo.protocol' && request.protocol === undefined && config?.characterWidth === 8
  }), { timeout: 10_000 }).toBe(true)

  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  const download = await downloadPromise
  const path = await download.path()
  const project = JSON.parse(await readFile(path!, 'utf8')) as Record<string, unknown>
  expect(project.version).toBe(2)
  const settings = project.outputSettings as Record<string, unknown>
  expect(settings.protocolId).toBe('demo.protocol')
  expect((settings.protocolConfig as Record<string, unknown>).characterWidth).toBe(8)
})

test('old host without protocol registry keeps built-in protocols and does not offer custom entries', async ({ page }) => {
  await page.route('**/api/designer/samples', route => route.fulfill({
    status: 200, headers: headers(), body: JSON.stringify({ samples: [] }),
  }))
  await page.route('**/api/designer/protocol-registry', route => route.fulfill({ status: 404, body: 'missing' }))
  await page.route('**/api/designer/capabilities', route => route.fulfill({
    status: 200, headers: headers(), body: JSON.stringify({
      ...customCapabilities(), protocolId: undefined, protocol: 'Cpcl',
    }),
  }))
  await page.route('**/api/designer/preview', route => route.fulfill({
    status: 200, headers: headers(), body: JSON.stringify({ pages: [] }),
  }))
  await page.goto('/')

  const selector = page.getByLabel('输出协议')
  await expect(selector.locator('option')).toHaveCount(5)
  await expect(selector.locator('option[value="custom:demo.protocol"]')).toHaveCount(0)
  await selector.selectOption('Zpl')
  await expect(page.locator('.capability-note')).toBeVisible()
})

test('preserves and disables an unknown protocol from a v2 project', async ({ page }) => {
  await page.route('**/api/designer/samples', route => route.fulfill({
    status: 200, headers: headers(), body: JSON.stringify({ samples: [] }),
  }))
  await page.route('**/api/designer/protocol-registry', route => route.fulfill({
    status: 200, headers: headers(), body: JSON.stringify({
      contract: '1',
      protocols: [{ id: 'cpcl', displayName: 'CPCL', isBuiltIn: true, configSchema: {}, constraints: {}, capabilities: {} }],
    }),
  }))
  await page.route('**/api/designer/capabilities', async route => {
    const request = route.request().postDataJSON() as Record<string, unknown>
    if (request.protocolId === 'unknown.protocol') {
      await route.fulfill({
        status: 400, headers: headers(), body: JSON.stringify({ errors: [{ code: 'InvalidProtocolConfiguration' }] }),
      })
      return
    }
    await route.fulfill({ status: 200, headers: headers(), body: JSON.stringify(customCapabilities()) })
  })
  const previewBodies: unknown[] = []
  await page.route('**/api/designer/preview', async route => {
    previewBodies.push(route.request().postDataJSON())
    await route.fulfill({
      status: 200, headers: headers(), body: JSON.stringify({ pages: [] }),
    })
  })
  await page.goto('/')

  const project = {
    format: 'Bing.EasyPrint.DesignerProject', version: 2,
    template: { Version: 2, Width: 576, Height: 360, Rows: [{ Height: 360, Cells: [{ Width: 576, Elements: [] }] }] },
    outputSettings: {
      outputTarget: 'Cpcl', protocolId: 'unknown.protocol',
      protocolConfig: { characterWidth: 9, lineHeight: 25, maxPageHeight: 700 },
      allowRasterizedLines: false, imageThreshold: 128,
      deviceFontCalibrations: { Tspl: {}, Zpl: {} }, requireDeclaredNativeBounds: false,
      validateGs1Associations: false, escPosDevice: {}, tsplMedia: {}, zplMedia: {},
    },
  }
  await page.getByTestId('template-import').setInputFiles({
    name: 'unknown-protocol.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(project)),
  })

  await expect(page.getByLabel('输出协议')).toHaveValue('custom:unknown.protocol')
  await page.getByTitle('输出校准设置').click()
  const panel = page.locator('.output-settings-panel')
  await expect(panel).toContainText('unknown.protocol')
  await expect(panel).toContainText('未注册')
  await page.waitForTimeout(600)
  expect(previewBodies.some(body => (body as Record<string, unknown>).protocolId === 'unknown.protocol')).toBe(false)

  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  const download = await downloadPromise
  const path = await download.path()
  const saved = JSON.parse(await readFile(path!, 'utf8')) as Record<string, unknown>
  const settings = saved.outputSettings as Record<string, unknown>
  expect(settings.protocolId).toBe('unknown.protocol')
  expect(settings.protocolConfig).toEqual(project.outputSettings.protocolConfig)
})

async function bundleManifest(response: Response): Promise<Record<string, unknown>> {
  const zip = await JSZip.loadAsync(await response.body())
  const manifest = zip.file('manifest.json')
  if (!manifest) throw new Error('preview bundle manifest missing')
  return JSON.parse(await manifest.async('string')) as Record<string, unknown>
}

test('real host discovers demo protocol, changes layout, exports and reloads v2', async ({ page }) => {
  await page.goto('/')
  await page.getByLabel('输出协议').selectOption('custom:demo.protocol')
  await page.getByTitle('输出校准设置').click()
  const panel = page.locator('.output-settings-panel')
  await expect(panel).toContainText('Demo Protocol')

  const project = JSON.parse(await readFile('../../samples/Protocols/DemoProtocol/demo-project.json', 'utf8')) as Record<string, unknown>
  const initialPreview = page.waitForResponse(response =>
    response.url().includes('/api/designer/preview-bundle') && response.status() === 200)
  await page.getByTestId('template-import').setInputFiles({
    name: 'real-host.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(project)),
  })
  const initialManifest = await bundleManifest(await initialPreview)
  const initialLayout = (initialManifest.layout as { pages: { elements: { type: number; width: number }[] }[] })
  const initialQrWidth = initialLayout.pages[0]!.elements.find(element => element.type === 3)!.width

  await expect(panel.getByLabel('二维码模块宽度')).toHaveValue('4')
  const changedPreview = page.waitForResponse(response =>
    response.url().includes('/api/designer/preview-bundle') &&
    (response.request().postDataJSON() as { protocolConfig?: { qrModuleWidth?: number } }).protocolConfig?.qrModuleWidth === 6)
  await panel.getByLabel('二维码模块宽度').fill('6')
  const changedManifest = await bundleManifest(await changedPreview)
  const changedLayout = (changedManifest.layout as { pages: { elements: { type: number; width: number }[] }[] })
  const changedQrWidth = changedLayout.pages.flatMap(page => page.elements).find(element => element.type === 3)!.width
  expect(changedQrWidth).toBeGreaterThan(initialQrWidth)

  await panel.getByLabel('启用新协议任务收尾').check()
  await panel.getByLabel('收尾进纸量').fill('2')
  await panel.getByLabel('收尾切纸').check()
  await panel.getByLabel('收尾插入位置').selectOption('JobEnd')
  const exportDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: /生成 Demo Protocol/ }).click()
  const archivePath = await (await exportDownload).path()
  const archive = await JSZip.loadAsync(await readFile(archivePath!))
  const pageEntry = archive.file('page-001.demo-protocol')
  expect(pageEntry).not.toBeNull()
  expect(archive.file('finish-001.demo-protocol')).not.toBeNull()
  const pageText = await pageEntry!.async('string')
  expect(pageText).toContain('Q ')
  expect(pageText).toContain(' HELLO')

  const saveDownload = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  const savedPath = await (await saveDownload).path()
  const saved = JSON.parse(await readFile(savedPath!, 'utf8')) as Record<string, unknown>
  const settings = saved.outputSettings as Record<string, unknown>
  const savedEditorState = saved.editorState as Record<string, unknown>
  const savedNodeStates = savedEditorState.nodeStates as Record<string, { id: string; locked: boolean }>
  expect(settings.protocolId).toBe('demo.protocol')
  expect((settings.protocolConfig as Record<string, unknown>).qrModuleWidth).toBe(6)
  expect(savedEditorState).toMatchObject(project.editorState as Record<string, unknown>)
  expect(savedNodeStates['Rows[0].Cells[0].Elements[0]']?.id).toBeTruthy()

  await page.getByTestId('template-import').setInputFiles({
    name: 'saved.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(saved)),
  })
  if (!(await page.locator('.output-settings-panel').isVisible()))
    await page.getByTitle('输出校准设置').click()
  await expect(page.locator('.output-settings-panel').getByLabel('二维码模块宽度')).toHaveValue('6')
  await expect(page.locator('.output-settings-panel').getByLabel('启用新协议任务收尾')).toBeChecked()
  await expect(page.locator('.output-settings-panel').getByLabel('收尾进纸量')).toHaveValue('2')
  await expect(page.locator('.output-settings-panel').getByLabel('启用新协议任务收尾')).toBeChecked()
  await expect(page.locator('.output-settings-panel').getByLabel('收尾进纸量')).toHaveValue('2')
  const roundTripSave = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  const roundTripped = JSON.parse(await readFile(await (await roundTripSave).path(), 'utf8')) as Record<string, unknown>
  expect((roundTripped.editorState as Record<string, unknown>).nodeStates).toEqual(savedNodeStates)
})
