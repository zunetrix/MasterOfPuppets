//  State
let ws = null;
let token = '';
let serverOrigin = ''; // e.g. "http://localhost:4782" or "https://xxxx.trycloudflare.com"
let macros = [];
let favs = JSON.parse(localStorage.getItem('mop_favs') || '[]');
let reconnectTimer = null;

//  Init
window.addEventListener('DOMContentLoaded', () => {
    // 1) Read token from URL hash: http://host/#token=abc  OR  http://host/#server=https://x&token=abc
    const hash = new URLSearchParams(location.hash.slice(1));
    token = hash.get('token') || localStorage.getItem('mop_token') || '';

    // 2) Server origin: from hash → localStorage → same origin as current page
    const hashServer = hash.get('server');
    serverOrigin = hashServer
        || localStorage.getItem('mop_server')
        || location.origin;

    // Persist so the next page load remembers
    if (token) localStorage.setItem('mop_token', token);
    if (hashServer) localStorage.setItem('mop_server', hashServer);

    renderFavs();
    loadStatus();
    loadMacros();
    connectWs();

    // Command box
    document.getElementById('cmdInput').addEventListener('keydown', e => {
        if (e.key === 'Enter') sendCommand();
    });
    document.getElementById('favCmdInput').addEventListener('keydown', e => {
        if (e.key === 'Enter') addFav();
    });
    document.getElementById('macroFilter').addEventListener('input', e => {
        renderMacros(e.target.value);
    });
    document.getElementById('clearLog').addEventListener('click', () => {
        document.getElementById('eventLog').innerHTML = '';
    });
});

//  API helpers
async function apiFetch(method, path, body) {
    const headers = { 'Content-Type': 'application/json' };
    if (token) headers['Authorization'] = 'Bearer ' + token;
    try {
        const r = await fetch(serverOrigin + path, { method, headers, body: body ? JSON.stringify(body) : undefined });
        return await r.json();
    } catch (e) {
        return { ok: false, error: e.message };
    }
}

async function loadStatus() {
    const s = await apiFetch('GET', '/api/v1/status');
    const charEl = document.getElementById('charName');
    if (s.characterName) {
        charEl.textContent = s.characterName + (s.macroRunning ? ' · ▶ macro' : '');
    } else {
        charEl.textContent = s.isLoggedIn ? 'Logged in' : 'Not logged in';
    }
}

async function loadMacros() {
    const r = await apiFetch('GET', '/api/v1/macros');
    if (r.macros) {
        macros = r.macros;
        renderMacros(document.getElementById('macroFilter')?.value || '');
    }
}

function renderMacros(filter) {
    const list = document.getElementById('macroList');
    list.innerHTML = '';
    const f = filter.trim().toLowerCase();
    const items = macros.filter(m =>
        !f || m.name.toLowerCase().includes(f) || (m.tags || []).some(t => t.toLowerCase().includes(f))
    );
    if (items.length === 0) {
        list.innerHTML = '<div style="color:var(--muted);font-size:.82rem;padding:6px">No macros found.</div>';
        return;
    }
    items.forEach(m => {
        const el = document.createElement('div');
        el.className = 'macro-item';
        const tags = (m.tags || []).map(t => `<span class="tag">${esc(t)}</span>`).join('');
        el.innerHTML = `
      <span class="macro-idx">#${m.index + 1}</span>
      <span class="macro-name">${esc(m.name)}</span>
      ${tags ? `<span class="macro-tags">${tags}</span>` : ''}
      <button class="btn-sm btn-run" onclick="runMacro('${esc(m.name)}')">▶ Run</button>`;
        list.appendChild(el);
    });
}

async function runMacro(name) {
    const r = await apiFetch('POST', '/api/v1/macro/run', { macro: name });
    showToast(r.ok ? `▶ ${name}` : r.error, r.ok);
    logEntry('commandResult', r.ok ? `▶ Run macro: ${name}` : `✗ ${r.error}`);
}

//  Command send
function sendCommand() {
    const type = document.getElementById('cmdType').value;
    const inp = document.getElementById('cmdInput');
    const cmd = inp.value.trim();
    if (!cmd) return;
    inp.value = '';
    logEntry('sent', `→ [${type}] ${cmd}`);

    if (ws && ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify({ type, payload: cmd }));
    } else {
        const route = type === 'chat' ? '/api/v1/chat' : '/api/v1/command';
        const body = type === 'chat' ? { message: cmd } : { cmd };
        apiFetch('POST', route, body).then(r => {
            logEntry(type + 'Result', r.ok ? '✓ ok' : '✗ ' + r.error);
            showToast(r.ok ? '✓ Sent' : r.error, r.ok);
        });
    }
}

