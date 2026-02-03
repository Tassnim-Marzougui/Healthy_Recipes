document.addEventListener('DOMContentLoaded', function () {
  var toggle = document.getElementById('chatbotToggle');
  var panel = document.getElementById('chatPanel');
  var closeBtn = document.getElementById('chatClose');
  var sendBtn = document.getElementById('chatSend');
  var input = document.getElementById('chatInput');

  function openPanel() {
    panel.classList.add('open');
    panel.setAttribute('aria-hidden', 'false');
  }
  function closePanel() {
    panel.classList.remove('open');
    panel.setAttribute('aria-hidden', 'true');
  }
  if (toggle) {
    toggle.addEventListener('click', function () {
      if (panel.classList.contains('open')) closePanel(); else openPanel();
    });
  }
  if (closeBtn) closeBtn.addEventListener('click', closePanel);
  if (sendBtn) sendBtn.addEventListener('click', function () {
    var v = input.value && input.value.trim();
    if (!v) return;
    var body = panel.querySelector('.chat-body');
    // show user message
    var userEl = document.createElement('div');
    userEl.style.marginBottom = '8px';
    userEl.style.padding = '8px';
    userEl.style.background = '#dcfce7';
    userEl.style.borderRadius = '8px';
    userEl.style.textAlign = 'right';
    userEl.textContent = v;
    body.appendChild(userEl);
    input.value = '';
    body.scrollTop = body.scrollHeight;

    // show loading placeholder for assistant
    var botEl = document.createElement('div');
    botEl.style.marginBottom = '8px';
    botEl.style.padding = '8px';
    botEl.style.background = '#eef2ff';
    botEl.style.borderRadius = '8px';
    botEl.textContent = '...';
    body.appendChild(botEl);
    body.scrollTop = body.scrollHeight;

    // send to server
    fetch('/api/chat', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ message: v })
    }).then(async function (res) {
      // If server returned non-JSON, capture text
      const text = await res.text();
      let parsed = null;
      try { parsed = text ? JSON.parse(text) : null; } catch (e) { parsed = text; }

      if (!res.ok) {
        // If parsed is object, prefer its error field
        const msg = parsed && typeof parsed === 'object' ? (parsed.error || parsed.detail || JSON.stringify(parsed)) : (parsed || res.statusText);
        throw { status: res.status, error: msg };
      }

      return parsed;
    }).then(function (data) {
      botEl.textContent = data?.reply || 'Désolé, aucune réponse.';
      body.scrollTop = body.scrollHeight;
    }).catch(function (err) {
      // Distinguish network failures from server JSON errors
      if (err instanceof TypeError) {
        botEl.textContent = 'Erreur réseau: impossible de contacter le serveur. Vérifiez que l\'application est lancée.';
      } else if (err && err.status) {
        botEl.textContent = 'Erreur serveur (' + err.status + '): ' + (err.error || JSON.stringify(err));
      } else {
        botEl.textContent = 'Erreur: ' + (err?.error || err?.message || JSON.stringify(err));
      }
      body.scrollTop = body.scrollHeight;
    });
  });

  // close when clicking outside
  document.addEventListener('click', function (e) {
    if (!panel || !toggle) return;
    var target = e.target;
    if (panel.classList.contains('open')) {
      if (!panel.contains(target) && !toggle.contains(target)) {
        closePanel();
      }
    }
  });
});
