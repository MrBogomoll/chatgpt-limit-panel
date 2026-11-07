import {spawn} from 'node:child_process';
import {mkdtemp,copyFile,lstat,unlink,rmdir,realpath,readFile} from 'node:fs/promises';
import {homedir,tmpdir} from 'node:os';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

async function launchDetached(file,args,options){
  await new Promise((resolve,reject)=>{
    const child=spawn(file,args,options);child.once('error',reject);child.once('spawn',()=>{child.unref();resolve();});
  });
}

// This action only opens a confirmation window. Actual removal is performed by
// the helper after the user confirms, independently of the removable MCP server.
export function createUninstaller({pluginRoot=fileURLToPath(new URL('../',import.meta.url)),codexHome=process.env.CODEX_HOME||path.join(homedir(),'.codex'),tempRoot=tmpdir(),launch=launchDetached}={}){
  let opening;
  return {open(language='ru'){
    if(opening)return opening;
    opening=(async()=>{
      const english=language==='en';let directory,file;
      try{
        const source=path.join(pluginRoot,'dist/overlay/ChatGPTLimitPanelUninstaller.exe'),info=await lstat(source);
        if(!info.isFile()||info.isSymbolicLink()||info.size===0)throw Error('Missing native helper');
        const home=await realpath(codexHome),root=await realpath(pluginRoot),relative=path.relative(path.join(home,'plugins/cache'),root).split(path.sep);
        if(relative.length!==3||relative[1]!=='quota-panel'||!/^\d+\.\d+\.\d+(?:[-+][A-Za-z0-9.-]+)?$/.test(relative[2])||!/^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$/.test(relative[0]))throw Error('Not an installed panel cache');
        const manifestFile=path.join(pluginRoot,'.codex-plugin/plugin.json');
        for(const checked of [source,manifestFile])for(let current=path.resolve(checked);;current=path.dirname(current)){
          if((await lstat(current)).isSymbolicLink())throw Error('Redirected helper path');
          if(current.toLowerCase()===path.resolve(codexHome).toLowerCase())break;
          const next=path.dirname(current);if(next===current)throw Error('Helper outside plugin home');
        }
        if((await realpath(source)).toLowerCase()!==path.join(root,'dist/overlay/ChatGPTLimitPanelUninstaller.exe').toLowerCase())throw Error('Redirected helper path');
        if((await realpath(manifestFile)).toLowerCase()!==path.join(root,'.codex-plugin/plugin.json').toLowerCase())throw Error('Redirected manifest path');
        const manifest=JSON.parse((await readFile(manifestFile,'utf8')).replace(/^\uFEFF/,''));
        if(manifest.name!=='quota-panel'||manifest.version!==relative[2]||manifest.interface?.displayName!=='ChatGPT Limit Panel')throw Error('Not the panel package');
        directory=await mkdtemp(path.join(tempRoot,'ChatGPTLimitPanel-uninstall-'));file=path.join(directory,'ChatGPTLimitPanelUninstaller.exe');
        await copyFile(source,file);
        await launch(file,['--root',root,'--home',home,'--language',english?'en':'ru'],{cwd:directory,windowsHide:false,detached:true,stdio:'ignore',shell:false});
        return {opened:true,language:english?'en':'ru',message:english?'The removal confirmation window is open.':'Открыто окно подтверждения удаления.'};
      }catch{
        if(file)try{await unlink(file);}catch{}
        if(directory)try{await rmdir(directory);}catch{}
        throw Error(english?'Could not open the removal window. Check the plugin installation.':'Не удалось открыть окно удаления. Проверь установку плагина.');
      }
    })().finally(()=>{opening=null;});
    return opening;
  }};
}
