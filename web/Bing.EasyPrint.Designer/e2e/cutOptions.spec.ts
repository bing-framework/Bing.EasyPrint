import { expect, test, type APIRequestContext, type Page } from '@playwright/test'
import { readFile } from 'node:fs/promises'
import JSZip from 'jszip'

const apiUrl = () => process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'

const escPosTemplate = {
  Version: 3,
  Width: 320,
  Height: 48,
  Rows: [{ Height: 48, Cells: [{ Width: 320, Elements: [{
    Type: 1,
    FontSize: 24,
    Field: { Value: 'CUT' },
  }] }] }],
}

const twoPageTemplate = {
  Version: 3,
  Width: 320,
  Height: 24,
  Rows: [
    { Height: 24, Cells: [{ Width: 320, Elements: [{ Type: 1, FontSize: 24, Field: { Value: 'A' } }] }] },
    { Height: 24, Cells: [{ Width: 320, Elements: [{ Type: 1, FontSize: 24, Field: { Value: 'B' } }] }] },
  ],
}

function escPosProfile(options: {
  supportedCutModes?: string[]
  supportsFeedAndCut?: boolean
  verticalDpi?: number
  motionUnitsPerInch?: number
} = {}) {
  return {
    name: 'Cut QA',
    imageCommand: 'RasterGsV0',
    bitImageMode: 'DoubleDensity24',
    horizontalDpi: options.verticalDpi ?? 203,
    verticalDpi: options.verticalDpi ?? 203,
    motionUnitsPerInch: options.motionUnitsPerInch ?? 203,
    supportsCut: true,
    ...(options.supportedCutModes === undefined ? {} : { supportedCutModes: options.supportedCutModes }),
    supportsFeedAndCut: options.supportsFeedAndCut ?? true,
    supportsNativeBarcode: true,
    supportsNativeQrCode: true,
    supportsPrintAreaWidth: true,
    supportsTextSize: true,
    unidirectionalBitImage: false,
  }
}

function escPosPayload(overrides: Record<string, unknown> = {}) {
  return {
    template: escPosTemplate,
    data: {},
    protocol: 'EscPos',
    escPosLayoutMode: 'Native',
    encoding: 'Ascii',
    ...overrides,
  }
}

async function archivePage(request: APIRequestContext, payload: Record<string, unknown>, extension = 'escpos') {
  const response = await request.post(`${apiUrl()}/api/designer/render`, { data: payload })
  expect(response.status()).toBe(200)
  const archive = await JSZip.loadAsync(await response.body())
  const page = archive.file(`page-001.${extension}`)
  expect(page).not.toBeNull()
  return page!.async('uint8array')
}

async function renderArchive(request: APIRequestContext, payload: Record<string, unknown>) {
  const response = await request.post(`${apiUrl()}/api/designer/render`, { data: payload })
  expect(response.status()).toBe(200)
  return JSZip.loadAsync(await response.body())
}

function designerProject(options: {
  cutOptions: Record<string, unknown>
  profile: Record<string, unknown>
}) {
  return {
    format: 'Bing.EasyPrint.DesignerProject',
    version: 1,
    template: escPosTemplate,
    outputSettings: {
      outputTarget: 'EscPosNative',
      allowRasterizedLines: false,
      imageThreshold: 128,
      encoding: 'Ascii',
      cutAfterPrint: true,
      cutOptions: options.cutOptions,
      deviceFontCalibrations: { Tspl: {}, Zpl: {} },
      requireDeclaredNativeBounds: false,
      escPosDevice: { profile: options.profile },
      tsplMedia: {},
      zplMedia: {},
    },
  }
}

function designerJobProject(options: {
  job: Record<string, unknown>
  profile: Record<string, unknown>
}) {
  return {
    format: 'Bing.EasyPrint.DesignerProject',
    version: 1,
    template: escPosTemplate,
    outputSettings: {
      outputTarget: 'EscPosNative',
      allowRasterizedLines: false,
      imageThreshold: 128,
      encoding: 'Ascii',
      job: options.job,
      deviceFontCalibrations: { Tspl: {}, Zpl: {} },
      requireDeclaredNativeBounds: false,
      escPosDevice: { profile: options.profile },
      tsplMedia: {},
      zplMedia: {},
    },
  }
}

