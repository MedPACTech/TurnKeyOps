// Isolated UI contract fixture, never imported into the application or production API.
import http from 'node:http';
import {randomUUID} from 'node:crypto';
const tenant='7d40ea6c-313f-4f53-bf7d-5d1ecb9cc50b';
const configuration={tradeProfiles:['general','concrete','framing','land-clearing','doors-locks'],defaultTradeProfile:'general',stageLabels:{DISCOVERY:'Site visit'},requiredFields:{},wonReasons:['Accepted proposal'],lostReasons:['Price','Timing'],referralRequired:false,assignmentMode:'manual',assignmentRules:[],aiActions:{}};
const associates=[{id:'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',name:'Jordan Ellis'},{id:'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',name:'Morgan Chen'}];
const records=new Map();
function lead(input={}) {const now=new Date().toISOString();return {id:randomUUID(),tenantId:tenant,title:'Browser fixture opportunity',contactName:'Fixture customer',companyName:'',email:'fixture@example.invalid',phone:'+15555550100',siteAddress:'Fixture job site',requestedWork:'Repair the site entrance',tradeProfile:'general',source:'Referral',referralName:'Fixture supplier',referralContactId:null,customerId:null,ownerMembershipId:null,ownerName:'Unassigned',estimatedValue:25000,service:'',propertyType:'commercial',stage:'NEW',stageLabel:'new',nextAction:'Respond to the customer',qualification:{},attribution:{},version:randomUUID(),createdAtUtc:now,updatedAtUtc:now,followUpAtUtc:null,intakeRequestId:null,estimateId:null,jobId:null,activity:[{id:randomUUID(),type:'created',actor:'Fixture user',text:'Opportunity created',occurredAtUtc:now}],fields:[{key:'requestedWork',label:'Requested work',required:true,value:'Repair the site entrance'}],missingRequired:[],bobSummary:'Required qualification is present. Respond to the customer.',...input};}
function reset(){records.clear();const item=lead({id:'11111111-2222-4333-8444-555555555555'});records.set(item.id,item);}reset();
http.createServer(async(req,res)=>{let raw='';for await(const chunk of req)raw+=chunk;const body=raw?JSON.parse(raw):{};const path=new URL(req.url,'http://localhost').pathname;const send=(data,status=200)=>{res.writeHead(status,{'Content-Type':'application/json'});res.end(JSON.stringify({success:status<400,data}));};
 if(path==='/reset'){reset();return send(true);}
 if(path==='/api/my-module-access')return send(['leads.read','leads.write']);
 if(path==='/api/auth/session')return send({valid:true});
 if(path==='/api/leads'&&req.method==='GET')return send({leads:[...records.values()],configuration,members:[],associates,canWrite:true,canConfigure:true});
 if(path==='/api/leads'&&req.method==='POST'){const item=lead(body);records.set(item.id,item);return send(item);}
 if(path==='/api/admin/tenant-settings/operational')return send({values:{leads:configuration},version:'fixture',schemaVersion:1});
 const parts=path.split('/');const item=records.get(parts[3]);if(!item)return send(null,404);
 if(parts[4]==='duplicates')return send([]);
 if(req.method==='GET')return send(item);
 if(body.expectedVersion!==item.version)return send(null,409);
 if(parts[4]==='stage'){item.stage=body.stage;item.stageLabel=configuration.stageLabels[body.stage]||body.stage.toLowerCase();}
 if(parts[4]==='activity')item.activity.push({id:randomUUID(),type:body.type,text:body.text,actor:'Fixture user',occurredAtUtc:new Date().toISOString()});
 if(req.method==='PUT'){Object.assign(item,body);item.ownerName=associates.find(p=>p.id===item.ownerProfileId)?.name||'Unassigned';}
 item.version=randomUUID();return send(item);
}).listen(5298,'127.0.0.1');
