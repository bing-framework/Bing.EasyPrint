import { expect, test, type Page } from '@playwright/test'

const contractVersionHeader = 'X-EasyPrint-Contract-Version'
const featuresHeader = 'X-EasyPrint-Features'
const requiredFeatures = 'structured-errors,strict-options,binding-diagnostics'
const supportedFeatures = `${requiredFeatures},multi-protocol,ordered-output-segments`

type ContractScenario = 'missingVersion' | 'missingFeature' | 'valid'

function responseHeaders(scenario: ContractScenario): Record<string, string> {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    [contractVersionHeader]: '1',
    [featuresHeader]: supportedFeatures,
  }
  if (scenario === 'missingVersion') delete headers[contractVersionHeader]
  if (scenario === 'missingFeature') headers[featuresHeader] = 'structured-errors,strict-options'
  return headers
}

function validCapabilities() {
  return {
    protocol: 'Cpcl', escPosLayoutMode: null, nativeFlow: null, nativeText: null, rasterFonts: null,
    dpi: 200, elementTypes: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10], fontKinds: [0], deviceFonts: [],
    textSizes: [24], textSize: { minimum: 8, maximum: 72 }, textRotations: [0],
    barcodeTypes: [0], barcodeRotations: [0], qrModuleSize: null, barcodeModuleWidth: null,
    barcodeHeight: null, supportsExplicitBarcodeModuleWidth: false, supportsWideModuleWidth: false,
    supportsBoldText: true, supportsUnderlineText: true, supportsRotatedUnderlineText: false,
    supportsHostFontFamily: false, supportsLegacyDeviceFont: true, supportsBarcodeHumanReadable: true,
    supportsCut: false, supportsExtraFeed: false, supportsFilledRectangles: true,
    supportsCoordinates: true, supportsExplicitQuietZone: false, jobFeedUnits: [],
    cutModes: [], cutFeedUnits: [], cutFeedMotionUnits: null, cutFeedDots: null,
    tsplCutter: null, zplCutter: null, supportsIndependentOutputSegments: false,
  }
}

const missingBindingTemplate = {
  Version: 1,
  Width: 576,
  Height: 100,
  Rows: [{
    Cells: [{ Elements: [{ Type: 1, FontSize: 24, Field: { Name: 'Missing' } }] }],
  }],
}

async function installHostRoutes(page: Page, scenario: ContractScenario,
  previewBody: Record<string, unknown>, onPreviewRequest?: (body: unknown) => void) {
  await page.route('**/api/designer/samples', async (route) => {
    await route.fulfill({
      status: 200,
      headers: responseHeaders('valid'),
      body: JSON.stringify({ samples: [] }),
    })
  })
  await page.route('**/api/designer/capabilities', async (route) => {
    await route.fulfill({
      status: 200,
      headers: responseHeaders(scenario),
      body: JSON.stringify(validCapabilities()),
    })
  })
  await page.route('**/api/designer/preview', async (route) => {
    onPreviewRequest?.(route.request().postDataJSON())
    await route.fulfill({
      status: 200,
      headers: responseHeaders(scenario),
      body: JSON.stringify(previewBody),
    })
  })
}

test('旧 Host 缺少版本响应头时显示更新提示并暂停能力新增入口', async ({ page }) => {
  await installHostRoutes(page, 'missingVersion', { pages: [], errors: [] })
  await page.goto('/')

  const capabilityNote = page.locator('.capability-note')
  await expect(capabilityNote).toHaveClass(/error/)
  await expect(capabilityNote).toHaveAttribute('title', '打印服务版本不兼容，请更新 Host。')
  await expect(page.getByRole('button', { name: '单行文字', exact: true })).toBeDisabled()
  await expect(page.locator('.diagnostic-panel')).toContainText('打印服务版本不兼容，请更新 Host。')
})

test('Host 缺少 binding-diagnostics 能力时显示更新提示', async ({ page }) => {
  await installHostRoutes(page, 'missingFeature', { pages: [], errors: [] })
  await page.goto('/')

  const capabilityNote = page.locator('.capability-note')
  await expect(capabilityNote).toHaveClass(/error/)
  await expect(capabilityNote).toHaveAttribute('title', '打印服务缺少所需功能，请更新 Host。')
  await expect(page.getByRole('button', { name: '单行文字', exact: true })).toBeDisabled()
  await expect(page.locator('.diagnostic-panel')).toContainText('打印服务缺少所需功能，请更新 Host。')
})

test('真实 Host 的绑定诊断显示字段、节点、代码和阶段并可定位', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)

  const previewResponsePromise = page.waitForResponse((response) =>
    response.url().includes('/api/designer/preview') &&
    response.request().method() === 'POST' &&
    response.status() === 400)
  await page.getByTestId('template-import').setInputFiles({
    name: 'missing-binding.json',
    mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify(missingBindingTemplate)),
  })

  const previewResponse = await previewResponsePromise
  expect(previewResponse.status()).toBe(400)
  const previewError = await previewResponse.json() as {
    errors?: Array<Record<string, unknown>>
  }
  expect(previewError.errors?.[0]).toMatchObject({
    code: 'BindingFailed',
    phase: 'Binding',
    fieldPath: 'Missing',
    sourcePath: 'Rows[0].Cells[0].Elements[0]',
  })

  const diagnostic = page.locator('.diagnostic-panel article').first()
  await expect(diagnostic).toContainText('代码：BindingFailed')
  await expect(diagnostic).toContainText('阶段：Binding')
  await expect(diagnostic).toContainText('字段：Missing')
  await expect(diagnostic).toContainText('Rows[0].Cells[0].Elements[0]')

  const locateNode = diagnostic.getByRole('button', { name: '定位节点' })
  await expect(locateNode).toBeEnabled()
  await locateNode.click()
  await expect(page.locator('.inspector-selection small')).toContainText('Rows[0].Cells[0].Elements[0]')
})
