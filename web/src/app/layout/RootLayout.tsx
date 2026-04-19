import { Outlet } from 'react-router-dom'

export function RootLayout() {
  return (
    <div className="min-h-screen bg-slate-50 text-slate-900 antialiased">
      <Outlet />
    </div>
  )
}
