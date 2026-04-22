import { browser } from '$app/environment';
import { writable } from 'svelte/store';
import { parseIntakeResponse } from '$lib/field-intake/parser';
import {
  INTAKE_STEP_FIELDS,
  INTAKE_STEP_ORDER,
  type FieldEstimateIntakeValues,
  type FieldIntakeState,
  type IntakeChatMessage,
  type IntakeStepId
} from '$lib/field-intake/types';

export type {
  FieldEstimateIntakeValues,
  FieldIntakeState,
  FinishTypeOption,
  IntakeChatMessage,
  IntakeStepId,
  ProjectTypeOption,
  ReinforcementTypeOption
} from '$lib/field-intake/types';

const STORAGE_KEY = 'turnkeyops_field_intake_state';

const initialBobPrompts: Record<Exclude<IntakeStepId, 'complete'>, string> = {
  jobType: 'What type of concrete job are you estimating?',
  dimensions: 'Got it. What are the dimensions? Enter the length and width in feet.',
  depth: 'How thick is the concrete pour?',
  pourCount: 'How many pours will this estimate need?',
  demoRequired: 'Will demo be required before the pour?',
  excavationRequired: 'Will excavation be required?',
  pumpRequired: 'Will you need a pump for this job?',
  reinforcementType: 'What reinforcement are you planning to use?',
  finishType: 'What finish should I use for this estimate?'
};

function createInitialState(): FieldIntakeState {
  return {
    values: {},
    currentStep: 'jobType',
    completedSteps: [],
    messages: [
      {
        id: crypto.randomUUID(),
        role: 'bob',
        text: initialBobPrompts.jobType
      }
    ]
  };
}

function isStepResolved(step: Exclude<IntakeStepId, 'complete'>, values: FieldEstimateIntakeValues): boolean {
  return INTAKE_STEP_FIELDS[step].every((field) => values[field] !== undefined);
}

function getCompletedSteps(values: FieldEstimateIntakeValues): IntakeStepId[] {
  return INTAKE_STEP_ORDER.filter(
    (step): step is Exclude<IntakeStepId, 'complete'> => step !== 'complete'
  ).filter((step) => isStepResolved(step, values));
}

function getNextPendingStep(values: FieldEstimateIntakeValues): IntakeStepId {
  for (const step of INTAKE_STEP_ORDER) {
    if (step === 'complete') {
      return 'complete';
    }

    if (!isStepResolved(step, values)) {
      return step;
    }
  }

  return 'complete';
}

function completionMessage(values: FieldEstimateIntakeValues): string {
  const parts = [
    values.projectType ? `${values.projectType.toLowerCase()} job` : null,
    values.lengthFt && values.widthFt ? `${values.lengthFt} x ${values.widthFt} ft` : null,
    values.depthIn ? `${values.depthIn}" thick` : null
  ].filter(Boolean);

  return parts.length
    ? `Perfect. I’ve captured the initial estimate details for this ${parts.join(', ')}.`
    : 'Perfect. I’ve captured the initial estimate details.';
}

function nextBobMessage(step: IntakeStepId, values: FieldEstimateIntakeValues): string | null {
  if (step === 'complete') {
    return completionMessage(values);
  }

  return initialBobPrompts[step];
}

function persist(state: FieldIntakeState) {
  if (!browser) return;
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(state));
}

function load(): FieldIntakeState | null {
  if (!browser) return null;

  const raw = sessionStorage.getItem(STORAGE_KEY);
  if (!raw) return null;

  try {
    return JSON.parse(raw) as FieldIntakeState;
  } catch {
    sessionStorage.removeItem(STORAGE_KEY);
    return null;
  }
}

function appendMessages(current: IntakeChatMessage[], userText: string, bobText: string | null): IntakeChatMessage[] {
  const messages: IntakeChatMessage[] = [...current, { id: crypto.randomUUID(), role: 'user', text: userText }];

  if (bobText) {
    messages.push({ id: crypto.randomUUID(), role: 'bob', text: bobText });
  }

  return messages;
}

function createFieldIntakeStore() {
  const { subscribe, set, update } = writable<FieldIntakeState>(load() ?? createInitialState());

  return {
    subscribe,
    reset() {
      const next = createInitialState();
      persist(next);
      set(next);
    },
    restore() {
      const next = load() ?? createInitialState();
      persist(next);
      set(next);
      return next;
    },
    answer(step: Exclude<IntakeStepId, 'complete'>, value: Partial<FieldEstimateIntakeValues>, userText: string) {
      update((current) => {
        if (current.currentStep !== step) return current;

        const nextValues = { ...current.values, ...value };
        const nextStep = getNextPendingStep(nextValues);
        const nextMessages = appendMessages(current.messages, userText, nextBobMessage(nextStep, nextValues));
        const nextState: FieldIntakeState = {
          values: nextValues,
          currentStep: nextStep,
          completedSteps: getCompletedSteps(nextValues),
          messages: nextMessages
        };

        persist(nextState);
        return nextState;
      });
    },
    interpretReply(input: string) {
      update((current) => {
        if (current.currentStep === 'complete') return current;

        const parseResult = parseIntakeResponse(input, current.currentStep, current.values);
        const nextValues = { ...current.values, ...parseResult.resolved };
        const currentStepResolved = isStepResolved(current.currentStep, nextValues);
        const nextStep = currentStepResolved ? getNextPendingStep(nextValues) : current.currentStep;
        const bobText = currentStepResolved
          ? nextBobMessage(nextStep, nextValues)
          : parseResult.clarificationPrompts[0] ?? nextBobMessage(current.currentStep, nextValues);

        const nextState: FieldIntakeState = {
          values: nextValues,
          currentStep: nextStep,
          completedSteps: getCompletedSteps(nextValues),
          messages: appendMessages(current.messages, input, bobText)
        };

        persist(nextState);
        return nextState;
      });
    }
  };
}

export const fieldIntake = createFieldIntakeStore();
