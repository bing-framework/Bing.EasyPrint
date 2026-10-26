export function releasePreviewImages(images: Record<string, string> | undefined): void {
  for (const url of Object.values(images ?? {})) URL.revokeObjectURL(url)
}
