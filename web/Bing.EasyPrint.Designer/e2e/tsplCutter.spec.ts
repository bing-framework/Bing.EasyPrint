import { expect, test, type APIRequestContext, type Page } from '@playwright/test'
import { readFile } from 'node:fs/promises'
import JSZip from 'jszip'

const apiUrl = () => process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'

const tsplTemplate = {
  Version: 3,
  Width: 240,
  Height: 80,
  Rows: [{ Height: 80, Cells: [{ Width: 240, Elements: [{
    Type: 1,
    FontSize: 24,
    Field: { Value: 'TSPL CUT' },
  }] }] }],
}

const twoPageTemplate = {
  Version: 3,
  Width: 240,
  Height: 80,
  Rows: [
    { Height: 80, Cells: [{ Width: 240, Elements: [{ Type: 1, FontSize: 24, Field: { Value: 'A' } }] }] },
    { Height: 80, Cells: [{ Width: 240, Elements: [{ Type: 1, FontSize: 24, Field: { Value: 'B' } }] }] },
  ],
}

function tsplProfile(cutter: Record<string, unknown> | undefined = undefined) {
  return {
    name: 'TSPL Cutter QA',
    firmwareVersion: 'tspl-cutter-test-1',
    printableWidthDots: 240,
    maxLabelHeightDots: 160,
    supportsImages: false,
    supportsGraphics: true,
    supportsNativeBarcode: true,
    supportsNativeQrCode: true,
    supportsRotation: true,
    deviceFontNames: ['0'],
    encodingCodePages: [20127],
    ...(cutter === undefined ? {} : { cutter }),
  }
}

function tsplPayload(overrides: Record<string, unknown> = {}) {
  return {
    template: tsplTemplate,
    data: {},
    protocol: 'Tspl',
    encoding: 'Ascii',
    ...overrides,
  }
}

function tsplMedia(profile: Record<string, unknown>) {
  return { profile }
}

async function renderArchive(request: APIRequestContext, payload: Record<string, unknown>) {
  const response = await request.post(`${apiUrl()}/api/designer/render`, { data: payload })
  expect(response.status()).toBe(200)
  return JSZip.loadAsync(await response.body())
}

async function renderPage(request: APIRequestContext, payload: Record<string, unknown>) {
  const archive = await renderArchive(request, payload)
  const entry = archive.file('page-001.tspl')
  expect(entry).not.toBeNull()
  return entry!.async('uint8array')
}

function decode(bytes: Uint8Array) {
  return new TextDecoder('ascii').decode(bytes)
}

async function importProject(page: Page, project: object, name: string) {
  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const importedTemplate = (project as { template?: unknown }).template
  const previewPromise = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST' &&
    (() => {
      const body = response.request().postDataJSON() as { protocol?: string; template?: unknown } | null
      return body?.protocol === 'Tspl' && JSON.stringify(body.template) === JSON.stringify(importedTemplate)
    })())
  await page.getByTestId('template-import').setInputFiles({
    name,
    mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify(project)),
  })
  expect((await previewPromise).status()).toBe(200)
  await expect(page.getByRole('combobox', { name: '输出协议' })).toHaveValue('Tspl')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
}

function designerProject(options: {
  profile: Record<string, unknown>
  cutAfterPrint?: boolean
  cutOptions?: Record<string, unknown>
  job?: Record<string, unknown>
}) {
  return {
    format: 'Bing.EasyPrint.DesignerProject',
    version: 1,
    template: tsplTemplate,
    outputSettings: {
      outputTarget: 'Tspl',
      allowRasterizedLines: false,
      imageThreshold: 128,
      encoding: 'Ascii',
      ...(options.cutAfterPrint === undefined ? {} : { cutAfterPrint: options.cutAfterPrint }),
      ...(options.cutOptions === undefined ? {} : { cutOptions: options.cutOptions }),
      ...(options.job === undefined ? {} : { job: options.job }),
      deviceFontCalibrations: { Tspl: {}, Zpl: {} },
      requireDeclaredNativeBounds: false,
      escPosDevice: {},
      tsplMedia: { profile: options.profile },
      zplMedia: {},
    },
  }
}

async function exportTsplArchive(page: Page) {
  const responsePromise = page.waitForResponse((response) =>
    response.url().includes('/api/designer/render') && response.request().method() === 'POST')
  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 TSPL' }).click()
  expect((await responsePromise).status()).toBe(200)
  return JSZip.loadAsync(await readFile(await (await downloadPromise).path()))
}

