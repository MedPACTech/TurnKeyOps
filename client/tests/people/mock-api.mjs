// Isolated UI contract fixture. Never imported by application code or deployed.
import http from 'node:http';
const modules=['dashboard','bob','calendar','jobs','requests','estimates','invoices','contacts','users','settings','billing'];
const owner={id:'11111111-1111-1111-1111-111111111111',firstName:'Existing',lastName:'Owner',profileTypes:['employee'],modulePermissions:null,effectivePermissions:[],role:'owner',membershipId:'owner-membership',isOwner:true,isActive:true,version:'1'};
const otherOwner={...owner,id:'55555555-5555-5555-5555-555555555555',firstName:'Second',membershipId:'second-owner'};
let people=[owner,otherOwner];
http.createServer(async(req,res)=>{
 res.setHeader('Access-Control-Allow-Origin','http://127.0.0.1:5191');
 res.setHeader('Access-Control-Allow-Headers','Authorization, Content-Type, X-Tenant-Id, X-Time-Zone');
 if(req.method==='OPTIONS'){res.writeHead(204);res.end();return;}
 const url=new URL(req.url,'http://127.0.0.1');
 const send=(data,status=200)=>{res.writeHead(status,{'Content-Type':'application/json'});res.end(JSON.stringify({success:status<400,data}));};
 if(url.pathname==='/health') return send('OK');
 if(url.pathname==='/reset'){people=[{...owner},{...otherOwner}];return send('OK');}
 if(url.pathname.startsWith('/api/public/tenant-settings/'))return send({values:{},version:null});
 if(!req.headers.authorization?.startsWith('Bearer ')) return send(null,401);
 if(url.pathname==='/api/auth/session') return send({valid:true});
 const claims=JSON.parse(Buffer.from(req.headers.authorization.split('.')[1],'base64url').toString());
 if(url.pathname==='/api/people/capabilities')return send({canDeleteUsers:claims.role.includes('owner')});
 if(url.pathname==='/api/my-module-access'){
  return send(claims.fixtureContacts ? ['contacts.read','contacts.write'] : claims.fixtureRestricted ? ['users.read'] : modules.flatMap(m=>[m+'.read',m+'.write']));
 }
 if(url.pathname==='/api/quote-requests' || url.pathname==='/api/quote-estimates')return send([]);
 if(url.pathname==='/api/admin/estimate-defaults')return send({});
 if(url.pathname==='/api/people/customers'||url.pathname==='/api/contacts/customers')return send([{id:'22222222-2222-2222-2222-222222222222',name:'Customer record'}]);
 if(url.pathname==='/api/TenantMembership'||url.pathname==='/api/Invite')return send([]);
 if(url.pathname==='/api/contacts' && req.method==='GET')return send(people.filter(p=>p.isActive&&p.profileTypes.some(t=>['customer','vendor'].includes(t))));
 if(url.pathname.startsWith('/api/contacts/')&&url.pathname.endsWith('/work'))return send({jobs:[],invoices:[],canViewJobs:!claims.fixtureContacts,canViewInvoices:!claims.fixtureContacts});
 if(url.pathname==='/api/people' && req.method==='GET')return send(people);
 let raw='';for await(const chunk of req)raw+=chunk;
 const input=raw?JSON.parse(raw):{};
 if(['/api/people','/api/contacts'].includes(url.pathname) && req.method==='POST'){
  const p={...input,id:'33333333-3333-3333-3333-333333333333',effectivePermissions:[],isOwner:false,isActive:true,version:'1'};people.push(p);return send(p);
 }
 const id=url.pathname.split('/')[3];const person=people.find(p=>p.id===id);
 if(person && req.method==='PUT') {Object.assign(person,input,{version:String(Number(person.version)+1)});return send(person);}
 if(person && (req.method==='DELETE'||url.pathname.endsWith('/delete'))){person.isActive=false;person.version=String(Number(person.version)+1);return send(null);}
 if(person && url.pathname.endsWith('/restore')){person.isActive=true;return send(null);}
 return send(null,404);
}).listen(5190,'127.0.0.1');
