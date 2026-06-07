using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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

        private Panel topBar;
        private RichTextBox scriptBox;
        private TabControl mainTabs;
        private ListView listViewInstances;

        private Button btnInjectRat;
        private Button btnExecute;
        private Button btnClear;
        private Button btnSave;
        private Button btnOpen;

        private Label lblGear;
        private ContextMenuStrip settingsMenu;
        private ToolStripMenuItem autoInjectMenuItem;
        private ToolStripMenuItem multiInstanceMenuItem;

        private Timer tmrCheckRoblox;
        private Timer tmrRefreshInstances;

        // Track whether this is the main window (only main does Cosmic.Setup/Initialize)
        private readonly bool _isMainWindow;

        public Form1() : this(true) { }

        public Form1(bool isMainWindow)
        {
            _isMainWindow = isMainWindow;
            this.SetStyle(ControlStyles.ResizeRedraw, true);
            SetupSynapseUI();
        }

        private void SetupSynapseUI()
        {
            this.Text = "JJSploit Premium";
            this.Size = new Size(950, 600);
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(10, 15, 25);
            this.ForeColor = Color.White;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9F);
            this.DoubleBuffered = true;

            // --- TOP BAR ---
            topBar = new Panel { Dock = DockStyle.Top, Height = 35, BackColor = Color.FromArgb(5, 10, 20) };

            var lblTitle = new Label
            {
                Text = "  JJSploit Premium",
                ForeColor = Color.FromArgb(0, 200, 255),
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
                ForeColor = Color.LightGray,
                AutoSize = true,
                Cursor = Cursors.Hand,
                Dock = DockStyle.None
            };
            lblGear.Click += (s, e) => settingsMenu.Show(lblGear, 0, lblGear.Height);
            topBar.Controls.Add(lblGear);

            topBar.MouseDown += DragForm;
            lblTitle.MouseDown += DragForm;
            this.Controls.Add(topBar);

            // Keep gear centered
            this.Resize += (s, e) => PositionGear();
            this.Load += (s, e) => PositionGear();

            // --- SETTINGS MENU ---
            settingsMenu = new ContextMenuStrip();
            settingsMenu.BackColor = Color.FromArgb(15, 20, 35);
            settingsMenu.ForeColor = Color.White;
            settingsMenu.ShowCheckMargin = true;
            settingsMenu.ShowImageMargin = false;

            autoInjectMenuItem = new ToolStripMenuItem("Auto Inject");
            autoInjectMenuItem.CheckOnClick = true;
            autoInjectMenuItem.BackColor = Color.FromArgb(15, 20, 35);
            autoInjectMenuItem.ForeColor = Color.White;
            autoInjectMenuItem.CheckedChanged += (s, e) => {
                Cosmic.SetAutoInject(autoInjectMenuItem.Checked);
            };

            multiInstanceMenuItem = new ToolStripMenuItem("Multi Instance");
            multiInstanceMenuItem.CheckOnClick = true;
            multiInstanceMenuItem.BackColor = Color.FromArgb(15, 20, 35);
            multiInstanceMenuItem.ForeColor = Color.White;

            var newWindowMenuItem = new ToolStripMenuItem("New Window (Multi Instance)");
            newWindowMenuItem.BackColor = Color.FromArgb(15, 20, 35);
            newWindowMenuItem.ForeColor = Color.White;
            newWindowMenuItem.Click += (s, e) =>
            {
                var newForm = new Form1(false);
                newForm.Show();
            };

            settingsMenu.Items.Add(autoInjectMenuItem);
            settingsMenu.Items.Add(multiInstanceMenuItem);
            settingsMenu.Items.Add(new ToolStripSeparator());
            settingsMenu.Items.Add(newWindowMenuItem);

            // --- BOTTOM ACTION BAR ---
            var bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = Color.FromArgb(5, 10, 20), Padding = new Padding(10, 10, 10, 10) };

            btnInjectRat = CreateActionBtn("Inject Rat", Color.FromArgb(0, 100, 200), DockStyle.Right);
            btnInjectRat.Click += BtnInjectRat_Click;

            btnExecute = CreateActionBtn("EXECUTE", Color.FromArgb(0, 150, 255), DockStyle.Left);
            btnExecute.Click += BtnExecute_Click;

            btnClear = CreateActionBtn("CLEAR", Color.FromArgb(40, 50, 70), DockStyle.Left);
            btnClear.Click += (s, e) => { scriptBox.Clear(); };

            btnSave = CreateActionBtn("SAVE", Color.FromArgb(40, 50, 70), DockStyle.Left);
            btnSave.Click += BtnSave_Click;

            btnOpen = CreateActionBtn("OPEN", Color.FromArgb(40, 50, 70), DockStyle.Left);
            btnOpen.Click += BtnOpen_Click;

            bottomBar.Controls.Add(btnInjectRat);
            bottomBar.Controls.Add(btnOpen);
            bottomBar.Controls.Add(btnSave);
            bottomBar.Controls.Add(btnClear);
            bottomBar.Controls.Add(btnExecute);

            this.Controls.Add(bottomBar);

            // --- MAIN TABS ---
            mainTabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Padding = new Point(15, 8),
                ItemSize = new Size(120, 30),
                SizeMode = TabSizeMode.Fixed
            };

            // 1. Editor Tab
            var tabEditor = new TabPage("Editor");
            tabEditor.BackColor = Color.FromArgb(15, 20, 35);

            scriptBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(8, 12, 24),
                ForeColor = Color.FromArgb(0, 255, 255),
                Font = new Font("Consolas", 12F),
                BorderStyle = BorderStyle.None,
                WordWrap = false,
                Text = "-- JJSploit Premium\n-- Write your Luau code here\n\nprint('Hello World!')"
            };
            tabEditor.Controls.Add(scriptBox);

            // 2. Instances Tab (Multi-Instance UI)
            var tabInstances = new TabPage("Instances");
            tabInstances.BackColor = Color.FromArgb(15, 20, 35);

            listViewInstances = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(8, 12, 24),
                ForeColor = Color.FromArgb(0, 255, 255),
                Font = new Font("Consolas", 10F),
                BorderStyle = BorderStyle.None,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            listViewInstances.Columns.Add("PID", 80);
            listViewInstances.Columns.Add("Status", 100);
            listViewInstances.Columns.Add("User ID", 130);
            listViewInstances.Columns.Add("Game ID", 130);

            // Right-click context menu for instances
            var instanceMenu = new ContextMenuStrip();
            instanceMenu.BackColor = Color.FromArgb(15, 20, 35);
            instanceMenu.ForeColor = Color.White;

            var executeOnItem = new ToolStripMenuItem("Execute Script on This Instance");
            executeOnItem.BackColor = Color.FromArgb(15, 20, 35);
            executeOnItem.ForeColor = Color.White;
            executeOnItem.Click += (s, e) =>
            {
                if (listViewInstances.SelectedItems.Count == 0) return;
                int pid = (int)listViewInstances.SelectedItems[0].Tag;
                string script = scriptBox.Text;
                if (string.IsNullOrWhiteSpace(script)) { MessageBox.Show("Write a script in the Editor tab first."); return; }
                try { Cosmic.Execute(pid, script); }
                catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
            };

            var killItem = new ToolStripMenuItem("Kill This Instance");
            killItem.BackColor = Color.FromArgb(15, 20, 35);
            killItem.ForeColor = Color.White;
            killItem.Click += (s, e) =>
            {
                if (listViewInstances.SelectedItems.Count == 0) return;
                int pid = (int)listViewInstances.SelectedItems[0].Tag;
                try { Cosmic.Kill(pid); }
                catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
            };

            instanceMenu.Items.Add(executeOnItem);
            instanceMenu.Items.Add(killItem);
            listViewInstances.ContextMenuStrip = instanceMenu;

            tabInstances.Controls.Add(listViewInstances);

            mainTabs.TabPages.Add(tabEditor);
            mainTabs.TabPages.Add(tabInstances);

            this.Controls.Add(mainTabs);

            // Child windows open directly to the Instances tab
            if (!_isMainWindow)
                mainTabs.SelectedTab = tabInstances;

            this.Load += Form1_Load;
            this.Shown += Form1_Shown;
        }

        private void PositionGear()
        {
            if (lblGear != null && topBar != null)
            {
                lblGear.Left = (topBar.Width - lblGear.Width) / 2;
                lblGear.Top = (topBar.Height - lblGear.Height) / 2;
            }
        }

        // Fixes the white lines at the sides
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
            using (Pen pen = new Pen(Color.FromArgb(5, 10, 20), 2))
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
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(bg, 0.3f);
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
                if (existing.TryGetValue(client.Pid, out var item))
                {
                    // Update existing row
                    item.SubItems[1].Text = "Connected";
                    item.SubItems[2].Text = client.UserId > 0 ? client.UserId.ToString() : "—";
                    item.SubItems[3].Text = client.GameId > 0 ? client.GameId.ToString() : "—";
                    existing.Remove(client.Pid);
                }
                else
                {
                    // New client
                    var newItem = new ListViewItem(client.Pid.ToString());
                    newItem.SubItems.Add("Connected");
                    newItem.SubItems.Add(client.UserId > 0 ? client.UserId.ToString() : "—");
                    newItem.SubItems.Add(client.GameId > 0 ? client.GameId.ToString() : "—");
                    newItem.Tag = client.Pid;
                    newItem.ForeColor = Color.FromArgb(0, 255, 180);
                    listViewInstances.Items.Add(newItem);
                }
            }

            // Remove disconnected
            foreach (var stale in existing.Values)
                stale.Remove();
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                if (_isMainWindow)
                {
                    await Cosmic.Setup();
                    Cosmic.Initialize();
                }

                // Periodic refresh of the instances list (works for all windows)
                tmrRefreshInstances = new Timer();
                tmrRefreshInstances.Interval = 1500;
                tmrRefreshInstances.Tick += (s, ev) => RefreshInstanceList();
                tmrRefreshInstances.Start();

                // Also listen for connect/disconnect events for immediate updates
                Cosmic.OnClientConnected += (pid) =>
                {
                    if (this.IsHandleCreated && !this.IsDisposed)
                        this.BeginInvoke((MethodInvoker)delegate { RefreshInstanceList(); });
                };

                Cosmic.OnUserInfo += (pid, userId, gameId) =>
                {
                    if (this.IsHandleCreated && !this.IsDisposed)
                        this.BeginInvoke((MethodInvoker)delegate { RefreshInstanceList(); });
                };

                Cosmic.OnClientDisconnected += (pid) =>
                {
                    if (this.IsHandleCreated && !this.IsDisposed)
                        this.BeginInvoke((MethodInvoker)delegate { RefreshInstanceList(); });
                };

                tmrCheckRoblox = new Timer();
                tmrCheckRoblox.Interval = 2000;
                tmrCheckRoblox.Tick += (s, ev) => {
                    if (Cosmic.IsRunning)
                    {
                        var procs = Cosmic.GetRobloxProcesses();
                        if (procs.Count == 0 && btnInjectRat.Text == "Injected Rat")
                        {
                            btnInjectRat.Text = "Inject Rat";
                            btnInjectRat.BackColor = Color.FromArgb(0, 100, 200);
                        }
                    }
                };
                tmrCheckRoblox.Start();
            }
            catch (Exception ex) { MessageBox.Show("Boot failure: " + ex.Message); }
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            if (_isMainWindow)
            {
                MessageBox.Show("this was made by UD\n\nclick inject Rat to inject to roblox and get some free Rats in your pc\n\nthanks for using jjsploit premium", "Welcome", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnExecute_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(scriptBox.Text)) return;
            try { Cosmic.Execute(scriptBox.Text); }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog { Filter = "Lua Script|*.lua|Text File|*.txt" })
            {
                if (sfd.ShowDialog() == DialogResult.OK) { File.WriteAllText(sfd.FileName, scriptBox.Text); }
            }
        }

        private void BtnOpen_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog { Filter = "Lua Script|*.lua;*.txt" })
            {
                if (ofd.ShowDialog() == DialogResult.OK) { scriptBox.Text = File.ReadAllText(ofd.FileName); }
            }
        }

        private void BtnInjectRat_Click(object sender, EventArgs e)
        {
            try
            {
                Cosmic.Attach();
                btnInjectRat.Text = "Injected Rat";
                btnInjectRat.BackColor = Color.FromArgb(16, 124, 16);
                btnInjectRat.FlatAppearance.MouseOverBackColor = Color.FromArgb(20, 150, 20);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Inject Rat Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}