#!/usr/bin/env python3
"""Refresh the explicit local walkthrough only; never sends communications."""
import argparse,base64,json,os,uuid,urllib.request,urllib.parse
from pathlib import Path
from datetime import datetime,timedelta,timezone
p=argparse.ArgumentParser();p.add_argument('--apply',action='store_true');args=p.parse_args()
base=os.environ.get('TKO_DEMO_API_URL','http://127.0.0.1:5188').rstrip('/')
assert urllib.parse.urlparse(base).hostname in ('localhost','127.0.0.1','::1'),'Local API only'
token=os.environ['TKO_DEMO_ACCESS_TOKEN'];claims=json.loads(base64.urlsafe_b64decode(token.split('.')[1]+'==='));tenant=claims['tenant_id']
assert tenant=='88888888-8888-4888-8888-888888888883','This dataset belongs to the local Carl Zipf walkthrough'
def call(path,data=None,method=None):
 headers={'Authorization':'Bearer '+token,'Content-Type':'application/json'}
 with urllib.request.urlopen(urllib.request.Request(base+'/api/'+path,data=None if data is None else json.dumps(data).encode(),headers=headers,method=method),timeout=30) as r:
  result=json.load(r);return result.get('data',result)
fields='title customerId contactName companyName email phone siteAddress requestedWork tradeProfile service propertyType source ownerProfileId ownerMembershipId estimatedValue referralContactId referralName nextAction followUpAtUtc qualification attribution'.split()
rows=json.loads((Path(__file__).parent/'fixtures/leads-demo.json').read_text());existing={x['id']:x for x in call('leads')['leads']}
for index,row in enumerate(rows):
 id=str(uuid.uuid5(uuid.NAMESPACE_URL,f"tko-local-estimates-demo-v1/{tenant}/{row['key']}"));old=existing.get(id)
 if old:assert old.get('attribution',{}).get('demoDataset')=='estimates-v1-local','Refusing to edit a non-demo Lead'
 print('UPDATE' if old else 'CREATE',row['propertyType'],row['title'])
 if not args.apply:continue
 dto={k:old[k] for k in fields if old and k in old}
 dto.update({k:v for k,v in row.items() if k in fields});dto.update(title=row['title']+' · Sample',tradeProfile='doors-locks',email=row['key']+'@example.invalid',phone=f'+161455501{index+10:02d}',referralName=row.get('referralName',''),followUpAtUtc=(datetime.now(timezone.utc)+timedelta(days=1+index%5)).isoformat(),attribution={**(old.get('attribution',{}) if old else {}),'demoDataset':'estimates-v1-local','sampleData':'Fictional walkthrough; no customer contact'})
 if old and old.get('estimateId'):
  packet=call('estimate-workspace/'+old['estimateId'])['packet'];dto['estimatedValue']=(packet.get('approvalSignature') or {}).get('total') or packet['totals']['estimatedTotal'] or max([x['total'] for x in (packet.get('pricing') or {}).get('options',[])],default=0)
 if old:dto['expectedVersion']=old['version'];saved=call('leads/'+id,dto,'PUT')
 else:
  saved=call('leads',{**dto,'id':id,'createCustomer':False})
  if row.get('stage','NEW')!='NEW':saved=call(f'leads/{id}/stage',dict(expectedVersion=saved['version'],stage=row['stage'],reason='Sample scenario'))
  # Restore the concrete next action after the canonical transition sets its default.
  saved=call('leads/'+id,{**dto,'expectedVersion':saved['version']},'PUT')
 if not any(x.get('text')=='Sample scenario: '+row['note'] for x in saved.get('activity',[])):
  call(f'leads/{id}/activity',dict(expectedVersion=saved['version'],type='note',text='Sample scenario: '+row['note']))
print('Completed local sample refresh.' if args.apply else 'Plan only; add --apply to update sample records.')
