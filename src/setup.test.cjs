const {test}=require('node:test');
const assert=require('node:assert/strict');
const vm=require('node:vm');
const fs=require('node:fs');
const path=require('node:path');
const backgroundCode=fs.readFileSync(path.join(__dirname,'../extension/background.js'),'utf8');
const optionsCode=fs.readFileSync(path.join(__dirname,'../extension/options.js'),'utf8');
const fixtureKey='A'.repeat(48);
function background(prepared,existing) {
  let installed,opened=0;
  const store={token:existing};
  const context={AFTERGLOW_SETUP_TOKEN:prepared,importScripts:name=>assert.equal(name,'pairing.js'),chrome:{storage:{local:{get:async()=>({...store}),set:async value=>Object.assign(store,value)}},action:{onClicked:{addListener:()=>{}}},runtime:{onInstalled:{addListener:fn=>installed=fn},onMessage:{addListener:()=>{}},openOptionsPage:async()=>{opened++;}}}};
  vm.runInNewContext(backgroundCode,context);
  return {store,install:reason=>installed({reason}),opened:()=>opened};
}
test('first install saves the private pre-pair key before opening setup',async()=>{
  const app=background(fixtureKey);await app.install('install');assert.equal(app.store.token,fixtureKey);assert.equal(app.opened(),1);
});
test('updating keeps an existing manually chosen pairing key and does not pop up options',async()=>{
  const existing='B'.repeat(48),app=background(fixtureKey,existing);await app.install('update');assert.equal(app.store.token,existing);assert.equal(app.opened(),0);
});
test('unconfigured and malformed setup keys never become credentials',async()=>{
  for(const prepared of ['', 'bad-key']){const app=background(prepared);await app.install('install');assert.equal(app.store.token,undefined);}
});
async function options(token,responseOk) {
  const input={value:'',addEventListener:()=>{}},status={},button={addEventListener:()=>{}};
  const requests=[];
  vm.runInNewContext(optionsCode,{document:{getElementById:id=>({token:input,status,save:button}[id])},chrome:{storage:{local:{get:async()=>({token}),set:async()=>{}}}},fetch:async(url,options)=>{requests.push({url,options});return {ok:responseOk};},AbortSignal});
  await new Promise(resolve=>setImmediate(resolve));
  return {input,status,requests};
}
test('prepared extension verifies pairing automatically without exposing the key in status or URLs',async()=>{
  const app=await options(fixtureKey,true);assert.equal(app.requests.length,1);assert.equal(app.requests[0].url,'http://127.0.0.1:47836/health');assert.equal(app.requests[0].options.headers['X-Afterglow-Token'],fixtureKey);assert.match(app.status.textContent,/Paired automatically/);assert.ok(!app.status.textContent.includes(fixtureKey));
});
test('failed auto-pair check offers reconnect rather than claiming success',async()=>{
  const app=await options(fixtureKey,false);assert.match(app.status.textContent,/manual pairing/);assert.ok(!app.status.textContent.includes('Paired automatically'));
});
test('ordinary blank installer extension keeps manual pairing available',async()=>{
  const app=await options('',true);assert.equal(app.requests.length,0);assert.equal(app.input.value,'');
});
