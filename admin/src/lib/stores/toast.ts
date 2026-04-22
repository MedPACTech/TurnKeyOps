/**
 * Simple toast notification store.
 */
import { writable } from 'svelte/store';

export type ToastType = 'success' | 'error' | 'info' | 'warning';

interface Toast {
  id: string;
  type: ToastType;
  message: string;
}

function createToastStore() {
  const { subscribe, update } = writable<Toast[]>([]);

  function add(type: ToastType, message: string, duration = 4000) {
    const id = crypto.randomUUID();
    update(toasts => [...toasts, { id, type, message }]);
    setTimeout(() => remove(id), duration);
  }

  function remove(id: string) {
    update(toasts => toasts.filter(t => t.id !== id));
  }

  return {
    subscribe,
    success: (msg: string) => add('success', msg),
    error: (msg: string) => add('error', msg, 6000),
    info: (msg: string) => add('info', msg),
    warning: (msg: string) => add('warning', msg)
  };
}

export const toast = createToastStore();
