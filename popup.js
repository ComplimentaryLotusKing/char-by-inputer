const textEl = document.getElementById('text');
const delayEl = document.getElementById('delay');
const jitterEl = document.getElementById('jitter');
const startBtn = document.getElementById('start');
const stopBtn = document.getElementById('stop');
const statusEl = document.getElementById('status');

function setStatus(msg, isError = false) {
  statusEl.textContent = msg;
  statusEl.style.color = isError ? '#dc2626' : '#2563eb';
}

// 粘贴卫生检查：某些反粘贴站点会在页面失焦（即打开本弹窗）时向剪贴板顶部写入空白内容，
// 导致粘贴只得一个空格。此处识别这种情况并明确提示，避免静默失败。
textEl.addEventListener('paste', () => {
  // 等粘贴完成（异步）再检查文本框内容
  setTimeout(() => {
    const val = textEl.value;
    if (val.length > 0 && val.trim() === '') {
      setStatus('检测到粘贴空白内容，剪贴板可能被网站的反粘贴脚本污染，请按 Win+V 选择正确的历史条目重新粘贴。', true);
    } else if (val.trim() !== '') {
      statusEl.textContent = '';
    }
  }, 0);
});

// 关键：确保 content.js 已就绪，否则动态注入
async function ensureContentScript(tabId) {
  // 先尝试 ping
  try {
    const res = await chrome.tabs.sendMessage(tabId, { type: 'PING' });
    if (res && res.ok) return true;
  } catch (e) {
    // 忽略，进入注入流程
  }
  // 动态注入
  try {
    await chrome.scripting.executeScript({
      target: { tabId },
      files: ['content.js']
    });
    // 注入后再 ping 一次
    const res2 = await chrome.tabs.sendMessage(tabId, { type: 'PING' });
    return !!(res2 && res2.ok);
  } catch (e) {
    return false;
  }
}

startBtn.addEventListener('click', async () => {
  const text = textEl.value;
  if (!text) {
    setStatus('请输入要输入的文本', true);
    return;
  }
  const delay = Math.max(0, parseInt(delayEl.value, 10) || 20);
  const jitter = jitterEl.checked;

  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  if (!tab || !tab.id) {
    setStatus('无法获取当前标签页', true);
    return;
  }

  const ready = await ensureContentScript(tab.id);
  if (!ready) {
    setStatus('当前页面不支持输入（浏览器内部页面或受限页面）', true);
    return;
  }

  try {
    await chrome.tabs.sendMessage(tab.id, {
      type: 'START_TYPING',
      text,
      delay,
      jitter
    });
  } catch (e) {
    setStatus('消息发送失败：' + e.message, true);
  }
});

stopBtn.addEventListener('click', async () => {
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  if (tab && tab.id) {
    try {
      await chrome.tabs.sendMessage(tab.id, { type: 'STOP_TYPING' });
    } catch (e) {}
  }
});