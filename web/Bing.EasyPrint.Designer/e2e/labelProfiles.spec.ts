import { expect, test, type APIRequestContext, type APIResponse, type Page, type Request } from '@playwright/test'
import { readFile } from 'node:fs/promises'
import JSZip from 'jszip'

const protocols = ['Tspl', 'Zpl'] as const
type LabelProtocol = (typeof protocols)[number]

const apiUrl = () => process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'
const protocolLabel = (protocol: LabelProtocol) => protocol === 'Tspl' ? 'TSPL' : 'ZPL'
const mediaKey = (protocol: LabelProtocol) => protocol === 'Tspl' ? 'tsplMedia' : 'zplMedia'
const fileExtension = (protocol: LabelProtocol) => protocol === 'Tspl' ? 'tspl' : 'zpl'

function labelMedia(protocol: LabelProtocol, profile: Record<string, unknown>) {
  return protocol === 'Tspl' ? { tsplMedia: { profile } } : { zplMedia: { profile } }
}

function textTemplate(options: {
  width?: number
  height?: number
  value?: string
  deviceFontName?: string
  rotation?: 0 | 90 | 180 | 270
} = {}) {
  const width = options.width ?? 240
  const height = options.height ?? 100
  const element = {
    Type: 1,
    FontSize: 24,
    ...(options.deviceFontName === undefined ? {} : { DeviceFontName: options.deviceFontName }),
    ...(options.rotation === undefined ? {} : { Rotation: options.rotation }),
    Field: { Value: options.value ?? 'ASCII' },
  }
  return {
    Version: 1,
    Width: width,
    Height: height,
    Rows: [{ Height: height, Cells: [{ Width: width, Elements: [element] }] }],
  }
}

function imageTemplate() {
  return {
    Version: 1,
    Width: 240,
    Height: 100,
    Rows: [{ Height: 100, Cells: [{ Width: 240, Elements: [{
      Type: 2,
      Width: 32,
      Height: 32,
      StartCoordinate: { X: 0, Y: 0 },
      Field: { Value: 'logo' },
    }] }] }],
  }
}

function restrictedApiProfile(protocol: LabelProtocol): Record<string, unknown> {
  return {
    name: `${protocolLabel(protocol)} QA`,
    firmwareVersion: 'profile-test-1',
    printableWidthDots: 240,
    maxLabelHeightDots: 100,
    maxImageDataBytes: 64,
    supportsImages: false,
    supportsGraphics: true,
    supportsNativeBarcode: false,
    supportsNativeBarcodeHumanReadable: false,
    supportsNativeQrCode: false,
    supportsRotation: false,
    deviceFontNames: ['1'],
    encodingCodePages: [20127],
    characterRanges: [{ first: 32, last: 126 }],
    ...(protocol === 'Zpl' ? { supportsCut: false } : {}),
  }
}

function importedUiProfile(protocol: LabelProtocol): Record<string, unknown> {
  return {
    name: `${protocolLabel(protocol)} Imported`,
    firmwareVersion: 'ui-test-1',
    printableWidthDots: 240,
    maxLabelHeightDots: 100,
    maxImageDataBytes: 4096,
    supportsImages: false,
    supportsGraphics: false,
    supportsNativeBarcode: false,
    supportsNativeBarcodeHumanReadable: false,
    supportsNativeQrCode: false,
    supportsRotation: false,
    deviceFontNames: ['0'],
    encodingCodePages: [20127],
    characterRanges: [{ first: 32, last: 126 }],
    ...(protocol === 'Zpl' ? { supportsCut: false } : {}),
  }
}

function designerProject(protocol: LabelProtocol, profile: Record<string, unknown>) {
  return {
    format: 'Bing.EasyPrint.DesignerProject',
    version: 1,
    template: textTemplate({ deviceFontName: '0' }),
    outputSettings: {
      outputTarget: protocol,
      allowRasterizedLines: false,
      imageThreshold: 128,
      encoding: 'Ascii',
      deviceFontCalibrations: { Tspl: {}, Zpl: {} },
      requireDeclaredNativeBounds: false,
      escPosDevice: {},
      tsplMedia: protocol === 'Tspl' ? { profile } : {},
      zplMedia: protocol === 'Zpl' ? { profile } : {},
    },
  }
}