async function saveProject(page: Page) {
  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  return JSON.parse((await readFile(await (await downloadPromise).path())).toString('utf8'))
}

test('TSPL 两种裁切策略返回能力并严格拒绝不完整契约', async ({ request }) => {
  const batchProfile = tsplProfile({
    strategy: 'Batch',
    supportedCutModes: ['Full', 'Partial'],
    supportsEndOfJob: true,
  })
  const batchCapabilities = await request.post(`${apiUrl()}/api/designer/capabilities`, {
    data: tsplPayload({ tsplMedia: tsplMedia(batchProfile) }),
  })
  expect(batchCapabilities.status()).toBe(200)
  expect(await batchCapabilities.json()).toMatchObject({
    protocol: 'Tspl',
    supportsCut: true,
    cutModes: ['Full', 'Partial'],
    cutFeedUnits: [],
    cutFeedDots: null,
    tsplCutter: {
      strategy: 'Batch',
      supportedCutModes: ['Full', 'Partial'],
      supportsEndOfJob: true,
      cutPositionFeedDots: null,
    },
  })

  const immediateProfile = tsplProfile({
    strategy: 'Immediate',
    supportedCutModes: ['Partial'],
    supportsEndOfJob: true,
    cutPositionFeedDots: 12,
  })
  const immediateCapabilities = await request.post(`${apiUrl()}/api/designer/capabilities`, {
    data: tsplPayload({ tsplMedia: tsplMedia(immediateProfile) }),
  })
  expect(immediateCapabilities.status()).toBe(200)
  expect(await immediateCapabilities.json()).toMatchObject({
    cutModes: ['Partial'],
    cutFeedUnits: ['Dots'],
    cutFeedDots: { minimum: 0, maximum: 9987 },
    tsplCutter: immediateProfile.cutter,
  })

  const invalidProfiles = [
    {
      name: 'Immediate 不能声明两种切法',
      profile: tsplProfile({ strategy: 'Immediate', supportedCutModes: ['Full', 'Partial'], supportsEndOfJob: true }),
    },
    {
      name: 'Batch 不能声明切刀位置补偿',
      profile: tsplProfile({ strategy: 'Batch', supportedCutModes: ['Partial'], supportsEndOfJob: true, cutPositionFeedDots: 12 }),
    },
    {
      name: '必须确认 EOJ',
      profile: tsplProfile({ strategy: 'Batch', supportedCutModes: ['Partial'], supportsEndOfJob: false }),
    },
    {
      name: '拒绝未知 cutter 字段',
      profile: tsplProfile({ strategy: 'Batch', supportedCutModes: ['Partial'], supportsEndOfJob: true, typo: true }),
    },
  ]
  for (const invalid of invalidProfiles) {
    const response = await request.post(`${apiUrl()}/api/designer/capabilities`, {
      data: tsplPayload({ tsplMedia: tsplMedia(invalid.profile) }),
    })
    expect(response.status(), invalid.name).toBe(400)
  }

  const batchWithPositiveJobFeed = await request.post(`${apiUrl()}/api/designer/preview`, {
    data: tsplPayload({
      tsplMedia: tsplMedia(batchProfile),
      job: {
        copies: 1,
        copyOrder: 'Collated',
        feedScope: 'JobEnd',
        feedUnit: 'Dots',
        feedAmount: 1,
        cutScope: 'JobEnd',
        cutOptions: { mode: 'Partial' },
      },
    }),
  })
  expect(batchWithPositiveJobFeed.status()).toBe(400)
})

test('TSPL Batch 半切输出并在无切纸时复用关闭状态', async ({ request }) => {
  const profile = tsplProfile({
    strategy: 'Batch',
    supportedCutModes: ['Partial'],
    supportsEndOfJob: true,
  })
  const media = tsplMedia(profile)

  const withoutCut = decode(await renderPage(request, tsplPayload({ tsplMedia: media })))
  expect(withoutCut).toContain('SET CUTTER OFF\r\nSET PARTIAL_CUTTER OFF\r\n')
  expect(withoutCut).not.toContain('SET PARTIAL_CUTTER BATCH\r\n')
  expect(withoutCut).not.toContain('\r\nCUT\r\n')
  expect(withoutCut.match(/SET PARTIAL_CUTTER OFF\r\n/g)).toHaveLength(1)

  const withPartialCut = decode(await renderPage(request, tsplPayload({
    tsplMedia: media,
    cutAfterPrint: true,
    cutOptions: { mode: 'Partial' },
  })))
  expect(withPartialCut).toContain('SET PARTIAL_CUTTER BATCH\r\n')
  expect(withPartialCut).not.toContain('\r\nCUT\r\n')
  expect(withPartialCut.match(/SET PARTIAL_CUTTER BATCH\r\n/g)).toHaveLength(1)
  expect(withPartialCut.match(/SET PARTIAL_CUTTER OFF\r\n/g)).toHaveLength(2)
  expect(withPartialCut.endsWith('SET CUTTER OFF\r\nSET PARTIAL_CUTTER OFF\r\n')).toBeTruthy()
})

