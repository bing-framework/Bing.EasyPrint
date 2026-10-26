import { expect, test } from '@playwright/test'
import JSZip from 'jszip'
import { readFile } from 'node:fs/promises'
import { readPreviewLayout } from './previewResponse'

const nestedLockTemplate = {
  Version: 2, Width: 200, Height: 100,
  Rows: [{ Height: 100, Cells: [{ Width: 200, Elements: [
    { Type: 6, Width: 80, Height: 40, StartCoordinate: { X: 20, Y: 20 }, Elements: [
      { Type: 1, FontSize: 18, Field: { Value: 'LOCKED-CHILD' }, StartCoordinate: { X: 4, Y: 4 } },
    ] },
    { Type: 7, Width: 20, Height: 20, StartCoordinate: { X: 120, Y: 20 } },
  ] }] }],
}

const rowsTemplate = {
  Version: 2, Width: 180, Height: 100,
  Rows: [
    { Height: 40, Cells: [{ Width: 180, Elements: [{ Type: 1, FontSize: 18, Field: { Value: 'ROW-A' }, StartCoordinate: { X: 10, Y: 8 } }] }] },
    { Height: 40, Cells: [{ Width: 180, Elements: [{ Type: 1, FontSize: 18, Field: { Value: 'ROW-B' }, StartCoordinate: { X: 10, Y: 8 } }] }] },
  ],
}

const arrangeTemplate = {
  Version: 2, Width: 240, Height: 120,
  Rows: [{ Height: 120, Cells: [{ Width: 240, Elements: [
    { Type: 1, FontSize: 24, Field: { Value: 'A' }, StartCoordinate: { X: 70, Y: 70 }, EndCoordinate: { X: 110, Y: 100 } },
    { Type: 1, FontSize: 24, Field: { Value: 'B' }, StartCoordinate: { X: 10, Y: 10 }, EndCoordinate: { X: 40, Y: 40 } },
  ] }] }],
}

const arrangeSizedTemplate = {
  Version: 2, Width: 240, Height: 120,
  Rows: [{ Height: 120, Cells: [{ Width: 240, Elements: [
    { Type: 7, Width: 40, Height: 30, StartCoordinate: { X: 70, Y: 70 } },
    { Type: 7, Width: 30, Height: 24, StartCoordinate: { X: 10, Y: 10 } },
  ] }] }],
}

async function uploadTemplate(page: import('@playwright/test').Page, name: string, value: unknown) {
  await page.getByTestId('template-import').setInputFiles({
    name, mimeType: name.endsWith('.zip') ? 'application/zip' : 'application/json',
    buffer: Buffer.from(JSON.stringify(value)),
  })
}

async function openTemplate(page: import('@playwright/test').Page, name: string, value: unknown) {
  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const preview = page.waitForResponse((response) => response.url().includes('/api/designer/preview') &&
    response.request().method() === 'POST')
  await uploadTemplate(page, name, value)
  await preview
}

async function exportTemplate(page: import('@playwright/test').Page) {
  const download = page.waitForEvent('download')
  await page.getByRole('button', { name: '导出 JSON' }).click()
  return JSON.parse((await readFile(await (await download).path())).toString('utf8'))
}

async function saveProject(page: import('@playwright/test').Page) {
  const download = page.waitForEvent('download')
  await page.getByRole('button', { name: '保存项目' }).click()
  return JSON.parse((await readFile(await (await download).path())).toString('utf8')) as {
    editorState: { nodeStates: Record<string, { id: string; locked: boolean }> }
  }
}