async function importProject(page: Page, project: object, name: string) {
  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const previewPromise = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await page.getByTestId('template-import').setInputFiles({
    name,
    mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify(project)),
  })
  await previewPromise
  await expect(page.getByRole('combobox', { name: '输出协议' })).toHaveValue('EscPosNative')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
}

async function exportEscPosPage(page: Page) {
  const responsePromise = page.waitForResponse((response) =>
    response.url().includes('/api/designer/render') && response.request().method() === 'POST')
  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 ESC/POS' }).click()
  expect((await responsePromise).status()).toBe(200)
  const archive = await JSZip.loadAsync(await readFile(await (await downloadPromise).path()))
  const pageEntry = archive.file('page-001.escpos')
  expect(pageEntry).not.toBeNull()
  return pageEntry!.async('uint8array')
}

async function exportEscPosArchive(page: Page) {
  const responsePromise = page.waitForResponse((response) =>
    response.url().includes('/api/designer/render') && response.request().method() === 'POST')
  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 ESC/POS' }).click()
  expect((await responsePromise).status()).toBe(200)
  return JSZip.loadAsync(await readFile(await (await downloadPromise).path()))
}

async function saveProject(page: Page) {
  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  return JSON.parse((await readFile(await (await downloadPromise).path())).toString('utf8'))
}

test('ESC/POS 裁切能力和 Function B 页面尾字节遵守默认与补偿契约', async ({ request }) => {
  const profile = escPosProfile({ supportedCutModes: ['Full', 'Partial'], supportsFeedAndCut: true, verticalDpi: 300 })
  const capabilitiesResponse = await request.post(`${apiUrl()}/api/designer/capabilities`, {
    data: escPosPayload({ escPosDevice: { profile: escPosProfile({ supportedCutModes: ['Partial'], supportsFeedAndCut: true }) } }),
  })
  expect(capabilitiesResponse.status()).toBe(200)
  expect(await capabilitiesResponse.json()).toMatchObject({
    cutModes: ['Partial'],
    cutFeedUnits: ['MotionUnits', 'Dots'],
    cutFeedMotionUnits: { minimum: 0, maximum: 255 },
  })

  const legacyBytes = await archivePage(request, escPosPayload({ cutAfterPrint: true }))
  expect(Array.from(legacyBytes.slice(-3))).toEqual([0x1D, 0x56, 0x00])

  const fullBytes = await archivePage(request, escPosPayload({
    cutAfterPrint: true,
    cutOptions: { mode: 'Full', feedToCutPosition: false, feedBeforeCutAmount: 0, feedBeforeCutUnit: 'MotionUnits' },
    escPosDevice: { profile },
  }))
  expect(Array.from(fullBytes.slice(-3))).toEqual([0x1D, 0x56, 0x00])

  const partialBytes = await archivePage(request, escPosPayload({
    cutAfterPrint: true,
    cutOptions: { mode: 'Partial' },
    escPosDevice: { profile },
  }))
  expect(Array.from(partialBytes.slice(-3))).toEqual([0x1D, 0x56, 0x01])

  const zeroFunctionBBytes = await archivePage(request, escPosPayload({
    cutAfterPrint: true,
    cutOptions: { mode: 'Partial', feedToCutPosition: true, feedBeforeCutAmount: 0, feedBeforeCutUnit: 'MotionUnits' },
    escPosDevice: { profile },
  }))
  expect(Array.from(zeroFunctionBBytes.slice(-4))).toEqual([0x1D, 0x56, 0x42, 0x00])

  const dotsBytes = await archivePage(request, escPosPayload({
    cutAfterPrint: true,
    cutOptions: { mode: 'Full', feedToCutPosition: true, feedBeforeCutAmount: 4, feedBeforeCutUnit: 'Dots' },
    escPosDevice: { profile },
  }))
  // ceil(4 dots * 203 motion-units/in / 300 vertical-dpi) = 3.
  expect(Array.from(dotsBytes.slice(-4))).toEqual([0x1D, 0x56, 0x41, 0x03])

  const disabledProfile = escPosProfile({ supportedCutModes: [], supportsFeedAndCut: true })
  const disabledCapabilities = await request.post(`${apiUrl()}/api/designer/capabilities`, {
    data: escPosPayload({ escPosDevice: { profile: disabledProfile } }),
  })
  expect(disabledCapabilities.status()).toBe(200)
  expect(await disabledCapabilities.json()).toMatchObject({
    cutModes: [], cutFeedUnits: [], cutFeedMotionUnits: null,
  })
  const disabledCut = await request.post(`${apiUrl()}/api/designer/preview`, {
    data: escPosPayload({
      cutAfterPrint: true,
      cutOptions: { mode: 'Full' },
      escPosDevice: { profile: disabledProfile },
    }),
  })
  expect(disabledCut.status()).toBe(400)
})

