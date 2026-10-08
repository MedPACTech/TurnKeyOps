import { load as sharedLoad, actions as sharedActions } from '../+page.server';
export const load = sharedLoad;
export const actions = { quote: sharedActions.quote };