test('nested locked descendants block element grouping without changing saved template', async ({ page }) => {
  await openTemplate(page, 'nested-lock.json', nestedLockTemplate)
  const childPath = 'Rows[0].Cells[0].Elements[0].Elements[0]'
  const groupPath = 'Rows[0].Cells[0].Elements[0]'
  const siblingPath = 'Rows[0].Cells[0].Elements[1]'
  await page.locator(`.tree-select[title="${childPath}"]`).click()
  await page.getByRole('button', { name: '锁定 文字 · LOCKED-CHILD' }).click()
  await page.locator(`.tree-select[title="${groupPath}"]`).click()
  await page.locator(`.tree-select[title="${siblingPath}"]`).click({ modifiers: ['Control'] })
  await page.getByRole('button', { name: '组件与表格' }).click()
  await page.getByRole('button', { name: '组合元素' }).click()

  await expect(page.locator('.toast-message')).toContainText('涉及已锁定节点')
  const saved = await exportTemplate(page)
  expect(saved.Rows[0].Cells[0].Elements[0].Elements[0].Field.Value).toBe('LOCKED-CHILD')
  const project = await saveProject(page)
  expect(project.editorState.nodeStates[childPath]?.locked).toBe(true)
})

test('row grouping preserves child IDs through the browser synchronous Vue watcher', async ({ page }) => {
  await openTemplate(page, 'rows.json', rowsTemplate)
  const before = await saveProject(page)
  const rowA = 'Rows[0].Cells[0].Elements[0]'
  const rowB = 'Rows[1].Cells[0].Elements[0]'
  const rowAId = before.editorState.nodeStates[rowA]?.id
  const rowBId = before.editorState.nodeStates[rowB]?.id
  await page.locator('.tree-select[title="Rows[0]"]').click()
  await page.locator('.tree-select[title="Rows[1]"]').click({ modifiers: ['Control'] })
  await page.getByRole('button', { name: '组件与表格' }).click()
  await page.getByRole('button', { name: '组合行' }).click()

  const after = await saveProject(page)
  expect(after.editorState.nodeStates['Rows[0].Cells[0].Elements[0].Rows[0].Cells[0].Elements[0]']?.id)
    .toBe(rowAId)
  expect(after.editorState.nodeStates['Rows[0].Cells[0].Elements[0].Rows[1].Cells[0].Elements[0]']?.id)
    .toBe(rowBId)
})

test('JSON v2 import is one undoable state transition and redo restores it', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const previous = await exportTemplate(page)
  await page.getByRole('button', { name: 'JSON', exact: true }).click()
  const rawBusinessJson = '{ invalid-json'
  await page.getByLabel('业务数据 JSON').fill(rawBusinessJson)
  const incoming = { format: 'Bing.EasyPrint.DesignerProject', version: 2,
    template: { Version: 1, Width: 220, Height: 80, Rows: [{ Cells: [{ Elements: [
      { Type: 1, FontSize: 18, Field: { Value: 'JSON-IMPORT' } },
    ] }] }] } }
  await uploadTemplate(page, 'project-v2.json', incoming)
  await expect(page.locator('.tree-select').filter({ hasText: 'JSON-IMPORT' })).toHaveCount(1)

  await page.getByTitle('撤销 Ctrl+Z').click()
  await expect(page.locator('.tree-select').filter({ hasText: 'JSON-IMPORT' })).toHaveCount(0)
  expect(await exportTemplate(page)).toEqual(previous)
  await expect(page.getByLabel('业务数据 JSON')).toHaveValue(rawBusinessJson)

  await page.getByTitle('重做 Ctrl+Y').click()
  await expect(page.locator('.tree-select').filter({ hasText: 'JSON-IMPORT' })).toHaveCount(1)
  expect(await exportTemplate(page)).toMatchObject(incoming.template)
  await expect(page.getByLabel('业务数据 JSON')).toHaveValue(rawBusinessJson)
})

