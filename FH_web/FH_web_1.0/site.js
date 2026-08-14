(() => {
  const pages = [...document.querySelectorAll('.page')];
  const nav = [...document.querySelectorAll('.chapter')];
  const transition = document.getElementById('pageTransition');
  const transitionNo = document.getElementById('transitionNo');
  const transitionTitle = document.getElementById('transitionTitle');
  const labels = [['00','INDEX'],['01','PLAY'],['02','FEVER'],['03','WORLD'],['04','MEDIA'],['05','MORE']];
  let current = 0, locked = false, motion = true, wheelLock = false;
  const wait = ms => new Promise(r => setTimeout(r, ms));

  function hardSwitch(next){
    pages.forEach((p,i)=>p.classList.toggle('active',i===next));
    nav.forEach((n,i)=>n.classList.toggle('active',i===next));
    current = next;
  }

  async function go(next){
    next = Math.max(0,Math.min(pages.length-1,Number(next)));
    if(next===current || locked) return;
    if(!motion){ hardSwitch(next); return; }
    locked = true;
    const [no,title] = labels[next];
    transitionNo.textContent = no;
    transitionTitle.textContent = title;
    transition.classList.remove('active');
    void transition.offsetWidth;
    transition.classList.add('active');
    await wait(330);        // curtain covers the screen
    hardSwitch(next);       // only now swap DOM page; never two visible pages
    await wait(410);
    transition.classList.remove('active');
    locked = false;
  }

  document.querySelectorAll('[data-page]').forEach(el=>{
    el.addEventListener('click', e=>{
      e.preventDefault();
      go(el.dataset.page);
    });
  });

  window.addEventListener('wheel', e=>{
    if(wheelLock || locked || Math.abs(e.deltaY)<18) return;
    wheelLock = true;
    go(current + (e.deltaY>0?1:-1));
    setTimeout(()=>wheelLock=false,650);
  },{passive:true});

  window.addEventListener('keydown', e=>{
    if(e.key==='ArrowDown'||e.key==='PageDown') go(current+1);
    if(e.key==='ArrowUp'||e.key==='PageUp') go(current-1);
    if(e.key==='Home') go(0);
    if(e.key==='End') go(5);
  });

  let touchY=0;
  window.addEventListener('touchstart',e=>touchY=e.touches[0].clientY,{passive:true});
  window.addEventListener('touchend',e=>{const dy=touchY-e.changedTouches[0].clientY;if(Math.abs(dy)>45)go(current+(dy>0?1:-1))},{passive:true});

  // settings
  const settings = document.getElementById('settings');
  const mask = document.getElementById('settingsMask');
  const open = tab => { settings.classList.add('open'); mask.classList.add('open'); activateTab(tab||'general'); };
  const close = () => { settings.classList.remove('open'); mask.classList.remove('open'); };
  document.getElementById('settingsButton').addEventListener('click',()=>open('general'));
  document.getElementById('settingsClose').addEventListener('click',close);
  mask.addEventListener('click',close);
  document.getElementById('agentPortal').addEventListener('click',()=>open('agent'));

  function activateTab(name){
    document.querySelectorAll('.settings-tabs button').forEach(b=>b.classList.toggle('active',b.dataset.tab===name));
    document.querySelectorAll('.settings-page').forEach(p=>p.classList.toggle('active',p.dataset.pane===name));
  }
  document.querySelectorAll('.settings-tabs button').forEach(b=>b.addEventListener('click',()=>activateTab(b.dataset.tab)));
  document.getElementById('motionToggle').addEventListener('change',e=>{motion=e.target.checked;document.body.classList.toggle('no-motion',!motion)});

  // simple local UI only
  let bgm=false;
  document.getElementById('bgmButton').addEventListener('click',e=>{bgm=!bgm;e.currentTarget.classList.toggle('on',bgm)});
  document.querySelectorAll('.filter button').forEach(b=>b.addEventListener('click',()=>{b.parentElement.querySelectorAll('button').forEach(x=>x.classList.remove('active'));b.classList.add('active')}));
  const terminal=document.getElementById('terminalText');
  const messages={status:'> /fh-status\n\n[PLACEHOLDER]\nLocal bridge is not connected.',verify:'> /fh-verify\n\n[PLACEHOLDER]\nSHA256 not checked.',apply:'> /fh-apply\n\n[BLOCKED]\nPublic website cannot modify local files.'};
  document.querySelectorAll('[data-agent]').forEach(b=>b.addEventListener('click',()=>terminal.textContent=messages[b.dataset.agent]||''));
  document.getElementById('clearTerminal').addEventListener('click',()=>terminal.textContent='');

  // ------------------------------------------------------------
  // QQ 式“弹弹”交互
  // hover: 轻微上浮
  // pointerdown: 压缩
  // pointerup: 0.94 -> 1.07 -> 0.985 -> 1 回弹
  // 使用 WAAPI，避免外部 Motion/CDN 导致加载问题。
  // ------------------------------------------------------------
  const bouncySelector = [
    '.chapter',
    '.pill',
    '.round',
    '.primary',
    '.secondary',
    '.filter button',
    '.world-cards .info-card',
    '.media-card',
    '.portal',
    '.settings>header button',
    '.settings-tabs button',
    '.agent-buttons button',
    '.terminal button'
  ].join(',');

  const bouncyItems = [...document.querySelectorAll(bouncySelector)]
    .filter(el => !el.matches(':disabled'));

  bouncyItems.forEach(el => {
    el.classList.add('bouncy-ui');

    let activeAnimation = null;
    let hovering = false;
    let pressing = false;

    const stopAnimation = () => {
      if(activeAnimation){
        try { activeAnimation.cancel(); } catch(_) {}
        activeAnimation = null;
      }
    };

    const animateTo = (frames, options) => {
      if(!motion || document.body.classList.contains('no-motion')) return;
      stopAnimation();
      activeAnimation = el.animate(frames, {
        fill: 'forwards',
        ...options
      });
      activeAnimation.addEventListener('finish', () => {
        activeAnimation = null;
      }, { once:true });
    };

    el.addEventListener('pointerenter', () => {
      hovering = true;
      if(pressing) return;
      animateTo(
        [
          { transform:'translateY(0) scale(1)' },
          { transform:'translateY(-3px) scale(1.018)' }
        ],
        { duration:170, easing:'cubic-bezier(.22,1,.36,1)' }
      );
    });

    el.addEventListener('pointerleave', () => {
      hovering = false;
      if(pressing) return;
      animateTo(
        [
          { transform:'translateY(-3px) scale(1.018)' },
          { transform:'translateY(0) scale(1)' }
        ],
        { duration:190, easing:'cubic-bezier(.22,1,.36,1)' }
      );
    });

    el.addEventListener('pointerdown', e => {
      if(e.pointerType === 'mouse' && e.button !== 0) return;
      pressing = true;
      try { el.setPointerCapture(e.pointerId); } catch(_) {}
      animateTo(
        [
          { transform:hovering ? 'translateY(-3px) scale(1.018)' : 'translateY(0) scale(1)' },
          { transform:'translateY(1px) scale(.94)' }
        ],
        { duration:105, easing:'cubic-bezier(.4,0,.8,.4)' }
      );
    });

    const release = e => {
      if(!pressing) return;
      pressing = false;
      try { el.releasePointerCapture(e.pointerId); } catch(_) {}

      const endY = hovering ? -3 : 0;
      const endScale = hovering ? 1.018 : 1;

      animateTo(
        [
          { offset:0,    transform:'translateY(1px) scale(.94)' },
          { offset:.38,  transform:`translateY(${endY - 2}px) scale(1.07)` },
          { offset:.68,  transform:`translateY(${endY + 1}px) scale(.985)` },
          { offset:.86,  transform:`translateY(${endY - .5}px) scale(1.025)` },
          { offset:1,    transform:`translateY(${endY}px) scale(${endScale})` }
        ],
        { duration:390, easing:'cubic-bezier(.16,1,.3,1)' }
      );
    };

    el.addEventListener('pointerup', release);
    el.addEventListener('pointercancel', release);
  });

})();