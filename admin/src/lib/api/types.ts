/** TurnKeyOps shared TypeScript types (mirroring C# DTOs) */

export type TradeType = 'Concrete' | 'Framing' | 'General';
export type JobStatus =
  | 'Created'
  | 'Scheduled'
  | 'InProgress'
  | 'OnHold'
  | 'Completed'
  | 'Cancelled'
  | 'Closed'
  | 'Lead'
  | 'Estimated'
  | 'Invoiced'
  | 'Paid';
export type EstimateStatus =
  | 'Draft'
  | 'Submitted'
  | 'UnderReview'
  | 'Revised'
  | 'Awarded'
  | 'Rejected'
  | 'Expired'
  | 'ConvertedToJob'
  | 'Sent'
  | 'Accepted'
  | 'Declined';
export type InvoiceStatus = 'Draft' | 'Sent' | 'Paid' | 'Overdue' | 'Void';
export type CalendarEventType = 'Job' | 'Estimate' | 'Meeting' | 'Reminder' | 'Inspection' | 'Delivery' | 'Other';

export interface CustomerDto {
  id: string;
  firstName: string;
  lastName: string;
  companyName?: string;
  email?: string;
  phone?: string;
  address?: string;
  city?: string;
  state?: string;
  zip?: string;
  notes?: string;
  dateCreated?: string;
  dateUpdated?: string;
}

export interface JobSiteDto {
  id: string;
  name: string;
  address?: string;
  city?: string;
  state?: string;
  zip?: string;
  latitude?: number;
  longitude?: number;
  notes?: string;
  dateCreated?: string;
  dateUpdated?: string;
}

export interface JobDto {
  id: string;
  name: string;
  description?: string;
  tradeType: TradeType;
  status: JobStatus;
  customerId: string;
  customerName?: string;
  jobSiteId?: string;
  jobSiteName?: string;
  estimateId?: string;
  estimateNumber?: string;
  invoiceId?: string;
  projectAddress?: string;
  projectName?: string;
  estimateSnapshot?: EstimateCalculationSnapshotDto;
  scheduledStart?: string;
  scheduledEnd?: string;
  estimatedTotal: number;
  invoicedTotal: number;
  paidTotal: number;
  notes?: string;
  dateCreated?: string;
  dateUpdated?: string;
}

export interface CalendarEventDto {
  id: string;
  title: string;
  description?: string;
  eventType: CalendarEventType;
  startUtc: string;
  endUtc: string;
  allDay: boolean;
  jobId?: string;
  jobName?: string;
  jobSiteId?: string;
  jobSiteName?: string;
  color?: string;
  weather?: WeatherForecastDto;
  dateCreated?: string;
  dateUpdated?: string;
}

export interface WeatherForecastDto {
  summary?: string;
  tempHigh?: number;
  tempLow?: number;
  precipChance?: number;
  windSpeed?: string;
  windDirection?: string;
  icon?: string;
  forecastDate?: string;
}

export interface EstimateDto {
  id: string;
  estimateNumber?: string;
  status: EstimateStatus;
  tradeType: TradeType;
  appointmentId?: string;
  customerId: string;
  customerName?: string;
  customerCompany?: string;
  jobId?: string;
  jobName?: string;
  jobSiteId?: string;
  convertedJobId?: string;
  projectAddress?: string;
  estimatorName?: string;
  projectName?: string;
  subtotal: number;
  taxRate: number;
  taxAmount: number;
  total: number;
  totalSqft?: number;
  depthInches?: number;
  cubicYards?: number;
  numberOfPours?: number;
  wallLinearFeet?: number;
  studCount?: number;
  sentDate?: string;
  expiryDate?: string;
  signatureDataUrl?: string;
  signedByName?: string;
  signedDate?: string;
  notes?: string;
  lineItems: EstimateLineItemDto[];
  submittedDate?: string;
  revisedDate?: string;
  awardedDate?: string;
  rejectedDate?: string;
  convertedToJobDate?: string;
  dateCreated?: string;
  dateUpdated?: string;
  structuredInput?: StructuredEstimateInputDto;
  calculationSnapshot?: EstimateCalculationSnapshotDto;
}

export interface EstimateLineItemDto {
  id: string;
  estimateId: string;
  sortOrder: number;
  description: string;
  category?: string;
  quantity: number;
  unit?: string;
  unitPrice: number;
  lineTotal: number;
  isCalculated: boolean;
  notes?: string;
}

export interface InvoiceDto {
  id: string;
  invoiceNumber?: string;
  status: InvoiceStatus;
  customerId: string;
  customerName?: string;
  jobId?: string;
  jobName?: string;
  estimateId?: string;
  subtotal: number;
  taxRate: number;
  taxAmount: number;
  total: number;
  amountPaid: number;
  balanceDue: number;
  issueDate: string;
  dueDate: string;
  paidDate?: string;
  stripePaymentUrl?: string;
  notes?: string;
  dateCreated?: string;
  dateUpdated?: string;
}

export interface ChatDto {
  id: string;
  title: string;
  messageCount: number;
  dateCreated?: string;
  dateUpdated?: string;
}

export interface ChatMessageDto {
  id: string;
  chatId: string;
  role: string;
  content: string;
  dateCreated?: string;
}

