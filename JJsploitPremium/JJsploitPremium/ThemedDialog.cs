using System.Drawing;
using System.Windows.Forms;

namespace JJSploitPremium
{
    public static class ThemedDialog
    {
        public static void ShowInfo(IWin32Window owner, string message, string title = "Info")
        {
            Show(owner, message, title, MessageBoxIcon.Information);
        }

        public static void ShowWarning(IWin32Window owner, string message, string title = "Warning")
        {
            Show(owner, message, title, MessageBoxIcon.Warning);
        }

        public static void ShowError(IWin32Window owner, string message, string title = "Error")
        {
            Show(owner, message, title, MessageBoxIcon.Error);
        }

        public static void Show(IWin32Window owner, string message, string title, MessageBoxIcon icon)
        {
            using (var form = new Form
            {
                Text = title,
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.CenterParent,
                BackColor = AppSettings.PanelBackground,
                ForeColor = AppSettings.TextColor,
                Font = new Font("Segoe UI", 9F),
                Size = new Size(420, 200),
                ShowInTaskbar = false,
                TopMost = true
            })
            {
                var topBar = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 36,
                    BackColor = AppSettings.TitleBarBackground
                };
                var lblTitle = new Label
                {
                    Text = "  " + title,
                    Dock = DockStyle.Fill,
                    ForeColor = AppSettings.AccentColor,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleLeft
                };
                topBar.Controls.Add(lblTitle);

                var iconLabel = new Label
                {
                    Text = GetIconChar(icon),
                    Font = new Font("Segoe UI", 22F),
                    ForeColor = AppSettings.AccentColor,
                    Location = new Point(16, 48),
                    AutoSize = true
                };

                var lblMessage = new Label
                {
                    Text = message,
                    Location = new Point(56, 50),
                    Size = new Size(340, 90),
                    ForeColor = AppSettings.TextColor,
                    BackColor = AppSettings.PanelBackground
                };

                var btnOk = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Size = new Size(90, 32),
                    Location = new Point(306, 148),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = AppSettings.AccentColor,
                    ForeColor = AppSettings.TextColor,
                    Cursor = Cursors.Hand
                };
                btnOk.FlatAppearance.BorderSize = 0;

                form.Controls.AddRange(new Control[] { topBar, iconLabel, lblMessage, btnOk });
                form.AcceptButton = btnOk;
                form.CancelButton = btnOk;

                form.Paint += (s, e) =>
                {
                    using (var pen = new Pen(AppSettings.AccentColor, 2))
                        e.Graphics.DrawRectangle(pen, 0, 0, form.Width - 1, form.Height - 1);
                };

                if (owner != null)
                    form.ShowDialog(owner);
                else
                    form.ShowDialog();
            }
        }

        private static string GetIconChar(MessageBoxIcon icon)
        {
            switch (icon)
            {
                case MessageBoxIcon.Error: return "✕";
                case MessageBoxIcon.Warning: return "!";
                default: return "i";
            }
        }
    }
}
