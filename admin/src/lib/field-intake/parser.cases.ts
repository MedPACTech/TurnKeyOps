import type { FieldEstimateIntakeValues, IntakeStepId } from './types';

export interface IntakeParserExampleCase {
  input: string;
  currentStep: IntakeStepId;
  currentValues: FieldEstimateIntakeValues;
  expectedResolved: Partial<FieldEstimateIntakeValues>;
}

export const intakeParserExampleCases: IntakeParserExampleCase[] = [
  {
    input: '25 by 20 driveway, 4 inches thick',
    currentStep: 'jobType',
    currentValues: {},
    expectedResolved: {
      projectType: 'Driveway',
      lengthFt: 25,
      widthFt: 20,
      depthIn: 4
    }
  },
  {
    input: '25x20 patio',
    currentStep: 'jobType',
    currentValues: {},
    expectedResolved: {
      projectType: 'Patio',
      lengthFt: 25,
      widthFt: 20
    }
  },
  {
    input: 'No demo, but yes excavation',
    currentStep: 'demoRequired',
    currentValues: {},
    expectedResolved: {
      demoRequired: false,
      excavationRequired: true
    }
  },
  {
    input: 'Stamped finish with rebar',
    currentStep: 'reinforcementType',
    currentValues: {},
    expectedResolved: {
      finishType: 'Stamped',
      reinforcementType: 'Rebar'
    }
  },
  {
    input: 'Two pours, pump needed',
    currentStep: 'pourCount',
    currentValues: {},
    expectedResolved: {
      pourCount: 2,
      pumpRequired: true
    }
  }
];
