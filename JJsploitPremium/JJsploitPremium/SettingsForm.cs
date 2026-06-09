using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace JJSploitPremium
{
    public class SettingsForm : Form
    {
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        private Panel topBar;
        private Panel scrollPanel;
        private TextBox txtWindowTitle;
        private TextBox txtInjectText;
        private TextBox txtBackgroundUrl;
        private TrackBar trkOverlay;
        private Label lblOverlayValue;
        private NumericUpDown numFontSize;
        private ComboBox cmbFontName;
        private CheckBox chkAutoInject;
        private readonly Dictionary<string, Color> _colors = new Dictionary<string, Color>();

        public SettingsForm()
        {
            Size = new Size(500, 720);
            FormBorderStyle = FormBorderStyle.None;
            BackColor = AppSettings.PanelBackground;
            ForeColor = AppSettings.TextColor;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9F);
            DoubleBuffered = true;
            SetupUI();
        }

        private void SetupUI()
        {
            topBar = new Panel { Dock = DockStyle.Top, Height = 35, BackColor = AppSettings.TitleBarBackground };

            var lblHeader = new Label
            {
                Text = "  Customise Everything",
                ForeColor = AppSettings.AccentColor,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Dock = DockStyle.Left,
                AutoSize = true,
                Padding = new Padding(5, 8, 0, 0)
            };
            topBar.Controls.Add(lblHeader);

            var btnClose = new Button
            {
                Text = "X",
                Width = 45,
                Height = 35,
                FlatStyle = FlatStyle.Flat,
                BackColor = AppSettings.TitleBarBackground,
                ForeColor = AppSettings.TextColor,
                Dock = DockStyle.Right,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 0, 0);
            btnClose.Click += (s, e) => Close();
            topBar.Controls.Add(btnClose);

            topBar.MouseDown += DragForm;
            lblHeader.MouseDown += DragForm;
            Controls.Add(topBar);

            var bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = AppSettings.TitleBarBackground
            };

            var btnSave = new Button
            {
                Text = "Apply & Save",
                Location = new Point(268, 10),
                Size = new Size(100, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppSettings.AccentColor,
                ForeColor = AppSettings.TextColor,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;

            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(378, 10),
                Size = new Size(100, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppSettings.ButtonBackground,
                ForeColor = AppSettings.TextColor,
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => Close();

            bottomBar.Controls.Add(btnSave);
            bottomBar.Controls.Add(btnCancel);
            Controls.Add(bottomBar);

            scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = AppSettings.BlendOverWindow(AppSettings.PanelBackground, 240),
                Padding = new Padding(0, 0, 4, 8)
            };
            Controls.Add(scrollPanel);

            int y = 12;

            txtWindowTitle = AddTextField("Window Title:", AppSettings.WindowTitle, ref y);
            txtInjectText = AddTextField("Inject Button Text:", AppSettings.InjectText, ref y);

            y += 8;
            AddSectionLabel("Background", ref y);

            var lblBgHint = new Label
            {
                Text = "Paste an image URL (png, jpg, webp, gif):",
                Location = new Point(16, y),
                Size = new Size(450, 18),
                ForeColor = AppSettings.TextColor,
                BackColor = AppSettings.PanelBackground
            };
            scrollPanel.Controls.Add(lblBgHint);
            y += 20;

            txtBackgroundUrl = new TextBox
            {
                Location = new Point(16, y),
                Size = new Size(450, 25),
                BackColor = AppSettings.EditorBackground,
                ForeColor = AppSettings.TextColor,
                BorderStyle = BorderStyle.FixedSingle,
                Text = AppSettings.BackgroundImageUrl
            };
            scrollPanel.Controls.Add(txtBackgroundUrl);
            y += 34;

            var lblOverlay = new Label
            {
                Text = "Background overlay darkness:",
                Location = new Point(16, y),
                Size = new Size(220, 18),
                ForeColor = AppSettings.TextColor,
                BackColor = AppSettings.PanelBackground
            };
            scrollPanel.Controls.Add(lblOverlay);

            lblOverlayValue = new Label
            {
                Text = AppSettings.BackgroundOverlayAlpha.ToString(),
                Location = new Point(380, y),
                Size = new Size(80, 18),
                ForeColor = AppSettings.AccentColor,
                BackColor = AppSettings.PanelBackground,
                TextAlign = ContentAlignment.MiddleRight
            };
            scrollPanel.Controls.Add(lblOverlayValue);
            y += 22;

            trkOverlay = new TrackBar
            {
                Location = new Point(12, y),
                Size = new Size(456, 45),
                Minimum = 0,
                Maximum = 220,
                TickFrequency = 20,
                Value = Math.Max(0, Math.Min(220, AppSettings.BackgroundOverlayAlpha)),
                BackColor = AppSettings.PanelBackground
            };
            trkOverlay.ValueChanged += (s, e) => lblOverlayValue.Text = trkOverlay.Value.ToString();
            scrollPanel.Controls.Add(trkOverlay);
            y += 48;

            y += 8;
            AddSectionLabel("Colors", ref y);

            _colors["AccentColor"] = AppSettings.AccentColor;
            _colors["WindowBackground"] = AppSettings.WindowBackground;
            _colors["TitleBarBackground"] = AppSettings.TitleBarBackground;
            _colors["PanelBackground"] = AppSettings.PanelBackground;
            _colors["EditorBackground"] = AppSettings.EditorBackground;
            _colors["EditorTextColor"] = AppSettings.EditorTextColor;
            _colors["ButtonBackground"] = AppSettings.ButtonBackground;
            _colors["TextColor"] = AppSettings.TextColor;
            _colors["InactiveTabColor"] = AppSettings.InactiveTabColor;
            _colors["SuccessColor"] = AppSettings.SuccessColor;
            _colors["InstanceTextColor"] = AppSettings.InstanceTextColor;

            AddColorRow("Accent / Primary", "AccentColor", ref y);
            AddColorRow("Window Background", "WindowBackground", ref y);
            AddColorRow("Title Bar", "TitleBarBackground", ref y);
            AddColorRow("Panel Background", "PanelBackground", ref y);
            AddColorRow("Editor Background", "EditorBackground", ref y);
            AddColorRow("Editor Text", "EditorTextColor", ref y);
            AddColorRow("Secondary Buttons", "ButtonBackground", ref y);
            AddColorRow("General Text", "TextColor", ref y);
            AddColorRow("Inactive Tabs", "InactiveTabColor", ref y);
            AddColorRow("Injected / Success", "SuccessColor", ref y);
            AddColorRow("Instance List Text", "InstanceTextColor", ref y);

            y += 8;
            AddSectionLabel("Editor Font", ref y);

            var fontRow = new Panel
            {
                Location = new Point(16, y),
                Size = new Size(450, 30),
                BackColor = AppSettings.PanelBackground
            };

            cmbFontName = new ComboBox
            {
                Location = new Point(0, 2),
                Size = new Size(200, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = AppSettings.EditorBackground,
                ForeColor = AppSettings.TextColor,
                FlatStyle = FlatStyle.Flat
            };
            cmbFontName.Items.AddRange(new object[] { "Consolas", "Cascadia Mono", "Courier New", "Lucida Console", "Segoe UI" });
            cmbFontName.SelectedItem = AppSettings.EditorFontName;
            if (cmbFontName.SelectedIndex < 0) cmbFontName.SelectedIndex = 0;

            numFontSize = new NumericUpDown
            {
                Location = new Point(220, 2),
                Size = new Size(60, 25),
                Minimum = 8,
                Maximum = 32,
                Value = AppSettings.EditorFontSize,
                BackColor = AppSettings.EditorBackground,
                ForeColor = AppSettings.TextColor
            };

            fontRow.Controls.Add(cmbFontName);
            fontRow.Controls.Add(numFontSize);
            scrollPanel.Controls.Add(fontRow);
            y += 38;

            y += 8;
            AddSectionLabel("Options", ref y);

            chkAutoInject = new CheckBox
            {
                Text = "Auto inject new Roblox instances",
                Location = new Point(16, y),
                Size = new Size(300, 22),
                Checked = Cosmic.AutoInjectEnabled,
                FlatStyle = FlatStyle.Flat,
                ForeColor = AppSettings.TextColor,
                BackColor = AppSettings.PanelBackground
            };
            scrollPanel.Controls.Add(chkAutoInject);
            y += 30;

            var btnNewWindow = new Button
            {
                Text = "Open Multi-Instance Window",
                Location = new Point(16, y),
                Size = new Size(200, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppSettings.ButtonBackground,
                ForeColor = AppSettings.TextColor,
                Cursor = Cursors.Hand
            };
            btnNewWindow.FlatAppearance.BorderSize = 0;
            btnNewWindow.Click += (s, e) => new Form1(false).Show();
            scrollPanel.Controls.Add(btnNewWindow);
            y += 44;

            var btnReset = new Button
            {
                Text = "Reset to Defaults",
                Location = new Point(16, y),
                Size = new Size(140, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppSettings.ButtonBackground,
                ForeColor = AppSettings.TextColor,
                Cursor = Cursors.Hand
            };
            btnReset.FlatAppearance.BorderSize = 0;
            btnReset.Click += BtnReset_Click;
            scrollPanel.Controls.Add(btnReset);

            scrollPanel.AutoScrollMinSize = new Size(0, y + 24);
        }

        private void AddSectionLabel(string text, ref int y)
        {
            var lbl = new Label
            {
                Text = text,
                Location = new Point(16, y),
                Size = new Size(450, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = AppSettings.AccentColor,
                BackColor = AppSettings.PanelBackground
            };
            scrollPanel.Controls.Add(lbl);
            y += 24;
        }

        private TextBox AddTextField(string label, string value, ref int y)
        {
            var lbl = new Label
            {
                Text = label,
                Location = new Point(16, y),
                Size = new Size(450, 18),
                ForeColor = AppSettings.TextColor,
                BackColor = AppSettings.PanelBackground
            };
            scrollPanel.Controls.Add(lbl);
            y += 20;

            var txt = new TextBox
            {
                Location = new Point(16, y),
                Size = new Size(450, 25),
                BackColor = AppSettings.EditorBackground,
                ForeColor = AppSettings.TextColor,
                BorderStyle = BorderStyle.FixedSingle,
                Text = value
            };
            scrollPanel.Controls.Add(txt);
            y += 32;
            return txt;
        }

        private void AddColorRow(string label, string key, ref int y)
        {
            var row = new Panel
            {
                Location = new Point(16, y),
                Size = new Size(450, 30),
                BackColor = AppSettings.PanelBackground
            };

            var lbl = new Label
            {
                Text = label,
                Location = new Point(0, 6),
                Size = new Size(200, 20),
                ForeColor = AppSettings.TextColor,
                BackColor = AppSettings.PanelBackground
            };

            var preview = new Button
            {
                Location = new Point(210, 2),
                Size = new Size(50, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = _colors[key],
                Cursor = Cursors.Hand,
                Tag = key
            };
            preview.FlatAppearance.BorderSize = 1;
            preview.FlatAppearance.BorderColor = AppSettings.InactiveTabColor;
            preview.Click += (s, e) =>
            {
                string colorKey = (string)((Button)s).Tag;
                using (var cd = new ColorDialog { Color = _colors[colorKey], FullOpen = true })
                {
                    if (cd.ShowDialog() == DialogResult.OK)
                    {
                        _colors[colorKey] = cd.Color;
                        ((Button)s).BackColor = cd.Color;
                    }
                }
            };

            var presets = new[]
            {
                Color.FromArgb(0, 100, 200),
                Color.FromArgb(120, 40, 180),
                Color.FromArgb(200, 40, 40),
                Color.FromArgb(40, 150, 40),
                Color.FromArgb(230, 140, 0)
            };

            int px = 270;
            foreach (var preset in presets)
            {
                var btn = new Button
                {
                    Location = new Point(px, 4),
                    Size = new Size(22, 22),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = preset,
                    Cursor = Cursors.Hand,
                    Tag = key
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.Click += (s, e) =>
                {
                    string colorKey = (string)((Button)s).Tag;
                    _colors[colorKey] = preset;
                    preview.BackColor = preset;
                };
                row.Controls.Add(btn);
                px += 26;
            }

            row.Controls.Add(lbl);
            row.Controls.Add(preview);
            scrollPanel.Controls.Add(row);
            y += 34;
        }

        private async void BtnReset_Click(object sender, EventArgs e)
        {
            AppSettings.ResetToDefaults();
            AppSettings.Save();
            var owner = Owner;
            Close();
            foreach (Form form in Application.OpenForms)
            {
                if (form is Form1 mainForm)
                {
                    mainForm.ApplyCustomSettings();
                    await mainForm.ReloadBackgroundAsync();
                }
            }
            using (var sf = new SettingsForm())
                sf.ShowDialog(owner);
        }

        private async void BtnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtWindowTitle.Text))
            {
                ThemedDialog.ShowWarning(this, "Please enter a valid window title.");
                return;
            }

            AppSettings.WindowTitle = txtWindowTitle.Text.Trim();
            AppSettings.InjectText = txtInjectText.Text.Trim();
            AppSettings.BackgroundImageUrl = string.IsNullOrWhiteSpace(txtBackgroundUrl.Text)
                ? BackgroundManager.DefaultImageUrl
                : txtBackgroundUrl.Text.Trim();
            AppSettings.BackgroundOverlayAlpha = trkOverlay.Value;
            AppSettings.AccentColor = _colors["AccentColor"];
            AppSettings.WindowBackground = _colors["WindowBackground"];
            AppSettings.TitleBarBackground = _colors["TitleBarBackground"];
            AppSettings.PanelBackground = _colors["PanelBackground"];
            AppSettings.EditorBackground = _colors["EditorBackground"];
            AppSettings.EditorTextColor = _colors["EditorTextColor"];
            AppSettings.ButtonBackground = _colors["ButtonBackground"];
            AppSettings.TextColor = _colors["TextColor"];
            AppSettings.InactiveTabColor = _colors["InactiveTabColor"];
            AppSettings.SuccessColor = _colors["SuccessColor"];
            AppSettings.InstanceTextColor = _colors["InstanceTextColor"];
            AppSettings.EditorFontSize = (int)numFontSize.Value;
            AppSettings.EditorFontName = cmbFontName.SelectedItem?.ToString() ?? "Consolas";
            AppSettings.Save();

            Cosmic.SetAutoInject(chkAutoInject.Checked);

            foreach (Form form in Application.OpenForms)
            {
                if (form is Form1 mainForm)
                {
                    mainForm.ApplyCustomSettings();
                    await mainForm.ReloadBackgroundAsync();
                }
            }

            Close();
        }

        private void DragForm(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 0x2, 0);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(AppSettings.AccentColor, 2))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }
}
