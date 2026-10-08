import {json,type RequestEvent} from '@sveltejs/kit';
export const GET=({url}:RequestEvent)=>{
 const scope=url.pathname.replace('/manifest.webmanifest','');
 return json({id:scope,name:'TurnKeyOps Leads',short_name:'Leads',start_url:scope,scope,display:'standalone',background_color:'#f6f7f9',theme_color:'#0f766e',icons:[{src:'/leads/icon.svg',sizes:'any',type:'image/svg+xml',purpose:'any'}]}, {headers:{'Content-Type':'application/manifest+json','Cache-Control':'private, no-store'}});
};
