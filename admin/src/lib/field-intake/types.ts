export type ProjectTypeOption = 'Driveway' | 'Patio' | 'Sidewalk' | 'Slab' | 'Steps' | 'Other';
export type ReinforcementTypeOption = 'Rebar' | 'Wire Mesh' | 'Fiber Mesh' | 'None';
export type FinishTypeOption = 'Broom' | 'Smooth' | 'Stamped' | 'Exposed Aggregate' | 'Other';

export interface FieldEstimateIntakeValues {
  projectType?: ProjectTypeOption;
  lengthFt?: number;
  widthFt?: number;
  depthIn?: number;
  pourCount?: number;
  demoRequired?: boolean;
  excavationRequired?: boolean;
  pumpRequired?: boolean;
  reinforcementType?: ReinforcementTypeOption;
  finishType?: FinishTypeOption;
}

export type IntakeStructuredField = keyof FieldEstimateIntakeValues;

export type IntakeStepId =
  | 'jobType'
  | 'dimensions'
  | 'depth'
  | 'pourCount'
  | 'demoRequired'
  | 'excavationRequired'
  | 'pumpRequired'
  | 'reinforcementType'
  | 'finishType'
  | 'complete';

export interface IntakeChatMessage {
  id: string;
  role: 'bob' | 'user';
  text: string;
}

export interface FieldIntakeState {
  values: FieldEstimateIntakeValues;
  currentStep: IntakeStepId;
  completedSteps: IntakeStepId[];
  messages: IntakeChatMessage[];
}

export const INTAKE_STEP_ORDER: IntakeStepId[] = [
  'jobType',
  'dimensions',
  'depth',
  'pourCount',
  'demoRequired',
  'excavationRequired',
  'pumpRequired',
  'reinforcementType',
  'finishType',
  'complete'
];

export const INTAKE_STEP_FIELDS: Record<Exclude<IntakeStepId, 'complete'>, IntakeStructuredField[]> = {
  jobType: ['projectType'],
  dimensions: ['lengthFt', 'widthFt'],
  depth: ['depthIn'],
  pourCount: ['pourCount'],
  demoRequired: ['demoRequired'],
  excavationRequired: ['excavationRequired'],
  pumpRequired: ['pumpRequired'],
  reinforcementType: ['reinforcementType'],
  finishType: ['finishType']
};