test('ZIP import is one undoable state transition and redo restores it', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const previous = await exportTemplate(page)
  const incoming = { Version: 1, Width: 210, Height: 70, Rows: [{ Cells: [{ Elements: [
    { Type: 1, FontSize: 18, Field: { Value: 'ZIP-IMPORT' } },
  ] }] }] }
  const archive = new JSZip()
  archive.file('template.json', JSON.stringify(incoming))
  archive.file('manifest.json', JSON.stringify({ format: 'Bing.EasyPrint.TemplateBundle', version: 1, resources: [] }))
  const buffer = await archive.generateAsync({ type: 'nodebuffer' })
  await page.getByTestId('template-import').setInputFiles({
    name: 'template-bundle.zip', mimeType: 'application/zip', buffer,
  })
  await expect(page.locator('.tree-select').filter({ hasText: 'ZIP-IMPORT' })).toHaveCount(1)

  await page.getByTitle('撤销 Ctrl+Z').click()
  await expect(page.locator('.tree-select').filter({ hasText: 'ZIP-IMPORT' })).toHaveCount(0)
  expect(await exportTemplate(page)).toEqual(previous)
  await page.getByTitle('重做 Ctrl+Y').click()
  await expect(page.locator('.tree-select').filter({ hasText: 'ZIP-IMPORT' })).toHaveCount(1)
  expect(await exportTemplate(page)).toEqual(incoming)
})

test('back-to-back arrangements each create an independent undo step without waiting', async ({ page }) => {
  await openTemplate(page, 'arrange.json', arrangeTemplate)
  const firstPath = 'Rows[0].Cells[0].Elements[0]'
  const secondPath = 'Rows[0].Cells[0].Elements[1]'
  await page.locator(`.tree-select[title="${firstPath}"]`).click()
  await page.locator(`.tree-select[title="${secondPath}"]`).click({ modifiers: ['Control'] })
  await expect(page.getByTitle('排列所选元素')).toBeEnabled()
  await page.getByLabel('对齐或均分方式').selectOption('align-left')
  await page.getByTitle('排列所选元素').click()
  await page.getByLabel('对齐或均分方式').selectOption('align-top')
  await page.getByTitle('排列所选元素').click()
  await page.getByTitle('撤销 Ctrl+Z').click()

  let restored = await exportTemplate(page)
  expect(restored.Rows[0].Cells[0].Elements.map((element: { StartCoordinate: { X: number; Y: number } }) => element.StartCoordinate))
    .toEqual([{ X: 10, Y: 70 }, { X: 10, Y: 10 }])
  await page.getByTitle('撤销 Ctrl+Z').click()
  restored = await exportTemplate(page)
  expect(restored.Rows[0].Cells[0].Elements.map((element: { StartCoordinate: { X: number; Y: number } }) => element.StartCoordinate))
    .toEqual([{ X: 70, Y: 70 }, { X: 10, Y: 10 }])
  await page.getByTitle('重做 Ctrl+Y').click()
  await page.getByTitle('重做 Ctrl+Y').click()
  restored = await exportTemplate(page)
  expect(restored.Rows[0].Cells[0].Elements.map((element: { StartCoordinate: { X: number; Y: number } }) => element.StartCoordinate))
    .toEqual([{ X: 10, Y: 10 }, { X: 10, Y: 10 }])
})

