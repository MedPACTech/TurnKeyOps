import { api } from '$api/client';
import type {
  CreateEstimateFromAppointmentRequestDto,
  EstimateCalculationSnapshotDto,
  EstimateDto,
  JobDto,
  StructuredEstimateInputDto,
  UpdateEstimateStructuredRequestDto
} from '$api/types';

export const estimateWorkflowApi = {
  createDraft(payload: CreateEstimateFromAppointmentRequestDto) {
    return api.post<EstimateDto>('/estimates/from-appointment', payload);
  },
  get(id: string) {
    return api.get<EstimateDto>(`/estimates/${id}`);
  },
  update(id: string, payload: UpdateEstimateStructuredRequestDto) {
    return api.put<EstimateDto>(`/estimates/${id}`, payload);
  },
  calculate(payload: StructuredEstimateInputDto) {
    return api.post<EstimateCalculationSnapshotDto>('/estimates/calculate', payload);
  },
  submit(id: string) {
    return api.post<EstimateDto>(`/estimates/${id}/submit`);
  },
  startReview(id: string) {
    return api.post<EstimateDto>(`/estimates/${id}/under-review`);
  },
  award(id: string) {
    return api.post<EstimateDto>(`/estimates/${id}/award`);
  },
  reject(id: string) {
    return api.post<EstimateDto>(`/estimates/${id}/reject`);
  },
  revise(id: string) {
    return api.post<EstimateDto>(`/estimates/${id}/revise`);
  },
  convertToJob(id: string) {
    return api.post<JobDto>(`/estimates/${id}/convert-to-job`);
  }
};
