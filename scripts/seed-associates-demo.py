#!/usr/bin/env python3
"""Create local employee profiles and assign tagged demo Leads. Never invites or sends messages."""
import argparse,base64,json,os,urllib.request,urllib.parse
p=argparse.ArgumentParser();p.add_argument('--apply',action='store_true');args=p.parse_args()
base=os.environ.get('TKO_DEMO_API_URL','http://127.0.0.1:5188').rstrip('/')
assert urllib.parse.urlparse(base).hostname in ('localhost','127.0.0.1','::1'),'Local API only'
token=os.environ['TKO_DEMO_ACCESS_TOKEN'];claims=json.loads(base64.urlsafe_b64decode(token.split('.')[1]+'==='))
assert claims['tenant_id']=='88888888-8888-4888-8888-888888888883','Local Carl Zipf walkthrough only'
def call(path,data=None,method=None):
 req=urllib.request.Request(base+'/api/'+path,data=None if data is None else json.dumps(data).encode(),headers={'Authorization':'Bearer '+token,'Content-Type':'application/json'},method=method)
 with urllib.request.urlopen(req,timeout=30) as r:
  result=json.load(r);return result.get('data',result)
samples=[('Jordan','Ellis','Residential service specialist','Residential'),('Morgan','Chen','Commercial account specialist','Commercial'),('Avery','Patel','Estimator','Residential'),('Casey','Brooks','Commercial project coordinator','Commercial'),('Riley','Torres','Field service associate','Residential'),('Cameron','Reed','Commercial estimator','Commercial')]
people=call('people');by_team={}
for first,last,title,team in samples:
 email=f'{first.lower()}.{last.lower()}@associates.example.invalid'
 found=next((p for p in people if p.get('contactEmail')==email),None)
 if found:
  assert found.get('team')==team+' · Sample' and found['isActive'] and 'employee' in found['profileTypes'],'Sample profile was changed; review before reusing'
 print('REUSE' if found else 'CREATE',first,last,team)
 if not args.apply:continue
 if not found:found=call('people',dict(firstName=first,lastName=last,loginIdentifier=email,contactEmail=email,profileTypes=['employee'],companyName='Carl Zipf · Sample',title=title,team=team+' · Sample',modulePermissions=[]))
 by_team.setdefault(team.lower(),[]).append(found['id'])
leads=sorted((l for l in call('leads')['leads'] if l.get('attribution',{}).get('demoDataset')=='estimates-v1-local'),key=lambda l:l['title'])
counts={}
for lead in leads:
 team=lead.get('propertyType');assert team in ('residential','commercial')
 index=counts.get(team,0);counts[team]=index+1
 print('ASSIGN',lead['title'],team)
 if not args.apply:continue
 profile=by_team[team][index%len(by_team[team])]
 if lead.get('ownerProfileId')==profile:continue
 call('leads/'+lead['id'],{**lead,'ownerProfileId':profile,'ownerMembershipId':None,'expectedVersion':lead['version']},'PUT')
print('Completed local associates and assignments.' if args.apply else 'Plan only; add --apply to create and assign.')
