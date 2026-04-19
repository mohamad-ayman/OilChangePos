export const adminUserKeys = {
  all: ['admin', 'users'] as const,
  list: () => [...adminUserKeys.all, 'list'] as const,
}

export const adminBranchKeys = {
  all: ['admin', 'branches'] as const,
  list: () => [...adminBranchKeys.all, 'list'] as const,
}
