// Explicit browser contract fixture; no production data or provider traffic.
import http from 'node:http';import {randomUUID} from 'node:crypto';
const id='11111111-2222-4333-8444-555555555555',member='22222222-2222-4333-8444-555555555555';
const choices={customers:[{id,name:'Fixture customer'}],sites:[{id,name:'Fixture site',address:'123 Test Lane'}],members:[{id:member,name:'Fixture technician',team:'Crew A'}]};
const policy={allowManual:true,enabledTrades:['concrete','doors-locks'],profiles:{},memberSkills:{},notificationTemplates:{},customerChannels:{}};
let job,state,canWrite,events;function reset(){canWrite=true;state='IN_PROGRESS';events=[];job={id,version:randomUUID(),name:'Fixture installation',customerName:'Fixture customer',jobSiteName:'Fixture site',projectAddress:'123 Test Lane',contactPhone:'+15555550100',scheduledStart:new Date().toISOString(),leadId:null,estimateId:null,description:'Approved door installation',acceptedEstimate:null,activity:[],execution:{origin:'Won Estimate',tradeProfile:'doors-locks',serviceType:'commercial',soldScope:'Install the customer-approved door and hardware.',tradeData:{},profile:{fields:{hardware:'Hardware'},stateLabels:{},customerAcceptanceRequired:true},tasks:[{id:'task-one',title:'Verify operation',required:true,stage:'complete',evidenceRequired:false,completedAtUtc:null,notes:'',evidenceIds:[]}],requirements:[],issues:[],changes:[],evidence:[],acceptances:[]}};}reset();
const detail=()=>({job,state,stateLabel:state.replaceAll('_',' '),nextAction:state==='IN_PROGRESS'?'Capture progress and complete the checklist':'Obtain customer acceptance and close',scheduleBlockers:[],startBlockers:[],completionBlockers:job.execution.tasks.filter(t=>!t.completedAtUtc).map(t=>'Complete: '+t.title),transitions:state==='COMPLETED'?['CLOSED','IN_PROGRESS']:['READY_FOR_COMPLETION','PAUSED','WAITING'],events,canWrite});
http.createServer(async(req,res)=>{let raw='';for await(const c of req)raw+=c;let body={};try{body=raw?JSON.parse(raw):{}}catch{}const path=new URL(req.url,'http://local').pathname;const send=(data,status=200)=>{res.writeHead(status,{'Content-Type':'application/json'});res.end(JSON.stringify({success:status<400,data}));};
 if(path.endsWith('/notifications'))return send([{receiptId:'fixture-receipt',channel:'email',template:'scheduled',status:'unconfirmed',message:'Delivery unconfirmed. Review provider history before requesting another notification.',createdAtUtc:new Date().toISOString()}]);
 if(path==='/reset'){reset();return send(true);}if(path==='/scenario'){canWrite=!body.readOnly;if(body.completed)state='COMPLETED';return send(true);}
 if(path==='/api/auth/session')return send({valid:true});if(path==='/api/my-module-access')return send(canWrite?['jobs.read','jobs.write','settings.read','settings.write','calendar.read']:['jobs.read']);
 if(path==='/api/job-workspace/choices')return send(choices);if(path==='/api/job-workspace/configuration')return send({policy,settings:{version:'v1'}});
 if(path==='/api/job-workspace')return send({jobs:[{...job,state,trade:'doors-locks',nextAction:detail().nextAction,blockers:0,modern:true}],canWrite,canConfigure:canWrite,configuration:policy});
 if(path===`/api/job-workspace/${id}`)return send(detail());
 if(path===`/api/job-workspace/${id}/command`){if(!canWrite)return send(null,403);if(body.expectedVersion!==job.version)return send(null,409);
  if(body.action==='task')job.execution.tasks.find(t=>t.id===body.itemId).completedAtUtc=new Date().toISOString();
  if(body.action==='issue')job.execution.issues.push({...body.issue,id:randomUUID()});
  if(body.action==='resolve-issue')job.execution.issues.find(i=>i.id===body.itemId).resolvedAtUtc=new Date().toISOString();
  if(body.action==='change')job.execution.changes.push({...body.change,id:randomUUID(),status:'DRAFT'});
  if(body.action==='transition')state=body.state;
  if(body.action==='accept')job.execution.acceptances.push({...body.acceptance,id:randomUUID(),acceptedAtUtc:new Date().toISOString()});
  job.activity.unshift({id:randomUUID(),label:body.text||body.action,type:'job.'+body.action,occurredAtUtc:new Date().toISOString()});job.version=randomUUID();return send(detail());}
 return send(null,404);
}).listen(5498,'127.0.0.1');