async function archiveEntry(response: APIResponse, name: string) {
  const archive = await JSZip.loadAsync(await response.body())
  const entry = archive.file(name)
  expect(entry).not.toBeNull()
  return entry!.async('uint8array')
}

async function renderBytes(request: APIRequestContext, body: Record<string, unknown>, protocol: LabelProtocol) {
  const response = await request.post(`${apiUrl()}/api/designer/render`, { data: body })
  expect(response.status()).toBe(200)
  return archiveEntry(response, `page-001.${fileExtension(protocol)}`)
}

async function importDesignerProject(page: Page, project: object, name: string, protocol: LabelProtocol) {
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
  await expect(page.getByRole('combobox', { name: '输出协议' })).toHaveValue(protocol)
}

async function exportProject(page: Page) {
  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  const download = await downloadPromise
  return readFile(await download.path())
}

async function exportProtocolBytes(page: Page, protocol: LabelProtocol) {
  const responsePromise = page.waitForResponse((response) =>
    response.url().includes('/api/designer/render') && response.request().method() === 'POST')
  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: `生成 ${protocolLabel(protocol)}` }).click()
  const response = await responsePromise
  expect(response.status()).toBe(200)
  const download = await downloadPromise
  const archive = await JSZip.loadAsync(await readFile(await download.path()))
  const entry = archive.file(`page-001.${fileExtension(protocol)}`)
  expect(entry).not.toBeNull()
  return entry!.async('uint8array')
}

async function imageResourceBase64() {
  const image = await readFile('../../tests/Bing.EasyPrint.Tests/Biz/Images/logo.jpg')
  return image.toString('base64')
}