test('ESC/POS 任务级切纸按页面、份数和任务边界生成独立页面尾字节', async ({ request }) => {
  const profile = escPosProfile({ supportedCutModes: ['Partial'], supportsFeedAndCut: true })
  const expectedByScope: Record<string, boolean[]> = {
    EachPage: [true, true, true, true],
    EachCopy: [false, true, false, true],
    JobEnd: [false, false, false, true],
  }

  for (const [cutScope, expectedCuts] of Object.entries(expectedByScope)) {
    const archive = await renderArchive(request, {
      template: twoPageTemplate,
      data: {},
      protocol: 'EscPos',
      escPosLayoutMode: 'Native',
      encoding: 'Ascii',
      escPosDevice: { profile },
      job: {
        copies: 2,
        copyOrder: 'Collated',
        feedScope: 'None',
        feedUnit: 'MotionUnits',
        feedAmount: 0,
        cutScope,
        cutOptions: { mode: 'Partial', feedToCutPosition: true, feedBeforeCutAmount: 0, feedBeforeCutUnit: 'MotionUnits' },
      },
    })
    const manifest = JSON.parse(await archive.file('job.json')!.async('text'))
    expect(manifest.pages.map((page: { sourcePageIndex: number; copyIndex: number; cutAfterPrint: boolean }) =>
      [page.sourcePageIndex, page.copyIndex, page.cutAfterPrint])).toEqual([
      [0, 0, expectedCuts[0]], [1, 0, expectedCuts[1]], [0, 1, expectedCuts[2]], [1, 1, expectedCuts[3]],
    ])

    for (let index = 0; index < expectedCuts.length; index++) {
      const page = await archive.file(`page-${String(index + 1).padStart(3, '0')}.escpos`)!.async('uint8array')
      if (expectedCuts[index]) expect(Array.from(page.slice(-4))).toEqual([0x1D, 0x56, 0x42, 0x00])
      else expect(Array.from(page.slice(-4))).not.toEqual([0x1D, 0x56, 0x42, 0x00])
    }
  }
})

