import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { isAxiosError } from 'axios'
import {
  listCatalogCompanies,
  listCatalogProducts,
  saveCatalogCompany,
  saveCatalogProduct,
} from '@/shared/api/catalogAdmin.api'
import type { CatalogCompanyListRow, CatalogProductListRow } from '@/shared/api/catalogAdmin.api'
import { catalogKeys } from '@/features/catalog/services/catalogQueryKeys'
import { inventoryKeys } from '@/features/inventory/services/inventoryQueryKeys'
import { posKeys } from '@/features/pos/services/posQueryKeys'
import { useAuthStore } from '@/shared/store/auth.store'
import { t } from '@/i18n'
import { PRODUCT_PACKAGE_SIZES, type ProductPackageSize } from '@/shared/config/productCatalogOptions'

const PRODUCT_CATEGORIES = ['Oil', 'Filter', 'Grease', 'Other'] as const

type ProductDraft = {
  name: string
  category: (typeof PRODUCT_CATEGORIES)[number]
  package: ProductPackageSize
  isActive: boolean
}

function catLabel(c: string): string {
  const key = `pos.cat.${c}` as const
  const v = t(key)
  return v === key ? c : v
}

function packLabel(p: string): string {
  const key = `catalog.pack.${p}` as const
  const v = t(key)
  return v === key ? p : v
}

type ChipGroupProps<T extends string> = {
  label: string
  value: T
  options: readonly T[]
  format: (opt: T) => string
  onChange: (v: T) => void
  disabled?: boolean
}

function ChipGroup<T extends string>({ label, value, options, format, onChange, disabled }: ChipGroupProps<T>) {
  return (
    <div className="space-y-2">
      <p className="text-xs font-semibold text-slate-600">{label}</p>
      <div className="flex flex-wrap gap-2">
        {options.map((opt) => (
          <button
            key={opt}
            type="button"
            disabled={disabled}
            onClick={() => onChange(opt)}
            className={[
              'rounded-full border px-3 py-1.5 text-sm font-medium transition',
              opt === value
                ? 'border-sky-600 bg-sky-600 text-white shadow-sm'
                : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300 hover:bg-slate-50',
            ].join(' ')}
          >
            {format(opt)}
          </button>
        ))}
      </div>
    </div>
  )
}

