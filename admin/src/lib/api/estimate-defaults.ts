import { api } from '$api/client';
import type { EstimateDefaultsDto } from '$api/types';

export const estimateDefaultsApi = {
  get() {
    return api.get<EstimateDefaultsDto>('/admin/estimate-defaults');
  },
  update(payload: EstimateDefaultsDto) {
    return api.put<EstimateDefaultsDto>('/admin/estimate-defaults', payload);
  }
};
