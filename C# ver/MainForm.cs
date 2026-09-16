using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CharByInputer
{
    public class MainForm : Form
    {
        private const int HotKeyStopId = 1;
        private const int HotKeyShowId = 2;
        private const uint WM_HOTKEY = 0x0312;
        private const int CountdownSeconds = 3;

        private readonly TextBox _textBox;
        private readonly NumericUpDown _delayBox;
        private readonly CheckBox _jitterBox;
        private readonly Button _startBtn;
        private readonly Button _stopBtn;
        private readonly Label _statusLabel;
        private readonly Random _random = new Random();

        private CancellationTokenSource _cts;
        private bool _typing;

        public MainForm()
        {
            Text = "逐字输入器";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            TopMost = true; // 始终置顶；配合 ShowWithoutActivation，显示/更新不抢焦点
            ClientSize = new Size(430, 330);
            Font = new Font("Microsoft YaHei UI", 9F);

            // 要输入的文本
            _textBox = new TextBox();
            _textBox.Multiline = true;
            _textBox.ScrollBars = ScrollBars.Vertical;
            _textBox.AcceptsReturn = true;
            _textBox.Bounds = new Rectangle(12, 10, 406, 158);
            Controls.Add(_textBox);

            // 速度设置
            Label speedLabel = new Label();
            speedLabel.Text = "速度";
            speedLabel.AutoSize = true;
            speedLabel.Location = new Point(14, 182);

            _delayBox = new NumericUpDown();
            _delayBox.Minimum = 0;
            _delayBox.Maximum = 2000;
            _delayBox.Increment = 5;
            _delayBox.Value = 10;
            _delayBox.Width = 68;
            _delayBox.Location = new Point(56, 178);

            Label unitLabel = new Label();
            unitLabel.Text = "ms/字";
            unitLabel.AutoSize = true;
            unitLabel.Location = new Point(128, 182);

            _jitterBox = new CheckBox();
            _jitterBox.Text = "随机抖动";
            _jitterBox.AutoSize = true;
            _jitterBox.Checked = false;
            _jitterBox.Location = new Point(198, 180);

            Controls.AddRange(new Control[] { speedLabel, _delayBox, unitLabel, _jitterBox });

            // 按钮
            _startBtn = new Button();
            _startBtn.Text = "开始输入";
            _startBtn.Size = new Size(116, 34);
            _startBtn.Location = new Point(12, 212);
            _startBtn.Click += OnStartClick;
            Controls.Add(_startBtn);

            _stopBtn = new Button();
            _stopBtn.Text = "停止";
            _stopBtn.Size = new Size(90, 34);
            _stopBtn.Location = new Point(138, 212);
            _stopBtn.Enabled = false;
            _stopBtn.Click += OnStopClick;
            Controls.Add(_stopBtn);

            // 状态栏
            _statusLabel = new Label();
            _statusLabel.Bounds = new Rectangle(12, 256, 406, 36);
            _statusLabel.ForeColor = Color.Firebrick;
            Controls.Add(_statusLabel);

            // 用法提示
            Label hint = new Label();
            hint.Bounds = new Rectangle(12, 296, 406, 22);
            hint.ForeColor = Color.Gray;
            hint.Text = "F8 停止 / 取消；F9 显示 / 隐藏。";
            Controls.Add(hint);
        }

        // 显示/恢复时不抢焦点（倒计时期间用户正在点击目标输入框）
        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeMethods.RegisterHotKey(Handle, HotKeyStopId, NativeMethods.MOD_NOREPEAT, NativeMethods.VK_F8);
            NativeMethods.RegisterHotKey(Handle, HotKeyShowId, NativeMethods.MOD_NOREPEAT, NativeMethods.VK_F9);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            NativeMethods.UnregisterHotKey(Handle, HotKeyStopId);
            NativeMethods.UnregisterHotKey(Handle, HotKeyShowId);
            base.OnHandleDestroyed(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 输入中不允许直接关闭：先取消，输入流程结束后窗口会重新出现，再关即可
            if (_typing)
            {
                if (_cts != null) _cts.Cancel();
                e.Cancel = true;
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == HotKeyStopId)
                {
                    if (_cts != null) _cts.Cancel();   // 停止/取消
                    else Show();                        // 空闲时按 F8 = 唤出窗口（不抢焦点）
                }
                else if (id == HotKeyShowId)
                {
                    Visible = !Visible;                 // F9 = 显示/隐藏（显示不抢焦点）
                }
                return;
            }
            base.WndProc(ref m);
        }

        private async void OnStartClick(object sender, EventArgs e)
        {
            if (_typing) return;
            // 归一化换行：Windows 文本是 \r\n（两个字符），只发一次回车，避免每行变成两行
            string text = _textBox.Text.Replace("\r\n", "\n");
            if (text.Length == 0)
            {
                SetStatus("请先输入要输入的文本", true);
                return;
            }

            int delay = Math.Max(0, (int)_delayBox.Value);
            bool jitter = _jitterBox.Checked;

            _typing = true;
            SetStartStopState(true);
            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;

            SetStatus(string.Format("{0} 秒后开始，请点击目标输入框（F8 取消）", CountdownSeconds), false);

            try
            {
                // 主窗口保持可见；倒计时期间请把焦点放到目标输入框

                // 倒计时，给用户时间把焦点放到目标输入框
                for (int i = CountdownSeconds; i > 0; i--)
                {
                    await Task.Delay(1000, token);
                    SetStatus(string.Format("{0} 秒后开始输入…（F8 取消）", i - 1), false);
                }

                // 逐字输入
                int total = text.Length;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < total; i++)
                {
                    token.ThrowIfCancellationRequested();
                    TypeSender.SendChar(text[i]);
                    int done = i + 1;
                    if (done % 10 == 0 || done == total)
                        SetStatus(string.Format("输入中 {0}/{1}", done, total), false);
                    int wait = jitter ? delay + _random.Next(0, Math.Max(1, delay / 2)) : delay;
                    if (wait > 0) await Task.Delay(wait, token);
                }
                sw.Stop();
                SetStatus(string.Format("输入完成：共 {0} 字，用时约 {1:F1} 秒", total, sw.Elapsed.TotalSeconds), false);
            }
            catch (OperationCanceledException)
            {
                SetStatus("已取消", true);
            }
            finally
            {
                _typing = false;
                _cts.Dispose();
                _cts = null;
                SetStartStopState(false);
                Show();
            }
        }

        private void OnStopClick(object sender, EventArgs e)
        {
            if (_cts != null) _cts.Cancel();
        }

        private void SetStartStopState(bool typing)
        {
            _startBtn.Enabled = !typing;
            _stopBtn.Enabled = typing;
        }

        private void SetStatus(string msg, bool isError)
        {
            _statusLabel.Text = msg;
            _statusLabel.ForeColor = isError ? Color.Firebrick : Color.SteelBlue;
        }

        internal static class NativeMethods
        {
            public const uint MOD_NOREPEAT = 0x4000;
            public const uint VK_F8 = 0x77;
            public const uint VK_F9 = 0x78;

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        }
    }
}