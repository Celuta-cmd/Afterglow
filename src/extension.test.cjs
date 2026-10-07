const {test} = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const pageCode = fs.readFileSync(path.join(__dirname, '../extension/page.js'), 'utf8');
function setup(metadata = null) {
  let listener;
  const events = [];
  const clicks = [];
  const elements = {
    '.title': {textContent:'A YouTube Music song'}, '.byline a': {textContent:'An artist'},
    'img.image, .thumbnail-image-wrapper img, img': {src:'https://yt3.googleusercontent.com/cover=w60-h60-l90-rj'}
  };
  for (const [selector, action] of [['#play-pause-button','toggle'],['.previous-button, #previous-button','previous'],['.next-button, #next-button','next']]) {
    elements[selector] = {querySelector: () => ({click: () => clicks.push(action)})};
  }
  const video = {paused:false, ended:false, currentTime:43, duration:240};
  const window = {addEventListener:(_,callback)=>listener=callback, postMessage:event=>events.push(event)};
  const origin = 'https://music.youtube.com';
  vm.runInNewContext(pageCode,{window, location:{href:origin+'/watch?v=test',origin}, navigator:{mediaSession:{metadata}}, document:{querySelector:s=>s==='video'?video:s==='ytmusic-player-bar'?{querySelector:s=>elements[s]}:null}});
  return {events, clicks, video, elements, send:(kind,extra={})=>listener({source:window, origin, data:{channel:'afterglow-ytm-v1',kind,...extra}}), foreign:()=>listener({source:{},origin, data:{channel:'afterglow-ytm-v1',kind:'request'}})};
}
test('current website DOM supplies track and artist, and expands Google artwork',()=>{
  const x=setup();x.send('request');const state=x.events[0].state;
  assert.equal(state.title,'A YouTube Music song');assert.equal(state.artist,'An artist');
  assert.equal(state.artwork,'https://yt3.googleusercontent.com/cover=w2400-h2400-l100-rj');assert.equal(state.playing,true);
});
test('Media Session artwork picks largest supplied image',()=>{
  const x=setup({artwork:[{src:'https://i.ytimg.com/small.jpg',sizes:'96x96'},{src:'https://i.ytimg.com/large.jpg',sizes:'512x512'}]});x.send('request');assert.equal(x.events[0].state.artwork,'https://i.ytimg.com/large.jpg');
});
test('missing DOM text falls back to Media Session metadata',()=>{
  const x=setup({title:'Metadata song',artist:'Metadata artist',album:'The album'});x.elements['.title']=null;x.elements['.byline a']=null;x.send('request');assert.equal(x.events[0].state.title,'Metadata song');assert.equal(x.events[0].state.artist,'Metadata artist');
});
test('Media Session artist wins when the byline link points to the album',()=>{
  const x=setup({artist:'The actual artist',album:'The album'});x.elements['.byline a']={textContent:'The album'};
  x.send('request');assert.equal(x.events[0].state.artist,'The actual artist');
});
test('playback controls click the actual inner buttons',()=>{
  const x=setup();for(const action of ['toggle','previous','next'])x.send('command',{action});assert.deepEqual(x.clicks,['toggle','previous','next']);
});
test('seek clamps to valid track duration',()=>{
  const x=setup();x.send('command',{action:'seek',value:999});assert.equal(x.video.currentTime,240);x.send('command',{action:'seek',value:-100});assert.equal(x.video.currentTime,0);
});
test('invalid seek and unknown commands do not change playback',()=>{
  const x=setup();x.send('command',{action:'seek',value:'bad'});x.send('command',{action:'delete-files'});assert.equal(x.video.currentTime,43);assert.equal(x.clicks.length,0);
});
test('foreign window messages are ignored',()=>{const x=setup();x.foreign();assert.equal(x.events.length,0);});
test('infinite duration becomes zero for streams',()=>{const x=setup();x.video.duration=Infinity;x.send('request');assert.equal(x.events[0].state.duration,0);});
test('extension only reads YouTube Music and only contacts loopback',()=>{
  const manifest=JSON.parse(fs.readFileSync(path.join(__dirname,'../extension/manifest.json')));
  assert.deepEqual(manifest.host_permissions,['http://127.0.0.1:47836/*']);assert.deepEqual(manifest.permissions,['storage']);
  for(const script of manifest.content_scripts)assert.deepEqual(script.matches,['https://music.youtube.com/*']);
});
