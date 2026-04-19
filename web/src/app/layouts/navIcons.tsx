import type { ReactNode } from 'react'
import type { NavIcon } from '@/shared/config/navigation'

type IconProps = { className?: string; active: boolean }

function iconWrap(className: string, children: ReactNode) {
  return (
    <span
      className={[
        'flex h-9 w-9 shrink-0 items-center justify-center rounded-lg transition-colors',
        className,
      ].join(' ')}
      aria-hidden
    >
      {children}
    </span>
  )
}

function PosIcon({ active }: IconProps) {
  return iconWrap(
    active ? 'bg-sky-600/15 text-sky-800' : 'bg-slate-100 text-slate-600 group-hover:bg-slate-200 group-hover:text-slate-800',
    <svg viewBox="0 0 24 24" fill="none" className="h-[1.125rem] w-[1.125rem]" stroke="currentColor" strokeWidth="1.75">
      <path d="M6 6h15l-1.5 9h-12L6 6Zm0 0L5 3H2" strokeLinecap="round" strokeLinejoin="round" />
      <circle cx="9" cy="20" r="1" fill="currentColor" stroke="none" />
      <circle cx="18" cy="20" r="1" fill="currentColor" stroke="none" />
    </svg>,
  )
}

function WarehouseIcon({ active }: IconProps) {
  return iconWrap(
    active ? 'bg-sky-600/15 text-sky-800' : 'bg-slate-100 text-slate-600 group-hover:bg-slate-200 group-hover:text-slate-800',
    <svg viewBox="0 0 24 24" fill="none" className="h-[1.125rem] w-[1.125rem]" stroke="currentColor" strokeWidth="1.75">
      <path d="M4 19V10l8-4 8 4v9" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M9 19v-5h6v5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>,
  )
}

function LayersIcon({ active }: IconProps) {
  return iconWrap(
    active ? 'bg-sky-600/15 text-sky-800' : 'bg-slate-100 text-slate-600 group-hover:bg-slate-200 group-hover:text-slate-800',
    <svg viewBox="0 0 24 24" fill="none" className="h-[1.125rem] w-[1.125rem]" stroke="currentColor" strokeWidth="1.75">
      <path d="M12 4 4 8l8 4 8-4-8-4Z" strokeLinejoin="round" />
      <path d="m4 12 8 4 8-4M4 16l8 4 8-4" strokeLinecap="round" strokeLinejoin="round" />
    </svg>,
  )
}

function InboxIcon({ active }: IconProps) {
  return iconWrap(
    active ? 'bg-sky-600/15 text-sky-800' : 'bg-slate-100 text-slate-600 group-hover:bg-slate-200 group-hover:text-slate-800',
    <svg viewBox="0 0 24 24" fill="none" className="h-[1.125rem] w-[1.125rem]" stroke="currentColor" strokeWidth="1.75">
      <path d="M4 13h4l2 3h4l2-3h4v6H4v-6Z" strokeLinejoin="round" />
      <path d="M4 13V8a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v5" strokeLinecap="round" />
    </svg>,
  )
}

function TagIcon({ active }: IconProps) {
  return iconWrap(
    active ? 'bg-sky-600/15 text-sky-800' : 'bg-slate-100 text-slate-600 group-hover:bg-slate-200 group-hover:text-slate-800',
    <svg viewBox="0 0 24 24" fill="none" className="h-[1.125rem] w-[1.125rem]" stroke="currentColor" strokeWidth="1.75">
      <path d="M3 5v6l10 10 7-7-10-10H6a3 3 0 0 0-3 3Z" strokeLinejoin="round" />
      <circle cx="7.5" cy="7.5" r="1" fill="currentColor" stroke="none" />
    </svg>,
  )
}

function TransferIcon({ active }: IconProps) {
  return iconWrap(
    active ? 'bg-sky-600/15 text-sky-800' : 'bg-slate-100 text-slate-600 group-hover:bg-slate-200 group-hover:text-slate-800',
    <svg viewBox="0 0 24 24" fill="none" className="h-[1.125rem] w-[1.125rem]" stroke="currentColor" strokeWidth="1.75">
      <path d="M7 7h13M7 7l3-3M7 7l3 3M17 17H4M17 17l-3 3M17 17l-3-3" strokeLinecap="round" strokeLinejoin="round" />
    </svg>,
  )
}

