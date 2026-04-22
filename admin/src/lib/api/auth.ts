import { api } from '$api/client';

export type OtpChannel = 'email' | 'sms';

export interface StartOtpRequest {
  destination: string;
  tenantId?: string;
}

export interface StartOtpResponse {
  channel?: string;
  destinationMasked?: string;
  expiresInSeconds?: number;
  requiresTermsAcceptance?: boolean;
  availableChannels?: string[];
  devCode?: string | null;
  challengeId?: string | null;
}

export interface CompleteOtpRequest {
  destination: string;
  code: string;
  challengeId: string;
  displayName?: string;
}

export interface AuthResult {
  accessToken?: string;
  token?: string;
  refreshToken?: string;
  [key: string]: unknown;
}

export interface StartRegistrationRequest {
  email: string;
  displayName: string;
  resetUrlBase: string;
}

export interface StartRegistrationResponse {
  challengeId?: string | null;
  email?: string;
  [key: string]: unknown;
}

export interface CompleteRegistrationRequest {
  challengeId?: string | null;
  token?: string | null;
  email?: string;
  displayName?: string;
  password?: string;
  newPassword?: string;
  accepted?: boolean;
  version?: string;
  terms?: {
    accepted: boolean;
    version: string;
  };
}

export function isEmailIdentifier(value: string): boolean {
  return value.includes('@');
}

export function buildStartOtpRequest(identifier: string, preferredChannel: OtpChannel): StartOtpRequest {
  return {
    destination: identifier.trim()
  };
}

export function buildCompleteOtpRequest(
  identifier: string,
  code: string,
  challengeId?: string | null
): CompleteOtpRequest {
  if (!challengeId) {
    throw new Error('Missing OTP challenge id');
  }

  return {
    destination: identifier.trim(),
    code: code.trim(),
    challengeId
  };
}

export async function startOtp(identifier: string, preferredChannel: OtpChannel): Promise<StartOtpResponse> {
  return api.post<StartOtpResponse>('/auth/startotp', buildStartOtpRequest(identifier, preferredChannel));
}

export async function completeOtp(
  identifier: string,
  code: string,
  challengeId?: string | null
): Promise<AuthResult> {
  return api.post<AuthResult>('/auth/completeotp', buildCompleteOtpRequest(identifier, code, challengeId));
}

export async function startEmailPasswordRegistration(
  email: string,
  displayName: string,
  resetUrlBase: string
): Promise<StartRegistrationResponse> {
  return api.post<StartRegistrationResponse>('/auth/start-email-password-registration', {
    email,
    displayName,
    resetUrlBase
  } satisfies StartRegistrationRequest);
}

export async function completeEmailPasswordRegistration(
  request: CompleteRegistrationRequest
): Promise<AuthResult> {
  return api.post<AuthResult>('/auth/complete-email-password-registration', request);
}
