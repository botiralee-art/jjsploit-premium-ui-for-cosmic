using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace JJSploitPremium
{
    public partial class Form1 : Form
    {
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        private Panel panelRoot;
        private Panel topBar;
        private Panel bottomBar;
        private Panel editorPanel;
        private Panel scriptToolbar;
        private Label lblTitle;
        private TabControl scriptTabs;
        private TabControl mainTabs;
        private ListView listViewInstances;

        private Button btnInject;
        private Button btnExecute;
        private Button btnClear;
        private Button btnSave;
        private Button btnOpen;
        private Button btnNewScriptTab;

        private Label lblGear;

        private Timer tmrCheckRoblox;
        private Timer tmrRefreshInstances;
        private Timer tmrSaveWorkspace;
        private int _scriptTabCounter;

        // Cache for Roblox usernames to display names
        private static readonly ConcurrentDictionary<long, string> UsernameCache = new ConcurrentDictionary<long, string>();

        // Track whether this is the main window (only main does Cosmic.Setup/Initialize)
        private readonly bool _isMainWindow;

        public Form1() : this(true) { }

        public Form1(bool isMainWindow)
        {
            _isMainWindow = isMainWindow;
            if (_isMainWindow)
            {
                AppSettings.Load();
            }
            this.SetStyle(ControlStyles.ResizeRedraw, true);
            SetupSynapseUI();
            BackgroundManager.LoadDefaultSync();
            ApplyCustomSettings();
            _ = BackgroundManager.RefreshAsync(AppSettings.BackgroundImageUrl);
        }

        private void SetupSynapseUI()
        {
            this.Text = AppSettings.WindowTitle;
            this.Size = new Size(950, 600);
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = AppSettings.WindowBackground;
            this.ForeColor = AppSettings.TextColor;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9F);
            this.DoubleBuffered = true;

            panelRoot = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };
            Controls.Add(panelRoot);

            BackgroundManager.ImageChanged += () =>
            {
                if (IsHandleCreated && !IsDisposed)
                    BeginInvoke((MethodInvoker)ApplyBackgroundImage);
            };

            // --- TOP BAR ---
            topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 35,
                BackColor = AppSettings.WithAlpha(AppSettings.TitleBarBackground, 200)
            };

            lblTitle = new Label
            {
                Text = "  " + AppSettings.WindowTitle,
                ForeColor = AppSettings.AccentColor,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Dock = DockStyle.Left,
                AutoSize = true,
                Padding = new Padding(5, 8, 0, 0)
            };
            topBar.Controls.Add(lblTitle);

            // WINFORMS DOCKING RULE: First added goes FAR RIGHT. Second added goes LEFT of it.
            // 1. MINIMIZE BUTTON (Far Right)
            var btnMin = CreateTopBtn("—", Color.FromArgb(50, 50, 50), DockStyle.Right);
            btnMin.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
            topBar.Controls.Add(btnMin);

            // 2. CLOSE BUTTON (Left of Minimize)
            var btnClose = CreateTopBtn("X", Color.FromArgb(200, 0, 0), DockStyle.Right);
            btnClose.Click += (s, e) =>
            {
                if (_isMainWindow)
                    Application.Exit();
                else
                    this.Close();
            };
            topBar.Controls.Add(btnClose);

            // Gear Icon (Settings) - Top Middle
            lblGear = new Label
            {
                Text = "⚙",
                Font = new Font("Segoe UI", 16F),
                ForeColor = AppSettings.TextColor,
                AutoSize = true,
                Cursor = Cursors.Hand,
                Dock = DockStyle.None
            };
            lblGear.Click += (s, e) =>
            {
                using (var sf = new SettingsForm())
                {
                    sf.ShowDialog(this);
                }
            };
            topBar.Controls.Add(lblGear);

            topBar.MouseDown += DragForm;
            lblTitle.MouseDown += DragForm;
            panelRoot.Controls.Add(topBar);

            // Keep gear centered
            this.Resize += (s, e) => PositionGear();
            this.Load += (s, e) => PositionGear();

            // --- BOTTOM ACTION BAR ---
            bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = AppSettings.WithAlpha(AppSettings.TitleBarBackground, 220),
                Padding = new Padding(10, 10, 10, 10)
            };

            btnInject = CreateActionBtn(AppSettings.InjectText, AppSettings.AccentColor, DockStyle.Right);
            btnInject.Click += BtnInject_Click;

            btnExecute = CreateActionBtn("EXECUTE", AdjustColorBrightness(AppSettings.AccentColor, 0.2f), DockStyle.Left);
            btnExecute.Click += BtnExecute_Click;

            btnClear = CreateActionBtn("CLEAR", AppSettings.ButtonBackground, DockStyle.Left);
            btnClear.Click += (s, e) => { GetActiveScriptBox()?.Clear(); ScheduleWorkspaceSave(); };

            btnSave = CreateActionBtn("SAVE", AppSettings.ButtonBackground, DockStyle.Left);
            btnSave.Click += BtnSave_Click;

            btnOpen = CreateActionBtn("OPEN", AppSettings.ButtonBackground, DockStyle.Left);
            btnOpen.Click += BtnOpen_Click;

            bottomBar.Controls.Add(btnInject);
            bottomBar.Controls.Add(btnOpen);
            bottomBar.Controls.Add(btnSave);
            bottomBar.Controls.Add(btnClear);
            bottomBar.Controls.Add(btnExecute);

            panelRoot.Controls.Add(bottomBar);

            // --- MAIN TABS ---
            mainTabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Padding = new Point(0, 0),
                ItemSize = new Size(120, 28),
                SizeMode = TabSizeMode.Fixed,
                DrawMode = TabDrawMode.OwnerDrawFixed,
                Appearance = TabAppearance.FlatButtons,
                BackColor = AppSettings.WindowBackground
            };
            mainTabs.DrawItem += MainTabs_DrawItem;
            mainTabs.Paint += MainTabs_Paint;
            StyleTabControl(mainTabs);

            // 1. Editor Tab
            var tabEditor = new TabPage("Editor");
            tabEditor.BackColor = Color.Transparent;
            tabEditor.UseVisualStyleBackColor = false;
            tabEditor.UseVisualStyleBackColor = false;

            editorPanel = new Panel { Dock = DockStyle.Fill, BackColor = AppSettings.WithAlpha(AppSettings.PanelBackground, 120) };

            scriptToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 34,
                BackColor = AppSettings.WithAlpha(AppSettings.TitleBarBackground, 210),
                Padding = new Padding(8, 4, 8, 4)
            };

            btnNewScriptTab = new Button
            {
                Text = "+ New Tab",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = AppSettings.ButtonBackground,
                ForeColor = AppSettings.TextColor,
                Cursor = Cursors.Hand,
                Dock = DockStyle.Left
            };
            btnNewScriptTab.FlatAppearance.BorderSize = 0;
            btnNewScriptTab.Click += (s, e) => AddScriptTab(null);
            scriptToolbar.Controls.Add(btnNewScriptTab);

            scriptTabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Padding = new Point(0, 0),
                ItemSize = new Size(110, 26),
                DrawMode = TabDrawMode.OwnerDrawFixed,
                SizeMode = TabSizeMode.Fixed,
                Appearance = TabAppearance.FlatButtons,
                BackColor = AppSettings.WindowBackground
            };
            scriptTabs.DrawItem += ScriptTabs_DrawItem;
            scriptTabs.MouseDown += ScriptTabs_MouseDown;
            scriptTabs.SelectedIndexChanged += (s, e) => ScheduleWorkspaceSave();
            scriptTabs.Paint += ScriptTabs_Paint;
            scriptTabs.Invalidate();
            StyleTabControl(scriptTabs);

            var scriptTabMenu = new ContextMenuStrip();
            scriptTabMenu.BackColor = AppSettings.PanelBackground;
            scriptTabMenu.ForeColor = AppSettings.TextColor;
            var renameItem = new ToolStripMenuItem("Rename Tab");
            renameItem.Click += (s, e) => RenameSelectedScriptTab();
            var closeItem = new ToolStripMenuItem("Close Tab");
            closeItem.Click += (s, e) => CloseSelectedScriptTab();
            scriptTabMenu.Items.Add(renameItem);
            scriptTabMenu.Items.Add(closeItem);
            scriptTabs.ContextMenuStrip = scriptTabMenu;

            editorPanel.Controls.Add(scriptTabs);
            editorPanel.Controls.Add(scriptToolbar);
            tabEditor.Controls.Add(editorPanel);

            LoadScriptTabs();

            tmrSaveWorkspace = new Timer { Interval = 600 };
            tmrSaveWorkspace.Tick += (s, e) =>
            {
                tmrSaveWorkspace.Stop();
                SaveWorkspaceNow();
            };

            this.FormClosing += (s, e) => SaveWorkspaceNow();

            // 2. Instances Tab (Multi-Instance UI)
            var tabInstances = new TabPage("Instances");
            tabInstances.BackColor = Color.Transparent;
            tabInstances.UseVisualStyleBackColor = false;

            listViewInstances = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BackColor = AppSettings.BlendOverWindow(AppSettings.EditorBackground, 220),
                ForeColor = AppSettings.InstanceTextColor,
                Font = new Font(AppSettings.EditorFontName, 10F),
                BorderStyle = BorderStyle.None,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            listViewInstances.Columns.Add("PID", 80);
            listViewInstances.Columns.Add("Status", 100);
            listViewInstances.Columns.Add("Roblox User", 220);
            listViewInstances.Columns.Add("Game ID", 130);

            // Right-click context menu for instances
            var instanceMenu = new ContextMenuStrip();
            instanceMenu.BackColor = AppSettings.PanelBackground;
            instanceMenu.ForeColor = AppSettings.TextColor;

            var executeOnItem = new ToolStripMenuItem("Execute Script on This Instance");
            executeOnItem.BackColor = AppSettings.PanelBackground;
            executeOnItem.ForeColor = AppSettings.TextColor;
            executeOnItem.Click += (s, e) =>
            {
                if (listViewInstances.SelectedItems.Count == 0) return;
                int pid = (int)listViewInstances.SelectedItems[0].Tag;
                string script = GetActiveScriptText();
                if (string.IsNullOrWhiteSpace(script)) { ThemedDialog.ShowInfo(this, "Write a script in the Editor tab first."); return; }
                try { Cosmic.Execute(pid, script); }
                catch (Exception ex) { ThemedDialog.ShowError(this, ex.Message); }
            };

            var killItem = new ToolStripMenuItem("Kill This Instance");
            killItem.BackColor = AppSettings.PanelBackground;
            killItem.ForeColor = AppSettings.TextColor;
            killItem.Click += (s, e) =>
            {
                if (listViewInstances.SelectedItems.Count == 0) return;
                int pid = (int)listViewInstances.SelectedItems[0].Tag;
                try { Cosmic.Kill(pid); }
                catch (Exception ex) { ThemedDialog.ShowError(this, ex.Message); }
            };

            instanceMenu.Items.Add(executeOnItem);
            instanceMenu.Items.Add(killItem);
            listViewInstances.ContextMenuStrip = instanceMenu;

            tabInstances.Controls.Add(listViewInstances);

            mainTabs.TabPages.Add(tabEditor);
            mainTabs.TabPages.Add(tabInstances);

            panelRoot.Controls.Add(mainTabs);

            // Child windows open directly to the Instances tab
            if (!_isMainWindow)
                mainTabs.SelectedTab = tabInstances;

            this.Load += Form1_Load;
            this.Shown += Form1_Shown;
        }

        public void ApplyCustomSettings()
        {
            if (InvokeRequired)
            {
                BeginInvoke((MethodInvoker)ApplyCustomSettings);
                return;
            }

            Text = AppSettings.WindowTitle;
            BackColor = AppSettings.WindowBackground;
            ForeColor = AppSettings.TextColor;

            ApplyBackgroundImage();

            if (panelRoot != null) panelRoot.BackColor = Color.Transparent;
            if (topBar != null) topBar.BackColor = AppSettings.WithAlpha(AppSettings.TitleBarBackground, 200);
            if (bottomBar != null) bottomBar.BackColor = AppSettings.WithAlpha(AppSettings.TitleBarBackground, 200);
            if (editorPanel != null) editorPanel.BackColor = AppSettings.WithAlpha(AppSettings.PanelBackground, 120);
            if (scriptToolbar != null) scriptToolbar.BackColor = AppSettings.WithAlpha(AppSettings.TitleBarBackground, 190);

            if (lblTitle != null)
            {
                lblTitle.Text = "  " + AppSettings.WindowTitle;
                lblTitle.ForeColor = AppSettings.AccentColor;
            }

            if (lblGear != null) lblGear.ForeColor = AppSettings.TextColor;

            if (mainTabs != null)
            {
                mainTabs.BackColor = AppSettings.WindowBackground;
                foreach (TabPage page in mainTabs.TabPages)
                {
                    page.BackColor = Color.Transparent;
                    page.ForeColor = AppSettings.TextColor;
                }
                mainTabs.Invalidate();
            }

            if (scriptTabs != null)
            {
                scriptTabs.BackColor = AppSettings.WindowBackground;
                foreach (TabPage page in scriptTabs.TabPages)
                {
                    page.BackColor = Color.Transparent;
                    if (page.Controls.Count > 0 && page.Controls[0] is RichTextBox box)
                    {
                        box.BackColor = AppSettings.BlendOverWindow(AppSettings.EditorBackground, 220);
                        box.ForeColor = AppSettings.EditorTextColor;
                        box.Font = new Font(AppSettings.EditorFontName, AppSettings.EditorFontSize);
                    }
                }
                scriptTabs.Invalidate();
            }

            if (listViewInstances != null)
            {
                listViewInstances.BackColor = AppSettings.BlendOverWindow(AppSettings.EditorBackground, 220);
                listViewInstances.ForeColor = AppSettings.InstanceTextColor;
                foreach (ListViewItem item in listViewInstances.Items)
                    item.ForeColor = AppSettings.InstanceTextColor;
            }

            if (btnNewScriptTab != null)
            {
                btnNewScriptTab.BackColor = AppSettings.ButtonBackground;
                btnNewScriptTab.ForeColor = AppSettings.TextColor;
            }

            if (btnClear != null) btnClear.BackColor = AppSettings.ButtonBackground;
            if (btnSave != null) btnSave.BackColor = AppSettings.ButtonBackground;
            if (btnOpen != null) btnOpen.BackColor = AppSettings.ButtonBackground;

            if (btnExecute != null)
            {
                btnExecute.BackColor = AdjustColorBrightness(AppSettings.AccentColor, 0.2f);
                btnExecute.ForeColor = AppSettings.TextColor;
                btnExecute.FlatAppearance.MouseOverBackColor = AdjustColorBrightness(AppSettings.AccentColor, 0.4f);
            }

            UpdateInjectButtonState();
            Invalidate();
        }

        private static void StyleTabControl(TabControl tc)
        {
            foreach (TabPage page in tc.TabPages)
            {
                page.UseVisualStyleBackColor = false;
                page.BackColor = Color.Transparent;
            }
        }

        private void MainTabs_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= mainTabs.TabPages.Count) return;

            bool selected = mainTabs.SelectedIndex == e.Index;
            Color back = selected ? AppSettings.AccentColor : AppSettings.InactiveTabColor;

            using (var brush = new SolidBrush(back))
                e.Graphics.FillRectangle(brush, e.Bounds);

            TextRenderer.DrawText(e.Graphics, mainTabs.TabPages[e.Index].Text, mainTabs.Font, e.Bounds,
                AppSettings.TextColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void MainTabs_Paint(object sender, PaintEventArgs e)
        {
            PaintTabContentBackground(e.Graphics, mainTabs);
        }

        private void ScriptTabs_Paint(object sender, PaintEventArgs e)
        {
            PaintTabContentBackground(e.Graphics, scriptTabs);
        }

        private void PaintTabContentBackground(Graphics g, TabControl tabs)
        {
            if (tabs.TabPages.Count == 0) return;

            int stripBottom = tabs.GetTabRect(0).Bottom;
            var content = new Rectangle(0, stripBottom, tabs.Width, tabs.Height - stripBottom);
            if (content.Height <= 0) return;

            PaintBackgroundRegion(g, content, AppSettings.WithAlpha(AppSettings.PanelBackground, 110));
        }

        private void PaintBackgroundRegion(Graphics g, Rectangle bounds, Color extraTint)
        {
            var img = BackgroundManager.CurrentImage;
            if (img != null)
                g.DrawImage(img, bounds);
            else
                using (var fill = new SolidBrush(AppSettings.WindowBackground))
                    g.FillRectangle(fill, bounds);

            int overlay = AppSettings.BackgroundOverlayAlpha;
            if (overlay > 0)
            {
                using (var dark = new SolidBrush(Color.FromArgb(overlay, 0, 0, 0)))
                    g.FillRectangle(dark, bounds);
            }

            if (extraTint.A > 0)
            {
                using (var tint = new SolidBrush(extraTint))
                    g.FillRectangle(tint, bounds);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            PaintBackgroundRegion(e.Graphics, ClientRectangle, Color.FromArgb(0, 0, 0, 0));
        }

        private void ApplyBackgroundImage()
        {
            Invalidate(true);
            mainTabs?.Invalidate();
            scriptTabs?.Invalidate();
        }

        public async Task ReloadBackgroundAsync()
        {
            await BackgroundManager.RefreshAsync(AppSettings.BackgroundImageUrl);
            ApplyBackgroundImage();
        }

        private void PositionGear()
        {
            if (lblGear != null && topBar != null)
            {
                lblGear.Left = (topBar.Width - lblGear.Width) / 2;
                lblGear.Top = (topBar.Height - lblGear.Height) / 2;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0083) // WM_NCCALCSIZE
            {
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(AppSettings.AccentColor, 2))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
            }
        }

        private Button CreateTopBtn(string txt, Color hover, DockStyle dock)
        {
            var b = new Button { Text = txt, Width = 45, Height = 35, FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, ForeColor = Color.White, Dock = dock, Cursor = Cursors.Hand };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = hover;
            return b;
        }

        private Button CreateActionBtn(string txt, Color bg, DockStyle dock)
        {
            var b = new Button { Text = txt, Width = 110, Height = 40, FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = Color.White, Dock = dock, Cursor = Cursors.Hand, Margin = new Padding(5, 0, 0, 0) };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = AdjustColorBrightness(bg, 0.3f);
            return b;
        }

        private void DragForm(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 0x2, 0); }
        }

        private void RefreshInstanceList()
        {
            if (!Cosmic.IsRunning) return;

            var clients = Cosmic.GetClients();
            var existing = new Dictionary<int, ListViewItem>();
            foreach (ListViewItem item in listViewInstances.Items)
                existing[(int)item.Tag] = item;

            // Update or add
            foreach (var client in clients)
            {
                string userDisplay = client.UserId > 0 ? client.UserId.ToString() : "—";
                if (client.UserId > 0)
                {
                    if (UsernameCache.TryGetValue(client.UserId, out var cached))
                    {
                        userDisplay = cached;
                    }
                    else
                    {
                        userDisplay = "Loading...";
                        _ = FetchAndRefreshUsername(client.UserId);
                    }
                }

                if (existing.TryGetValue(client.Pid, out var item))
                {
                    // Update existing row
                    item.SubItems[1].Text = "Connected";
                    item.SubItems[2].Text = userDisplay;
                    item.SubItems[3].Text = client.GameId > 0 ? client.GameId.ToString() : "—";
                    existing.Remove(client.Pid);
                }
                else
                {
                    // New client
                    var newItem = new ListViewItem(client.Pid.ToString());
                    newItem.SubItems.Add("Connected");
                    newItem.SubItems.Add(userDisplay);
                    newItem.SubItems.Add(client.GameId > 0 ? client.GameId.ToString() : "—");
                    newItem.Tag = client.Pid;
                    newItem.ForeColor = AppSettings.InstanceTextColor;
                    listViewInstances.Items.Add(newItem);
                }
            }

            // Remove disconnected
            foreach (var stale in existing.Values)
                stale.Remove();
        }

        private async Task FetchAndRefreshUsername(long userId)
        {
            await GetRobloxUsernameAsync(userId);
            if (this.IsHandleCreated && !this.IsDisposed)
            {
                this.BeginInvoke((MethodInvoker)delegate { RefreshInstanceList(); });
            }
        }

        public static async Task<string> GetRobloxUsernameAsync(long userId)
        {
            if (userId <= 0) return "—";
            if (UsernameCache.TryGetValue(userId, out var cached)) return cached;

            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                    var response = await client.GetStringAsync($"https://users.roblox.com/v1/users/{userId}").ConfigureAwait(false);
                    
                    string name = ExtractJsonValue(response, "name");
                    string displayName = ExtractJsonValue(response, "displayName");

                    string result;
                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(displayName))
                    {
                        result = name.Equals(displayName, StringComparison.OrdinalIgnoreCase) 
                            ? name 
                            : $"{displayName} (@{name})";
                    }
                    else if (!string.IsNullOrEmpty(name))
                    {
                        result = name;
                    }
                    else
                    {
                        result = userId.ToString();
                    }

                    UsernameCache[userId] = result;
                    return result;
                }
            }
            catch
            {
                return userId.ToString();
            }
        }

        private static string ExtractJsonValue(string json, string key)
        {
            string search = $"\"{key}\"";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return null;
            idx = json.IndexOf(':', idx + search.Length);
            if (idx < 0) return null;
            idx = json.IndexOf('"', idx + 1);
            if (idx < 0) return null;
            int end = json.IndexOf('"', idx + 1);
            if (end < 0) return null;
            return json.Substring(idx + 1, end - idx - 1);
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                btnInject.Enabled = false;
                btnExecute.Enabled = false;

                if (_isMainWindow)
                {
                    await Cosmic.Setup();
                    Cosmic.Initialize();
                    await BackgroundManager.RefreshAsync(AppSettings.BackgroundImageUrl);
                    ApplyBackgroundImage();
                }

                while (!Cosmic.IsRunning) await Task.Delay(100);

                btnInject.Enabled = true;
                btnExecute.Enabled = true;

                // Periodic refresh of the instances list (works for all windows)
                tmrRefreshInstances = new Timer();
                tmrRefreshInstances.Interval = 1500;
                tmrRefreshInstances.Tick += (s, ev) => RefreshInstanceList();
                tmrRefreshInstances.Start();

                // Also listen for connect/disconnect events for immediate updates
                Cosmic.OnClientConnected += (pid) =>
                {
                    if (this.IsHandleCreated && !this.IsDisposed)
                        this.BeginInvoke((MethodInvoker)delegate
                        {
                            RefreshInstanceList();
                            UpdateInjectButtonState();
                        });
                };

                Cosmic.OnUserInfo += (pid, userId, gameId) =>
                {
                    if (this.IsHandleCreated && !this.IsDisposed)
                        this.BeginInvoke((MethodInvoker)delegate { RefreshInstanceList(); });
                };

                Cosmic.OnClientDisconnected += (pid) =>
                {
                    if (this.IsHandleCreated && !this.IsDisposed)
                        this.BeginInvoke((MethodInvoker)delegate
                        {
                            RefreshInstanceList();
                            UpdateInjectButtonState();
                        });
                };

                tmrCheckRoblox = new Timer();
                tmrCheckRoblox.Interval = 2000;
                tmrCheckRoblox.Tick += (s, ev) =>
                {
                    if (Cosmic.IsRunning)
                        UpdateInjectButtonState();
                };
                tmrCheckRoblox.Start();
            }
            catch (Exception ex) { ThemedDialog.ShowError(this, ex.Message, "Boot Failure"); }
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            if (_isMainWindow)
            {
                ThemedDialog.ShowInfo(this, "Click Inject when you are fully loaded into a game. Accept the Administrator prompt when it appears.", "Welcome");
            }
        }

        private void BtnExecute_Click(object sender, EventArgs e)
        {
            string script = GetActiveScriptText();
            if (string.IsNullOrWhiteSpace(script)) return;
            try { Cosmic.Execute(script); }
            catch (Exception ex) { ThemedDialog.ShowError(this, ex.Message); }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            var box = GetActiveScriptBox();
            if (box == null) return;

            var data = GetActiveScriptTabData();
            string initial = data?.FilePath;
            using (var sfd = new SaveFileDialog
            {
                Filter = "Lua Script|*.lua|Text File|*.txt",
                FileName = string.IsNullOrEmpty(initial) ? null : Path.GetFileName(initial),
                InitialDirectory = string.IsNullOrEmpty(initial) ? null : Path.GetDirectoryName(initial)
            })
            {
                if (sfd.ShowDialog() != DialogResult.OK) return;

                File.WriteAllText(sfd.FileName, box.Text);
                if (data != null)
                {
                    data.Content = box.Text;
                    data.FilePath = sfd.FileName;
                    data.Title = Path.GetFileNameWithoutExtension(sfd.FileName);
                    if (scriptTabs.SelectedTab != null)
                        scriptTabs.SelectedTab.Text = data.Title;
                }

                ScheduleWorkspaceSave();
            }
        }

        private void BtnOpen_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog { Filter = "Lua Script|*.lua;*.txt" })
            {
                if (ofd.ShowDialog() != DialogResult.OK) return;

                string content = File.ReadAllText(ofd.FileName);
                AddScriptTab(new ScriptTabData
                {
                    Title = Path.GetFileNameWithoutExtension(ofd.FileName),
                    Content = content,
                    FilePath = ofd.FileName
                });
            }
        }

        private async void BtnInject_Click(object sender, EventArgs e)
        {
            if (!Cosmic.IsRunning)
            {
                ThemedDialog.ShowWarning(this, "The executor is still starting. Wait a moment and try again.", "Inject Failed");
                return;
            }

            btnInject.Enabled = false;
            string previousText = btnInject.Text;
            btnInject.Text = "Injecting...";

            try
            {
                var result = await Cosmic.AttachAsync();
                if (result.Succeeded)
                {
                    SetInjectedState(true);
                }
                else
                {
                    UpdateInjectButtonState();
                    ThemedDialog.ShowError(this, result.Message ?? "Inject failed.", "Inject Failed");
                }
            }
            catch (Exception ex)
            {
                UpdateInjectButtonState();
                ThemedDialog.ShowError(this, ex.Message, "Inject Failed");
            }
            finally
            {
                btnInject.Enabled = true;
                if (btnInject.Text == "Injecting...")
                    btnInject.Text = previousText;
            }
        }

        private void SetInjectedState(bool injected)
        {
            if (injected)
            {
                btnInject.Text = "Injected";
                btnInject.BackColor = AppSettings.SuccessColor;
                btnInject.ForeColor = AppSettings.TextColor;
                btnInject.FlatAppearance.MouseOverBackColor = AdjustColorBrightness(AppSettings.SuccessColor, 0.2f);
            }
            else
            {
                btnInject.Text = AppSettings.InjectText;
                btnInject.BackColor = AppSettings.AccentColor;
                btnInject.ForeColor = AppSettings.TextColor;
                btnInject.FlatAppearance.MouseOverBackColor = AdjustColorBrightness(AppSettings.AccentColor, 0.2f);
            }
        }

        private void UpdateInjectButtonState()
        {
            if (btnInject == null || btnInject.Text == "Injecting...") return;

            bool hasRoblox = Cosmic.GetRobloxProcesses().Count > 0;
            bool connected = Cosmic.ClientCount > 0;
            SetInjectedState(connected && hasRoblox);
        }

        private void LoadScriptTabs()
        {
            scriptTabs.TabPages.Clear();
            _scriptTabCounter = 0;

            foreach (var tab in ScriptWorkspace.Load())
                AddScriptTab(tab, select: false);

            if (scriptTabs.TabPages.Count == 0)
                AddScriptTab(null);

            if (scriptTabs.TabPages.Count > 0)
                scriptTabs.SelectedIndex = 0;
        }

        private void AddScriptTab(ScriptTabData data, bool select = true)
        {
            if (data == null)
            {
                _scriptTabCounter++;
                data = new ScriptTabData
                {
                    Title = "Script " + _scriptTabCounter,
                    Content = string.Empty
                };
            }
            else if (data.Title != null && data.Title.StartsWith("Script "))
            {
                if (int.TryParse(data.Title.Substring(7), out int n) && n > _scriptTabCounter)
                    _scriptTabCounter = n;
            }

            var page = new TabPage(data.Title)
            {
                BackColor = Color.Transparent,
                UseVisualStyleBackColor = false,
                Tag = data
            };

            var box = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = AppSettings.BlendOverWindow(AppSettings.EditorBackground, 220),
                ForeColor = AppSettings.EditorTextColor,
                Font = new Font(AppSettings.EditorFontName, AppSettings.EditorFontSize),
                BorderStyle = BorderStyle.None,
                WordWrap = false,
                Text = data.Content ?? string.Empty
            };
            box.TextChanged += (s, e) =>
            {
                data.Content = box.Text;
                ScheduleWorkspaceSave();
            };

            page.Controls.Add(box);
            scriptTabs.TabPages.Add(page);
            if (select)
                scriptTabs.SelectedTab = page;

            ScheduleWorkspaceSave();
        }

        private RichTextBox GetActiveScriptBox()
        {
            if (scriptTabs?.SelectedTab == null) return null;
            return scriptTabs.SelectedTab.Controls.Count > 0
                ? scriptTabs.SelectedTab.Controls[0] as RichTextBox
                : null;
        }

        private string GetActiveScriptText()
        {
            return GetActiveScriptBox()?.Text ?? string.Empty;
        }

        private ScriptTabData GetActiveScriptTabData()
        {
            return scriptTabs?.SelectedTab?.Tag as ScriptTabData;
        }

        private void ScheduleWorkspaceSave()
        {
            if (tmrSaveWorkspace == null) return;
            tmrSaveWorkspace.Stop();
            tmrSaveWorkspace.Start();
        }

        private void SaveWorkspaceNow()
        {
            if (scriptTabs == null) return;

            var tabs = new List<ScriptTabData>();
            foreach (TabPage page in scriptTabs.TabPages)
            {
                var data = page.Tag as ScriptTabData;
                if (data == null) continue;

                if (page.Controls.Count > 0 && page.Controls[0] is RichTextBox box)
                    data.Content = box.Text;

                tabs.Add(new ScriptTabData
                {
                    Title = page.Text,
                    Content = data.Content,
                    FilePath = data.FilePath
                });
            }

            ScriptWorkspace.Save(tabs);
        }

        private void RenameSelectedScriptTab()
        {
            if (scriptTabs.SelectedTab == null) return;
            var data = GetActiveScriptTabData();
            string current = scriptTabs.SelectedTab.Text;

            using (var prompt = new Form
            {
                Text = "Rename Tab",
                Size = new Size(360, 140),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.FromArgb(15, 20, 35),
                ForeColor = Color.White,
                MaximizeBox = false,
                MinimizeBox = false
            })
            {
                var txt = new TextBox
                {
                    Text = current,
                    Location = new Point(16, 16),
                    Size = new Size(312, 24),
                    BackColor = Color.FromArgb(8, 12, 24),
                    ForeColor = Color.White
                };
                var ok = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Location = new Point(168, 56),
                    Size = new Size(75, 28)
                };
                var cancel = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(253, 56),
                    Size = new Size(75, 28)
                };
                prompt.Controls.AddRange(new Control[] { txt, ok, cancel });
                prompt.AcceptButton = ok;
                prompt.CancelButton = cancel;

                if (prompt.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(txt.Text))
                {
                    scriptTabs.SelectedTab.Text = txt.Text.Trim();
                    if (data != null) data.Title = scriptTabs.SelectedTab.Text;
                    ScheduleWorkspaceSave();
                }
            }
        }

        private void CloseSelectedScriptTab()
        {
            if (scriptTabs.TabPages.Count <= 1)
            {
                ThemedDialog.ShowInfo(this, "You need at least one script tab.", "Close Tab");
                return;
            }

            int index = scriptTabs.SelectedIndex;
            scriptTabs.TabPages.RemoveAt(index);
            scriptTabs.SelectedIndex = Math.Min(index, scriptTabs.TabPages.Count - 1);
            ScheduleWorkspaceSave();
        }

        private void ScriptTabs_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= scriptTabs.TabPages.Count) return;

            var page = scriptTabs.TabPages[e.Index];
            bool selected = scriptTabs.SelectedIndex == e.Index;
            Color back = selected ? AppSettings.AccentColor : AppSettings.InactiveTabColor;
            Color fore = AppSettings.TextColor;

            using (var brush = new SolidBrush(back))
                e.Graphics.FillRectangle(brush, e.Bounds);

            string text = page.Text;
            if (text.Length > 18) text = text.Substring(0, 16) + "...";

            TextRenderer.DrawText(e.Graphics, text, scriptTabs.Font, e.Bounds, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            if (scriptTabs.TabPages.Count > 1)
            {
                var closeRect = new Rectangle(e.Bounds.Right - 18, e.Bounds.Top + 7, 12, 12);
                using (var pen = new Pen(fore, 2))
                {
                    e.Graphics.DrawLine(pen, closeRect.Left, closeRect.Top, closeRect.Right, closeRect.Bottom);
                    e.Graphics.DrawLine(pen, closeRect.Right, closeRect.Top, closeRect.Left, closeRect.Bottom);
                }
            }
        }

        private void ScriptTabs_MouseDown(object sender, MouseEventArgs e)
        {
            for (int i = 0; i < scriptTabs.TabPages.Count; i++)
            {
                Rectangle bounds = scriptTabs.GetTabRect(i);
                if (!bounds.Contains(e.Location)) continue;

                if (e.Button == MouseButtons.Middle ||
                    (e.Button == MouseButtons.Left && scriptTabs.TabPages.Count > 1 &&
                     e.X >= bounds.Right - 20))
                {
                    scriptTabs.SelectedIndex = i;
                    CloseSelectedScriptTab();
                    return;
                }

                scriptTabs.SelectedIndex = i;
                return;
            }
        }

        public static Color AdjustColorBrightness(Color color, float correctionFactor)
        {
            float red = (float)color.R;
            float green = (float)color.G;
            float blue = (float)color.B;

            if (correctionFactor < 0)
            {
                correctionFactor = 1 + correctionFactor;
                red *= correctionFactor;
                green *= correctionFactor;
                blue *= correctionFactor;
            }
            else
            {
                red = (255 - red) * correctionFactor + red;
                green = (255 - green) * correctionFactor + green;
                blue = (255 - blue) * correctionFactor + blue;
            }

            return Color.FromArgb(color.A, (int)red, (int)green, (int)blue);
        }
    }
}