test('R4-B: repeated axis arrangements use the refreshed layout and keep each changed step undoable', async ({ page }) => {
  await openTemplate(page, 'arrange-same-axis.json', arrangeSizedTemplate)
  const firstPath = 'Rows[0].Cells[0].Elements[0]'
  const secondPath = 'Rows[0].Cells[0].Elements[1]'
  await page.locator(`.tree-select[title="${firstPath}"]`).click()
  await page.locator(`.tree-select[title="${secondPath}"]`).click({ modifiers: ['Control'] })

  const refreshedLayout = page.waitForResponse((response) => response.url().includes('/api/designer/preview') &&
    response.request().method() === 'POST')
  await page.getByLabel('对齐或均分方式').selectOption('align-left')
  await page.getByTitle('排列所选元素').click()
  const layout = await readPreviewLayout(await refreshedLayout)
  expect(layout.pages[0]?.elements
    .filter((element) => element.sourcePath === firstPath || element.sourcePath === secondPath)
    .map((element) => element.width)).toEqual([40, 30])
  await page.getByLabel('对齐或均分方式').selectOption('align-right')
  await page.getByTitle('排列所选元素').click()

  let restored = await exportTemplate(page)
  expect(restored.Rows[0].Cells[0].Elements.map((element: { StartCoordinate: { X: number } }) => element.StartCoordinate.X))
    .toEqual([10, 20])
  await page.getByTitle('撤销 Ctrl+Z').click()
  restored = await exportTemplate(page)
  expect(restored.Rows[0].Cells[0].Elements.map((element: { StartCoordinate: { X: number } }) => element.StartCoordinate.X))
    .toEqual([10, 10])
  await page.getByTitle('撤销 Ctrl+Z').click()
  restored = await exportTemplate(page)
  expect(restored.Rows[0].Cells[0].Elements.map((element: { StartCoordinate: { X: number } }) => element.StartCoordinate.X))
    .toEqual([70, 10])

  await page.getByLabel('对齐或均分方式').selectOption('align-top')
  await page.getByTitle('排列所选元素').click()
  await page.getByLabel('对齐或均分方式').selectOption('align-bottom')
  await page.getByTitle('排列所选元素').click()
  restored = await exportTemplate(page)
  expect(restored.Rows[0].Cells[0].Elements.map((element: { StartCoordinate: { Y: number } }) => element.StartCoordinate.Y))
    .toEqual([10, 16])

  await page.getByTitle('撤销 Ctrl+Z').click()
  restored = await exportTemplate(page)
  expect(restored.Rows[0].Cells[0].Elements.map((element: { StartCoordinate: { Y: number } }) => element.StartCoordinate.Y))
    .toEqual([10, 10])
  await page.getByTitle('撤销 Ctrl+Z').click()
  restored = await exportTemplate(page)
  expect(restored.Rows[0].Cells[0].Elements.map((element: { StartCoordinate: { Y: number } }) => element.StartCoordinate.Y))
    .toEqual([70, 10])

  await page.getByLabel('对齐或均分方式').selectOption('align-left')
  await page.getByTitle('排列所选元素').click()
  await page.getByLabel('对齐或均分方式').selectOption('align-left')
  await page.getByTitle('排列所选元素').click()
  restored = await exportTemplate(page)
  expect(restored.Rows[0].Cells[0].Elements.map((element: { StartCoordinate: { X: number } }) => element.StartCoordinate.X))
    .toEqual([10, 10])
  await page.getByTitle('撤销 Ctrl+Z').click()
  restored = await exportTemplate(page)
  expect(restored.Rows[0].Cells[0].Elements.map((element: { StartCoordinate: { X: number } }) => element.StartCoordinate.X))
    .toEqual([70, 10])
})

test('R4-B: stale layouts stay disabled during preview and after a failed preview', async ({ page }) => {
  await openTemplate(page, 'arrange-pending.json', arrangeSizedTemplate)
  const firstPath = 'Rows[0].Cells[0].Elements[0]'
  const secondPath = 'Rows[0].Cells[0].Elements[1]'
  await page.locator(`.tree-select[title="${firstPath}"]`).click()
  await page.locator(`.tree-select[title="${secondPath}"]`).click({ modifiers: ['Control'] })

  let releasePreview!: () => void
  let previewStarted!: () => void
  const previewStartedPromise = new Promise<void>((resolve) => { previewStarted = resolve })
  const previewGate = new Promise<void>((resolve) => { releasePreview = resolve })
  let finishPreview!: () => void
  const previewFinishedPromise = new Promise<void>((resolve) => { finishPreview = resolve })
  await page.route('**/api/designer/preview*', async (route) => {
    previewStarted()
    await previewGate
    const response = await route.fetch()
    await route.fulfill({ response })
    finishPreview()
  })
  try {
    await page.getByLabel('对齐或均分方式').selectOption('align-left')
    await page.getByTitle('排列所选元素').click()
    await previewStartedPromise
    await page.getByLabel('对齐或均分方式').selectOption('align-right')
    const arrangement = page.getByTitle(/当前模板的引擎预览布局未就绪/)
    await expect(arrangement).toBeDisabled()
    let restored = await exportTemplate(page)
    expect(restored.Rows[0].Cells[0].Elements.map((element: { StartCoordinate: { X: number } }) => element.StartCoordinate.X))
      .toEqual([10, 10])
  } finally {
    releasePreview()
    await previewFinishedPromise
    await page.unroute('**/api/designer/preview*').catch(() => {})
  }

  await expect(page.getByTitle('排列所选元素')).toBeEnabled()
  await page.getByTitle('排列所选元素').click()
  await expect(page.getByTitle('排列所选元素')).toBeEnabled()
  let restored = await exportTemplate(page)
  expect(restored.Rows[0].Cells[0].Elements.map((element: { StartCoordinate: { X: number } }) => element.StartCoordinate.X))
    .toEqual([10, 20])

  const failedPreview = page.waitForResponse((response) => response.url().includes('/api/designer/preview') &&
    response.status() === 503)
  await page.route('**/api/designer/preview*', async (route) => {
    await route.fulfill({ status: 503, body: 'preview failure probe' })
  })
  await page.getByLabel('对齐或均分方式').selectOption('align-top')
  await page.getByTitle('排列所选元素').click()
  await failedPreview
  await page.getByLabel('对齐或均分方式').selectOption('align-bottom')
  await expect(page.getByTitle(/当前模板的引擎预览布局未就绪/)).toBeDisabled()
  restored = await exportTemplate(page)
  expect(restored.Rows[0].Cells[0].Elements.map((element: { StartCoordinate: { X: number; Y: number } }) => element.StartCoordinate))
    .toEqual([{ X: 10, Y: 10 }, { X: 20, Y: 10 }])
})

