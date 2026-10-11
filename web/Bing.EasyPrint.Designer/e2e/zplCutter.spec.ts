import { expect, test, type APIRequestContext, type Page } from '@playwright/test'
import { readFile } from 'node:fs/promises'
import JSZip from 'jszip'

const apiUrl = () => process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'

const zplTemplate = {
  Version: 3,
  Width: 240,
  Height: 80,
  Rows: [{ Height: 80, Cells: [{ Width: 240, Elements: [{
    Type: 1,
    FontSize: 24,
    Field: { Value: 'ZPL CUT' },
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

function zplProfile(cutter: Record<string, unknown> | undefined = undefined) {
  return {
    name: 'ZPL Cutter QA',
    firmwareVersion: 'zpl-cutter-test-1',
    printableWidthDots: 240,
    maxLabelHeightDots: 160,
    supportsImages: false,
    supportsGraphics: true,
    supportsNativeBarcode: true,
    supportsNativeQrCode: true,
    supportsRotation: true,
    supportsCut: true,
    deviceFontNames: ['0'],
    encodingCodePages: [20127],
    ...(cutter === undefined ? {} : { cutter }),
  }
}

function zplPayload(overrides: Record<string, unknown> = {}) {
  return {
    template: zplTemplate,
    data: {},
    protocol: 'Zpl',
    encoding: 'Ascii',
    ...overrides,
  }
}

function zplMedia(profile: Record<string, unknown>) {
  return { profile }
}

async function renderArchive(request: APIRequestContext, payload: Record<string, unknown>) {
  const response = await request.post(`${apiUrl()}/api/designer/render`, { data: payload })
  expect(response.status()).toBe(200)
  return JSZip.loadAsync(await response.body())
}

async function renderPage(request: APIRequestContext, payload: Record<string, unknown>) {
  const archive = await renderArchive(request, payload)
  const entry = archive.file('page-001.zpl')
  expect(entry).not.toBeNull()
  return entry!.async('uint8array')
}

function decode(bytes: Uint8Array) {
  return new TextDecoder('ascii').decode(bytes)
}

function archiveFiles(archive: JSZip) {
  return Object.keys(archive.files).filter((name) => !archive.files[name].dir).sort()
}

async function importProject(page: Page, project: object, name: string) {
  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const importedTemplate = (project as { template?: unknown }).template
  const previewPromise = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') && response.request().method() === 'POST' &&
    (() => {
      const body = response.request().postDataJSON() as { protocol?: string; template?: unknown } | null
      return body?.protocol === 'Zpl' && JSON.stringify(body.template) === JSON.stringify(importedTemplate)
    })())
  await page.getByTestId('template-import').setInputFiles({
    name,
    mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify(project)),
  })
  expect((await previewPromise).status()).toBe(200)
  await expect(page.getByRole('combobox', { name: '输出协议' })).toHaveValue('Zpl')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
}

function designerProject(options: {
  profile: Record<string, unknown>
  cutOptions?: Record<string, unknown>
  job?: Record<string, unknown>
}) {
  return {
    format: 'Bing.EasyPrint.DesignerProject',
    version: 1,
    template: zplTemplate,
    outputSettings: {
      outputTarget: 'Zpl',
      allowRasterizedLines: false,
      imageThreshold: 128,
      encoding: 'Ascii',
      cutAfterPrint: true,
      cutOptions: options.cutOptions ?? { mode: 'Full' },
      ...(options.job === undefined ? {} : { job: options.job }),
      deviceFontCalibrations: { Tspl: {}, Zpl: {} },
      requireDeclaredNativeBounds: false,
      escPosDevice: {},
      tsplMedia: {},
      zplMedia: { profile: options.profile },
    },
  }
}

async function exportZplArchive(page: Page) {
  const responsePromise = page.waitForResponse((response) =>
    response.url().includes('/api/designer/render') && response.request().method() === 'POST' &&
    (response.request().postDataJSON() as { protocol?: string } | null)?.protocol === 'Zpl')
  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 ZPL' }).click()
  expect((await responsePromise).status()).toBe(200)
  return JSZip.loadAsync(await readFile(await (await downloadPromise).path()))
}

async function saveProject(page: Page) {
  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  return JSON.parse((await readFile(await (await downloadPromise).path())).toString('utf8'))
}

test('ZPL 未配置 Cutter 时保持旧单段字节', async ({ request }) => {
  const legacy = await renderPage(request, zplPayload())
  const explicitProfile = await renderPage(request, zplPayload({
    zplMedia: zplMedia(zplProfile()),
  }))

  expect(explicitProfile).toEqual(legacy)
  const text = decode(legacy)
  expect(text).toContain('^XA\r\n')
  expect(text).toContain('^XZ\r\n')
  expect(text).not.toContain('^MMC\r\n')
  expect(text).not.toContain('^MMD\r\n')
  expect(text).not.toContain('~JK\r\n')
})

test('ZPL Cutter、Delayed 和 Kiosk 返回受控能力契约', async ({ request }) => {
  const cases = [
    {
      profile: zplProfile({
        strategy: 'Cutter',
        supportedCutModes: ['Full'],
        supportsTearOff: true,
      }),
      independent: false,
    },
    {
      profile: zplProfile({
        strategy: 'Delayed',
        supportedCutModes: ['Full'],
        supportsTearOff: true,
      }),
      independent: true,
    },
    {
      profile: zplProfile({
        strategy: 'Kiosk',
        supportedCutModes: ['Full', 'Partial'],
        supportsTearOff: true,
        kioskPartialUncutMillimeters: 18,
        kioskCutMarginMillimeters: 4,
      }),
      independent: false,
    },
  ]

  for (const item of cases) {
    const response = await request.post(`${apiUrl()}/api/designer/capabilities`, {
      data: zplPayload({ zplMedia: zplMedia(item.profile) }),
    })
    expect(response.status()).toBe(200)
    const body = await response.json()
    expect(body).toMatchObject({
      protocol: 'Zpl',
      supportsCut: true,
      zplCutter: item.profile.cutter,
      supportsIndependentOutputSegments: item.independent,
    })
  }
})

test('ZPL Delayed 无 Job 时保留有序页面和独立 ~JK 清单', async ({ request }) => {
  const profile = zplProfile({
    strategy: 'Delayed',
    supportedCutModes: ['Full'],
    supportsTearOff: true,
  })
  const archive = await renderArchive(request, zplPayload({
    zplMedia: zplMedia(profile),
    cutAfterPrint: true,
    cutOptions: { mode: 'Full' },
  }))

  expect(archiveFiles(archive)).toEqual(['job.json', 'page-001-segment-002.zpl', 'page-001.zpl'])
  const page = decode(await archive.file('page-001.zpl')!.async('uint8array'))
  expect(page).toContain('^MMD\r\n')
  expect(page).toContain('^PQ1\r\n')
  expect(page).not.toContain('~JK\r\n')
  expect(decode(await archive.file('page-001-segment-002.zpl')!.async('uint8array'))).toBe('~JK\r\n')

  const manifest = JSON.parse(await archive.file('job.json')!.async('text'))
  expect(manifest).toMatchObject({
    outputVersion: 2,
    preserveFileBoundaries: true,
    protocol: 'Zpl',
    files: [
      { file: 'page-001.zpl', pageIndex: 0, segmentIndex: 0, kind: 'Commands' },
      { file: 'page-001-segment-002.zpl', pageIndex: 0, segmentIndex: 1, kind: 'Control' },
    ],
    pages: [{
      sourcePageIndex: null,
      copyIndex: null,
      feedAfterPrint: null,
      cutAfterPrint: null,
      segments: [
        { file: 'page-001.zpl', kind: 'Commands' },
        { file: 'page-001-segment-002.zpl', kind: 'Control' },
      ],
    }],
  })
})

test('ZPL Delayed 任务 EachCopy 和 JobEnd 保留文件归属', async ({ request }) => {
  const profile = zplProfile({
    strategy: 'Delayed',
    supportedCutModes: ['Full'],
    supportsTearOff: true,
  })
  const jobs = [
    {
      scope: 'EachCopy',
      expectedCuts: [false, true, false, true],
    },
    {
      scope: 'JobEnd',
      expectedCuts: [false, false, false, true],
    },
  ] as const

  for (const item of jobs) {
    const archive = await renderArchive(request, zplPayload({
      template: twoPageTemplate,
      zplMedia: zplMedia(profile),
      job: {
        copies: 2,
        copyOrder: 'Collated',
        feedScope: 'None',
        feedUnit: 'Dots',
        feedAmount: 0,
        cutScope: item.scope,
        cutOptions: { mode: 'Full' },
      },
    }))
    const manifest = JSON.parse(await archive.file('job.json')!.async('text'))
    expect(manifest.outputVersion).toBe(2)
    expect(manifest.preserveFileBoundaries).toBe(true)
    expect(manifest.pages.map((page: { sourcePageIndex: number; copyIndex: number; cutAfterPrint: boolean }) =>
      [page.sourcePageIndex, page.copyIndex, page.cutAfterPrint])).toEqual([
      [0, 0, item.expectedCuts[0]], [1, 0, item.expectedCuts[1]],
      [0, 1, item.expectedCuts[2]], [1, 1, item.expectedCuts[3]],
    ])

    for (const [pageIndex, page] of manifest.pages.entries()) {
      const expectedSegments = item.expectedCuts[pageIndex] ? 2 : 1
      expect(page.segments).toHaveLength(expectedSegments)
      expect(page.segments[0]).toEqual({
        file: `page-${String(pageIndex + 1).padStart(3, '0')}.zpl`,
        kind: 'Commands',
      })
      const command = decode(await archive.file(page.segments[0].file)!.async('uint8array'))
      expect(command).toContain(item.expectedCuts[pageIndex] ? '^MMD\r\n' : '^MMT\r\n')
      if (item.expectedCuts[pageIndex]) {
        expect(page.segments[1]).toEqual({
          file: `page-${String(pageIndex + 1).padStart(3, '0')}-segment-002.zpl`,
          kind: 'Control',
        })
        expect(decode(await archive.file(page.segments[1].file)!.async('uint8array'))).toBe('~JK\r\n')
      }
    }

    expect(manifest.files).toHaveLength(4 + item.expectedCuts.filter(Boolean).length)
  }
})

test('ZPL Kiosk 全切半切输出 ^KV、^CN 且保持单段边界', async ({ request }) => {
  const profile = zplProfile({
    strategy: 'Kiosk',
    supportedCutModes: ['Full', 'Partial'],
    supportsTearOff: true,
    kioskPartialUncutMillimeters: 18,
    kioskCutMarginMillimeters: 4,
  })
  const full = await renderPage(request, zplPayload({
    zplMedia: zplMedia(profile),
    cutAfterPrint: true,
    cutOptions: { mode: 'Full' },
  }))
  const partialArchive = await renderArchive(request, zplPayload({
    zplMedia: zplMedia(profile),
    cutAfterPrint: true,
    cutOptions: { mode: 'Partial' },
  }))
  const fullText = decode(full)
  const partialText = decode(await partialArchive.file('page-001.zpl')!.async('uint8array'))

  expect(fullText).toContain('^MMK\r\n^KV0,4\r\n')
  expect(fullText).toContain('^CN1\r\n')
  expect(partialText).toContain('^MMK\r\n^KV18,4\r\n')
  expect(partialText).toContain('^CN0\r\n')
  expect(archiveFiles(partialArchive)).toEqual(['page-001.zpl'])
  expect(partialText).not.toContain('~JK\r\n')
})

test('ZPL Cutter 未知字段、范围、TearOff 和普通半切严格拒绝', async ({ request }) => {
  const invalidProfiles = [
    {
      name: '普通 Cutter 不得声明半切',
      profile: zplProfile({ strategy: 'Cutter', supportedCutModes: ['Partial'], supportsTearOff: true }),
    },
    {
      name: 'Delayed 不得声明 Kiosk 参数',
      profile: zplProfile({ strategy: 'Delayed', supportedCutModes: ['Full'], supportsTearOff: true, kioskCutMarginMillimeters: 4 }),
    },
    {
      name: '必须确认 TearOff',
      profile: zplProfile({ strategy: 'Cutter', supportedCutModes: ['Full'], supportsTearOff: false }),
    },
    {
      name: 'Kiosk 边距范围无效',
      profile: zplProfile({ strategy: 'Kiosk', supportedCutModes: ['Full'], supportsTearOff: true, kioskCutMarginMillimeters: 1 }),
    },
    {
      name: 'Kiosk 半切长度范围无效',
      profile: zplProfile({ strategy: 'Kiosk', supportedCutModes: ['Full', 'Partial'], supportsTearOff: true,
        kioskPartialUncutMillimeters: 61, kioskCutMarginMillimeters: 4 }),
    },
    {
      name: '拒绝未知 cutter 字段',
      profile: zplProfile({ strategy: 'Cutter', supportedCutModes: ['Full'], supportsTearOff: true, typo: true }),
    },
  ]

  for (const invalid of invalidProfiles) {
    const response = await request.post(`${apiUrl()}/api/designer/capabilities`, {
      data: zplPayload({ zplMedia: zplMedia(invalid.profile) }),
    })
    expect(response.status(), invalid.name).toBe(400)
  }
})

test('UI ZPL Delayed 改 Kiosk 后保存重载、撤销并在移动端导出一致', async ({ page }) => {
  const delayedProfile = zplProfile({
    strategy: 'Delayed',
    supportedCutModes: ['Full'],
    supportsTearOff: true,
  })
  await importProject(page, designerProject({ profile: delayedProfile }), 'zpl-delayed-cutter.json')
  await page.getByRole('button', { name: '输出校准设置' }).click()
  await page.getByRole('button', { name: '编辑ZPL机型参数' }).click()

  const strategy = page.getByLabel('ZPL 裁切策略')
  await expect(strategy).toHaveValue('Delayed')
  await strategy.selectOption('Kiosk')
  await expect(page.getByLabel('ZPL Kiosk 裁切边距')).toHaveValue('9')
  await page.getByLabel('ZPL 半切').check()
  await page.getByLabel('ZPL Kiosk 半切未切长度').fill('18')
  const profilePreview = page.waitForResponse((response) => {
    if (!response.url().includes('/api/designer/preview') || response.request().method() !== 'POST' || !response.ok()) return false
    const body = response.request().postDataJSON() as {
      protocol?: string
      zplMedia?: { profile?: { cutter?: Record<string, unknown> } }
    } | null
    const cutter = body?.zplMedia?.profile?.cutter
    return body?.protocol === 'Zpl' && cutter?.strategy === 'Kiosk' &&
      cutter?.supportedCutModes instanceof Array && cutter.supportedCutModes.includes('Partial') &&
      cutter?.kioskPartialUncutMillimeters === 18
  })
  await page.getByLabel('ZPL Kiosk 裁切边距').fill('4')
  await profilePreview

  const pageMode = page.getByLabel('页面切法')
  await pageMode.selectOption('Partial')
  await expect(pageMode).toHaveValue('Partial')
  await page.waitForTimeout(350)
  const beforeArchive = await exportZplArchive(page)
  const beforeBytes = await beforeArchive.file('page-001.zpl')!.async('uint8array')
  expect(decode(beforeBytes)).toContain('^KV18,4\r\n^PQ1\r\n^CN0\r\n')
  expect(archiveFiles(beforeArchive)).toEqual(['page-001.zpl'])

  await page.getByLabel('ZPL Kiosk 裁切边距').fill('9')
  await expect(page.getByLabel('ZPL Kiosk 裁切边距')).toHaveValue('9')
  await page.waitForTimeout(350)
  await page.getByRole('button', { name: '撤销 Ctrl+Z' }).click()
  await expect(page.getByLabel('ZPL Kiosk 裁切边距')).toHaveValue('4')

  const saved = await saveProject(page)
  expect(saved.outputSettings).toMatchObject({
    outputTarget: 'Zpl',
    cutAfterPrint: true,
    cutOptions: { mode: 'Partial' },
  })
  expect(saved.outputSettings.zplMedia.profile.cutter).toEqual({
    strategy: 'Kiosk',
    supportedCutModes: ['Full', 'Partial'],
    supportsTearOff: true,
    kioskPartialUncutMillimeters: 18,
    kioskCutMarginMillimeters: 4,
  })

  await importProject(page, saved, 'zpl-kiosk-cutter-saved.json')
  await page.getByRole('button', { name: '输出校准设置' }).click()
  await page.getByRole('button', { name: '编辑ZPL机型参数' }).click()
  await expect(page.getByLabel('ZPL 裁切策略')).toHaveValue('Kiosk')
  await expect(page.getByLabel('ZPL Kiosk 裁切边距')).toHaveValue('4')
  await expect(page.getByLabel('ZPL Kiosk 半切未切长度')).toHaveValue('18')
  await expect(page.getByLabel('ZPL 半切')).toBeChecked()
  const afterArchive = await exportZplArchive(page)
  expect(await afterArchive.file('page-001.zpl')!.async('uint8array')).toEqual(beforeBytes)

  await page.setViewportSize({ width: 390, height: 844 })
  await expect(page.getByRole('button', { name: '生成 ZPL' })).toBeVisible()
  const mobileArchive = await exportZplArchive(page)
  expect(await mobileArchive.file('page-001.zpl')!.async('uint8array')).toEqual(beforeBytes)
})
