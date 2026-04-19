import type { AppAuthRole } from '@/shared/api/auth.api'



export type NavIcon = 'placeholder'



export type NavItem = {

  id: string

  /** i18n key under `nav.*` */

  labelKey: string

  path: string

  icon: NavIcon

  /** Roles that may see this item. `all` = any authenticated role. */

  allowedRoles: AppAuthRole[] | 'all'

}



export type NavSection = {

  id: string

  /** i18n key under `nav.section.*` */

  labelKey: string

  items: NavItem[]

}



/**

 * Sidebar grouped by module category (ERP-style).

 * Paths are absolute from site root; shell lives under `/app`.

 */

export const navigationSections: NavSection[] = [

  {

    id: 'overview',

    labelKey: 'nav.section.overview',

    items: [

      { id: 'dashboard', labelKey: 'nav.dashboard', path: '/app', icon: 'placeholder', allowedRoles: 'all' },

    ],

  },

  {

    id: 'sales',

    labelKey: 'nav.section.sales',

    items: [

      { id: 'pos', labelKey: 'nav.pos', path: '/app/pos', icon: 'placeholder', allowedRoles: ['admin', 'manager', 'cashier'] },

    ],

  },

  {

    id: 'stock',

    labelKey: 'nav.section.stock',

    items: [

      {
        id: 'main-warehouse',
        labelKey: 'nav.mainWarehouse',
        path: '/app/inventory',
        icon: 'placeholder',
        allowedRoles: ['admin'],
      },
      {
        id: 'stock-balances',
        labelKey: 'nav.stockBalances',
        path: '/app/stock-balances',
        icon: 'placeholder',
        allowedRoles: ['admin', 'manager'],
      },
      {
        id: 'stock-requests',
        labelKey: 'nav.stockRequests',
        path: '/app/stock-requests',
        icon: 'placeholder',
        allowedRoles: ['admin', 'manager', 'cashier'],
      },
      { id: 'products', labelKey: 'nav.products', path: '/app/products', icon: 'placeholder', allowedRoles: ['admin'] },

      { id: 'transfers', labelKey: 'nav.transfers', path: '/app/transfers', icon: 'placeholder', allowedRoles: ['admin'] },

      { id: 'purchases', labelKey: 'nav.purchases', path: '/app/purchases', icon: 'placeholder', allowedRoles: ['admin'] },

    ],

  },

  {

    id: 'insights',

    labelKey: 'nav.section.insights',

    items: [

      { id: 'reports', labelKey: 'nav.reports', path: '/app/reports', icon: 'placeholder', allowedRoles: ['admin', 'manager'] },

    ],

  },

  {

    id: 'admin',

    labelKey: 'nav.section.admin',

    items: [{ id: 'users', labelKey: 'nav.users', path: '/app/users', icon: 'placeholder', allowedRoles: ['admin'] }],

  },

]



export function getNavSectionsForRole(role: AppAuthRole): NavSection[] {

  return navigationSections

    .map((section) => ({

      ...section,

      items: section.items.filter((item) => {

        if (item.allowedRoles === 'all') return true

        return item.allowedRoles.includes(role)

      }),

    }))

    .filter((s) => s.items.length > 0)

}



/** Flat list (same order as sidebar) — for tests or non-UI use. */

export function getNavItemsForRole(role: AppAuthRole): NavItem[] {

  return getNavSectionsForRole(role).flatMap((s) => s.items)

}


