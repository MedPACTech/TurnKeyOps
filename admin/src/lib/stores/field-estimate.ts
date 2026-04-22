import { browser } from '$app/environment';
import { writable } from 'svelte/store';
import type {
  FieldEstimateContextDetails,
  FieldEstimateStartContext,
  MobileCurrentAppointmentContextDto
} from '$lib/api/types';

const STORAGE_KEY = 'turnkeyops_field_estimate_context';

function createFieldEstimateStore() {
  const initial = load();
  const { subscribe, set, update } = writable<FieldEstimateStartContext | null>(initial);

  function persist(value: FieldEstimateStartContext | null) {
    if (!browser) return;
    if (!value) {
      sessionStorage.removeItem(STORAGE_KEY);
      return;
    }

    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(value));
  }

  return {
    subscribe,
    clear() {
      persist(null);
      set(null);
    },
    beginFromAppointment(appointment: MobileCurrentAppointmentContextDto) {
      const next: FieldEstimateStartContext = {
        source: 'appointment',
        draftContextId: crypto.randomUUID(),
        createdAt: new Date().toISOString(),
        details: {
          appointmentId: appointment.appointmentId,
          estimateId: appointment.estimateId,
          estimateNumber: appointment.estimateNumber,
          customerName: appointment.customerName,
          customerCompany: appointment.customerCompany,
          projectAddress: appointment.projectAddress,
          appointmentDateTime: appointment.appointmentDateTime,
          estimatorName: appointment.estimatorName,
          projectName: appointment.projectName
        }
      };
      persist(next);
      set(next);
    },
    beginNewEstimate() {
      const next: FieldEstimateStartContext = {
        source: 'new',
        draftContextId: crypto.randomUUID(),
        createdAt: new Date().toISOString(),
        details: {
          customerName: '',
          customerCompany: '',
          projectAddress: '',
          estimatorName: '',
          projectName: ''
        }
      };
      persist(next);
      set(next);
    },
    setDetails(details: FieldEstimateContextDetails) {
      update((current) => {
        if (!current) return current;
        const next = { ...current, details };
        persist(next);
        return next;
      });
    },
    restore() {
      const next = load();
      set(next);
      return next;
    },
    update(updater: (value: FieldEstimateStartContext | null) => FieldEstimateStartContext | null) {
      update((current) => {
        const next = updater(current);
        persist(next);
        return next;
      });
    }
  };
}

function load(): FieldEstimateStartContext | null {
  if (!browser) return null;

  const raw = sessionStorage.getItem(STORAGE_KEY);
  if (!raw) return null;

  try {
    return JSON.parse(raw) as FieldEstimateStartContext;
  } catch {
    sessionStorage.removeItem(STORAGE_KEY);
    return null;
  }
}

export const fieldEstimate = createFieldEstimateStore();