test('非法裁切模式、补偿和范围组合在预检拒绝，TSPL/ZPL 保留旧链路', async ({ request }) => {
  const profile = escPosProfile({ supportedCutModes: ['Full', 'Partial'], supportsFeedAndCut: true, verticalDpi: 300 })
  const invalidBodies: Array<{ name: string; payload: Record<string, unknown> }> = [
    {
      name: 'unknown mode',
      payload: escPosPayload({ cutAfterPrint: true, cutOptions: { mode: 'Guillotine' }, escPosDevice: { profile } }),
    },
    {
      name: 'positive compensation without feed',
      payload: escPosPayload({ cutAfterPrint: true, cutOptions: { mode: 'Full', feedBeforeCutAmount: 1 }, escPosDevice: { profile } }),
    },
    {
      name: 'negative compensation',
      payload: escPosPayload({ cutAfterPrint: true, cutOptions: { mode: 'Full', feedBeforeCutAmount: -1 }, escPosDevice: { profile } }),
    },
    {
      name: 'fractional compensation',
      payload: escPosPayload({ cutAfterPrint: true, cutOptions: { mode: 'Full', feedBeforeCutAmount: 1.5 }, escPosDevice: { profile } }),
    },
    {
      name: 'Function B without profile support',
      payload: escPosPayload({
        cutAfterPrint: true,
        cutOptions: { mode: 'Full', feedToCutPosition: true, feedBeforeCutAmount: 0 },
        escPosDevice: { profile: escPosProfile({ supportedCutModes: ['Full'], supportsFeedAndCut: false }) },
      }),
    },
    {
      name: 'Lines compensation',
      payload: escPosPayload({
        cutAfterPrint: true,
        cutOptions: { mode: 'Full', feedToCutPosition: true, feedBeforeCutAmount: 1, feedBeforeCutUnit: 'Lines' },
        escPosDevice: { profile },
      }),
    },
    {
      name: 'Dots compensation over 255 motion units',
      payload: escPosPayload({
        cutAfterPrint: true,
        cutOptions: { mode: 'Full', feedToCutPosition: true, feedBeforeCutAmount: 378, feedBeforeCutUnit: 'Dots' },
        escPosDevice: { profile },
      }),
    },
    {
      name: 'unknown nested cutOptions field',
      payload: escPosPayload({
        cutAfterPrint: true,
        cutOptions: { mode: 'Full', typo: true },
        escPosDevice: { profile },
      }),
    },
    {
      name: 'job cut scope None',
      payload: escPosPayload({
        job: { cutScope: 'None', cutOptions: { mode: 'Full' } },
        escPosDevice: { profile },
      }),
    },
    {
      name: 'page and job cutOptions together',
      payload: escPosPayload({
        cutAfterPrint: true,
        cutOptions: { mode: 'Full' },
        job: { cutScope: 'JobEnd', cutOptions: { mode: 'Partial' } },
        escPosDevice: { profile },
      }),
    },
    {
      name: 'page cutOptions with cutAfterPrint false',
      payload: escPosPayload({
        cutAfterPrint: false,
        cutOptions: { mode: 'Full' },
        escPosDevice: { profile },
      }),
    },
  ]

  for (const invalid of invalidBodies) {
    const response = await request.post(`${apiUrl()}/api/designer/preview`, { data: invalid.payload })
    expect(response.status(), invalid.name).toBe(400)
  }

  const protocolCases = [
    { protocol: 'Tspl', extension: 'tspl' },
    { protocol: 'Zpl', extension: 'zpl' },
  ] as const
  for (const { protocol, extension } of protocolCases) {
    const legacyPayload = { template: escPosTemplate, data: {}, protocol, encoding: 'Ascii' }
    const legacyBytes = await archivePage(request, legacyPayload, extension)
    const explicit = await request.post(`${apiUrl()}/api/designer/preview`, {
      data: {
        ...legacyPayload,
        cutAfterPrint: true,
        cutOptions: { mode: 'Partial' },
      },
    })
    expect(explicit.status(), `${protocol} cutOptions`).toBe(400)

    const legacyExplicitFalse = await archivePage(request, { ...legacyPayload, cutAfterPrint: false }, extension)
    expect(legacyExplicitFalse).toEqual(legacyBytes)
  }
})

test('UI 启用 Partial 与补偿后保存重载和导出字节一致，撤销可恢复', async ({ page }) => {
  const profile = escPosProfile({ supportedCutModes: ['Full', 'Partial'], supportsFeedAndCut: true })
  await importProject(page, designerProject({
    profile,
    cutOptions: { mode: 'Partial', feedToCutPosition: true, feedBeforeCutAmount: 0, feedBeforeCutUnit: 'MotionUnits' },
  }), 'cut-ui-roundtrip.json')
  await page.getByRole('button', { name: '输出校准设置' }).click()

  const customToggle = page.getByLabel('启用页面自定义裁切参数')
  await expect(customToggle).toBeChecked()
  const mode = page.getByLabel('页面切法')
  await mode.selectOption('Partial')
  await expect(mode).toHaveValue('Partial')

  const feedToggle = page.getByLabel('页面进纸到切刀位置')
  await expect(feedToggle).toBeEnabled()
  await feedToggle.check()
  const amount = page.getByLabel('页面切前补偿量')
  await expect(amount).toBeEnabled()
  await amount.fill('4')
  await page.getByLabel('页面切前补偿单位').selectOption('Dots')
  await expect(amount).toHaveValue('4')
  await page.waitForTimeout(400)

  await page.getByRole('button', { name: '撤销 Ctrl+Z' }).click()
  await expect(amount).toHaveValue('0')
  await page.getByRole('button', { name: '重做 Ctrl+Y' }).click()
  await expect(amount).toHaveValue('4')

  const beforeBytes = await exportEscPosPage(page)
  const saved = await saveProject(page)
  expect(saved.outputSettings).toMatchObject({
    outputTarget: 'EscPosNative',
    cutAfterPrint: true,
    cutOptions: { mode: 'Partial', feedToCutPosition: true, feedBeforeCutAmount: 4, feedBeforeCutUnit: 'Dots' },
  })

  await importProject(page, saved, 'cut-ui-roundtrip-saved.json')
  await page.getByRole('button', { name: '输出校准设置' }).click()
  await expect(page.getByLabel('页面切法')).toHaveValue('Partial')
  await expect(page.getByLabel('页面进纸到切刀位置')).toBeChecked()
  await expect(page.getByLabel('页面切前补偿量')).toHaveValue('4')
  await expect(page.getByLabel('页面切前补偿单位')).toHaveValue('Dots')
  expect(await exportEscPosPage(page)).toEqual(beforeBytes)
})

