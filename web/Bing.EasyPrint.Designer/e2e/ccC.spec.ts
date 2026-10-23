import { expect, test } from '@playwright/test'
import { readFile } from 'node:fs/promises'
import JSZip from 'jszip'

test('GS1-128 CC-C 真实 Host 编辑、输出和项目往返', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.locator('.sample-picker select').selectOption('blank')
  await page.getByRole('button', { name: '组件与表格' }).click()
  await page.getByRole('button', { name: 'GS1 Composite', exact: true }).click()
  await page.getByLabel('Composite 线性组件', { exact: true }).selectOption('11')
  await expect(page.getByLabel('Composite 二维模式', { exact: true })).toHaveValue('3')
  await expect(page.getByLabel('Composite 二维模式', { exact: true }).locator('option'))
    .toHaveText(['Auto', 'CC-A', 'CC-B', 'CC-C'])
  await page.getByLabel('显示内容', { exact: true }).fill('(01)12345678901231')
  const preview = page.waitForResponse(response => response.url().includes('/api/designer/preview') &&
    response.status() === 200 && JSON.stringify(response.request().postDataJSON()?.template).includes('(10)CCCTEST'))
  await page.getByLabel('Composite 次数据').fill('(10)CCCTEST')
  const response = await preview
  expect(response.request().headers()['x-easyprint-required-features']).toContain('gs1-composite-cc-c')
  const layout = await response.json()
  const element = layout.pages.flatMap((p: { elements: any[] }) => p.elements).find((e: { type: number }) => e.type === 11)
  expect(element.width).toBeGreaterThan(0)
  expect(element.height).toBeGreaterThan(40)
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  await expect(page.locator('.status-spinner')).toHaveCount(0)
  await expect.poll(() => page.evaluate(() => {
    const root = document.querySelector('.page-frame')!.firstElementChild as HTMLElement & { __vueParentComponent?: any }
    return root.__vueParentComponent?.exposed?.getNode?.()?.find('Text').some((node: any) => node.text() === 'GS1 Composite') ?? false
  })).toBe(true)

  const output = page.waitForEvent('download')
  await page.getByRole('button', { name: '生成 CPCL' }).click()
  const archive = await JSZip.loadAsync(await readFile(await (await output).path()))
  const bytes = Buffer.from(await archive.file('page-001.cpcl')!.async('uint8array'))
  const header = Buffer.from(`CG ${Math.ceil(element.width / 8)} ${element.height} ${element.x} ${element.y} `)
  expect(bytes.indexOf(header)).toBeGreaterThanOrEqual(0)
  expect(bytes.length).toBeGreaterThan(Math.ceil(element.width / 8) * element.height)

  const download = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  const saved = await readFile(await (await download).path())
  const project = JSON.parse(saved.toString())
  expect(project.template.Rows[0].Cells[0].Elements[0]).toMatchObject({ Type: 11, LinearType: 11, Mode: 3,
    Field: { Value: '(01)12345678901231' }, SecondaryField: { Value: '(10)CCCTEST' } })
  await page.reload()
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  const restored = page.waitForResponse(response => response.url().includes('/api/designer/preview') &&
    response.status() === 200 && JSON.stringify(response.request().postDataJSON()?.template).includes('(10)CCCTEST'))
  await page.getByTestId('template-import').setInputFiles({ name: 'cc-c.json', mimeType: 'application/json', buffer: saved })
  expect((await (await restored).json()).pages).toEqual(layout.pages)
  await page.locator('.tree-row.tree-element .tree-select').filter({ hasText: 'GS1 Composite' }).click()
  await expect(page.getByLabel('Composite 线性组件', { exact: true })).toHaveValue('11')
  await expect(page.getByLabel('Composite 二维模式', { exact: true })).toHaveValue('3')
})

