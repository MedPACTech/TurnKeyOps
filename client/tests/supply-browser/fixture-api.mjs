// Explicit browser contract fixture; no production data or provider traffic.
import http from 'node:http';import {randomUUID} from 'node:crypto';
const id='11111111-2222-4333-8444-555555555555',member='22222222-2222-4333-8444-555555555555';
const choices={customers:[{id,name:'Fixture customer'}],sites:[{id,name:'Fixture site',address:'123 Test Lane'}],members:[{id:member,name:'Fixture technician',team:'Crew A'}]};
const policy={allowManual:true,enabledTrades:['concrete','doors-locks'],profiles:{},memberSkills:{},notificationTemplates:{},customerChannels:{}};
let job,state,canWrite,events;function reset(){canWrite=true;state='IN_PROGRESS';events=[];job={id,version:randomUUID(),name:'Fixture installation',customerName:'Fixture customer',jobSiteName:'Fixture site',projectAddress:'123 Test Lane',contactPhone:'+15555550100',scheduledStart:new Date().toISOString(),leadId:null,estimateId:null,description:'Approved door installation',acceptedEstimate:null,activity:[],execution:{origin:'Won Estimate',tradeProfile:'doors-locks',serviceType:'commercial',soldScope:'Install the customer-approved door and hardware.',tradeData:{},profile:{fields:{hardware:'Hardware'},stateLabels:{},customerAcceptanceRequired:true},tasks:[{id:'task-one',title:'Verify operation',required:true,stage:'complete',evidenceRequired:false,completedAtUtc:null,notes:'',evidenceIds:[]}],requirements:[],issues:[],changes:[],evidence:[],acceptances:[]}};}reset();
const detail=()=>({job,state,stateLabel:state.replaceAll('_',' '),nextAction:state==='IN_PROGRESS'?'Capture progress and complete the checklist':'Obtain customer acceptance and close',scheduleBlockers:[],startBlockers:[],completionBlockers:job.execution.tasks.filter(t=>!t.completedAtUtc).map(t=>'Complete: '+t.title),transitions:state==='COMPLETED'?['CLOSED','IN_PROGRESS']:['READY_FOR_COMPLETION','PAUSED','WAITING'],events,canWrite});

let supply;function resetSupply(){supply={version:randomUUID(),canWrite:true,canInventoryWrite:true,canPurchase:true,canPurchaseWrite:true,canConfigure:true,
 catalog:[{id:'hardware',name:'Lockset',kind:'PRODUCT',unit:'each',stocked:true,jobSpecific:false,sku:'LOCK-1',active:true,trades:['doors-locks'],defaultCost:10}],
 locations:[{id:'33333333-2222-4333-8444-555555555555',name:'Warehouse',type:'warehouse'}],
 demands:[{id:'44444444-2222-4333-8444-555555555555',jobId:id,jobName:'Fixture installation',requirementId:'55555555-2222-4333-8444-555555555555',description:'Install locksets',itemId:'hardware',quantity:8,unit:'each',strategy:'stock',trade:'doors-locks',requiredAtUtc:null,gate:'blocker',overrideReason:'',readiness:{status:'PURCHASE_REQUIRED',required:8,reserved:0,received:0,consumed:0,shortage:8,reasons:['Missing 8 each']}}],
 balances:[{itemId:'hardware',locationId:'33333333-2222-4333-8444-555555555555',onHand:12,reserved:0,available:12}],reservations:[],vendors:[{contactId:'66666666-2222-4333-8444-555555555555',name:'Fixture vendor',preferred:true}],vendorChoices:[],jobChoices:[],
 orders:[{id:'77777777-2222-4333-8444-555555555555',number:'PO-000001',vendorId:'66666666-2222-4333-8444-555555555555',status:'ACKNOWLEDGED',expectedAtUtc:'2026-10-01T12:00:00Z',locationId:'33333333-2222-4333-8444-555555555555',vendorReference:'Fixture confirmation',approvalReasons:[],total:80,lines:[{id:'88888888-2222-4333-8444-555555555555',demandId:'44444444-2222-4333-8444-555555555555',description:'Install locksets',quantity:8,unit:'each',unitCost:10,received:0,directToJob:false}]}],requests:[],receipts:[],suggestions:[],policy:null,units:['each','box','load'],metrics:{shortages:1,lateOrders:1,receivedQuantity:0,damagedQuantity:0,orderValue:80}};}resetSupply();

http.createServer(async(req,res)=>{let raw='';for await(const c of req)raw+=c;let body={};try{body=raw?JSON.parse(raw):{}}catch{}const path=new URL(req.url,'http://local').pathname;const send=(data,status=200)=>{res.writeHead(status,{'Content-Type':'application/json'});res.end(JSON.stringify({success:status<400,data}));};
 if(path.endsWith('/notifications'))return send([{receiptId:'fixture-receipt',channel:'email',template:'scheduled',status:'unconfirmed',message:'Delivery unconfirmed. Review provider history before requesting another notification.',createdAtUtc:new Date().toISOString()}]);
 if(path==='/reset'){resetSupply();reset();return send(true);}if(path==='/scenario'){canWrite=!body.readOnly;if(body.readOnly){supply.canWrite=false;supply.canInventoryWrite=false;supply.canPurchaseWrite=false;supply.canConfigure=false;}if(body.completed)state='COMPLETED';return send(true);}
 if(path==='/api/auth/session')return send({valid:true});if(path==='/api/my-module-access')return send(canWrite?['inventory.read','inventory.write','purchasing.read','purchasing.write','jobs.read','jobs.write','calendar.read']:['inventory.read','purchasing.read','jobs.read']);

 if(path==='/api/inventory'||path==='/api/purchasing')return send(supply);
 if(path==='/api/inventory/command'||path==='/api/purchasing/command'){
  if(!canWrite)return send(null,403);if(body.expectedVersion!==supply.version)return send(null,409);
  if(body.action==='reserve'){const d=supply.demands[0];d.readiness.reserved+=body.quantity;d.readiness.shortage-=body.quantity;d.readiness.status='RESERVED';d.readiness.reasons=[];supply.reservations.push({id:randomUUID(),demandId:d.id,itemId:'hardware',locationId:supply.locations[0].id,quantity:body.quantity,status:'active'});supply.balances[0].reserved+=body.quantity;supply.balances[0].available-=body.quantity;}
  if(body.action==='receive'){const order=supply.orders[0];order.lines[0].received+=body.quantity;order.status=order.lines[0].received===8?'RECEIVED':'PARTIALLY_RECEIVED';supply.receipts.push({id:randomUUID(),orderId:order.id,lineId:order.lines[0].id,accepted:body.quantity,damaged:body.damaged,reference:body.reference,atUtc:new Date().toISOString(),files:[]});}
  if(body.action==='catalog')supply.catalog.push(body.item);if(body.action==='location')supply.locations.push({...body.location,id:randomUUID()});
  supply.version=randomUUID();return send(supply);
 }
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
}).listen(5598,'127.0.0.1');