test('TSPL Batch 任务 EachCopy 两页两份只在份边界启用批次切纸', async ({ request }) => {
  const profile = tsplProfile({
    strategy: 'Batch',
    supportedCutModes: ['Partial'],
    supportsEndOfJob: true,
  })
  const archive = await renderArchive(request, {
    ...tsplPayload({
      template: twoPageTemplate,
      tsplMedia: tsplMedia(profile),
      job: {
        copies: 2,
        copyOrder: 'Collated',
        feedScope: 'None',
        feedUnit: 'Dots',
        feedAmount: 0,
        cutScope: 'EachCopy',
        cutOptions: { mode: 'Partial' },
      },
    }),
  })
  const manifest = JSON.parse(await archive.file('job.json')!.async('text'))
  expect(manifest.pages.map((page: { sourcePageIndex: number; copyIndex: number; cutAfterPrint: boolean }) =>
    [page.sourcePageIndex, page.copyIndex, page.cutAfterPrint])).toEqual([
    [0, 0, false], [1, 0, true], [0, 1, false], [1, 1, true],
  ])

  const pages = await Promise.all(manifest.pages.map((page: { file: string }) => archive.file(page.file)!.async('uint8array')))
  const texts = pages.map(decode)
  for (const [index, page] of manifest.pages.entries()) {
    const text = texts[index]
    if (page.cutAfterPrint) {
      expect(text).toContain('SET PARTIAL_CUTTER BATCH\r\n')
      expect(text.endsWith('SET CUTTER OFF\r\nSET PARTIAL_CUTTER OFF\r\n')).toBeTruthy()
    } else {
      expect(text).not.toContain('SET PARTIAL_CUTTER BATCH\r\n')
      expect(text.endsWith('PRINT 1,1\r\nEOJ\r\n')).toBeTruthy()
    }
  }
  expect(texts.filter((text) => text.includes('SET PARTIAL_CUTTER BATCH\r\n'))).toHaveLength(2)
  expect(texts.filter((text) => text.includes('\r\nCUT\r\n'))).toHaveLength(0)
})

test('TSPL Immediate 切刀位置补偿追加 FEED 后 EOJ 再 CUT，并支持无补偿', async ({ request }) => {
  const profile = tsplProfile({
    strategy: 'Immediate',
    supportedCutModes: ['Full'],
    supportsEndOfJob: true,
    cutPositionFeedDots: 12,
  })
  const media = tsplMedia(profile)
  const compensated = decode(await renderPage(request, tsplPayload({
    tsplMedia: media,
    cutAfterPrint: true,
    cutOptions: { mode: 'Full', feedToCutPosition: true, feedBeforeCutAmount: 4, feedBeforeCutUnit: 'Dots' },
  })))
  expect(compensated).toContain('FEED 16\r\nEOJ\r\nCUT\r\n')
  expect(compensated.endsWith('FEED 16\r\nEOJ\r\nCUT\r\n')).toBeTruthy()

  const noCompensation = decode(await renderPage(request, tsplPayload({
    tsplMedia: media,
    cutAfterPrint: true,
    cutOptions: { mode: 'Full' },
  })))
  expect(noCompensation.endsWith('EOJ\r\nCUT\r\n')).toBeTruthy()
  expect(noCompensation).not.toContain('FEED 12\r\n')
})