test('GS1-128 CC-A 和 CC-B 真实 Host 编辑、预览、输出与项目往返', async ({ page }) => {
  for (const { mode, label, suffix } of [
    { mode: '1', label: 'CC-A', suffix: 'A' },
    { mode: '2', label: 'CC-B', suffix: 'B' },
  ]) {
    const secondary = `(10)GS1AB${suffix}`
    await page.goto('/')
    await expect(page.locator('.capability-note')).toHaveClass(/ready/)
    await page.locator('.sample-picker select').selectOption('blank')
    await page.getByRole('button', { name: '组件与表格' }).click()
    await page.getByRole('button', { name: 'GS1 Composite', exact: true }).click()
    await page.getByLabel('Composite 线性组件', { exact: true }).selectOption('11')
    await page.getByLabel('Composite 二维模式', { exact: true }).selectOption(mode)
    await expect(page.getByLabel('Composite 二维模式', { exact: true })).toHaveValue(mode)
    // Changing the linear type preserves existing input; the user supplies bracketed GS1 data.
    await expect(page.getByLabel('显示内容', { exact: true })).toHaveValue('12345678901231')
    await page.getByLabel('显示内容', { exact: true }).fill('(01)12345678901231')
    await expect(page.getByLabel('显示内容', { exact: true })).toHaveValue('(01)12345678901231')

    const preview = page.waitForResponse(response => response.url().includes('/api/designer/preview') &&
      response.status() === 200 && JSON.stringify(response.request().postDataJSON()?.template).includes(secondary))
    await page.getByLabel('Composite 次数据', { exact: true }).fill(secondary)
    const response = await preview
    const requiredFeatures = response.request().headers()['x-easyprint-required-features'] ?? ''
    expect(requiredFeatures).toContain('gs1-composite')
    expect(requiredFeatures).toContain('gs1-composite-gs1-128-ab')
    expect(requiredFeatures).not.toContain('gs1-composite-cc-c')
    expect(response.request().postDataJSON()?.template).toMatchObject({ Rows: [{ Cells: [{ Elements: [{
      LinearType: 11, Mode: Number(mode), Field: { Value: '(01)12345678901231' }, SecondaryField: { Value: secondary },
    }] }] }] })
    const layout = await response.json()
    const element = layout.pages.flatMap((p: { elements: any[] }) => p.elements).find((e: { type: number }) => e.type === 11)
    expect(element.width).toBeGreaterThan(0)
    expect(element.height).toBeGreaterThan(40)
    await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
    await expect(page.locator('.status-spinner')).toHaveCount(0)

    const renderResponse = page.waitForResponse(render => render.url().includes('/api/designer/render') &&
      render.status() === 200 && JSON.stringify(render.request().postDataJSON()?.template).includes(secondary))
    const output = page.waitForEvent('download')
    await page.getByRole('button', { name: '生成 CPCL' }).click()
    const rendered = await renderResponse
    expect(rendered.request().headers()['x-easyprint-required-features']).toContain('gs1-composite-gs1-128-ab')
    const archive = await JSZip.loadAsync(await readFile(await (await output).path()))
    const bytes = Buffer.from(await archive.file('page-001.cpcl')!.async('uint8array'))
    const header = Buffer.from(`CG ${Math.ceil(element.width / 8)} ${element.height} ${element.x} ${element.y} `)
    expect(bytes.indexOf(header)).toBeGreaterThanOrEqual(0)
    expect(bytes.length).toBeGreaterThan(Math.ceil(element.width / 8) * element.height)

    const savedDownload = page.waitForEvent('download')
    await page.getByRole('button', { name: '保存项目' }).click()
    const saved = await readFile(await (await savedDownload).path())
    const project = JSON.parse(saved.toString())
    expect(project.template.Rows[0].Cells[0].Elements[0]).toMatchObject({ Type: 11, LinearType: 11,
      Mode: Number(mode), Field: { Value: '(01)12345678901231' }, SecondaryField: { Value: secondary } })

    await page.reload()
    await expect(page.locator('.capability-note')).toHaveClass(/ready/)
    const restored = page.waitForResponse(restoredResponse => restoredResponse.url().includes('/api/designer/preview') &&
      restoredResponse.status() === 200 && JSON.stringify(restoredResponse.request().postDataJSON()?.template).includes(secondary))
    await page.getByTestId('template-import').setInputFiles({ name: `cc-${suffix.toLowerCase()}.json`,
      mimeType: 'application/json', buffer: saved })
    expect((await (await restored).json()).pages).toEqual(layout.pages)
    await page.locator('.tree-row.tree-element .tree-select').filter({ hasText: 'GS1 Composite' }).click()
    await expect(page.getByLabel('Composite 线性组件', { exact: true })).toHaveValue('11')
    await expect(page.getByLabel('Composite 二维模式', { exact: true })).toHaveValue(mode)
    await expect(page.getByLabel('Composite 二维模式', { exact: true }).locator(`option[value="${mode}"]`))
      .toHaveText(label)
  }
})

