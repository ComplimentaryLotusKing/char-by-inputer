let lastRange = null;   // 新增
let startToken = 0;     // 新增

(() => {
  // 防重复注入
  if (window.__typingExtInjected__) return;
  window.__typingExtInjected__ = true;

  let typingSession = null;
  let lastFocused = null;
  let lastSelectionStart = 0;
  let lastSelectionEnd = 0;

  function isEditable(el) {
    if (!el) return false;
    const tag = el.tagName;
    if (tag === 'INPUT') {
      const type = (el.type || 'text').toLowerCase();
      return ['text', 'search', 'url', 'tel', 'password', 'email', 'number'].includes(type);
    }
    if (tag === 'TEXTAREA') return true;
    if (el.isContentEditable) return true;
    return false;
  }

function capture(el) {
  if (!isEditable(el)) return;
  lastFocused = el;
  if (el.isContentEditable) {
    const sel = window.getSelection();
    if (sel.rangeCount > 0 && el.contains(sel.anchorNode)) {
      lastRange = sel.getRangeAt(0).cloneRange();
    }
  } else {
    try {
      lastSelectionStart = el.selectionStart ?? 0;
      lastSelectionEnd = el.selectionEnd ?? 0;
    } catch (e) {
      lastSelectionStart = 0;
      lastSelectionEnd = 0;
    }
  }
}

  // 被动监听聚焦 / 光标变化
  document.addEventListener('focusin', (e) => capture(e.target), true);
  document.addEventListener('selectionchange', () => capture(document.activeElement));

  // 主动抓一次：动态注入时如果没有历史聚焦事件，就用当前 activeElement
  capture(document.activeElement);

  function stopTyping() {
    startToken++;   // 新增
    if (typingSession) {
      clearTimeout(typingSession.timer);
      typingSession = null;
    }
    const bar = document.getElementById('__typing_progress__');
    if (bar) bar.remove();
  }

  function showProgress(current, total) {
    let bar = document.getElementById('__typing_progress__');
    if (!bar) {
      bar = document.createElement('div');
      bar.id = '__typing_progress__';
      Object.assign(bar.style, {
        position: 'fixed',
        right: '16px',
        bottom: '16px',
        zIndex: 2147483647,
        background: 'rgba(0,0,0,0.75)',
        color: '#fff',
        padding: '6px 12px',
        borderRadius: '16px',
        font: '12px/1.4 sans-serif',
        pointerEvents: 'none'
      });
      document.body.appendChild(bar);
    }
    bar.textContent = `输入中 ${current}/${total}`;
  }

  function insertChar(el, char) {
    if (el.isContentEditable || el.tagName === 'TEXTAREA') {
      if (char === '\n') {
        document.execCommand('insertLineBreak', false, null);
      } else {
        document.execCommand('insertText', false, char);
      }
    } else {
      const start = el.selectionStart;
      const end = el.selectionEnd;
      const value = el.value;
      el.value = value.slice(0, start) + char + value.slice(end);
      el.selectionStart = el.selectionEnd = start + char.length;
      el.dispatchEvent(new Event('input', { bubbles: true }));
    }
  }

function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }

async function startTyping({ text, delay, jitter }) {
  stopTyping();
  const myToken = ++startToken;

  const el = lastFocused || document.activeElement;
  if (!el || !isEditable(el)) return;

  el.focus();
  await sleep(40);
  if (myToken !== startToken) return;

  try {
    if (el.isContentEditable) {
      const sel = window.getSelection();
      if (lastRange && el.contains(lastRange.startContainer)) {
        sel.removeAllRanges();
        sel.addRange(lastRange);
      } else {
        const range = document.createRange();
        range.selectNodeContents(el);
        range.collapse(false);
        sel.removeAllRanges();
        sel.addRange(range);
      }
    } else if (typeof el.setSelectionRange === 'function') {
      const pos = Math.min(lastSelectionStart, el.value.length);
      el.setSelectionRange(pos, pos);
    }
  } catch (e) {}

  await sleep(40);
  if (myToken !== startToken) return;

  let index = 0;
  const total = text.length;

  function typeNext() {
    if (!typingSession) return;
    if (index >= total) { stopTyping(); return; }
    insertChar(el, text[index]);
    index++;
    showProgress(index, total);

    const wait = Math.round(delay + (jitter ? Math.random() * delay * 0.5 : 0));
    typingSession.timer = setTimeout(typeNext, wait);
  }

  typingSession = { timer: null, text, delay, jitter, element: el };
  typeNext();
}

  chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
    if (msg.type === 'PING') {
      sendResponse({ ok: true });
    } else if (msg.type === 'START_TYPING') {
      startTyping(msg);
      sendResponse({ ok: true });
    } else if (msg.type === 'STOP_TYPING') {
      stopTyping();
      sendResponse({ ok: true });
    }
    return true;
  });
})();