test('UI 保留不支持的切法并可修正为白名单值', async ({ page }) => {
  const profile = escPosProfile({ supportedCutModes: ['Full'], supportsFeedAndCut: false })
  await importProject(page, designerProject({
    profile,
    cutOptions: { mode: 'Partial', feedToCutPosition: false, feedBeforeCutAmount: 0, feedBeforeCutUnit: 'MotionUnits' },
  }), 'cut-ui-unsupported.json')
  await page.getByRole('button', { name: '输出校准设置' }).click()

  const mode = page.getByLabel('页面切法')
  await expect(mode).toHaveValue('Partial')
  await expect(page.getByText('当前机型不支持所选切法，已保留该值供纠正。')).toBeVisible()
  await expect(mode.locator('option[value="Partial"]')).toHaveJSProperty('disabled', true)

  const previewPromise = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST' && response.ok())
  await mode.selectOption('Full')
  await previewPromise
  await expect(mode).toHaveValue('Full')
  await expect(page.getByText('当前机型不支持所选切法，已保留该值供纠正。')).toHaveCount(0)
})

test('UI 任务级 EachCopy 裁切保存重载后导出相同字节', async ({ page }) => {
  const profile = escPosProfile({ supportedCutModes: ['Partial'], supportsFeedAndCut: true })
  const job = {
    copies: 2,
    copyOrder: 'Collated',
    feedScope: 'None',
    feedUnit: 'Lines',
    feedAmount: 0,
    cutScope: 'EachCopy',
    cutOptions: { mode: 'Partial', feedToCutPosition: true, feedBeforeCutAmount: 0, feedBeforeCutUnit: 'MotionUnits' },
  }
  await importProject(page, designerJobProject({ profile, job }), 'cut-ui-job.json')
  await page.getByRole('button', { name: '输出校准设置' }).click()

  await expect(page.getByLabel('启用任务级输出')).toBeChecked()
  await expect(page.getByLabel('任务打印份数')).toHaveValue('2')
  await expect(page.getByLabel('任务切纸范围')).toHaveValue('EachCopy')

  const customToggle = page.getByLabel('启用任务自定义裁切参数')
  await expect(customToggle).toBeChecked()
  const mode = page.getByLabel('任务切法')
  await mode.selectOption('Partial')
  await expect(mode).toHaveValue('Partial')

  const feedToggle = page.getByLabel('任务进纸到切刀位置')
  await expect(feedToggle).toBeChecked()
  const amount = page.getByLabel('任务切前补偿量')
  await amount.fill('4')
  await page.getByLabel('任务切前补偿单位').selectOption('Dots')
  await expect(amount).toHaveValue('4')
  await page.waitForTimeout(400)

  const beforeArchive = await exportEscPosArchive(page)
  const beforeManifest = JSON.parse(await beforeArchive.file('job.json')!.async('text'))
  expect(beforeManifest.pages.map((item: { copyIndex: number; cutAfterPrint: boolean }) =>
    [item.copyIndex, item.cutAfterPrint])).toEqual([[0, true], [1, true]])
  const beforePages = await Promise.all(beforeManifest.pages.map((item: { file: string }) =>
    beforeArchive.file(item.file)!.async('uint8array')))
  for (const bytes of beforePages) expect(Array.from(bytes.slice(-4))).toEqual([0x1D, 0x56, 0x42, 0x04])

  const saved = await saveProject(page)
  expect(saved.outputSettings).not.toHaveProperty('cutOptions')
  expect(saved.outputSettings).toMatchObject({
    outputTarget: 'EscPosNative',
    job: {
      copies: 2,
      cutScope: 'EachCopy',
      cutOptions: { mode: 'Partial', feedToCutPosition: true, feedBeforeCutAmount: 4, feedBeforeCutUnit: 'Dots' },
    },
  })

  await importProject(page, saved, 'cut-ui-job-saved.json')
  await page.getByRole('button', { name: '输出校准设置' }).click()
  await expect(page.getByLabel('启用任务级输出')).toBeChecked()
  await expect(page.getByLabel('任务切法')).toHaveValue('Partial')
  await expect(page.getByLabel('任务进纸到切刀位置')).toBeChecked()
  await expect(page.getByLabel('任务切前补偿量')).toHaveValue('4')
  await expect(page.getByLabel('任务切前补偿单位')).toHaveValue('Dots')

  const afterArchive = await exportEscPosArchive(page)
  const afterManifest = JSON.parse(await afterArchive.file('job.json')!.async('text'))
  expect(afterManifest.pages.map((item: { copyIndex: number; cutAfterPrint: boolean }) =>
    [item.copyIndex, item.cutAfterPrint])).toEqual([[0, true], [1, true]])
  const afterPages = await Promise.all(afterManifest.pages.map((item: { file: string }) =>
    afterArchive.file(item.file)!.async('uint8array')))
  expect(afterPages).toEqual(beforePages)
})

