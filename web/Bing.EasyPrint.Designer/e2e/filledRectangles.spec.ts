import { expect, test, type APIRequestContext, type Page } from '@playwright/test'
import { readFile } from 'node:fs/promises'
import JSZip from 'jszip'

const apiUrl = () => process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'
const protocols = ['Tspl', 'Zpl'] as const
type LabelProtocol = (typeof protocols)[number]

const rectangleTemplate = {
  Version: 2,
  Width: 240,
  Height: 120,
  Rows: [{ Height: 120, Cells: [{ Width: 240, Elements: [{
    Type: 7,
    Width: 120,
    Height: 64,
    LineWidth: 2,
    Filled: true,
    StartCoordinate: { X: 24, Y: 16 },
  }] }] }],
}

function labelProfile(protocol: LabelProtocol) {
  return {
    name: `${protocol} filled rectangle QA`,
    firmwareVersion: 'filled-rectangle-1',
    printableWidthDots: 240,
    maxLabelHeightDots: 120,
    supportsImages: false,
    supportsGraphics: true,
    supportsNativeBarcode: false,
    supportsNativeQrCode: false,
    supportsRotation: false,
    deviceFontNames: ['0'],
    encodingCodePages: [20127],
    characterRanges: [{ first: 32, last: 126 }],
    ...(protocol === 'Zpl' ? { supportsCut: false } : {}),
  }
}

function labelMedia(protocol: LabelProtocol, profile: Record<string, unknown>) {
  return protocol === 'Tspl' ? { tsplMedia: { profile } } : { zplMedia: { profile } }
}

function payload(protocol: LabelProtocol, profile?: Record<string, unknown>) {
  return {
    template: rectangleTemplate,
    data: {},
    protocol,
    encoding: 'Ascii',
    ...(profile === undefined ? {} : labelMedia(protocol, profile)),
  }
}

async function renderPage(request: APIRequestContext, body: Record<string, unknown>, protocol: LabelProtocol) {
  const response = await request.post(`${apiUrl()}/api/designer/render`, { data: body })
  expect(response.status()).toBe(200)
  const archive = await JSZip.loadAsync(await response.body())
  const entry = archive.file(`page-001.${protocol === 'Tspl' ? 'tspl' : 'zpl'}`)
  expect(entry).not.toBeNull()
  return entry!.async('uint8array')
}

function decode(bytes: Uint8Array) {
  return new TextDecoder('ascii').decode(bytes)
}

async function importTemplate(page: Page, template: unknown, name: string) {
  const preview = page.waitForResponse((response) => response.url().includes('/api/designer/preview') && response.request().method() === 'POST')
  await page.getByTestId('template-import').setInputFiles({
    name,
    mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify(template)),
  })
  expect((await preview).status()).toBe(200)
  await expect(page.locator('.tree-row.tree-element .tree-select')).toHaveCount(1)
  await page.locator('.tree-row.tree-element .tree-select').click()
}

for (const protocol of protocols) {
  test(`${protocol} 支持填充矩形，且机型禁用图片时仍保留图形能力`, async ({ request }) => {
    const profile = labelProfile(protocol)
    const capabilitiesResponse = await request.post(`${apiUrl()}/api/designer/capabilities`, {
      data: payload(protocol, profile),
    })
    expect(capabilitiesResponse.status()).toBe(200)
    const capabilities = await capabilitiesResponse.json()
    expect(capabilities).toMatchObject({
      protocol,
      labelProfile: { name: profile.name },
      supportsFilledRectangles: true,
    })
    expect(capabilities.elementTypes).toContain(7)
    expect(capabilities.elementTypes).not.toContain(2)

    const preview = await request.post(`${apiUrl()}/api/designer/preview`, { data: payload(protocol, profile) })
    expect(preview.status()).toBe(200)
    const previewBody = await preview.json()
    expect(previewBody.pages[0].elements).toEqual(expect.arrayContaining([
      expect.objectContaining({ type: 7, x: 24, y: 16, width: 120, height: 64 }),
    ]))

    const bytes = decode(await renderPage(request, payload(protocol, profile), protocol))
    expect(bytes).toContain(protocol === 'Tspl' ? 'BAR 24,16,120,64' : '^FO24,16^GB120,64,64,B,0^FS')
  })
}

test('ESC/POS Native 严格拒绝填充矩形', async ({ request }) => {
  const response = await request.post(`${apiUrl()}/api/designer/render`, {
    data: { ...payload('Tspl'), protocol: 'EscPos', escPosLayoutMode: 'Native' },
  })
  expect(response.status()).toBe(400)
  const error = await response.json()
  expect(error.errors?.[0]?.message ?? error.message ?? '').toMatch(/填充矩形|ESC\/POS|Native/)
  expect(error.errors?.[0]?.sourcePath).toBe('Rows[0].Cells[0].Elements[0]')
})

test('设计器可勾选填充、保存重载并导出 TSPL 字节', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  const uiTemplate = JSON.parse(JSON.stringify(rectangleTemplate)) as typeof rectangleTemplate
  uiTemplate.Rows[0].Cells[0].Elements[0].Filled = false
  await importTemplate(page, uiTemplate, 'filled-rectangle.json')
  await page.getByRole('combobox', { name: '输出协议' }).selectOption('Tspl')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)

  const filled = page.getByLabel('填充矩形')
  await expect(filled).not.toBeChecked()
  await filled.check()
  await expect(filled).toBeChecked()

  const savePromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  const saved = JSON.parse((await readFile(await (await savePromise).path())).toString('utf8'))
  expect(saved.template.Rows[0].Cells[0].Elements[0]).toMatchObject({ Type: 7, Filled: true })

  await importTemplate(page, saved, 'filled-rectangle-project.json')
  await expect(page.getByLabel('填充矩形')).toBeChecked()

  const renderPromise = page.waitForResponse((response) => response.url().includes('/api/designer/render') && response.request().method() === 'POST')
  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 TSPL' }).click()
  expect((await renderPromise).status()).toBe(200)
  const archive = await JSZip.loadAsync(await readFile(await (await downloadPromise).path()))
  const pageBytes = await archive.file('page-001.tspl')!.async('uint8array')
  expect(decode(pageBytes)).toContain('BAR 24,16,120,64')
})
