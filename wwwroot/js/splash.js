// Show splash for ~2.5s then hide
(function(){
  try{
    const splash = document.getElementById('splash');
    if(!splash) return;

    // Prefer waiting for the logo animation to finish so the logo is visible,
    // but cap with a maximum timeout (ms) to avoid blocking.
    const MAX_WAIT = 3000; // maximum wait before hiding splash
    const AFTER_LOGO_DELAY = 300; // small pause after logo animation ends

    function hideSplash(){
      if(!splash) return;
      splash.classList.add('splash-hide');
      setTimeout(()=>{ splash.parentNode && splash.parentNode.removeChild(splash); }, 700);
    }

    // If a logo element exists, listen for its animationend event
    const logo = splash.querySelector('.logo');
    let handled = false;

    if(logo){
      const onAnimEnd = (e)=>{
        if(handled) return;
        handled = true;
        setTimeout(hideSplash, AFTER_LOGO_DELAY);
      };
      logo.addEventListener('animationend', onAnimEnd, { once: true });
      // also listen to transitionend just in case
      logo.addEventListener('transitionend', onAnimEnd, { once: true });
    }

    // Always hide after MAX_WAIT as fallback
    setTimeout(()=>{ if(!handled){ handled = true; hideSplash(); } }, MAX_WAIT);

    // Also tie to window load to ensure visuals are ready
    if (document.readyState === 'complete'){
      // nothing special — the logo listener will handle timing
    } else {
      window.addEventListener('load', ()=>{
        // Do nothing here: we rely on the logo animationend or MAX_WAIT
      });
    }

  }catch(e){ console.warn('Splash error', e); }
})();
