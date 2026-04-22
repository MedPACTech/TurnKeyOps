import type {
  FieldEstimateIntakeValues,
  FinishTypeOption,
  IntakeStepId,
  IntakeStructuredField,
  ProjectTypeOption,
  ReinforcementTypeOption
} from './types';
import { INTAKE_STEP_FIELDS } from './types';

export interface IntakeParseResult {
  resolved: Partial<FieldEstimateIntakeValues>;
  unresolved: IntakeStructuredField[];
  clarificationPrompts: string[];
  matchedFields: IntakeStructuredField[];
}

const NUMBER_WORDS: Record<string, number> = {
  one: 1,
  two: 2,
  three: 3,
  four: 4,
  five: 5,
  six: 6,
  seven: 7,
  eight: 8,
  nine: 9,
  ten: 10,
  eleven: 11,
  twelve: 12
};

const PROJECT_TYPE_MATCHERS: Array<{ value: ProjectTypeOption; pattern: RegExp }> = [
  { value: 'Driveway', pattern: /\bdriveway\b/ },
  { value: 'Patio', pattern: /\bpatio\b/ },
  { value: 'Sidewalk', pattern: /\bsidewalk\b|\bwalkway\b/ },
  { value: 'Slab', pattern: /\bslab\b/ },
  { value: 'Steps', pattern: /\bsteps?\b|\bstoop\b/ }
];

const REINFORCEMENT_MATCHERS: Array<{ value: ReinforcementTypeOption; pattern: RegExp }> = [
  { value: 'Fiber Mesh', pattern: /\bfiber(?:\s|-)?mesh\b/ },
  { value: 'Wire Mesh', pattern: /\bwire(?:\s|-)?mesh\b/ },
  { value: 'Rebar', pattern: /\brebar\b/ },
  { value: 'None', pattern: /\bno reinforcement\b|\bwithout reinforcement\b|\bnone\b/ }
];

const FINISH_MATCHERS: Array<{ value: FinishTypeOption; pattern: RegExp }> = [
  { value: 'Exposed Aggregate', pattern: /\bexposed aggregate\b/ },
  { value: 'Stamped', pattern: /\bstamped\b/ },
  { value: 'Broom', pattern: /\bbroom\b/ },
  { value: 'Smooth', pattern: /\bsmooth\b/ }
];

const BOOLEAN_PROMPTS: Record<'demoRequired' | 'excavationRequired' | 'pumpRequired', string> = {
  demoRequired: 'Will demo be required before the pour?',
  excavationRequired: 'Will excavation be required?',
  pumpRequired: 'Will you need a pump for this job?'
};

const FIELD_CLARIFICATIONS: Record<IntakeStructuredField, string> = {
  projectType: 'I still need the job type. You can say driveway, patio, sidewalk, slab, or steps.',
  lengthFt: 'I still need the overall length and width in feet. You can say something like “25 by 20”.',
  widthFt: 'I still need the overall length and width in feet. You can say something like “25 by 20”.',
  depthIn: 'I still need the concrete depth in inches.',
  pourCount: 'I still need the number of pours for this estimate.',
  demoRequired: BOOLEAN_PROMPTS.demoRequired,
  excavationRequired: BOOLEAN_PROMPTS.excavationRequired,
  pumpRequired: BOOLEAN_PROMPTS.pumpRequired,
  reinforcementType: 'What reinforcement are you planning to use? You can say rebar, wire mesh, fiber mesh, or none.',
  finishType: 'What finish should I use for this estimate? You can say broom, smooth, stamped, or exposed aggregate.'
};