export interface DashboardDto {
  activeJobs: number;
  pendingEstimates: number;
  overdueInvoices: number;
  revenueThisMonth: number;
  outstandingBalance: number;
  upcomingEvents: CalendarEventDto[];
}

export interface ConcreteCalculatorRequest {
  lengthFeet: number;
  widthFeet: number;
  depthInches: number;
  wastePercent: number;
  numberOfPours: number;
  readyMixPricePerCy?: number;
  laborPricePerSqft?: number;
  rebarPricePerSqft?: number;
}

export interface ConcreteCalculatorResult {
  sqft: number;
  depthInches: number;
  cubicYards: number;
  cubicYardsPerPour: number;
  rebarLinearFeet: number;
  formBoardLinearFeet: number;
  estimatedMaterialCost: number;
  estimatedLaborCost: number;
  estimatedTotal: number;
  numberOfPours: number;
}

export interface MobileCurrentAppointmentContextDto {
  appointmentId: string;
  customerName: string;
  customerCompany?: string;
  projectAddress: string;
  appointmentDateTime: string;
  estimatorName: string;
  estimateId?: string;
  estimateNumber?: string;
  projectName?: string;
}

export interface FieldEstimateContextDetails {
  appointmentId?: string;
  estimateId?: string;
  estimateNumber?: string;
  customerName: string;
  customerCompany?: string;
  projectAddress: string;
  appointmentDateTime?: string;
  estimatorName: string;
  projectName?: string;
}

export interface FieldEstimateStartContext {
  source: 'appointment' | 'new';
  draftContextId: string;
  createdAt: string;
  details: FieldEstimateContextDetails;
}

export interface EstimateDefaultsDto {
  concreteCostPerYard: number;
  minimumLoadFee: number;
  shortLoadFee: number;
  deliveryFee: number;
  fuelSurcharge: number;
  defaultPumpFee: number;
  additiveCost: number;
  fiberMeshCost: number;
  colorCost: number;
  sealerCost: number;

  demoCostRate: number;
  excavationCostRate: number;
  haulOffFee: number;
  baseMaterialUnitCost: number;
  compactionCost: number;
  vaporBarrierCost: number;
  gradingCost: number;
  accessDifficultyEasyPercent: number;
  accessDifficultyModeratePercent: number;
  accessDifficultyHardPercent: number;

  rebarCostPerFoot: number;
  meshCost: number;
  chairsCost: number;
  dowelsCost: number;
  anchorBoltsCost: number;

  formMaterialCost: number;
  formComplexitySimpleMultiplier: number;
  formComplexityStandardMultiplier: number;
  formComplexityComplexMultiplier: number;
  formLaborHoursPerLinearFoot: number;

  sawCutCost: number;
  jointMaterialCost: number;
  expansionJointCost: number;
  curingCompoundCost: number;
  stampPatternCost: number;
  decorativePremium: number;

  laborRatePerHour: number;
  overtimeMultiplier: number;
  defaultCrewSize: number;
  demoHoursPer100SqFt: number;
  prepHoursPer100SqFt: number;
  formHoursPer100LinearFt: number;
  reinforcementHoursPer100SqFt: number;
  pourHoursPer100SqFt: number;
  finishHoursPer100SqFt: number;

  skidSteerCost: number;
  excavatorCost: number;
  compactorCost: number;
  sawEquipmentCost: number;
  powerTrowelCost: number;
  trailerTruckCost: number;
  generatorCost: number;
  buggyCost: number;
  otherEquipmentCost: number;

  overheadPercent: number;
  contingencyPercent: number;
  profitPercent: number;
  taxPercent: number;
  travelCharge: number;
  rushFee: number;
  weatherRiskAllowance: number;
}

export interface StructuredEstimateInputDto {
  projectType?: string;
  lengthFt?: number;
  widthFt?: number;
  depthIn?: number;
  wastePercent?: number;
  pourCount?: number;
  demoRequired?: boolean;
  excavationRequired?: boolean;
  pumpRequired?: boolean;
  reinforcementType?: string;
  finishType?: string;
}

export interface EstimateCalculationSnapshotDto {
  squareFeet: number;
  cubicFeet: number;
  cubicYards: number;
  cubicYardsWithWaste: number;
  concreteMaterialCost: number;
  deliveredConcreteCost: number;
  sitePrepSubtotal: number;
  reinforcementSubtotal: number;
  formworkSubtotal: number;
  finishSubtotal: number;
  totalLaborHours: number;
  regularLaborCost: number;
  overtimeLaborCost: number;
  laborSubtotal: number;
  equipmentSubtotal: number;
  directCost: number;
  overheadAmount: number;
  contingencyAmount: number;
  profitAmount: number;
  taxAmount: number;
  finalEstimatedPrice: number;
  pricePerSquareFoot: number;
  pricePerYard: number;
}

export interface CreateEstimateFromAppointmentRequestDto {
  appointmentId?: string;
  customerName: string;
  customerCompany?: string;
  projectAddress: string;
  estimatorName: string;
  projectName?: string;
  estimateNumber?: string;
  structuredInput: StructuredEstimateInputDto;
}

export interface UpdateEstimateStructuredRequestDto {
  customerName: string;
  customerCompany?: string;
  projectAddress: string;
  estimatorName: string;
  projectName?: string;
  appointmentId?: string;
  structuredInput: StructuredEstimateInputDto;
}
