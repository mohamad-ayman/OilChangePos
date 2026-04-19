/**
 * Allowed **package / العبوة** codes for catalog & POS product creation UIs.
 * Stored in `Products.PackageSize` (SQL `nvarchar(20)` — keep each code ≤ 20 chars).
 *
 * To add a size:
 * 1. Append a short code here (e.g. `'12L'`, `'500ml'`).
 * 2. Add `catalog.pack.<code>` in `src/i18n/messages.ts` for Arabic label (optional; UI falls back to the code).
 * 3. If you add codes, align any server-side validation / seed data that references package sizes.
 */
/** Liter sizes ascending (1L → 20L), then non-volume `Unit`. */
export const PRODUCT_PACKAGE_SIZES = ['1L', '4L', '5L', '16L', '20L', 'Unit'] as const

export type ProductPackageSize = (typeof PRODUCT_PACKAGE_SIZES)[number]
