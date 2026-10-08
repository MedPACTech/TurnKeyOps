#!/usr/bin/env python3
"""Explicit local-only demo seed. Requires an existing authorized API bearer in env.
No credentials are persisted, no grants created, and no delivery endpoint is called.
Run without --apply for a plan. Existing deterministic demo IDs are left untouched.
"""
import argparse,base64,json,os,urllib.request,urllib.parse,urllib.error,uuid
p=argparse.ArgumentParser();p.add_argument('--apply',action='store_true');p.add_argument('--tenant-slug',choices=['bdr','thinkpink','carlzipf'],required=True);a=p.parse_args()
base=os.environ.get('TKO_DEMO_API_URL','http://127.0.0.1:5188').rstrip('/')
assert urllib.parse.urlparse(base).hostname in ('localhost','127.0.0.1','::1'),'Only a local API is allowed'
token=os.environ['TKO_DEMO_ACCESS_TOKEN'];claims=json.loads(base64.urlsafe_b64decode(token.split('.')[1]+'==='));tenant=claims['tenant_id']
expected={'bdr':'7d40ea6c-313f-4f53-bf7d-5d1ecb9cc50b','thinkpink':'88888888-8888-8888-8888-888888888882','carlzipf':'88888888-8888-4888-8888-888888888883'}
assert tenant==expected[a.tenant_slug],'Session tenant must match the selected demo tenant'
def call(path,data=None,method=None,auth=True):
 headers={'Accept':'application/json'}
 if auth:headers['Authorization']='Bearer '+token
 if data is not None:headers['Content-Type']='application/json'
 req=urllib.request.Request(base+'/api/'+path,data=None if data is None else json.dumps(data).encode(),headers=headers,method=method)
 try:
  with urllib.request.urlopen(req,timeout=60) as r:
   v=json.load(r);return v.get('data',v)
 except urllib.error.HTTPError as e:
  detail=e.read().decode();raise RuntimeError(f'{path}: HTTP {e.code}: {detail[:1200]}') from None
scenarios=[('measure','01 — Confirm site measurements'),('ready','02 — Ready to send'),('approval','03 — Discount needs approval'),('sent','04 — Customer chooses repair or replacement'),('accepted','05 — Signed scope linked to Job'),('revision','06 — Revision after customer feedback')]
existing={x['id']:x for x in call('leads')['leads']}
if not a.apply:
 for key,title in scenarios:
  id=str(uuid.uuid5(uuid.NAMESPACE_URL,f'tko-local-estimates-demo-v1/{tenant}/{key}'));print(('KEEP' if id in existing else 'CREATE'),'DEMO '+title)
 raise SystemExit()
config=call('estimate-workspace/configuration');policy=config['policy']
items=[('service','Service visit','service','visit',95,35),('labor','Installation labor','labor','hour',110,48),('lock','Commercial lockset','material','each',240,115),('closer','Door closer upgrade','material','each',185,85),('opening','Replacement opening allowance','allowance','each',1450,850)]
changed=False
for key,name,kind,unit,price,cost in items:
 id='demo-v1-'+key
 if not any(x['id']==id for x in policy['catalog']):
  policy['catalog'].append(dict(id=id,name='DEMO ONLY — '+name,kind=kind,unit=unit,tradeProfile='doors-locks',unitPrice=price,unitCost=cost,enabled=True,taxable=kind!='labor',sample=False));changed=True
# Defaults are added only to otherwise unconfigured local settings.
if policy['taxPercent'] is None:policy['taxPercent']=8;changed=True
if not policy['terms']:policy['terms']='DEMO ONLY. Fictional walkthrough; not a commercial offer or contract. Rates and 8% tax are illustrative.';policy['maxDiscountPercent']=10;policy['depositPercent']=20;changed=True
if changed:call('estimate-workspace/configuration',{'expectedVersion':config['settings'].get('version') or '', 'policy':policy},'PUT')
def selection(key,qty=1,qkey='manual'):
 return dict(catalogId='demo-v1-'+key,quantity=qty,quantityKey=qkey,confirmed=True,overrideReason='')