test('旧 Host 保留 DataBar Composite 并明确拒绝 CC-C', async ({ page }) => {
  await page.route('**/api/designer/**', async route => {
    const response = await route.fetch()
    const headers = response.headers()
    headers['x-easyprint-features'] = (headers['x-easyprint-features'] ?? '').split(',')
      .filter(feature => !['gs1-databar-expanded-stacked-columns', 'gs1-databar-expanded-stacked-rows'].includes(feature.trim())).join(',')
    headers['x-easyprint-features'] = (headers['x-easyprint-features'] ?? '').split(',')
      .filter(feature => !['gs1-composite-cc-c', 'gs1-composite-ean-upc', 'gs1-composite-ean-upc-addons',
        'gs1-composite-truncated', 'gs1-composite-stacked-omnidirectional'].includes(feature.trim())).join(',')
    if (route.request().url().includes('/capabilities')) {
      const json = await response.json()
      delete json.expandedStackedColumns
      delete json.expandedStackedRows
      if (json.gs1Composite) delete json.gs1Composite.expandedStackedColumns
      if (json.gs1Composite) delete json.gs1Composite.expandedStackedRows
      if (json.gs1Composite) {
        json.gs1Composite.modes = ['Auto', 'CcA', 'CcB']
        json.gs1Composite.linearTypes = ['Gs1DataBarOmnidirectional', 'Gs1DataBarLimited']
        if (json.gs1Composite.primaryFormats) json.gs1Composite.primaryFormats = json.gs1Composite.primaryFormats
          .filter((format: string) => format !== 'ean-upc-digits-with-addon')
      }
      await route.fulfill({ response, headers, json })
    } else await route.fulfill({ response, headers })
  })
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.locator('.sample-picker select').selectOption('blank')
  await page.getByRole('button', { name: '组件与表格' }).click()
  await page.getByRole('button', { name: 'GS1 Composite', exact: true }).click()
  await expect(page.getByLabel('Composite 线性组件', { exact: true }).locator('option[value="11"]')).toHaveJSProperty('disabled', true)
  await expect(page.getByLabel('Composite 线性组件', { exact: true }).locator('option[value="25"]')).toHaveJSProperty('disabled', false)
  const template = { Version: 3, Width: 600, Height: 300, Rows: [{ Cells: [{ Elements: [
    { Type: 11, LinearType: 11, Mode: 3, Field: { Value: '(01)12345678901231' },
      SecondaryField: { Value: '(10)CCCTEST' }, ModuleWidth: 2, LinearBarHeight: 40 },
  ] }] }] }
  await page.getByTestId('template-import').setInputFiles({ name: 'cc-c.json', mimeType: 'application/json',
    buffer: Buffer.from(JSON.stringify(template)) })
  await expect(page.getByText('打印服务缺少 GS1-128 + CC-C Composite 功能，请更新 Host。', { exact: false }).first()).toBeVisible()
  await page.locator('.tree-row.tree-element .tree-select').filter({ hasText: 'GS1 Composite' }).click()
  await expect(page.getByText('当前 Host 未协商 GS1-128 + CC-C；此组合的预览与输出不可用。')).toBeVisible()
})

