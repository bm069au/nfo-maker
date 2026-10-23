using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
[assembly: AssemblyTitle("NFO Maker")]
[assembly: AssemblyProduct("NFO Maker")]
[assembly: AssemblyCompany("Brett Martin Moore")]
[assembly: AssemblyCopyright("Copyright © 2026 Brett Martin Moore")]
[assembly: AssemblyVersion("1.0.3.0")]
[assembly: AssemblyFileVersion("1.0.3.0")]
internal static class AppBranding {
    internal const string Creator = "Brett Martin Moore";
    internal const string Email = "bm069au@gmail.com";
    internal static void SetIcon(Form form) {
        try { form.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
    }
    internal static string EmailUri(bool donation) {
        return "mailto:" + Email + "?subject=" + Uri.EscapeDataString("NFO Maker — " + (donation ? "Donation enquiry" : "Contact"));
    }
    internal static void Contact(IWin32Window owner, bool donation) {
        try { Process.Start(new ProcessStartInfo(EmailUri(donation)) { UseShellExecute = true }); }
        catch { MessageBox.Show(owner, "Please email " + Email + ". The address is also shown in Help > About.", "Contact Brett Martin Moore"); }
    }
    internal static Form CreateAbout() {
        var dialog = new Form { Text = "About NFO Maker", ClientSize = new Size(520, 330), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterParent, Font = new Font("Segoe UI", 10), AutoScaleMode = AutoScaleMode.Dpi };
            SetIcon(dialog);
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 6 };
            dialog.Controls.Add(panel);
            panel.Controls.Add(new Label { Text = "NFO Maker", AutoSize = true, Font = new Font("Segoe UI", 18, FontStyle.Bold), UseMnemonic = false });
            panel.Controls.Add(new Label { Text = "Version 1.0.3 · Created by Brett Martin Moore", AutoSize = true, Padding = new Padding(0, 8, 0, 6) });
            panel.Controls.Add(new Label { Text = "This program is free.\n\nI'm often short of funds to keep making these tools. If you can afford to help, donations are welcome and appreciated.\n\nDonating is entirely optional.", AutoSize = true, MaximumSize = new Size(470, 0) });
            var address = new LinkLabel { Text = Email, AutoSize = true, Margin = new Padding(0, 10, 0, 10) };
            address.LinkClicked += delegate { Contact(dialog, false); }; panel.Controls.Add(address);
            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            var contact = new Button { Text = "Contact", AutoSize = true };
            var donate = new Button { Text = "Donations", AutoSize = true };
            var close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.OK };
            contact.Click += delegate { Contact(dialog, false); }; donate.Click += delegate { Contact(dialog, true); };
            buttons.Controls.Add(contact); buttons.Controls.Add(donate); buttons.Controls.Add(close); panel.Controls.Add(buttons);
            dialog.AcceptButton = close; dialog.CancelButton = close; return dialog;
    }
    internal static void About(IWin32Window owner) {
        using (var dialog = CreateAbout()) dialog.ShowDialog(owner);
    }
}
