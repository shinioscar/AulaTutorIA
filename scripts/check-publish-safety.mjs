// Read-only publication checks. Never print private values or matched content.
import fs from 'node:fs';
import { spawnSync } from 'node:child_process';
function git(args,input){const r=spawnSync('git',args,{input,encoding:null,maxBuffer:256*1024*1024});if(r.status!==0)throw new Error('Git safety inspection failed.');return r.stdout;}
const tracked=git(['ls-files','-z']).toString('utf8').split('\0').filter(Boolean);
const forbidden=tracked.filter(p=>!p.endsWith('.env.example')&&(/(^|\/)\.env($|\.)/.test(p)||/\.(pem|pfx|key|p12|p8|dump|backup|bak|keystore|jks)$/i.test(p)||/^(\.cache|artifacts|backups)\//.test(p)));
if(tracked.some(p=>/(^|\/)\.pg(pass|_service\.conf)$/.test(p)))throw new Error('Private PostgreSQL configuration is tracked.');
if(forbidden.length)throw new Error('Private file types are tracked. Publication stopped; inspect paths locally.');
if(fs.existsSync('.env')&&spawnSync('git',['check-ignore','-q','--','.env']).status!==0)throw new Error('.env must be ignored.');
const privateValues=fs.existsSync('.env')?fs.readFileSync('.env','utf8').split(/\r?\n/).flatMap(line=>{const m=line.match(/^(?:[A-Z_]*API_KEY|POSTGRES_PASSWORD)=(.*)$/);return m&&m[1].length>=8?[Buffer.from(m[1])]:[];}):[];
function hasSecret(content){const text=content.toString('utf8');return privateValues.some(v=>content.includes(v))||/-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----/.test(text)||/\bgsk_[A-Za-z0-9]{30,}\b/.test(text)||/\bAIza[A-Za-z0-9_-]{30,}\b/.test(text);}
let current=0;
for(const file of tracked){if(!fs.existsSync(file)||/\.(png|jpe?g|gif|pdf|woff2?)$/i.test(file))continue;current++;if(hasSecret(fs.readFileSync(file)))throw new Error('Potential secret in current tracked content. Publication stopped; no matched content printed.');}
const objects=[...new Set(git(['rev-list','--objects','--all']).toString('utf8').trim().split('\n').filter(l=>l.includes(' ')&&!/\.(png|jpe?g|gif|pdf|woff2?)$/i.test(l)).map(l=>l.split(' ')[0]))];
let scanned=0;
for(let batch=0;batch<objects.length;batch+=100){
 const data=git(['cat-file','--batch'],objects.slice(batch,batch+100).join('\n')+'\n');let offset=0;
 while(offset<data.length){const end=data.indexOf(10,offset);if(end<0)throw new Error('Invalid Git object inspection.');const header=data.subarray(offset,end).toString('ascii').split(' ');const size=Number(header[2]);if(!Number.isFinite(size))throw new Error('Invalid Git object size.');const content=data.subarray(end+1,end+1+size);offset=end+size+2;if(header[1]!=='blob')continue;scanned++;
 if(hasSecret(content))throw new Error('Potential secret in Git history. Publication stopped; no matched content printed.');
 }
}
console.log(JSON.stringify({status:'passed',trackedFiles:tracked.length,currentTextFiles:current,historicalTextBlobs:scanned,configuredSecretMatches:0,scope:'Current tracked paths/content and all reachable historical text blobs; binary visual evidence reviewed separately.'}));
