// Quoting alone does not prevent spreadsheet formula execution. Keep numeric negative
// values numeric while neutralizing formula text hidden behind whitespace/control chars.
export const financeCsvCell=(value:unknown)=>{
 let text=String(value??'');
 if(typeof value!=='number'&&(/^[\s\u0000-\u001f\ufeff]*[=+@-]/u.test(text)||/^[\t\r\n]/u.test(text)))text="'"+text;
 return `"${text.replaceAll('"','""')}"`;
};