test('R3-B: a locked descendant blocks arranging its unlocked group without changing history', async ({ page }) => {
  const template = {
    Version: 2, Width: 300, Height: 100,
    Rows: [{ Height: 100, Cells: [{ Width: 300, Elements: [
      { Type: 6, Width: 140, Height: 40, StartCoordinate: { X: 20, Y: 20 }, Elements: [
        { Type: 1, FontSize: 24, Field: { Value: 'A' }, StartCoordinate: { X: 4, Y: 4 } },
      ] },
      { Type: 7, Width: 20, Height: 20, StartCoordinate: { X: 220, Y: 20 } },
    ] }] }],
  }
  await openTemplate(page, 'nested-lock-arrange-valid-cpcl.json', template)
  await expect(page.locator('.preview-source.service')).toHaveText('引擎预览')
  const childPath = 'Rows[0].Cells[0].Elements[0].Elements[0]'
  const groupPath = 'Rows[0].Cells[0].Elements[0]'
  const siblingPath = 'Rows[0].Cells[0].Elements[1]'
  await page.locator(`.tree-select[title="${childPath}"]`).click()
  await page.getByRole('button', { name: '锁定 文字 · A', exact: true }).click()
  await page.locator(`.tree-select[title="${groupPath}"]`).click()
  await page.locator(`.tree-select[title="${siblingPath}"]`).click({ modifiers: ['Control'] })
  await page.getByLabel('对齐或均分方式').selectOption('align-right')

  const button = page.getByTitle(/排列涉及已锁定节点/)
  await expect(button).toBeDisabled()
  const before = await exportTemplate(page)
  const projectBefore = await saveProject(page)
  expect(projectBefore.editorState.nodeStates[childPath]?.locked).toBe(true)
  expect(projectBefore.template.Rows[0].Cells[0].Elements[0].StartCoordinate).toEqual({ X: 20, Y: 20 })

  await page.getByRole('button', { name: '解锁 文字 · A', exact: true }).click()
  await page.getByTitle('撤销 Ctrl+Z').click()
  const projectAfterUndo = await saveProject(page)
  expect(projectAfterUndo.editorState.nodeStates[childPath]?.locked).toBe(true)
  expect(projectAfterUndo.template.Rows[0].Cells[0].Elements[0].StartCoordinate).toEqual({ X: 20, Y: 20 })
  expect(before.Rows[0].Cells[0].Elements[0].StartCoordinate).toEqual({ X: 20, Y: 20 })
})
