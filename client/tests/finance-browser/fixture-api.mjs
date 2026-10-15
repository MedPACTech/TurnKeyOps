// Isolated UI contract fixture. Never imported by application code or deployed.
import http from 'node:http';
import {payrollRequest,resetPayroll} from '../payroll-browser/data.mjs';
import {constructionFixture,resetConstructionFixture} from './construction-data.mjs';
import {financeFixture,resetFinanceFixture} from './data.mjs';
const modules=['dashboard','bob','calendar','jobs','requests','estimates','invoices','contacts','users','settings','billing','finance','payroll'];
const owner={id:'11111111-1111-1111-1111-111111111111',firstName:'Existing',lastName:'Owner',profileTypes:['employee'],modulePermissions:null,effectivePermissions:[],role:'owner',membershipId:'owner-membership',isOwner:true,isActive:true,version:'1'};
const otherOwner={...owner,id:'55555555-5555-5555-5555-555555555555',firstName:'Second',membershipId:'second-owner'};
let people=[owner,otherOwner];
let paymentIntents=[],retainedBalance=50,opsTimeState='SUBMITTED';
let portalVersion=1, portalGrants=[];
const seedCustomer={id:'22222222-2222-2222-2222-222222222222',firstName:'Customer',lastName:'Record',email:'customer@example.invalid'};
let customerRecords=[{...seedCustomer}];
http.createServer(async(req,res)=>{
 res.setHeader('Access-Control-Allow-Origin','http://127.0.0.1:5611');
 res.setHeader('Access-Control-Allow-Headers','Authorization, Content-Type, X-Tenant-Id, X-Time-Zone');
 if(req.method==='OPTIONS'){res.writeHead(204);res.end();return;}
 const url=new URL(req.url,'http://127.0.0.1');
 const send=(data,status=200)=>{res.writeHead(status,{'Content-Type':'application/json'});res.end(JSON.stringify(status<400?{success:true,data}:{success:false,...(data??{})}));};
 if(url.pathname==='/advance-construction'){const state=constructionFixture();state.version=String(Number(state.version)+1);return send('OK');}
 if(url.pathname==='/health') return send('OK');
 if(url.pathname==='/reset'){resetPayroll();resetFinanceFixture();resetConstructionFixture();paymentIntents=[];retainedBalance=50;opsTimeState='SUBMITTED';people=[{...owner},{...otherOwner}];portalVersion=1;portalGrants=[];customerRecords=[{...seedCustomer}];return send('OK');}
 if(url.pathname.startsWith('/api/public/tenant-settings/'))return send({values:{},version:null});
 if(url.pathname==='/api/auth/startotp') return send({challengeId:'fixture-multi',channel:'email'});
 if(url.pathname==='/api/auth/completeotp') return send({requiresTenantSelection:true,preTenantToken:`${Buffer.from('{}').toString('base64url')}.${Buffer.from(JSON.stringify({sub:'11111111-1111-1111-1111-111111111111',pt:1,exp:Math.floor(Date.now()/1000)+600})).toString('base64url')}.fixture`});
 if(!req.headers.authorization?.startsWith('Bearer ')) return send(null,401);
 if(url.pathname==='/api/auth/session') return send({valid:true});
 const claims=JSON.parse(Buffer.from(req.headers.authorization.split('.')[1],'base64url').toString());
 if(url.pathname==='/api/auth/workspaces') {
  const ids=['7d40ea6c-313f-4f53-bf7d-5d1ecb9cc50b','88888888-8888-4888-8888-888888888883'];
  if(req.method==='GET') return send(ids.map(tenantId=>({tenantId})));
  let raw=''; for await(const chunk of req) raw+=chunk;
  const {tenantId}=JSON.parse(raw);
  if(!ids.includes(tenantId)) return send(null,403);
  const token=`${Buffer.from('{}').toString('base64url')}.${Buffer.from(JSON.stringify({...claims,tid:tenantId,role:['owner'],exp:Math.floor(Date.now()/1000)+3600})).toString('base64url')}.fixture`;
  return send({accessToken:token,refreshToken:'new-workspace-refresh'});
 }
 if(url.pathname.startsWith('/api/payroll'))return payrollRequest(req,url,claims,send,res);
 if(url.pathname==='/api/finance/migration/template'&&req.method==='GET')return send({request:{id:'aaaaaaaa-0000-4000-8000-000000000001',expectedVersion:'1',expectedSupplyVersion:'s1',sourceSystem:'Acumatica',exportId:'',sourceFileSha256:'',cutoverDate:'0001-01-01',strategy:'OPEN_ITEMS_AND_BALANCES',reason:'',newAccounts:[],accounts:[],entities:[],trialBalance:[],openItems:[],jobCosts:[],commitments:[],inventory:[],controls:{ar:0,ap:0,jobCosts:0,commitments:0,inventory:0,cash:{operating:0}}},accountChoices:[{code:'1000',name:'Operating cash'}],steps:['Map source accounts and tenant records.','Preview and reconcile source totals.']});
 if(url.pathname==='/api/finance/operations'&&req.method==='GET')return send({version:'1',employees:[{employeeId:owner.id,membershipId:owner.membershipId}],apRetainage:[{id:'retained-bill',number:'SUB-1',vendorId:'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',gross:500,net:450,withheld:50,held:retainedBalance}],state:{apRetainageAccount:'',reversalPolicy:{enabled:false},reversalAttempts:[],time:claims.fixtureTime?[{id:'time-one',employeeId:owner.id,jobId:financeFixture().jobChoices[0].id,date:'2026-09-15',hours:8,status:opsTimeState}]:[],rates:[],accruals:[],recurring:[],closeTasks:[],bankSplits:[],assets:[],vendors:[]},close:[{id:'2026-09',issues:['Time requires review.']}],vendorPayments1099:[]});
 if(url.pathname==='/api/finance/construction/payments'&&req.method==='GET')return send({version:constructionFixture().version,configured:!!claims.fixturePayments,intents:paymentIntents,receipts:[]});
 if(url.pathname==='/api/finance/construction'&&req.method==='GET')return claims.fixtureRestricted?send(null,403):send({...constructionFixture(),canControl:!claims.fixtureWriter,canWrite:!claims.fixtureReader});
 if(url.pathname==='/api/finance'&&req.method==='GET'){const w=financeFixture();if(claims.fixturePayments)w.state.openItems.push({id:'ar-app-1',kind:'AR',partyId:'22222222-2222-2222-2222-222222222222',jobId:w.jobChoices[0].id,number:'APP-1',total:270,date:'2026-09-30',dueDate:'2026-10-30',journalId:'journal-app-1'});return claims.fixtureRestricted?send(null,403):send(w);}
 if(url.pathname==='/api/people/capabilities')return send({canDeleteUsers:claims.role.includes('owner')});
 if(url.pathname==='/api/my-module-access'){
  return send(claims.fixtureContacts ? ['contacts.read','contacts.write'] : claims.fixtureRestricted ? ['users.read'] : modules.flatMap(m=>[m+'.read',m+'.write']));
 }
 if(url.pathname==='/api/quote-requests' || url.pathname==='/api/quote-estimates')return send([]);
 if(url.pathname==='/api/admin/estimate-defaults')return send({});
 if(url.pathname==='/api/Customers/search')return send(customerRecords);
 if(url.pathname==='/api/people/customers'||url.pathname==='/api/contacts/customers')return send(customerRecords.map(c=>({...c,name:[c.firstName,c.lastName].join(' ')})));
 if(url.pathname==='/api/TenantMembership'||url.pathname==='/api/Invite')return send([]);
 if(url.pathname==='/api/contacts' && req.method==='GET')return send(people.filter(p=>p.isActive&&p.profileTypes.some(t=>['customer','vendor'].includes(t))));
 if(url.pathname.startsWith('/api/contacts/')&&url.pathname.endsWith('/work'))return send({jobs:[],invoices:[],canViewJobs:!claims.fixtureContacts,canViewInvoices:!claims.fixtureContacts});
 if(url.pathname==='/api/people' && req.method==='GET')return send(people);
 let raw='';for await(const chunk of req)raw+=chunk;
 const input=raw?JSON.parse(raw):{};
 if(url.pathname==='/api/finance/construction/payments/command'&&req.method==='POST'){if(!claims.fixturePayments)return send({message:'Provider not configured'},400);if(input.action==='create'){if(input.openItemId!=='ar-app-1'||!input.cashAccountId)return send({message:'Choose construction AR and cash'},400);paymentIntents.push({id:input.id,openItemId:input.openItemId,amount:270,status:'READY',url:'https://checkout.stripe.com/c/pay/test'});}else if(input.action==='cancel')paymentIntents.find(i=>i.id===input.targetId).status='CANCELLED';return send({saved:true});}
 if(url.pathname==='/api/finance/construction/command'&&req.method==='POST')return constructionFixture(input).error?send({message:'Finance changed'},409):send({saved:true});
 if(url.pathname==='/api/finance/migration/preview')return send({fingerprint:'preview-fingerprint',canImport:false,errors:['Source AR control differs by 10.00.'],warnings:['Confirm the export cutover freeze.'],checks:[{name:'AR',imported:110,source:100,difference:10}],journalLines:2,openItems:1,jobCosts:0,commitments:0,inventory:0});
 if(url.pathname==='/api/finance/bob')return send({resultJson:JSON.stringify({Tool:input.toolKey,AsOf:input.input.asOf,Title:'Financial close review',Summary:'1 source-backed finding.',Findings:[{Severity:'blocking',Message:'2026-09: Unposted vendor bills require review.',SourceIds:['bill:bill-one'],SuggestedAction:'Review source bill approval and receipt exceptions.'}],Limitations:['No messages, payments or postings were made.']})});
 if(url.pathname==='/api/finance/operations/command'){if(input.targetId==='time-one'){if(input.action!=='reject-time')return send({message:'Reject action required for this test'},400);opsTimeState='REJECTED';}return send({saved:true});}
 if(url.pathname==='/api/finance/command'&&input.action==='release-ap-retainage'){if(input.amount>retainedBalance)return send({message:'Release exceeds retainage'},400);retainedBalance-=input.amount;return send({saved:true});}
 if(url.pathname==='/api/finance/command'){const result=financeFixture(input);return result.error?send({message:result.error},result.status):send(result.data);}
 if(url.pathname==='/api/Customers'&&['PUT','POST'].includes(req.method)){const c={...input,id:input.id??crypto.randomUUID(),dateUpdated:new Date().toISOString()};customerRecords=customerRecords.filter(v=>v.id!==c.id).concat(c);return send(c);}
 if(url.pathname.startsWith('/api/admin/portal')) {
  if(!claims.role.includes('owner'))return send(null,403);
  const customer='22222222-2222-2222-2222-222222222222';
  if(url.pathname==='/api/admin/portal')return send({version:String(portalVersion),configuration:{enabled:true},grants:portalGrants,people:people.filter(p=>p.isActive&&p.customerId).map(p=>({...p,userId:p.id}))});
  if(url.pathname.includes('/workspace/'))return send({work:[{kind:'job',id:'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',title:'Front entry repair',siteId:'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',siteName:'Main office'}]});
  if(input.expectedVersion!==String(portalVersion))return send(null,409);
  if(url.pathname.endsWith('/grants')){portalGrants.push({...input.grant,id:crypto.randomUUID(),revoked:false});portalVersion++;return send(null);}
  if(url.pathname.includes('/contacts/')&&url.pathname.endsWith('/revoke')){portalGrants.forEach(g=>{if(g.userId===url.pathname.split('/')[5])g.revoked=true;});portalVersion++;return send(null);}
  if(url.pathname.includes('/grants/')&&url.pathname.endsWith('/revoke')){const g=portalGrants.find(g=>g.id===url.pathname.split('/')[5]);if(g)g.revoked=true;portalVersion++;return send(null);}
 }
 if(['/api/people','/api/contacts'].includes(url.pathname) && req.method==='POST'){
  const p={...input,id:'33333333-3333-3333-3333-333333333333',effectivePermissions:[],isOwner:false,isActive:true,version:'1'};people.push(p);return send(p);
 }
 const id=url.pathname.split('/')[3];const person=people.find(p=>p.id===id);
 if(person && req.method==='PUT') {Object.assign(person,input,{version:String(Number(person.version)+1)});return send(person);}
 if(person && (req.method==='DELETE'||url.pathname.endsWith('/delete'))){person.isActive=false;person.version=String(Number(person.version)+1);return send(null);}
 if(person && url.pathname.endsWith('/restore')){person.isActive=true;return send(null);}
 return send(null,404);
}).listen(5610,'127.0.0.1');