test('UI TSPL Batch 半切项目保存重载、撤销后导出字节一致', async ({ page }) => {
  const profile = tsplProfile({
    strategy: 'Batch',
    supportedCutModes: ['Full', 'Partial'],
    supportsEndOfJob: true,
  })
  await importProject(page, designerProject({ profile, cutAfterPrint: true }), 'tspl-batch-cutter.json')
  await page.getByRole('button', { name: '输出校准设置' }).click()
  await page.getByRole('button', { name: '编辑TSPL机型参数' }).click()

  await expect(page.getByLabel('TSPL 裁切策略')).toHaveValue('Batch')
  await expect(page.getByLabel('TSPL 支持EOJ')).toBeChecked()
  const customToggle = page.getByLabel('启用页面自定义裁切参数')
  await customToggle.check()
  const mode = page.getByLabel('页面切法')
  const previewPromise = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST' && response.ok())
  await mode.selectOption('Partial')
  await previewPromise
  await expect(mode).toHaveValue('Partial')
  const beforeArchive = await exportTsplArchive(page)
  const beforeBytes = await beforeArchive.file('page-001.tspl')!.async('uint8array')

  await mode.selectOption('Full')
  await expect(mode).toHaveValue('Full')
  await page.waitForTimeout(350)
  await page.getByRole('button', { name: '撤销 Ctrl+Z' }).click()
  await expect(mode).toHaveValue('Partial')

  const saved = await saveProject(page)
  expect(saved.outputSettings).toMatchObject({
    outputTarget: 'Tspl',
    cutAfterPrint: true,
    cutOptions: { mode: 'Partial' },
  })
  expect(saved.outputSettings.tsplMedia.profile.cutter).toEqual(profile.cutter)

  await importProject(page, saved, 'tspl-batch-cutter-saved.json')
  await page.getByRole('button', { name: '输出校准设置' }).click()
  await page.getByRole('button', { name: '编辑TSPL机型参数' }).click()
  await expect(page.getByLabel('页面切法')).toHaveValue('Partial')
  const afterArchive = await exportTsplArchive(page)
  expect(await afterArchive.file('page-001.tspl')!.async('uint8array')).toEqual(beforeBytes)
})

test('UI TSPL Immediate 基准点加 Dots 补偿后保存重载并导出 FEED16', async ({ page }) => {
  const profile = tsplProfile({
    strategy: 'Immediate',
    supportedCutModes: ['Full'],
    supportsEndOfJob: true,
    cutPositionFeedDots: 12,
  })
  await importProject(page, designerProject({ profile, cutAfterPrint: true }), 'tspl-immediate-cutter.json')
  await page.getByRole('button', { name: '输出校准设置' }).click()
  await page.getByRole('button', { name: '编辑TSPL机型参数' }).click()

  await expect(page.getByLabel('TSPL 裁切策略')).toHaveValue('Immediate')
  await expect(page.getByLabel('TSPL 切刀位置点数')).toHaveValue('12')
  await page.getByLabel('启用页面自定义裁切参数').check()
  const feedToggle = page.getByLabel('页面进纸到切刀位置')
  await expect(feedToggle).toBeEnabled()
  await feedToggle.check()
  const amount = page.getByLabel('页面切前补偿量')
  await amount.fill('4')
  await page.getByLabel('页面切前补偿单位').selectOption('Dots')
  await expect(amount).toHaveValue('4')
  await page.waitForTimeout(400)

  const saved = await saveProject(page)
  expect(saved.outputSettings).toMatchObject({
    outputTarget: 'Tspl',
    cutAfterPrint: true,
    cutOptions: { mode: 'Full', feedToCutPosition: true, feedBeforeCutAmount: 4, feedBeforeCutUnit: 'Dots' },
  })
  await importProject(page, saved, 'tspl-immediate-cutter-saved.json')
  await page.getByRole('button', { name: '输出校准设置' }).click()
  await page.getByRole('button', { name: '编辑TSPL机型参数' }).click()
  await expect(page.getByLabel('页面进纸到切刀位置')).toBeChecked()
  await expect(page.getByLabel('页面切前补偿量')).toHaveValue('4')
  const archive = await exportTsplArchive(page)
  const bytes = await archive.file('page-001.tspl')!.async('uint8array')
  expect(decode(bytes).endsWith('FEED 16\r\nEOJ\r\nCUT\r\n')).toBeTruthy()

  await page.setViewportSize({ width: 390, height: 844 })
  await expect(page.getByRole('button', { name: '生成 TSPL' })).toBeVisible()
  const mobileArchive = await exportTsplArchive(page)
  const mobileBytes = await mobileArchive.file('page-001.tspl')!.async('uint8array')
  expect(mobileBytes).toEqual(bytes)
})
