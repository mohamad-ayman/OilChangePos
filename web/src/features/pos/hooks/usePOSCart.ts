import { useCallback, useMemo, useReducer } from 'react'
import type { Product } from '@/entities/product'
import * as posEngine from '@/features/pos/engine/posEngine'
import type { POSCartState } from '@/features/pos/engine/posEngine'

type CartAction =
  | { type: 'ADD'; product: Product; qty?: number }
  | { type: 'REMOVE'; uid: string }
  | { type: 'SET_QTY'; uid: string; quantity: number }
  | { type: 'DISCOUNT_PCT'; percent: number }
  | { type: 'DISCOUNT_FIXED'; amount: number }
  | { type: 'CLEAR_DISCOUNT' }
  | { type: 'CLEAR_CART' }

function cartReducer(state: POSCartState, action: CartAction): POSCartState {
  switch (action.type) {
    case 'ADD':
      return posEngine.addItem(state, action.product, action.qty)
    case 'REMOVE':
      return posEngine.removeItem(state, action.uid)
    case 'SET_QTY':
      return posEngine.updateQuantity(state, action.uid, action.quantity)
    case 'DISCOUNT_PCT':
      return posEngine.applyDiscountPercent(state, action.percent)
    case 'DISCOUNT_FIXED':
      return posEngine.applyDiscountFixed(state, action.amount)
    case 'CLEAR_DISCOUNT':
      return posEngine.clearDiscount(state)
    case 'CLEAR_CART':
      return posEngine.emptyCart()
    default:
      return state
  }
}

/**
 * Single reducer for cart — one state object per transaction keeps updates predictable
 * and avoids prop-drilling multiple useStates.
 */
export function usePOSCart() {
  const [cart, dispatch] = useReducer(cartReducer, undefined, posEngine.emptyCart)

  const totals = useMemo(() => posEngine.calculateTotal(cart), [cart])

  const addProduct = useCallback((product: Product, qty?: number) => {
    dispatch({ type: 'ADD', product, qty })
  }, [])

  const removeLine = useCallback((uid: string) => {
    dispatch({ type: 'REMOVE', uid })
  }, [])

  const setLineQty = useCallback((uid: string, quantity: number) => {
    dispatch({ type: 'SET_QTY', uid, quantity })
  }, [])

  const setDiscountPercent = useCallback((percent: number) => {
    dispatch({ type: 'DISCOUNT_PCT', percent })
  }, [])

  const setDiscountFixed = useCallback((amount: number) => {
    dispatch({ type: 'DISCOUNT_FIXED', amount })
  }, [])

  const clearDiscount = useCallback(() => {
    dispatch({ type: 'CLEAR_DISCOUNT' })
  }, [])

  const clearCart = useCallback(() => {
    dispatch({ type: 'CLEAR_CART' })
  }, [])

  return {
    cart,
    totals,
    addProduct,
    removeLine,
    setLineQty,
    setDiscountPercent,
    setDiscountFixed,
    clearDiscount,
    clearCart,
  }
}