test('缺少 GS1-128 CC-A/CC-B 特性时保留导入模式并拒绝预览', async ({ page }) => {
  await page.route('**/api/designer/**', async route => {
    const response = await route.fetch()
    const headers = response.headers()
    headers['x-easyprint-features'] = (headers['x-easyprint-features'] ?? '').split(',')
      .filter(feature => !['gs1-databar-expanded-stacked-columns', 'gs1-databar-expanded-stacked-rows'].includes(feature.trim())).join(',')
    headers['x-easyprint-features'] = (headers['x-easyprint-features'] ?? '').split(',')
      .filter(feature => !['gs1-composite-gs1-128-ab', 'gs1-composite-ean-upc', 'gs1-composite-ean-upc-addons',
        'gs1-composite-truncated', 'gs1-composite-stacked-omnidirectional'].includes(feature.trim())).join(',')
    if (route.request().url().includes('/capabilities')) {
      const json = await response.json()
      delete json.expandedStackedColumns
      delete json.expandedStackedRows
      if (json.gs1Composite) delete json.gs1Composite.expandedStackedColumns
      if (json.gs1Composite) delete json.gs1Composite.expandedStackedRows
      if (json.gs1Composite) json.gs1Composite.linearTypes = json.gs1Composite.linearTypes
        .filter((type: string) => !['Ean8', 'Ean13', 'UpcA', 'UpcE', 'Gs1DataBarTruncated',
          'Gs1DataBarStackedOmnidirectional'].includes(type))
      if (json.gs1Composite?.primaryFormats) json.gs1Composite.primaryFormats = json.gs1Composite.primaryFormats
        .filter((format: string) => format !== 'ean-upc-digits-with-addon')
      await route.fulfill({ response, headers, json })
    } else await route.fulfill({ response, headers })
  })
  await page.goto('/')
  await expect(page.locator('.capability-note')).toHaveClass(/ready/)
  await page.locator('.sample-picker select').selectOption('blank')
  await page.getByRole('button', { name: '组件与表格' }).click()
  await page.getByRole('button', { name: 'GS1 Composite', exact: true }).click()
  await page.getByLabel('Composite 线性组件', { exact: true }).selectOption('11')
  await expect(page.getByLabel('Composite 二维模式', { exact: true }).locator('option[value="1"]'))
    .toHaveJSProperty('disabled', true)
  await expect(page.getByLabel('Composite 二维模式', { exact: true }).locator('option[value="3"]'))
    .toHaveJSProperty('disabled', false)

  const template = { Version: 3, Width: 600, Height: 300, Rows: [{ Cells: [{ Elements: [
    { Type: 11, LinearType: 11, Mode: 1, Field: { Value: '(01)12345678901231' },
      SecondaryField: { Value: '(10)GS1ABOLD' }, ModuleWidth: 2, LinearBarHeight: 40 },
  ] }] }] }
  const previewRequest = page.waitForRequest(request => request.url().includes('/api/designer/preview') &&
    JSON.stringify(request.postDataJSON()?.template).includes('(10)GS1ABOLD'))
  await page.getByTestId('template-import').setInputFiles({ name: 'gs1-128-cc-a-old-host.json',
    mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(template)) })
  const request = await previewRequest
  expect(request.postDataJSON()?.template).toMatchObject({ Rows: [{ Cells: [{ Elements: [{ LinearType: 11, Mode: 1 }] }] }] })
  expect(request.headers()['x-easyprint-required-features']).toContain('gs1-composite-gs1-128-ab')
  await expect(page.getByText('打印服务缺少 GS1-128 + CC-A/CC-B Composite 功能，请更新 Host。', { exact: false }).first())
    .toBeVisible()
  await page.locator('.tree-row.tree-element .tree-select').filter({ hasText: 'GS1 Composite' }).click()
  await expect(page.getByLabel('Composite 二维模式', { exact: true })).toHaveValue('1')
  await expect(page.getByText('当前 Host 未协商 GS1-128 + CC-A/CC-B Composite；导入的模式值会保留，但预览与输出不可用。'))
    .toBeVisible()
})