function ReceiptIcon({ active }: IconProps) {
  return iconWrap(
    active ? 'bg-sky-600/15 text-sky-800' : 'bg-slate-100 text-slate-600 group-hover:bg-slate-200 group-hover:text-slate-800',
    <svg viewBox="0 0 24 24" fill="none" className="h-[1.125rem] w-[1.125rem]" stroke="currentColor" strokeWidth="1.75">
      <path d="M7 4h10a1 1 0 0 1 1 1v14l-2-1-2 1-2-1-2 1-2-1-2 1V5a1 1 0 0 1 1-1Z" strokeLinejoin="round" />
      <path d="M9 9h6M9 13h4" strokeLinecap="round" />
    </svg>,
  )
}

function WalletIcon({ active }: IconProps) {
  return iconWrap(
    active ? 'bg-sky-600/15 text-sky-800' : 'bg-slate-100 text-slate-600 group-hover:bg-slate-200 group-hover:text-slate-800',
    <svg viewBox="0 0 24 24" fill="none" className="h-[1.125rem] w-[1.125rem]" stroke="currentColor" strokeWidth="1.75">
      <path d="M4 7a3 3 0 0 1 3-3h10a3 3 0 0 1 3 3v10a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V7Z" strokeLinejoin="round" />
      <path d="M4 10h16" strokeLinecap="round" />
      <circle cx="16" cy="14" r="1" fill="currentColor" stroke="none" />
    </svg>,
  )
}

function ChartIcon({ active }: IconProps) {
  return iconWrap(
    active ? 'bg-sky-600/15 text-sky-800' : 'bg-slate-100 text-slate-600 group-hover:bg-slate-200 group-hover:text-slate-800',
    <svg viewBox="0 0 24 24" fill="none" className="h-[1.125rem] w-[1.125rem]" stroke="currentColor" strokeWidth="1.75">
      <path d="M4 19V5M4 19h16M8 15v-4M12 15V9M16 15v-6" strokeLinecap="round" strokeLinejoin="round" />
    </svg>,
  )
}

function UsersIcon({ active }: IconProps) {
  return iconWrap(
    active ? 'bg-sky-600/15 text-sky-800' : 'bg-slate-100 text-slate-600 group-hover:bg-slate-200 group-hover:text-slate-800',
    <svg viewBox="0 0 24 24" fill="none" className="h-[1.125rem] w-[1.125rem]" stroke="currentColor" strokeWidth="1.75">
      <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" strokeLinecap="round" />
      <circle cx="9" cy="7" r="4" />
      <path d="M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75" strokeLinecap="round" />
    </svg>,
  )
}

function PlaceholderIcon({ active }: IconProps) {
  return iconWrap(
    active ? 'bg-sky-600/15 text-sky-800' : 'bg-slate-100 text-slate-600 group-hover:bg-slate-200 group-hover:text-slate-800',
    <svg viewBox="0 0 24 24" fill="none" className="h-[1.125rem] w-[1.125rem]" stroke="currentColor" strokeWidth="1.75">
      <circle cx="12" cy="12" r="3" />
      <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41" strokeLinecap="round" />
    </svg>,
  )
}

export function NavItemIcon({ icon, active }: { icon: NavIcon; active: boolean }) {
  const props: IconProps = { active }
  switch (icon) {
    case 'pos':
      return <PosIcon {...props} />
    case 'mainWarehouse':
      return <WarehouseIcon {...props} />
    case 'stockBalances':
      return <LayersIcon {...props} />
    case 'stockRequests':
      return <InboxIcon {...props} />
    case 'products':
      return <TagIcon {...props} />
    case 'transfers':
      return <TransferIcon {...props} />
    case 'purchases':
      return <ReceiptIcon {...props} />
    case 'reports':
      return <ChartIcon {...props} />
    case 'expenses':
      return <WalletIcon {...props} />
    case 'users':
      return <UsersIcon {...props} />
    default:
      return <PlaceholderIcon {...props} />
  }
}
