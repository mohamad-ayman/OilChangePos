import { createBrowserRouter, Navigate, RouterProvider } from 'react-router-dom'
import { AuthHydrationBoundary } from '@/features/auth/components/AuthHydrationBoundary'
import {
  AppShell,
  ShellDashboardPage,
  ShellModulePlaceholder,
} from '@/app/layouts/AppShell'
import { InventoryPage } from '@/features/inventory'
import { MainWarehousePage } from '@/features/main-warehouse'
import { POSPage } from '@/features/pos'
import { ReportsDashboardPage } from '@/features/reports'
import {
  CreateTransferWizard,
  DirectTransferPage,
  TransferDetailsPage,
  TransfersLayout,
  TransfersPage,
} from '@/features/transfers'
import { ProtectedRoute } from '@/app/router/ProtectedRoute'
import { RequireRole } from '@/app/router/RequireRole'
import { LoginPage } from '@/pages/LoginPage'
import { NotFoundPage } from '@/pages/NotFoundPage'
import { CatalogPage } from '@/features/catalog/pages/CatalogPage'
import { AdminUsersPage } from '@/features/admin/pages/AdminUsersPage'

const router = createBrowserRouter([
  {
    element: <AuthHydrationBoundary />,
    children: [
      { path: '/login', element: <LoginPage /> },
      {
        element: <ProtectedRoute />,
        children: [
          { path: '/', element: <Navigate to="/app" replace /> },
          {
            path: '/app',
            element: <AppShell />,
            children: [
              { index: true, element: <ShellDashboardPage /> },
              { path: 'pos', element: <POSPage /> },
              { path: 'stock-balances', element: <InventoryPage /> },
              { path: 'reports', element: <ReportsDashboardPage /> },
              {
                element: <RequireRole allowedRoles={['admin']} />,
                children: [
                  { path: 'inventory', element: <MainWarehousePage /> },
                  { path: 'products', element: <CatalogPage /> },
                  {
                    path: 'transfers',
                    element: <TransfersLayout />,
                    children: [
                      { path: 'requests', element: <TransfersPage /> },
                      { path: 'workflow/new', element: <CreateTransferWizard /> },
                      { path: 'workflow/:transferId', element: <TransferDetailsPage /> },
                      { index: true, element: <DirectTransferPage /> },
                    ],
                  },
                  { path: 'purchases', element: <ShellModulePlaceholder moduleId="purchases" /> },
                  { path: 'users', element: <AdminUsersPage /> },
                ],
              },
              { path: '*', element: <NotFoundPage /> },
            ],
          },
          { path: '*', element: <NotFoundPage /> },
        ],
      },
    ],
  },
])

export function AppRouter() {
  return <RouterProvider router={router} />
}