export function CatalogPage() {
  const qc = useQueryClient()
  const user = useAuthStore((s) => s.user)
  const canEdit = user?.role === 'admin'

  const [companySearch, setCompanySearch] = useState('')
  const [selectedCompanyId, setSelectedCompanyId] = useState<number | null>(null)
  const [companyDraft, setCompanyDraft] = useState({ name: '', isActive: true })

  /** `null` = follow list / auto-pick first; `new` = empty form for create; `number` = selected row */
  const [selectedProductId, setSelectedProductId] = useState<number | null | 'new'>(null)
  const [productDraft, setProductDraft] = useState<ProductDraft>({
    name: '',
    category: 'Oil',
    package: PRODUCT_PACKAGE_SIZES[0],
    isActive: true,
  })

  const companiesQ = useQuery({
    queryKey: catalogKeys.companies(),
    queryFn: listCatalogCompanies,
  })

  const companies = companiesQ.data ?? []

  const filteredCompanies = useMemo(() => {
    const q = companySearch.trim().toLowerCase()
    if (!q) return companies
    return companies.filter((c) => c.name.toLowerCase().includes(q))
  }, [companies, companySearch])

  const productsQ = useQuery({
    queryKey: catalogKeys.products(selectedCompanyId ?? 0),
    queryFn: () => listCatalogProducts(selectedCompanyId!),
    enabled: selectedCompanyId != null && selectedCompanyId > 0,
  })

  const products = productsQ.data ?? []

  const syncCompanyFromRow = useCallback((row: CatalogCompanyListRow) => {
    setSelectedCompanyId(row.id)
    setCompanyDraft({ name: row.name, isActive: row.isActive })
    setSelectedProductId(null)
    setProductDraft({ name: '', category: 'Oil', package: PRODUCT_PACKAGE_SIZES[0], isActive: true })
  }, [])

  const syncProductFromRow = useCallback((row: CatalogProductListRow) => {
    setSelectedProductId(row.id)
    const cat = (PRODUCT_CATEGORIES as readonly string[]).includes(row.productCategory)
      ? (row.productCategory as (typeof PRODUCT_CATEGORIES)[number])
      : 'Other'
    const pack = (PRODUCT_PACKAGE_SIZES as readonly string[]).includes(row.packageSize)
      ? (row.packageSize as (typeof PRODUCT_PACKAGE_SIZES)[number])
      : 'Unit'
    setProductDraft({
      name: row.name,
      category: cat,
      package: pack,
      isActive: row.isActive,
    })
  }, [])

  const didPickInitialCompany = useRef(false)
  useEffect(() => {
    if (companies.length === 0) {
      didPickInitialCompany.current = false
      setSelectedCompanyId(null)
      setCompanyDraft({ name: '', isActive: true })
      return
    }
    if (!didPickInitialCompany.current) {
      didPickInitialCompany.current = true
      syncCompanyFromRow(companies[0])
      return
    }
    if (selectedCompanyId != null && !companies.some((c) => c.id === selectedCompanyId)) {
      syncCompanyFromRow(companies[0])
    }
  }, [companies, selectedCompanyId, syncCompanyFromRow])

  const productListKey = products.map((p) => p.id).join(',')
  useEffect(() => {
    if (selectedCompanyId == null || productsQ.isPending) return
    if (products.length === 0) {
      setSelectedProductId(null)
      setProductDraft({ name: '', category: 'Oil', package: PRODUCT_PACKAGE_SIZES[0], isActive: true })
      return
    }
    if (selectedProductId === 'new') return
    if (typeof selectedProductId === 'number' && products.some((p) => p.id === selectedProductId)) return
    syncProductFromRow(products[0])
  }, [selectedCompanyId, productListKey, products, productsQ.isPending, selectedProductId, syncProductFromRow])

  const invalidateRelated = useCallback(async () => {
    await qc.invalidateQueries({ queryKey: catalogKeys.root })
    await qc.invalidateQueries({ queryKey: inventoryKeys.products() })
    await qc.invalidateQueries({ queryKey: posKeys.root })
  }, [qc])

  const saveCompanyMut = useMutation({
    mutationFn: saveCatalogCompany,
    onSuccess: () => void invalidateRelated(),
  })

  const saveProductMut = useMutation({
    mutationFn: saveCatalogProduct,
    onSuccess: () => void invalidateRelated(),
  })

  const onSaveCompany = (createNew: boolean) => {
    if (!canEdit) return
    const name = companyDraft.name.trim()
    if (!name) {
      window.alert(t('catalog.companyNameRequired'))
      return
    }
    saveCompanyMut.mutate(
      {
        createNew,
        existingCompanyId: createNew ? null : selectedCompanyId,
        name,
        isActive: companyDraft.isActive,
      },
      {
        onSuccess: () => window.alert(createNew ? t('catalog.companyCreated') : t('catalog.companySaved')),
        onError: (e) => window.alert(isAxiosError(e) ? String(e.response?.data ?? e.message) : String(e)),
      },
    )
  }

  const onSaveProduct = (createNew: boolean) => {
    if (!canEdit) return
    if (selectedCompanyId == null) {
      window.alert(t('catalog.selectCompanyFirst'))
      return
    }
    const name = productDraft.name.trim()
    if (!name) {
      window.alert(t('catalog.productNameRequired'))
      return
    }
    saveProductMut.mutate(
      {
        createNew,
        companyId: selectedCompanyId,
        existingProductId: createNew || selectedProductId === 'new' ? null : selectedProductId,
        name,
        category: productDraft.category,
        package: productDraft.package,
        isActive: productDraft.isActive,
      },
      {
        onSuccess: (_, body) => {
          if (body.createNew) setSelectedProductId(null)
          window.alert(body.createNew ? t('catalog.productCreated') : t('catalog.productSaved'))
        },
        onError: (e) => window.alert(isAxiosError(e) ? String(e.response?.data ?? e.message) : String(e)),
      },
    )
  }

  const clearCompany = () => {
    setSelectedCompanyId(null)
    setCompanyDraft({ name: '', isActive: true })
    setSelectedProductId(null)
    setProductDraft({ name: '', category: 'Oil', package: PRODUCT_PACKAGE_SIZES[0], isActive: true })
  }

  const clearProduct = () => {
    setSelectedProductId('new')
    setProductDraft({ name: '', category: 'Oil', package: PRODUCT_PACKAGE_SIZES[0], isActive: true })
  }

  const busy = saveCompanyMut.isPending || saveProductMut.isPending

  return (
    <div className="space-y-6 rounded-2xl border border-slate-200/90 bg-gradient-to-b from-slate-50/80 to-white p-4 shadow-sm shadow-slate-900/[0.04] ring-1 ring-slate-900/[0.02] sm:p-6">
      <header className="mb-6 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="border-r-4 border-sky-600 pe-4">
          <h1 className="text-xl font-bold text-slate-900">{t('catalog.pageTitle')}</h1>
          <p className="mt-2 max-w-3xl text-sm leading-relaxed text-slate-600">{t('catalog.pageSubtitle')}</p>
        </div>
        {!canEdit ? (
          <p className="mt-4 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900">{t('catalog.adminOnly')}</p>
        ) : null}
      </header>

      <div className="grid gap-6 lg:grid-cols-2">
        {/* Companies — on the reading start side in RTL (first column) */}
        <section className="flex min-h-[28rem] flex-col rounded-2xl border border-slate-200 bg-white shadow-sm">
          <div className="border-b border-sky-600 bg-gradient-to-l from-sky-50/80 to-white px-4 py-3">
            <h2 className="text-lg font-bold text-slate-900">{t('catalog.companiesSection')}</h2>
            <p className="text-xs text-slate-600">{t('catalog.companiesHint')}</p>
          </div>
          <div className="space-y-3 border-b border-slate-100 bg-slate-50/80 p-4">
            <input
              value={companySearch}
              onChange={(e) => setCompanySearch(e.target.value)}
              placeholder={t('catalog.searchCompanies')}
              className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm text-slate-900 shadow-inner outline-none ring-sky-500/30 focus:ring-2"
            />
            <div className="grid gap-3 sm:grid-cols-[1fr_auto] sm:items-end">
              <label className="block text-sm font-medium text-slate-700">
                {t('catalog.companyName')}
                <input
                  value={companyDraft.name}
                  disabled={!canEdit || busy}
                  onChange={(e) => setCompanyDraft((d) => ({ ...d, name: e.target.value }))}
                  className="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm text-slate-900 outline-none focus:border-sky-500 disabled:bg-slate-100"
                />
              </label>
              <label className="flex cursor-pointer items-center gap-2 text-sm text-slate-700 sm:pb-2">
                <input
                  type="checkbox"
                  checked={companyDraft.isActive}
                  disabled={!canEdit || busy}
                  onChange={(e) => setCompanyDraft((d) => ({ ...d, isActive: e.target.checked }))}
                  className="size-4 rounded border-slate-300"
                />
                {t('catalog.active')}
              </label>
            </div>
            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                disabled={!canEdit || busy}
                onClick={() => onSaveCompany(true)}
                className="rounded-xl bg-emerald-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-emerald-700 disabled:opacity-40"
              >
                {t('catalog.addCompany')}
              </button>
              <button
                type="button"
                disabled={!canEdit || busy || selectedCompanyId == null}
                onClick={() => onSaveCompany(false)}
                className="rounded-xl bg-amber-500 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-amber-600 disabled:opacity-40"
              >
                {t('catalog.saveCompany')}
              </button>
              <button
                type="button"
                disabled={!canEdit || busy}
                onClick={clearCompany}
                className="rounded-xl border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-40"
              >
                {t('catalog.newCompany')}
              </button>
              <button
                type="button"
                disabled={busy}
                onClick={() => void companiesQ.refetch()}
                className="rounded-xl border border-sky-200 bg-sky-50 px-4 py-2 text-sm font-semibold text-sky-800 hover:bg-sky-100 disabled:opacity-40"
              >
                {t('catalog.refresh')}
              </button>
            </div>
          </div>
          <div className="min-h-0 flex-1 overflow-auto p-2">
            {companiesQ.isPending ? (
              <p className="p-4 text-center text-sm text-slate-500">{t('common.loading')}</p>
            ) : (
              <table className="w-full border-collapse text-sm">
                <thead className="sticky top-0 z-10 bg-white text-xs uppercase text-slate-500 shadow-sm">
                  <tr>
                    <th className="px-3 py-2 text-start font-semibold">{t('catalog.col.company')}</th>
                    <th className="px-2 py-2 text-center font-semibold">{t('catalog.col.active')}</th>
                    <th className="px-2 py-2 text-end font-semibold">{t('catalog.col.count')}</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredCompanies.map((c) => (
                    <tr
                      key={c.id}
                      onClick={() => syncCompanyFromRow(c)}
                      className={[
                        'cursor-pointer border-b border-slate-100 transition',
                        c.id === selectedCompanyId ? 'bg-sky-50' : 'hover:bg-slate-50',
                      ].join(' ')}
                    >
                      <td className="px-3 py-2.5 font-medium text-slate-900">{c.name}</td>
                      <td className="px-2 py-2.5 text-center">{c.isActive ? '✓' : '—'}</td>
                      <td className="px-2 py-2.5 text-end tabular-nums text-slate-600">{c.productCount}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </section>

        {/* Products for selected company */}
        <section className="flex min-h-[28rem] flex-col rounded-2xl border border-slate-200 bg-white shadow-sm">
          <div className="border-b border-emerald-600 bg-gradient-to-l from-emerald-50/80 to-white px-4 py-3">
            <h2 className="text-lg font-bold text-slate-900">{t('catalog.productsSection')}</h2>
            <p className="text-xs text-slate-600">
              {selectedCompanyId != null
                ? t('catalog.productsForCompany').replace('{name}', companyDraft.name || '—')
                : t('catalog.pickCompany')}
            </p>
          </div>
          <div className="space-y-4 border-b border-slate-100 bg-slate-50/80 p-4">
            <label className="block text-sm font-medium text-slate-700">
              {t('catalog.productName')}
              <input
                value={productDraft.name}
                disabled={!canEdit || busy || selectedCompanyId == null}
                onChange={(e) => setProductDraft((d) => ({ ...d, name: e.target.value }))}
                className="mt-1 w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm text-slate-900 outline-none focus:border-emerald-500 disabled:bg-slate-100"
              />
            </label>
            <ChipGroup
              label={t('catalog.productType')}
              value={productDraft.category}
              options={PRODUCT_CATEGORIES}
              format={(o) => catLabel(o)}
              onChange={(v) => setProductDraft((d) => ({ ...d, category: v }))}
              disabled={!canEdit || busy || selectedCompanyId == null}
            />
            <ChipGroup
              label={t('catalog.package')}
              value={productDraft.package}
              options={PRODUCT_PACKAGE_SIZES}
              format={(o) => packLabel(o)}
              onChange={(v) => setProductDraft((d) => ({ ...d, package: v }))}
              disabled={!canEdit || busy || selectedCompanyId == null}
            />
            <label className="flex cursor-pointer items-center gap-2 text-sm text-slate-700">
              <input
                type="checkbox"
                checked={productDraft.isActive}
                disabled={!canEdit || busy || selectedCompanyId == null}
                onChange={(e) => setProductDraft((d) => ({ ...d, isActive: e.target.checked }))}
                className="size-4 rounded border-slate-300"
              />
              {t('catalog.active')}
            </label>
            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                disabled={!canEdit || busy || selectedCompanyId == null}
                onClick={() => onSaveProduct(true)}
                className="rounded-xl bg-emerald-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-emerald-700 disabled:opacity-40"
              >
                {t('catalog.addProduct')}
              </button>
              <button
                type="button"
                disabled={!canEdit || busy || selectedCompanyId == null || selectedProductId === 'new' || selectedProductId === null}
                onClick={() => onSaveProduct(false)}
                className="rounded-xl bg-amber-500 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-amber-600 disabled:opacity-40"
              >
                {t('catalog.saveProduct')}
              </button>
              <button
                type="button"
                disabled={!canEdit || busy || selectedCompanyId == null}
                onClick={clearProduct}
                className="rounded-xl border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-40"
              >
                {t('catalog.newProduct')}
              </button>
            </div>
          </div>
          <div className="min-h-0 flex-1 overflow-auto p-2">
            {selectedCompanyId == null ? (
              <p className="p-4 text-center text-sm text-slate-500">{t('catalog.pickCompany')}</p>
            ) : productsQ.isPending ? (
              <p className="p-4 text-center text-sm text-slate-500">{t('common.loading')}</p>
            ) : (
              <table className="w-full border-collapse text-sm">
                <thead className="sticky top-0 z-10 bg-white text-xs uppercase text-slate-500 shadow-sm">
                  <tr>
                    <th className="px-3 py-2 text-start font-semibold">{t('catalog.col.product')}</th>
                    <th className="px-2 py-2 text-start font-semibold">{t('catalog.col.type')}</th>
                    <th className="px-2 py-2 text-start font-semibold">{t('catalog.col.package')}</th>
                    <th className="px-2 py-2 text-center font-semibold">{t('catalog.col.active')}</th>
                  </tr>
                </thead>
                <tbody>
                  {products.map((p) => (
                    <tr
                      key={p.id}
                      onClick={() => syncProductFromRow(p)}
                      className={[
                        'cursor-pointer border-b border-slate-100 transition',
                        typeof selectedProductId === 'number' && p.id === selectedProductId ? 'bg-emerald-50' : 'hover:bg-slate-50',
                      ].join(' ')}
                    >
                      <td className="px-3 py-2.5 font-medium text-slate-900">{p.name}</td>
                      <td className="px-2 py-2.5 text-slate-700">{catLabel(p.productCategory)}</td>
                      <td className="px-2 py-2.5 text-slate-700">{packLabel(p.packageSize)}</td>
                      <td className="px-2 py-2.5 text-center">{p.isActive ? '✓' : '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </section>
      </div>

      <aside className="mt-6 rounded-2xl border border-slate-200 bg-white p-4 text-sm leading-relaxed text-slate-600 shadow-sm">
        <p className="font-semibold text-slate-800">{t('catalog.uxTitle')}</p>
        <p className="mt-2">{t('catalog.uxBody')}</p>
      </aside>
    </div>
  )
}