for (const protocol of protocols) {
  test(`${protocolLabel(protocol)} 机型配置返回限制能力并拒绝越界预览`, async ({ request }) => {
    const profile = restrictedApiProfile(protocol)
    const capabilityResponse = await request.post(`${apiUrl()}/api/designer/capabilities`, {
      data: { protocol, encoding: 'Ascii', ...labelMedia(protocol, profile) },
    })
    expect(capabilityResponse.status()).toBe(200)
    const capabilities = await capabilityResponse.json()

    expect(capabilities).toMatchObject({
      protocol,
      labelProfile: {
        name: profile.name,
        firmwareVersion: profile.firmwareVersion,
        printableWidthDots: 240,
        maxLabelHeightDots: 100,
        maxImageDataBytes: 64,
        encodingCodePages: [20127],
        characterRanges: [{ first: 32, last: 126 }],
      },
      fontKinds: [1],
      textSize: null,
      textRotations: [0],
      barcodeRotations: [0],
    })
    expect(capabilities.deviceFonts.map((font: { name: string }) => font.name)).toEqual(['1'])
    expect(capabilities.elementTypes).toEqual(expect.arrayContaining([1, 5, 6, 7, 9]))
    for (const type of [2, 3, 4, 8, 10]) expect(capabilities.elementTypes).not.toContain(type)
    if (protocol === 'Zpl') expect(capabilities.supportsCut).toBe(false)

    const validBody = {
      template: textTemplate({ deviceFontName: '1' }),
      data: {},
      protocol,
      encoding: 'Ascii',
      ...labelMedia(protocol, profile),
    }
    expect((await request.post(`${apiUrl()}/api/designer/preview`, { data: validBody })).status()).toBe(200)

    const tooWide = await request.post(`${apiUrl()}/api/designer/preview`, {
      data: { ...validBody, template: textTemplate({ width: 241, deviceFontName: '1' }) },
    })
    expect(tooWide.status()).toBe(400)

    const tooHigh = await request.post(`${apiUrl()}/api/designer/preview`, {
      data: { ...validBody, template: textTemplate({ height: 101, deviceFontName: '1' }) },
    })
    expect(tooHigh.status()).toBe(400)

    const missingGlyph = await request.post(`${apiUrl()}/api/designer/preview`, {
      data: { ...validBody, template: textTemplate({ value: 'A中', deviceFontName: '1' }) },
    })
    expect(missingGlyph.status()).toBe(400)
    expect(JSON.stringify(await missingGlyph.json())).toMatch(/字库|覆盖|U\+/)

    const rotated = await request.post(`${apiUrl()}/api/designer/preview`, {
      data: { ...validBody, template: textTemplate({ deviceFontName: '1', rotation: 90 }) },
    })
    expect(rotated.status()).toBe(400)
    expect(JSON.stringify(await rotated.json())).toMatch(/旋转|rotation/i)

    const resources = { logo: await imageResourceBase64() }
    const forbiddenImage = await request.post(`${apiUrl()}/api/designer/preview`, {
      data: { ...validBody, template: imageTemplate(), resources },
    })
    expect(forbiddenImage.status()).toBe(400)
    expect(JSON.stringify(await forbiddenImage.json())).toMatch(/图片|机型/)

    const imageBudgetProfile = { ...profile, supportsImages: true, maxImageDataBytes: 1 }
    const overBudgetImage = await request.post(`${apiUrl()}/api/designer/preview`, {
      data: {
        template: imageTemplate(),
        data: {},
        protocol,
        encoding: 'Ascii',
        resources,
        ...labelMedia(protocol, imageBudgetProfile),
      },
    })
    expect(overBudgetImage.status()).toBe(400)
    expect(JSON.stringify(await overBudgetImage.json())).toMatch(/图片|字节|上限/)
  })

  test(`${protocolLabel(protocol)} 默认机型配置与无配置导出字节一致`, async ({ request }) => {
    const template = textTemplate()
    const withoutProfile = { template, data: {}, protocol, encoding: 'Ascii' }
    const defaultProfile = {
      ...withoutProfile,
      ...labelMedia(protocol, { name: 'Custom' }),
    }
    const oldBytes = await renderBytes(request, withoutProfile, protocol)
    const defaultBytes = await renderBytes(request, defaultProfile, protocol)
    expect(defaultBytes).toEqual(oldBytes)
  })

  test(`${protocolLabel(protocol)} 机型配置拒绝未知字段、非法代码页和协议错配`, async ({ request }) => {
    const base = { protocol, encoding: 'Ascii' }
    const invalidProfiles = [
      { name: 'unknown field', profile: { typo: true } },
      { name: 'invalid code page', profile: { encodingCodePages: protocol === 'Tspl' ? [1252] : [936] } },
      { name: 'duplicate device font', profile: { deviceFontNames: ['1', '1'] } },
      { name: 'overlapping character ranges', profile: {
        characterRanges: [{ first: 32, last: 64 }, { first: 64, last: 126 }],
      } },
      ...(protocol === 'Tspl' ? [{ name: 'ZPL-only field', profile: { supportsCut: false } }] : []),
    ]
    for (const invalid of invalidProfiles) {
      const response = await request.post(`${apiUrl()}/api/designer/capabilities`, {
        data: { ...base, ...labelMedia(protocol, invalid.profile) },
      })
      expect(response.status(), invalid.name).toBe(400)
    }

    const oppositeMedia = protocol === 'Tspl'
      ? { zplMedia: { profile: {} } }
      : { tsplMedia: { profile: {} } }
    const mismatch = await request.post(`${apiUrl()}/api/designer/capabilities`, {
      data: { protocol, encoding: 'Ascii', ...oppositeMedia },
    })
    expect(mismatch.status()).toBe(400)
  })

  test(`${protocolLabel(protocol)} 编辑器导入、能力过滤、项目往返和非法草稿回归`, async ({ page }) => {
    const label = protocolLabel(protocol)
    const outputProtocol = page.getByRole('combobox', { name: '输出协议' })

    await page.goto('/')
    await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
    await outputProtocol.selectOption(protocol)
    await expect(page.locator('.capability-note')).toHaveClass(/ready/)
    await page.locator('.output-settings-button').click()
    const createButton = page.getByRole('button', { name: `新建${label}机型配置` })
    await expect(createButton).toBeVisible()
    await createButton.click()
    await expect(page.getByLabel(`${label} 机型名称`)).toBeVisible()

    const fontMode = page.getByLabel(`${label} 字体范围模式`)
    await fontMode.selectOption('none')
    await expect(page.getByRole('button', { name: '单行文字', exact: true })).toBeDisabled()
    await fontMode.selectOption('list')
    await expect(page.getByLabel(`${label} 机型字体列表`)).toBeVisible()
    await page.getByLabel(`${label} 机型字体列表`).fill('0')
    await expect(page.locator('.capability-note')).toHaveClass(/ready/)
    await expect(page.getByRole('button', { name: '单行文字', exact: true })).toBeEnabled()

    const project = designerProject(protocol, importedUiProfile(protocol))
    await importDesignerProject(page, project, `${protocol.toLowerCase()}-profile-project.json`, protocol)
    await expect(page.locator('.capability-note')).toHaveClass(/ready/)
    await page.locator('.output-settings-button').click()
    const editButton = page.getByRole('button', { name: `编辑${label}机型参数` })
    await expect(editButton).toBeVisible()
    await expect(page.getByLabel(`${label} 机型名称`)).toHaveCount(0)
    await editButton.click()

    for (const field of [
      '机型名称', '可打印宽度', '最大标签高度', '单条图片上限', '支持图片', '支持原生二维码',
      '支持原生条码', '支持原生条码可读文字', '支持文字及条码旋转', '字体范围模式', '机型代码页列表', '字库覆盖区间',
    ]) await expect(page.getByLabel(`${label} ${field}`, { exact: true })).toBeVisible()
    if (protocol === 'Zpl') await expect(page.getByLabel('ZPL 支持切纸')).toBeVisible()

    const tools = page.locator('.element-tools button')
    await expect(tools.filter({ hasText: '图片' })).toBeDisabled()
    await expect(tools.filter({ hasText: '二维码' })).toBeDisabled()
    await expect(tools.filter({ hasText: '一维条码' })).toBeDisabled()
    await expect(tools.filter({ hasText: '线段' })).toBeDisabled()
    await expect(tools.filter({ hasText: '单行文字' })).toBeEnabled()

    const beforeBytes = await exportProtocolBytes(page, protocol)
    const saved = await exportProject(page)
    const savedProject = JSON.parse(saved.toString('utf8'))
    expect(savedProject.outputSettings[mediaKey(protocol)].profile).toMatchObject({
      name: importedUiProfile(protocol).name,
      deviceFontNames: ['0'],
      encodingCodePages: [20127],
    })

    const reloadPreview = page.waitForResponse((response) =>
      response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
    await page.getByTestId('template-import').setInputFiles({
      name: 'saved-profile-project.json', mimeType: 'application/json', buffer: saved,
    })
    await reloadPreview
    await expect(outputProtocol).toHaveValue(protocol)
    await expect(page.locator('.capability-note')).toHaveClass(/ready/)
    const afterBytes = await exportProtocolBytes(page, protocol)
    expect(afterBytes).toEqual(beforeBytes)

    const nameInput = page.getByLabel(`${label} 机型名称`)
    const originalName = await nameInput.inputValue()
    const updatedName = `${originalName} edited`
    await nameInput.fill(updatedName)
    await expect(nameInput).toHaveValue(updatedName)
    await page.waitForTimeout(350)
    await page.getByRole('button', { name: '撤销 Ctrl+Z' }).click()
    await expect(nameInput).toHaveValue(originalName)
    await page.getByRole('button', { name: '重做 Ctrl+Y' }).click()
    await expect(nameInput).toHaveValue(updatedName)
    const redoProject = JSON.parse((await exportProject(page)).toString('utf8'))
    expect(redoProject.outputSettings[mediaKey(protocol)].profile.name).toBe(updatedName)

    const widthInput = page.getByLabel(`${label} 可打印宽度`)
    await widthInput.fill('0')
    await expect(page.getByRole('alert')).toBeVisible()
    await expect(page.locator('.status-bar')).toContainText('机型配置有误')

    const renderRequests: string[] = []
    const requestListener = (candidate: Request) => {
      if (candidate.url().includes('/api/designer/render')) renderRequests.push(candidate.url())
    }
    page.on('request', requestListener)
    await page.getByRole('button', { name: `生成 ${label}` }).click()
    await expect(page.locator('.toast-message')).toContainText(/有效|无效|整数|约束/)
    await expect.poll(() => renderRequests.length).toBe(0)
    const blockedSave = page.waitForEvent('download', { timeout: 500 }).catch(() => undefined)
    await page.getByRole('button', { name: '保存项目' }).click()
    expect(await blockedSave).toBeUndefined()
    page.off('request', requestListener)

    await widthInput.fill('240')
    await expect(page.getByRole('alert')).toHaveCount(0)
    await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
    await expect(page.locator('.status-bar')).not.toContainText('机型配置有误')
    await expect(page.getByRole('button', { name: `生成 ${label}` })).toBeEnabled()
    expect(await exportProtocolBytes(page, protocol)).toEqual(beforeBytes)
  })
}
