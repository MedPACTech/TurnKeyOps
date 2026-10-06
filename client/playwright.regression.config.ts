// Same release gates on isolated storage/ports; never attach to a developer's existing Azurite.
import original from './playwright.config';
import {defineConfig} from '@playwright/test';
const connection='DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:11000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:11001/devstoreaccount1;TableEndpoint=http://127.0.0.1:11002/devstoreaccount1';
process.env.PLAYWRIGHT_API_URL='http://127.0.0.1:5288';
const servers=original.webServer as any[];
export default defineConfig({...original,use:{...original.use,baseURL:'http://127.0.0.1:5289'},webServer:[
 {...servers[0],command:'azurite --silent --location /tmp/turnkeyops-leads-regression-azurite --blobHost 127.0.0.1 --blobPort 11000 --queueHost 127.0.0.1 --queuePort 11001 --tableHost 127.0.0.1 --tablePort 11002',url:'http://127.0.0.1:11000/devstoreaccount1'},
 {...servers[1],url:'http://127.0.0.1:5288',env:{...servers[1].env,ASPNETCORE_URLS:'http://127.0.0.1:5288',AzureStorageSettings__ConnectionString:connection,IBeam__Repositories__AzureTables__ConnectionString:connection}},{...servers[2],command:'npm run dev -- --host 127.0.0.1 --port 5289',url:'http://127.0.0.1:5289/bdr/public',env:{...servers[2].env,TKO_API_BASE_URL:'http://127.0.0.1:5288',PUBLIC_TKO_API_BASE_URL:'http://127.0.0.1:5288'}}
]});