function normalizeText(input: string): string {
  return input
    .toLowerCase()
    .replace(/[’']/g, '')
    .replace(/[;,]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

function hasNumericAmbiguity(text: string): boolean {
  return /\b(?:about|around|approx(?:imately)?|maybe|roughly)\b/.test(text);
}

function toNumber(token: string | undefined): number | undefined {
  if (!token) return undefined;

  const lower = token.toLowerCase();
  if (NUMBER_WORDS[lower] !== undefined) {
    return NUMBER_WORDS[lower];
  }

  const numeric = Number(lower);
  return Number.isFinite(numeric) ? numeric : undefined;
}

function parseSingleMatch<T extends string>(
  text: string,
  matchers: Array<{ value: T; pattern: RegExp }>
): { value?: T; ambiguous: boolean } {
  const matches = matchers.filter((matcher) => matcher.pattern.test(text)).map((matcher) => matcher.value);
  const uniqueMatches = [...new Set(matches)];

  if (uniqueMatches.length === 1) {
    return { value: uniqueMatches[0], ambiguous: false };
  }

  return { ambiguous: uniqueMatches.length > 1 };
}

function parseDimensions(text: string): { lengthFt?: number; widthFt?: number } {
  if (hasNumericAmbiguity(text)) return {};

  const match = text.match(
    /\b(\d+(?:\.\d+)?)\s*(?:feet|foot|ft)?\s*(?:x|by)\s*(\d+(?:\.\d+)?)\s*(?:feet|foot|ft)?\b/
  );

  if (!match) return {};

  const lengthFt = Number(match[1]);
  const widthFt = Number(match[2]);
  if (Number.isNaN(lengthFt) || Number.isNaN(widthFt)) return {};

  return { lengthFt, widthFt };
}

function parseDepth(text: string): number | undefined {
  if (hasNumericAmbiguity(text)) return undefined;

  const match = text.match(/\b(\d+(?:\.\d+)?)\s*(?:inches|inch|in|")\s*(?:thick|deep)?\b/);
  if (!match) return undefined;

  const depth = Number(match[1]);
  return Number.isNaN(depth) ? undefined : depth;
}

function parsePourCount(text: string): number | undefined {
  const match = text.match(
    /\b(\d+(?:\.\d+)?|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve)\s+pours?\b/
  );

  const count = toNumber(match?.[1]);
  if (count === undefined || !Number.isInteger(count) || count <= 0) {
    return undefined;
  }

  return count;
}

function parseBooleanFlag(
  text: string,
  type: 'demoRequired' | 'excavationRequired' | 'pumpRequired'
): boolean | undefined {
  if (hasNumericAmbiguity(text)) return undefined;

  let positivePatterns: RegExp[] = [];
  let negativePatterns: RegExp[] = [];

  switch (type) {
    case 'demoRequired':
      positivePatterns = [
        /\bdemo (?:needed|required|included)\b/,
        /\b(?:need|needs|require|required|with|include|including|yes)\s+(?:a\s+)?demo\b/
      ];
      negativePatterns = [/\bno demo\b/, /\bwithout demo\b/, /\bdemo not (?:needed|required)\b/];
      break;
    case 'excavationRequired':
      positivePatterns = [
        /\bexcavation (?:needed|required|included)\b/,
        /\b(?:need|needs|require|required|with|include|including|yes)\s+excavation\b/
      ];
      negativePatterns = [
        /\bno excavation\b/,
        /\bwithout excavation\b/,
        /\bexcavation not (?:needed|required)\b/
      ];
      break;
    case 'pumpRequired':
      positivePatterns = [
        /\bpump (?:needed|required|included)\b/,
        /\bneed(?:ed)? (?:a\s+)?pump\b/,
        /\bwith (?:a\s+)?pump\b/,
        /\byes (?:on|for)?\s*(?:the\s+)?pump\b/
      ];
      negativePatterns = [/\bno pump\b/, /\bwithout (?:a\s+)?pump\b/, /\bpump not (?:needed|required)\b/];
      break;
  }

  const positive = positivePatterns.some((pattern) => pattern.test(text));
  const negative = negativePatterns.some((pattern) => pattern.test(text));

  if (positive === negative) {
    return undefined;
  }

  return positive;
}

function uniq<T>(values: T[]): T[] {
  return [...new Set(values)];
}

function getUnresolvedFields(
  step: IntakeStepId,
  values: FieldEstimateIntakeValues
): IntakeStructuredField[] {
  if (step === 'complete') return [];

  return INTAKE_STEP_FIELDS[step].filter((field) => values[field] === undefined);
}

export function parseIntakeResponse(
  input: string,
  currentStep: IntakeStepId,
  currentValues: FieldEstimateIntakeValues
): IntakeParseResult {
  const text = normalizeText(input);
  const resolved: Partial<FieldEstimateIntakeValues> = {};
  const ambiguousFields = new Set<IntakeStructuredField>();

  const projectType = parseSingleMatch(text, PROJECT_TYPE_MATCHERS);
  if (projectType.value) {
    resolved.projectType = projectType.value;
  } else if (projectType.ambiguous) {
    ambiguousFields.add('projectType');
  }

  const dimensions = parseDimensions(text);
  if (dimensions.lengthFt !== undefined && dimensions.widthFt !== undefined) {
    resolved.lengthFt = dimensions.lengthFt;
    resolved.widthFt = dimensions.widthFt;
  }

  const depthIn = parseDepth(text);
  if (depthIn !== undefined) {
    resolved.depthIn = depthIn;
  }

  const pourCount = parsePourCount(text);
  if (pourCount !== undefined) {
    resolved.pourCount = pourCount;
  }

  const demoRequired = parseBooleanFlag(text, 'demoRequired');
  if (demoRequired !== undefined) {
    resolved.demoRequired = demoRequired;
  }

  const excavationRequired = parseBooleanFlag(text, 'excavationRequired');
  if (excavationRequired !== undefined) {
    resolved.excavationRequired = excavationRequired;
  }

  const pumpRequired = parseBooleanFlag(text, 'pumpRequired');
  if (pumpRequired !== undefined) {
    resolved.pumpRequired = pumpRequired;
  }

  const reinforcementType = parseSingleMatch(text, REINFORCEMENT_MATCHERS);
  if (reinforcementType.value) {
    resolved.reinforcementType = reinforcementType.value;
  } else if (reinforcementType.ambiguous) {
    ambiguousFields.add('reinforcementType');
  }

  const finishType = parseSingleMatch(text, FINISH_MATCHERS);
  if (finishType.value) {
    resolved.finishType = finishType.value;
  } else if (finishType.ambiguous) {
    ambiguousFields.add('finishType');
  }

  const mergedValues = { ...currentValues, ...resolved };
  const unresolved = uniq([...getUnresolvedFields(currentStep, mergedValues), ...ambiguousFields]);

  return {
    resolved,
    unresolved,
    clarificationPrompts: uniq(unresolved.map((field) => FIELD_CLARIFICATIONS[field])),
    matchedFields: Object.keys(resolved) as IntakeStructuredField[]
  };
}
