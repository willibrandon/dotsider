// demo.js — Live terminal demo for the dotsider docs site.
// Connects to a running dotsider instance over WebSocket and renders
// it in an xterm.js terminal embedded in the demo page (demo.mdx).

(async () => {
  // Load xterm.js CSS if not already present
  if (!document.querySelector('link[href*="xterm"]')) {
    const l = document.createElement('link');
    l.rel = 'stylesheet';
    l.href = 'https://esm.sh/@xterm/xterm@5.5.0/css/xterm.css';
    document.head.appendChild(l);
    await new Promise(r => { l.onload = r; l.onerror = r; });
  }

  // Dynamic imports — loaded from esm.sh CDN
  const { Terminal } = await import('https://esm.sh/@xterm/xterm@5.5.0');
  const { Unicode11Addon } = await import('https://esm.sh/@xterm/addon-unicode11@0.8.0');
  const { WebglAddon } = await import('https://esm.sh/@xterm/addon-webgl@0.18.0');

  const tv = document.getElementById('tv');
  const statusEl = document.getElementById('status');

  // Terminal dimensions and theme match the dotsider TUI defaults
  const term = new Terminal({
    cols: 120,
    rows: 36,
    cursorBlink: true,
    cursorStyle: 'block',
    fontSize: 14,
    fontFamily:
      '"JetBrains Mono", "Cascadia Code", "Fira Code", Menlo, Monaco, monospace',
    theme: {
      background: '#121218',
      foreground: '#e0e0e0',
      cursor: '#00c8b4',
      selectionBackground: '#00644080',
      black: '#121218',
      red: '#ff5555',
      green: '#00c8b4',
      yellow: '#f1fa8c',
      blue: '#6c8fff',
      magenta: '#ff79c6',
      cyan: '#00ffc8',
      white: '#e0e0e0',
      brightBlack: '#3c3c50',
      brightGreen: '#00ffc8',
      brightCyan: '#00ffc8',
      brightWhite: '#ffffff',
    },
    allowProposedApi: true,
  });

  // Unicode 11 gives correct widths for box-drawing and CJK characters
  const u11 = new Unicode11Addon();
  term.loadAddon(u11);
  term.unicode.activeVersion = '11';

  term.open(tv);
  // Suppress the browser context menu so right-click doesn't cover the TUI
  tv.addEventListener('contextmenu', (e) => e.preventDefault());
  try { term.loadAddon(new WebglAddon()); } catch (e) { /* fallback to DOM renderer */ }

  // Handle OSC 52 clipboard write sequences from the TUI.
  // Native terminals handle OSC 52 natively; xterm.js in the browser
  // needs an explicit handler to bridge to the Clipboard API.
  term.parser.registerOscHandler(52, (data) => {
    const idx = data.indexOf(';');
    if (idx >= 0) {
      const b64 = data.substring(idx + 1);
      if (b64) {
        try {
          const bytes = Uint8Array.from(atob(b64), c => c.charCodeAt(0));
          const text = new TextDecoder().decode(bytes);
          navigator.clipboard.writeText(text).catch(() => {});
        } catch (_) { /* invalid base64 */ }
      }
    }
    return true;
  });

  // WebSocket connection — wss in production, ws for local dev
  const isProduction = window.location.hostname === 'dotsider.dev';
  const wsProto = isProduction ? 'wss' : 'ws';
  const wsHost = isProduction ? 'dotsider.dev' : 'localhost:5174';
  const wsUrl = wsProto + '://' + wsHost + '/ws';
  let ws;
  let wasConnected = false;
  let lastConnectTime = 0;
  let reconnectTimer = 0;

  function scheduleReconnect(delayMs) {
    clearTimeout(reconnectTimer);
    reconnectTimer = setTimeout(connect, delayMs);
  }

  function connect() {
    clearTimeout(reconnectTimer);
    wasConnected = false;
    lastConnectTime = Date.now();
    statusEl.textContent = 'Connecting...';
    statusEl.style.color = '#f1fa8c';
    ws = new WebSocket(wsUrl);

    // Give the server 5s to respond before giving up
    const timeout = setTimeout(() => { ws.close(); }, 5000);

    ws.onopen = () => {
      clearTimeout(timeout);
      wasConnected = true;
      statusEl.textContent = 'Connected';
      statusEl.style.color = '#00c8b4';
    };
    ws.onmessage = (e) => {
      // Reply to DA (Device Attributes) queries so the TUI knows
      // we're a VT-compatible terminal
      if (e.data.includes('\x1b[c') && ws.readyState === WebSocket.OPEN)
        ws.send('\x1b[?62;4c');
      term.write(e.data);
    };
    ws.onclose = () => {
      clearTimeout(timeout);
      // Auto-restart if the session was alive for >3s (avoids rapid reconnect loops)
      if (wasConnected && Date.now() - lastConnectTime > 3000) {
        statusEl.textContent = 'Restarting...';
        statusEl.style.color = '#f1fa8c';
        term.reset();
        scheduleReconnect(1500);
      } else {
        statusEl.textContent = 'Offline';
        statusEl.style.color = '#ff5555';
      }
    };
    ws.onerror = () => { clearTimeout(timeout); };
  }

  // Forward user keyboard/mouse input to the remote TUI
  term.onData((d) => { if (ws?.readyState === WebSocket.OPEN) ws.send(d); });
  term.onBinary((d) => { if (ws?.readyState === WebSocket.OPEN) ws.send(d); });

  // Manual reconnect button
  document.getElementById('reconnect-btn')
    .addEventListener('click', () => {
    if (ws) { ws.onclose = null; ws.close(); }
    statusEl.textContent = 'Reconnecting...';
    statusEl.style.color = '#f1fa8c';
    scheduleReconnect(1500);
  });

  // Reconnect when the tab becomes visible again — browsers throttle
  // backgrounded tabs and may kill the WebSocket, leaving the demo
  // stuck on "Offline" with no further retries. Only reconnect when
  // the socket is actually dead to avoid churning healthy connections.
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState !== 'visible') return;
    if (!ws || ws.readyState >= WebSocket.CLOSING) {
      term.reset();
      connect();
    }
  });

  connect();
})();
