import type { BpTemplate, FontElement, TemplateElement } from './model'

const point = (X: number, Y: number) => ({ X, Y })

function text(Name: string | undefined, Value: string | undefined, x: number, y: number, w: number, h: number, size = 24, extra: Partial<FontElement> = {}): FontElement {
  return {
    Type: 1,
    FontSize: size,
    FontName: 0,
    FontStyle: 0,
    Bold: false,
    TextAlignment: 0,
    Field: { ...(Name ? { Name } : {}), ...(Value ? { Value } : {}), NeedFeedLine: true, Fit: false },
    StartCoordinate: point(x, y),
    EndCoordinate: point(x + w, y + h),
    ...extra,
  }
}

function oneCell(width: number, height: number, elements: TemplateElement[]) {
  return { Height: height, Cells: [{ Width: width, Elements: elements }] }
}

export function createBlankSample(): BpTemplate {
  return { Version: 1, Width: 576, Height: 360, Rows: [oneCell(576, 360, [])] }
}

export function createProductSample(): BpTemplate {
  const width = 576
  const height = 360
  return {
    Version: 1,
    Width: width,
    Height: height,
    Rows: [oneCell(width, height, [
      text('Product.Name', undefined, 24, 20, 370, 50, 32, { Bold: true }),
      text(undefined, 'EAN / ITEM CODE', 24, 82, 250, 24, 16),
      {
        Type: 2,
        Width: 92,
        Height: 92,
        IsCompress: false,
        Field: { Name: 'Product.Image', NeedFeedLine: true, Fit: false },
        StartCoordinate: point(452, 22),
        EndCoordinate: point(544, 114),
      },
      {
        Type: 3,
        UnitWidth: 3,
        ErrorLevel: 'M',
        Width: 112,
        Height: 112,
        Field: { Name: 'Product.Code', NeedFeedLine: true, Fit: false },
        StartCoordinate: point(430, 220),
        EndCoordinate: point(542, 332),
      },
      {
        Type: 4,
        BarcodeType: 5,
        LineWidth: 2,
        Ratio: 2,
        Width: 350,
        Height: 56,
        Field: { Name: 'Product.Barcode', NeedFeedLine: true, Fit: false },
        StartCoordinate: point(24, 246),
        EndCoordinate: point(374, 302),
      },
      {
        Type: 5,
        LineWidth: 2,
        StartCoordinate: point(24, 210),
        EndCoordinate: point(378, 210),
        LineEndCoordinate: point(378, 210),
      },
      text('Product.Price', undefined, 24, 122, 260, 70, 48, { Bold: true }),
      text('Product.Unit', undefined, 292, 144, 78, 32, 16),
    ])],
  }
}

export function createReceiptSample(): BpTemplate {
  const width = 576
  return {
    Version: 1,
    Width: width,
    Height: 440,
    AutoHeight: true,
    Rows: [
      { IsHeader: true, Height: 52, Cells: [{ Width: width, Elements: [text(undefined, 'NORTH MARKET  ·  RECEIPT', 18, 12, 520, 30, 24, { Bold: true })] }] },
      {
        RepeatSource: 'Items',
        Height: 58,
        Condition: { Path: 'Quantity', Operator: 'gt', Value: 0 },
        Cells: [
          { Width: 370, Elements: [text('Name', undefined, 16, 10, 350, 32, 24)] },
          { Width: 206, Elements: [text('Amount', undefined, 8, 10, 190, 32, 24, { TextAlignment: 2 })] },
        ],
      },
      { Height: 46, Cells: [{ Width: width, Elements: [text('Subtotal', undefined, 18, 8, 540, 30, 24, { Bold: true, TextAlignment: 2 })] }] },
    ],
  }
}

export function createV2ReceiptSample(): BpTemplate {
  const width = 576
  return {
    Version: 2, Width: width, Height: 480, AutoHeight: true, Variables: { shop: 'Store.Name' },
    Rows: [
      { Section: 'PageHeader', Height: 32, Cells: [{ Width: width, Elements: [text('$var.shop', undefined, 12, 3, 552, 26, 24, { Bold: true })] }] },
      { Section: 'DocumentHeader', Height: 32, Cells: [{ Width: width, Elements: [text('OrderNo', undefined, 12, 4, 552, 24, 24)] }] },
      { Section: 'PageHeader', Height: 32, Cells: [
        { Width: 380, Border: { BottomStyle: 1 }, Elements: [text(undefined, '商品', 8, 3, 360, 26)] },
        { Width: 196, Border: { BottomStyle: 1 }, Elements: [text(undefined, '金额', 8, 3, 176, 26, 24, { TextAlignment: 2 })] },
      ] },
      { Section: 'Body', RepeatSource: 'Items', Height: 40, Cells: [
        { Width: 380, Elements: [text('Name', undefined, 8, 5, 360, 28)] },
        { Width: 196, Elements: [text('Amount', undefined, 8, 5, 176, 28, 24, { TextAlignment: 2 })] },
      ] },
      { Section: 'DocumentFooter', Height: 36, Cells: [{ Width: width, Border: { TopStyle: 1 }, Elements: [text('Total', undefined, 12, 5, 552, 26, 24, { Bold: true, TextAlignment: 2 })] }] },
      { Section: 'PageFooter', Height: 28, Cells: [{ Width: width, Elements: [text(undefined, '感谢惠顾', 12, 2, 552, 24, 16, { TextAlignment: 1 })] }] },
    ],
  }
}

export type SampleKey = 'blank' | 'product' | 'receipt' | 'receipt-v2'

export function makeSample(key: SampleKey): BpTemplate {
  if (key === 'blank') return createBlankSample()
  if (key === 'product') return createProductSample()
  if (key === 'receipt') return createReceiptSample()
  if (key === 'receipt-v2') return createV2ReceiptSample()
  return createReceiptSample()
}

export const sampleData = {
  product: {
    Product: { Name: '有机阳光玫瑰葡萄', Price: '¥ 32.80', Unit: '500g / 盒', Code: '6973120042806', Barcode: '6973120042806', Image: 'product/photo.jpg' },
  },
  receipt: {
    Items: [
      { Name: 'Hand-drip coffee', Quantity: 2, Amount: '2 × 18.00' },
      { Name: 'Butter croissant', Quantity: 1, Amount: '1 × 16.00' },
      { Name: 'Organic oranges', Quantity: 3, Amount: '3 × 12.50' },
    ],
    Subtotal: '小计                         ¥89.50',
  },
  receiptV2: {
    Store: { Name: 'NORTH MARKET' }, OrderNo: '订单 #20260928',
    Items: Array.from({ length: 15 }, (_, index) => ({ Name: `商品 ${index + 1}`, Amount: `${(index + 1) * 3}.00` })),
    Total: '合计 360.00',
  },
}
