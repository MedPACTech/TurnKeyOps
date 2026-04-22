import { api } from '$lib/api/client';
import type { MobileCurrentAppointmentContextDto } from '$lib/api/types';

export const mobileApi = {
  getCurrentAppointment() {
    return api.get<MobileCurrentAppointmentContextDto | null>('/mobile/appointments/current');
  }
};
