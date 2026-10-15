import http from 'node:http';
const source={module:'jobs',entityId:'aaaaaaaa-0000-4000-8000-000000000001',condition:'readiness-blocked',version:'1'};
const fact={source,title:'Fixture residence',explanation:'Required material is not confirmed.',nextAction:'Resolve material readiness',severity:2,capabilityId:'job.readiness',trade:'concrete'};
const attention={...fact,id:'attention-1',recommendedAction:fact.nextAction,status:'open'};
let workflow=null;let dismissed=false;let policy={enabled:true,proactiveEnabled:true,enabledModules:['bob','jobs'],actions:{},escalations:[],roleModules:{},externalNotificationsEnabled:false,customerBobEnabled:false};let version='1';let rules=[];
http.createServer(async(req,res)=>{const url=new URL(req.url,'http://localhost');const send=(data,status=200)=>{res.writeHead(status,{'Content-Type':'application/json'});res.end(JSON.stringify({success:status<400,data,errors:status>=400?[{message:'Forbidden fixture operation'}]:undefined}));};
 if(url.pathname==='/health')return send('ok');if(url.pathname==='/reset'){dismissed=false;workflow=null;version='1';rules=[];return send('ok');}
 if(url.pathname.startsWith('/api/public/tenant-settings'))return send({values:{}});
 if(url.pathname==='/api/auth/session')return send({valid:true});
 if(url.pathname==='/api/my-module-access')return send(['bob.read','bob.write','jobs.read','jobs.write','settings.read']);
 if(url.pathname==='/api/bob/intelligence/briefing')return send({context:{role:'operations',asOfUtc:'2026-10-14T12:00:00Z',modules:['jobs'],unavailableModules:[],facts:[fact]},priorities:dismissed?[]:[attention],atRisk:dismissed?[]:[attention],needsDecision:workflow?.status==='waiting_approval'?[workflow]:[],handled:workflow?.status==='completed'?[workflow]:[],watching:[],nextActions:[]});
 if(url.pathname==='/api/bob/intelligence/activity')return send({workflows:[],events:[{id:'event-1',action:'attention.checked',rationale:'Checked authoritative module conditions',atUtc:'2026-10-14T12:00:00Z'}],metrics:{completed:0,waitingApproval:0,failed:0,attentionResolved:0,feedback:0}});
 if(url.pathname==='/api/bob/intelligence/automation')return send({version,policy,rules,capabilities:[{id:'job.readiness',module:'jobs',risk:0,writePermissions:[],requiredInputs:['jobId'],isDraft:false},{id:'job.schedule',module:'jobs',risk:2,writePermissions:['jobs.write','calendar.write'],requiredInputs:['jobId','expectedVersion'],isDraft:false}],suggestions:[]});
 if(req.method!=='GET'){let raw='';for await(const c of req)raw+=c;let body=raw?JSON.parse(raw):{};
 if(url.pathname.endsWith('/workflows/from-attention')){workflow={id:'aaaaaaaa-0000-4000-8000-000000000099',goal:'Create a job readiness task',status:'ready',createdUtc:'2026-10-14T12:00:00Z',updatedUtc:'2026-10-14T12:00:00Z',steps:[{capabilityId:'job.task',status:'pending',rationale:'Create an internal task in the authoritative Job workflow',input:{jobId:source.entityId,expectedVersion:'1'}}]};return send(workflow);}
 if(workflow&&url.pathname.endsWith('/advance')){workflow.status='waiting_approval';workflow.steps[0].status='waiting_approval';return send(workflow);}
 if(workflow&&url.pathname.endsWith('/approve')){workflow.status='completed';workflow.steps[0].status='completed';return send(workflow);}
 if(url.pathname.endsWith('/command'))return send({intent:'review',status:'ready',message:'Review these source facts. No business data changed.',matches:[fact],actions:[]});
 if(url.pathname.endsWith('/dismiss')){dismissed=true;return send({dismissed:true});}
 if(url.pathname.endsWith('/feedback'))return send({recorded:true});
 if(url.pathname.endsWith('/policy')){policy=body.policy;version=String(Number(version)+1);return send({updated:true});}
 if(url.pathname.endsWith('/rules')){rules.push(body.rule);version=String(Number(version)+1);return send({updated:true});}}
 return send({});
}).listen(5620,'127.0.0.1');