//  Favourites
function renderFavs() {
    const list = document.getElementById('favList');
    list.innerHTML = '';
    if (favs.length === 0) {
        list.innerHTML = '<div style="color:var(--muted);font-size:.82rem;padding:4px 0">No favourites yet.</div>';
        return;
    }
    favs.forEach((fav, i) => {
        const type = fav.type || 'command';
        const badge = type === 'chat' ? '<span style="font-size:10px;background:var(--surface);padding:1px 4px;border-radius:4px;color:var(--muted);border:1px solid var(--border)">Chat</span>' : '';
        const el = document.createElement('div');
        el.className = 'fav-item';
        el.innerHTML = `
      <span class="fav-name" title="${esc(fav.cmd)}">${esc(fav.name)} ${badge}</span>
      <span class="fav-cmd">${esc(fav.cmd)}</span>
      <button class="btn-sm btn-run" onclick="sendFav(${i})">▶</button>
      <button class="btn-icon btn-del" title="Remove" onclick="removeFav(${i})">✕</button>`;
        list.appendChild(el);
    });
}

function sendFav(i) {
    const fav = favs[i];
    if (!fav || !fav.cmd) return;
    const type = fav.type || 'command';
    logEntry('sent', `→ [${type}] ${fav.cmd}`);

    if (ws && ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify({ type, payload: fav.cmd }));
    } else {
        const route = type === 'chat' ? '/api/v1/chat' : '/api/v1/command';
        const body = type === 'chat' ? { message: fav.cmd } : { cmd: fav.cmd };
        apiFetch('POST', route, body).then(r =>
            logEntry(type + 'Result', r.ok ? '✓ ok' : '✗ ' + r.error));
    }
}

function addFav() {
    const name = document.getElementById('favNameInput').value.trim();
    const type = document.getElementById('favTypeInput').value;
    const cmd = document.getElementById('favCmdInput').value.trim();
    if (!name || !cmd) return;
    favs.push({ name, cmd, type });
    saveFavs();
    document.getElementById('favNameInput').value = '';
    document.getElementById('favCmdInput').value = '';
}

function removeFav(i) { favs.splice(i, 1); saveFavs(); }

function saveFavs() {
    localStorage.setItem('mop_favs', JSON.stringify(favs));
    renderFavs();
}

//  WebSocket
function buildWsUrl() {
    // Derive WS URL from the configured server origin
    // http://...  → ws://...
    // https://... → wss://...
    const wsOrigin = serverOrigin
        .replace(/^https:\/\//, 'wss://')
        .replace(/^http:\/\//, 'ws://');
    return wsOrigin + '/ws' + (token ? '?token=' + encodeURIComponent(token) : '');
}

function connectWs() {
    const wsUrl = buildWsUrl();
    setWsStatus('connecting');
    logEntry('pong', `⚡ Connecting to ${wsUrl.replace(/\?token=.*/, '?token=…')}`);

    try {
        ws = new WebSocket(wsUrl);
    } catch (e) {
        setWsStatus('');
        logEntry('pong', `✗ WebSocket error: ${e.message}`);
        scheduleReconnect();
        return;
    }

    ws.onopen = () => {
        setWsStatus('connected');
        logEntry('pong', '✓ WebSocket connected');
        loadStatus();
    };

    ws.onmessage = e => {
        try {
            const ev = JSON.parse(e.data);
            const name = ev.name || ev.type || 'event';
            let label = '';
            if (name === 'macroStarted') label = `▶ Macro started: ${ev.data?.macro ?? ''}`;
            else if (name === 'macroStopped') label = '■ Macro stopped';
            else if (name === 'commandResult') label = ev.data?.ok ? '✓ ok' : `✗ ${ev.data?.error}`;
            else if (name === 'broadcastReceived') label = `📡 ${ev.data?.cmd ?? ''}`;
            else if (name === 'pong') label = '🏓 pong';
            else label = JSON.stringify(ev.data ?? ev);
            logEntry(name, label);
            if (name === 'macroStarted' || name === 'macroStopped') loadStatus();
        } catch { /* ignore */ }
    };

    ws.onclose = () => {
        setWsStatus('');
        logEntry('pong', '⚠ Disconnected - reconnecting in 4 s…');
        scheduleReconnect();
    };

    ws.onerror = () => { ws.close(); };
}

function scheduleReconnect() {
    clearTimeout(reconnectTimer);
    reconnectTimer = setTimeout(connectWs, 4000);
}

function setWsStatus(cls) {
    const el = document.getElementById('wsStatus');
    el.className = cls;
    el.querySelector('span').textContent =
        cls === 'connected' ? 'Connected'
            : cls === 'connecting' ? 'Connecting…'
                : 'Disconnected';
}

//  Log
function logEntry(cls, text) {
    const log = document.getElementById('eventLog');
    const ts = new Date().toLocaleTimeString();
    const el = document.createElement('div');
    el.className = `log-entry ${cls}`;
    el.innerHTML = `<span class="log-ts">${ts}</span>${esc(text)}`;
    log.appendChild(el);
    log.scrollTop = log.scrollHeight;
}

//  Toast
let toastTimer;
function showToast(msg, ok) {
    const t = document.getElementById('toast');
    t.textContent = msg;
    t.className = 'show ' + (ok ? 'ok' : 'err');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => { t.className = ok ? 'ok' : 'err'; }, 2500);
}

//  Util
function esc(s) {
    return String(s ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}
