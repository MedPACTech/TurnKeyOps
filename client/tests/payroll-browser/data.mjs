const employee='11111111-1111-1111-1111-111111111111';const second='55555555-5555-4555-8555-555555555555';
let state;let bobActions={};
export const resetPayroll=()=>{bobActions={};state={version:'1',employeeId:employee,configuration:{enabled:true,jobsEnabled:false,financeEnabled:false,schedulingEnabled:false,requireJob:false,requireCostCode:true,crewTimeEnabled:true,timezone:'America/New_York',provider:'generic-csv',frequency:'WEEKLY',locationPolicy:'OFF',weeklyOvertime:40,dailyOvertime:8,dailyDoubleTime:12,seventhDayOvertime:false,weekStart:1,overtimeMultiplier:1.5,doubleTimeMultiplier:2,costCodes:['GENERAL','TRAINING'],classifications:['Laborer','Foreman'],earningsTypes:['REGULAR','OVERTIME','DOUBLE_TIME'],leaveTypes:['PTO','SICK'],earningsMapping:{REGULAR:'REG',OVERTIME:'OT',DOUBLE_TIME:'DT'}},grants:Object.fromEntries(['time.self','time.crew','time.approve','payroll.prepare','payroll.export','payroll.results','payroll.compensation.read','payroll.admin'].map(k=>[k,true])),time:[{id:'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',employeeId:second,workDate:'2026-10-12',timezone:'America/New_York',hours:8,clockIn:null,clockOut:null,jobId:null,costCode:'GENERAL',classification:'Laborer',earningType:'REGULAR',source:'MANUAL',status:'SUBMITTED',notes:'Recorded field work',breaks:[]}],employees:[{employeeId:employee,membershipId:'owner-member'},{employeeId:second,membershipId:'worker-member'}],crews:[{id:'cccccccc-cccc-4ccc-8ccc-cccccccccccc',name:'Crew A',leadId:employee,members:[employee,second]}],jobs:[],schedule:[],periods:[{id:'dddddddd-dddd-4ddd-8ddd-dddddddddddd',start:'2026-10-12',end:'2026-10-18',payDate:'2026-10-23',status:'OPEN'}],profiles:[],batches:[],actuals:[],leave:[],wageRequirements:[],exceptions:[],reconciliation:[],reports:{byEmployee:[{employeeId:second,hours:8}],byJob:[{jobId:null,hours:8}],byCostCode:[{costCode:'GENERAL',hours:8}],byTrade:[]}};};resetPayroll();
export async function payrollRequest(req,url,claims,send,res){
 if(url.pathname==='/api/payroll'&&req.method==='GET'){const s=structuredClone(state);if(claims.fixturePayrollField){s.grants=Object.fromEntries(Object.keys(s.grants).map(k=>[k,k==='time.self']));s.time=s.time.filter(t=>t.employeeId===employee);s.crews=[];s.profiles=[];s.batches=[];}return send(s);}
 if(url.pathname==='/api/payroll/readiness')return send({title:'Payroll readiness',exceptions:state.exceptions,action:'No changes made.'});
 if(url.pathname==='/api/payroll/bob'){
 let raw='';for await(const part of req)raw+=part;const c=JSON.parse(raw);const id=crypto.randomUUID();if(c.toolKey==='payroll.readiness')return send({id,status:'completed',resultJson:JSON.stringify({title:'Payroll readiness',exceptions:[]})});bobActions[id]=c.input;return send({id,status:'proposed'});
 }
 if(url.pathname.match(/^\/api\/payroll\/bob\/[^/]+\/approve$/)){
 const id=url.pathname.split('/')[4];const command=bobActions[id];if(!command||command.expectedVersion!==state.version)return send({message:'Payroll changed before Bob approval'},409);state.batches.push({id:command.id,periodId:command.targetId,status:'PREPARED',exportVersion:1,calculationHash:'fixture-source-hash',lines:[{hours:8,gross:200,type:'REGULAR'}]});state.version=String(Number(state.version)+1);return send({id,status:'completed'});
 }
 if(url.pathname==='/api/payroll/command'){
 let raw='';for await(const part of req)raw+=part;const c=JSON.parse(raw);if(c.expectedVersion!==state.version)return send({message:'Payroll changed. Review saved punch conflict.'},409);
 if(c.action==='clock-in')state.time.push({...c.time,clockIn:c.at,hours:0,status:'ACTIVE',breaks:[]});
 if(c.action==='clock-out'){const t=state.time.find(t=>t.id===c.targetId);t.clockOut=c.at;t.hours=8;t.status='DRAFT';}
 if(c.action==='manual')state.time.push({...c.time,status:'DRAFT'});
 if(c.action==='approve'||c.action==='submit'){const t=state.time.find(t=>t.id===c.targetId);t.status=c.action==='approve'?'APPROVED':'SUBMITTED';}
 if(c.action==='crew-time')state.time.push(...c.entries.map(t=>({...t,status:'DRAFT',source:'CREW'})));
 if(c.action==='configure')state.configuration=c.configuration;
 if(c.action==='prepare')state.batches.push({id:c.id,periodId:c.targetId,status:'PREPARED',exportVersion:1,calculationHash:'fixture-source-hash',lines:[{hours:8,gross:200,type:'REGULAR'}]});
 if(c.action==='export')state.batches.find(b=>b.id===c.targetId).status='EXPORTED';
 state.version=String(Number(state.version)+1);return send({version:state.version});
 }
 if(url.pathname.startsWith('/api/payroll/export/')){res.writeHead(200,{'Content-Type':'text/csv'});res.end('Batch,Hours,Gross\r\nfixture,8,200');return;}
 return send({message:'Unsupported payroll fixture request'},404);
}
