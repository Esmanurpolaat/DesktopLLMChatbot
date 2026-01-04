using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DesktopLLMChatbot
{
    public partial class Form1 : Form

    {
        // ---- Basit Ayarlar ----
        private const string OLLAMA_URL = "http://localhost:11434";
        private string _model = "llama3.1";
        private string _systemPrompt = "You are a helpful assistant.";

        // ---- HTTP + Cancel ----
        private readonly HttpClient _http;
        private CancellationTokenSource? _cts;

        // ---- Chat geçmiþi ----
        private readonly List<(string role, string content)> _history = new();

        // ---- UI Kontrolleri ----
        private TextBox txtModel = null!;
        private TextBox txtSystem = null!;
        private RichTextBox rtbChat = null!;
        private TextBox txtInput = null!;
        private Button btnSend = null!;
        private Button btnStop = null!;
        private Button btnClear = null!;
        private Label lblStatus = null!;

        public Form1()
        {
            Text = "LLM Chatbot (WinForms + Ollama)";
            Width = 1100;
            Height = 780;
            StartPosition = FormStartPosition.CenterScreen;

            _http = new HttpClient
            {
                BaseAddress = new Uri(OLLAMA_URL),
                Timeout = Timeout.InfiniteTimeSpan // streaming için
            };

            BuildUI();

            lblStatus.Text = $"Ollama: {OLLAMA_URL}";
            AppendSystem("Hazýr. Enter=Gönder, Shift+Enter=Alt satýr.");
        }

        private void BuildUI()
        {
            // Üst panel
            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 55,
                Padding = new Padding(10)
            };

            var lblModel = new Label { Text = "Model:", AutoSize = true, Top = 18, Left = 10 };
            txtModel = new TextBox { Left = 65, Top = 14, Width = 220, Text = _model };

            var lblSys = new Label { Text = "System:", AutoSize = true, Top = 18, Left = 310 };
            txtSystem = new TextBox { Left = 370, Top = 14, Width = 520, Text = _systemPrompt };

            btnClear = new Button { Text = "Temizle", Left = 910, Top = 12, Width = 120, Height = 30 };
            btnClear.Click += (_, __) =>
            {
                _history.Clear();
                rtbChat.Clear();
                AppendSystem("Konuþma temizlendi.");
            };

            topPanel.Controls.Add(lblModel);
            topPanel.Controls.Add(txtModel);
            topPanel.Controls.Add(lblSys);
            topPanel.Controls.Add(txtSystem);
            topPanel.Controls.Add(btnClear);

            // Chat alaný
            rtbChat = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new System.Drawing.Font("Segoe UI", 10),
                BackColor = System.Drawing.Color.White
            };

            // Status alt çizgi
            var statusPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                Padding = new Padding(10, 2, 10, 2)
            };
            lblStatus = new Label { Dock = DockStyle.Fill, Text = "..." };
            statusPanel.Controls.Add(lblStatus);

            // Alt panel (input)
            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 120,
                Padding = new Padding(10)
            };

            txtInput = new TextBox
            {
                Multiline = true,
                Left = 10,
                Top = 10,
                Width = 820,
                Height = 95,
                ScrollBars = ScrollBars.Vertical
            };

            // Enter gönder, Shift+Enter alt satýr
            txtInput.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && !e.Shift)
                {
                    e.SuppressKeyPress = true;
                    await SendAsync();
                }
            };

            btnSend = new Button
            {
                Text = "Gönder",
                Left = 850,
                Top = 10,
                Width = 180,
                Height = 45
            };
            btnSend.Click += async (_, __) => await SendAsync();

            btnStop = new Button
            {
                Text = "Durdur",
                Left = 850,
                Top = 60,
                Width = 180,
                Height = 45,
                Enabled = false
            };
            btnStop.Click += (_, __) => _cts?.Cancel();

            bottomPanel.Controls.Add(txtInput);
            bottomPanel.Controls.Add(btnSend);
            bottomPanel.Controls.Add(btnStop);

            Controls.Add(rtbChat);
            Controls.Add(statusPanel);
            Controls.Add(bottomPanel);
            Controls.Add(topPanel);
        }

        private async Task SendAsync()
        {
            var text = (txtInput.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(text)) return;

            _model = (txtModel.Text ?? "llama3.1").Trim();
            _systemPrompt = (txtSystem.Text ?? "You are a helpful assistant.").Trim();

            // UI state
            btnSend.Enabled = false;
            btnStop.Enabled = true;
            txtInput.Enabled = false;
            lblStatus.Text = "Yanýt üretiliyor...";

            // Cancel previous
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            // Kullanýcý mesajý
            txtInput.Clear();
            AppendUser(text);
            _history.Add(("user", text));

            // Asistan baþlýðý
            AppendAssistantHeader();

            var assistantBuilder = new StringBuilder();

            try
            {
                await StreamOllamaChatAsync(
                    model: _model,
                    systemPrompt: _systemPrompt,
                    messages: _history,
                    onToken: (token) =>
                    {
                        assistantBuilder.Append(token);
                        AppendAssistantToken(token);
                    },
                    ct: _cts.Token
                );

                var final = assistantBuilder.ToString().TrimEnd();
                _history.Add(("assistant", final));
                lblStatus.Text = "Bitti.";
            }
            catch (OperationCanceledException)
            {
                lblStatus.Text = "Durduruldu.";
                AppendSystem("Yanýt durduruldu.");
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Hata oluþtu.";
                AppendSystem("Hata: " + ex.Message);
            }
            finally
            {
                btnSend.Enabled = true;
                btnStop.Enabled = false;
                txtInput.Enabled = true;
                txtInput.Focus();
            }
        }

        private async Task StreamOllamaChatAsync(
            string model,
            string systemPrompt,
            IReadOnlyList<(string role, string content)> messages,
            Action<string> onToken,
            CancellationToken ct)
        {
            var payload = new
            {
                model = model,
                stream = true,
                messages = BuildMessages(systemPrompt, messages)
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var reader = new System.IO.StreamReader(stream);

            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    using var doc = JsonDocument.Parse(line);

                    if (doc.RootElement.TryGetProperty("message", out var msgEl) &&
                        msgEl.TryGetProperty("content", out var contentEl))
                    {
                        var token = contentEl.GetString() ?? "";
                        if (token.Length > 0)
                        {
                            // UI thread
                            BeginInvoke(new Action(() => onToken(token)));
                        }
                    }

                    if (doc.RootElement.TryGetProperty("done", out var doneEl) &&
                        doneEl.ValueKind == JsonValueKind.True)
                        break;
                }
                catch
                {
                    // bazý satýrlar parse edilemeyebilir
                }
            }
        }

        private static object[] BuildMessages(string systemPrompt, IReadOnlyList<(string role, string content)> messages)
        {
            var list = new List<object>();

            if (!string.IsNullOrWhiteSpace(systemPrompt))
                list.Add(new { role = "system", content = systemPrompt });

            foreach (var (role, content) in messages)
                list.Add(new { role, content });

            return list.ToArray();
        }

        // ---- Chat UI yazdýrma ----
        private void AppendUser(string text)
        {
            rtbChat.AppendText("[Sen]\n" + text + "\n\n");
            rtbChat.ScrollToCaret();
        }

        private void AppendAssistantHeader()
        {
            rtbChat.AppendText("[Asistan]\n");
            rtbChat.ScrollToCaret();
        }

        private void AppendAssistantToken(string token)
        {
            rtbChat.AppendText(token);
            rtbChat.ScrollToCaret();
        }

        private void AppendSystem(string text)
        {
            rtbChat.AppendText("\n[Sistem]\n" + text + "\n\n");
            rtbChat.ScrollToCaret();
        }
    }
}