def option(id,name,items,required=True,group=''):
 return dict(id=id,name=name,items=items,required=required,exclusiveGroup=group)
for key,title in scenarios:
 id=str(uuid.uuid5(uuid.NAMESPACE_URL,f'tko-local-estimates-demo-v1/{tenant}/{key}'))
 if id in existing and (existing[id].get('estimateId') or existing[id]['stage'] not in ('NEW','QUALIFIED')):print('Kept existing demo:',title,id);continue
 scope='DEMO ONLY — Fictional training site. Service 2 commercial door openings, replace 2 locksets and allow 4 labor hours. Confirm access and hardware before real work.'
 lead=call('leads',dict(id=id,title='DEMO '+title,contactName='Demo Customer '+key.title(),companyName='Demo Workshop — fictional',email=f'{key}@example.invalid',siteAddress='DEMO '+title+' — fictional training site',requestedWork=scope,tradeProfile='doors-locks',service='Door hardware',propertyType='commercial',source='Referral',referralName='DEMO facilities partner',qualification={'openingCount':'2 (demo site notes)','hardware':'Commercial locksets; confirm in field'},attribution={'demoDataset':'estimates-v1-local'},createCustomer=True))
 lead=call(f'leads/{id}/stage',dict(expectedVersion=lead['version'],stage='QUALIFIED',reason='Demo qualification complete'))
 lead=call(f'leads/{id}/estimate',dict(expectedVersion=lead['version']))
 w=call(f'estimate-workspace/{id}');doc=w['packet']['document']
 doc.update(scope=scope,terms=policy['terms'],exclusions='DEMO: concealed frame damage and electrical work are excluded. No actual work is authorized.',timing='DEMO: coordinate a fictional site visit next week.',inputs={'openingCount':{'value':2,'confirmed':key!='measure'},'laborHours':{'value':4,'confirmed':key!='measure'}},options=[option('base','Service and lockset replacement',[selection('service'),selection('lock',qkey='openingCount'),selection('labor',qkey='laborHours')]),option('upgrade','Optional closer upgrade',[selection('closer')],False)])
 if key=='sent':doc['options']=[option('repair','Repair existing openings',[selection('service'),selection('labor',qkey='laborHours')],False,'repair-or-replace'),option('replace','Replace complete openings',[selection('opening',2),selection('labor',qkey='laborHours')],False,'repair-or-replace')]
 w=call(f'estimate-workspace/{id}',dict(expectedVersion=w['packet']['version'],document=doc,discountPercent=15 if key=='approval' else 0),'PUT')
 if key in ('sent','accepted','revision'):
  if w['state']=='NEEDS_APPROVAL':w=call(f'estimate-workspace/{id}/approve',dict(expectedVersion=w['packet']['version']))
  w=call(f'estimate-workspace/{id}/issue',dict(expectedVersion=w['packet']['version']))
  packet=w['packet'];access=urllib.parse.parse_qs(urllib.parse.urlparse(packet['delivery']['reviewUrl']).query)['token'][0]
  if key=='accepted':
   call(f'public/quote-estimates/{a.tenant_slug}/{id}/approve',dict(accessToken=access,signerPrintedName='DEMO SIGNER — simulated acceptance',intentToSign=True,consentVersion=packet['approvalConsentVersion'],revisionNumber=packet['revisionNumber'],documentHash=packet['documentHash'],selectedOptionIds=['base','upgrade']),auth=False)
   w=call(f'estimate-workspace/{id}');call(f'estimate-workspace/{id}/job',dict(expectedVersion=w['packet']['version']))
  elif key=='revision':
   call(f'public/quote-estimates/{a.tenant_slug}/{id}/request-changes',dict(accessToken=access,revisionNumber=packet['revisionNumber'],documentHash=packet['documentHash'],responseNote='DEMO feedback: please confirm the access window and omit the closer upgrade.'),auth=False)
   w=call(f'estimate-workspace/{id}');w=call(f'estimate-workspace/{id}/revision',dict(expectedVersion=w['packet']['version']))
 print('Created demo:',title,id)
print('Done. No delivery endpoint was called. Open /'+a.tenant_slug+'/admin/estimates.')