test('UI 不支持 Function B 时可取消已保存的进纸到切刀位置', async ({ page }) => {
  const profile = escPosProfile({ supportedCutModes: ['Full'], supportsFeedAndCut: false })
  await importProject(page, designerProject({
    profile,
    cutOptions: { mode: 'Full', feedToCutPosition: true, feedBeforeCutAmount: 0, feedBeforeCutUnit: 'MotionUnits' },
  }), 'cut-ui-feed-unsupported.json')
  await page.getByRole('button', { name: '输出校准设置' }).click()

  const feedToggle = page.getByLabel('页面进纸到切刀位置')
  await expect(feedToggle).toBeChecked()
  await expect(feedToggle).toBeEnabled()
  const previewPromise = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST' && response.ok())
  await feedToggle.uncheck()
  await previewPromise
  await expect(feedToggle).not.toBeChecked()

  const bytes = await exportEscPosPage(page)
  expect(Array.from(bytes.slice(-3))).toEqual([0x1D, 0x56, 0x00])
})

test('UI 非法切前补偿保持并拒绝保存，修正后恢复预览与保存', async ({ page }) => {
  const profile = escPosProfile({ supportedCutModes: ['Full', 'Partial'], supportsFeedAndCut: true })
  await importProject(page, designerProject({
    profile,
    cutOptions: { mode: 'Full', feedToCutPosition: true, feedBeforeCutAmount: 0, feedBeforeCutUnit: 'MotionUnits' },
  }), 'cut-ui-invalid-amount.json')
  await page.getByRole('button', { name: '输出校准设置' }).click()

  const amount = page.getByLabel('页面切前补偿量')
  const expectInvalidAmount = async (raw: string) => {
    const previewPromise = page.waitForResponse((response) =>
      response.url().includes('/api/designer/preview') && response.request().method() === 'POST' && response.status() === 400)
    await amount.fill(raw)
    await expect(amount).toHaveValue(raw)
    await previewPromise

    const downloadPromise = page.waitForEvent('download', { timeout: 1000 }).catch(() => undefined)
    await page.getByRole('button', { name: '保存项目' }).click()
    expect(await downloadPromise).toBeUndefined()
  }

  await expectInvalidAmount('-1')
  await expectInvalidAmount('0.5')

  const recoveredPreview = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST' && response.ok())
  await amount.fill('4')
  await recoveredPreview
  await expect(amount).toHaveValue('4')

  const saved = await saveProject(page)
  expect(saved.outputSettings.cutOptions).toMatchObject({
    mode: 'Full', feedToCutPosition: true, feedBeforeCutAmount: 4, feedBeforeCutUnit: 'MotionUnits',
  })
})
