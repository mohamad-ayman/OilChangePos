/**
 * POS/catalog style: `شركة — اسم الصنف` (e.g. Mobil — 5w30).
 * Mirrors server `ProductDisplayNames.CatalogLine` in `OilChangePOS.Business/Services.cs`.
 * Omits the company segment when missing or whitespace.
 */
export function catalogLine(
  companyName: string | null | undefined,
  productName: string | null | undefined,
): string {
  const pn = (productName ?? '').trim()
  if (pn.length === 0) return '—'
  const cn = companyName?.trim()
  if (!cn) return pn
  return `${cn} — ${pn}`
}

export type CatalogProductParts = {
  companyName?: string | null
  /** Product / inventory name (not the composed label). */
  name?: string | null
  /** e.g. 4L, 5L — appended when non-empty. */
  packageSize?: string | null
}

/**
 * Canonical shelf label: `شركة — صنف — تعبئة` when package is set (e.g. Mobil — 5W30 — 4L).
 * Matches server `ProductDisplayNames.CatalogDisplayName`.
 */
export function catalogDisplayName(parts: CatalogProductParts): string {
  const base = catalogLine(parts.companyName, parts.name)
  const pack = parts.packageSize?.trim()
  if (!pack) return base
  if (base === '—') return pack
  return `${base} — ${pack}`
}
