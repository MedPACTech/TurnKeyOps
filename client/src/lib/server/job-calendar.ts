import type {RequestEvent} from '@sveltejs/kit';import {leadApi} from './leads';
export type OperationalEvent={id:string;jobId:string;title:string;jobName:string;jobEventType:string;eventStatus:string;startUtc:string;endUtc:string};
export async function loadJobCalendar(event:RequestEvent){
 const from=new Date();from.setDate(from.getDate()-7);const to=new Date();to.setDate(to.getDate()+60);
 try{return {operationalEvents:(await leadApi<OperationalEvent[]>(event,`calendar?start=${encodeURIComponent(from.toISOString())}&end=${encodeURIComponent(to.toISOString())}`)).filter(e=>e.jobEventType),operationalEventsError:''};}
 catch{return {operationalEvents:[] as OperationalEvent[],operationalEventsError:'Job schedule events could not be loaded. Refresh to retry.'};}
}
