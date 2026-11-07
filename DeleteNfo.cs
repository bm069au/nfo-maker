using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace NfoMaker {
    internal sealed class NfoDeletion {
        internal string Path;
        internal long Length, Modified;
        internal static NfoDeletion Capture(string path) {
            var info = new FileInfo(path);
            if (!info.Extension.Equals(".nfo", StringComparison.OrdinalIgnoreCase) || (info.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Only ordinary .nfo files can be deleted: " + path);
            return new NfoDeletion { Path = info.FullName, Length = info.Length, Modified = info.LastWriteTimeUtc.Ticks };
        }
        internal void Delete(bool backup) {
            var current = Capture(Path);
            if (current.Length != Length || current.Modified != Modified) throw new IOException("File changed since preview. Review the list again: " + Path);
            if (backup) File.Move(Path, Path + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".bak");
            else File.Delete(Path);
        }
        internal static List<NfoDeletion> InFolder(string folder, bool recursive) {
            var result = new List<NfoDeletion>();
            var pending = new Stack<string>(); pending.Push(System.IO.Path.GetFullPath(folder));
            while (pending.Count > 0) {
                string directory = pending.Pop();
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new IOException("Directory links are not followed: " + directory);
                foreach (string file in Directory.GetFiles(directory).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    if (System.IO.Path.GetExtension(file).Equals(".nfo", StringComparison.OrdinalIgnoreCase) && (File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0) result.Add(Capture(file));
                if (recursive) foreach (string child in Directory.GetDirectories(directory))
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) pending.Push(child);
            }
            return result;
        }
    }
    internal sealed class DeleteNfoDialog : Form {
        internal DeleteNfoDialog(string path, bool directory) {
            Text = directory ? "Delete NFOs in folder" : "Delete this NFO"; AppBranding.SetIcon(this);
            Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi; Size = new Size(850, 510); MinimumSize = new Size(650, 420); StartPosition = FormStartPosition.CenterParent;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(12) };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var explanation = new Label { AutoSize = true, MaximumSize = new Size(780, 0), Text = "Only the listed .nfo files will be removed. Videos are untouched.\r\n" + path };
            var recursive = new CheckBox { Text = "Include subfolders", AutoSize = true, Visible = directory, Checked = directory };
            var files = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true, IntegralHeight = false };
            var backup = new CheckBox { Text = "Keep a backup (.bak) beside each NFO before removing it", Checked = true, AutoSize = true };
            var warning = new Label { AutoSize = true, MaximumSize = new Size(780, 0) };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            var remove = new Button { Text = "Delete listed NFOs", AutoSize = true, Padding = new Padding(10, 4, 10, 4) };
            buttons.Controls.Add(cancel); buttons.Controls.Add(remove);
            layout.Controls.Add(explanation); layout.Controls.Add(recursive); layout.Controls.Add(files); layout.Controls.Add(backup); layout.Controls.Add(warning); layout.Controls.Add(buttons); Controls.Add(layout); CancelButton = cancel;
            List<NfoDeletion> preview = new List<NfoDeletion>();
            Action refresh = delegate {
                try {
                    preview = directory ? NfoDeletion.InFolder(path, recursive.Checked) : new List<NfoDeletion> { NfoDeletion.Capture(path) };
                    files.Items.Clear();
                    foreach (var file in preview) files.Items.Add(file.Path);
                    remove.Enabled = preview.Count > 0;
                    explanation.Text = preview.Count + " NFO(s) in the preview. Only these files will be removed.\r\n" + path;
                    if (preview.Count == 0) warning.Text = "No NFO files found in this scope.";
                }
                catch (Exception ex) { preview.Clear(); files.Items.Clear(); remove.Enabled = false; warning.Text = "Cannot prepare deletion: " + ex.Message; }
            };
            Action describe = delegate { warning.Text = backup.Checked ? "Deletion keeps the original bytes in .bak files, which can be renamed to restore them." : "Permanent deletion: NO backup and NO Recycle Bin. The listed NFOs cannot be restored by this app."; remove.Text = backup.Checked ? "Delete with backup" : "Delete permanently"; };
            recursive.CheckedChanged += delegate { describe(); refresh(); }; backup.CheckedChanged += delegate { describe(); };
            remove.Click += delegate {
                int count = 0;
                try { foreach (var file in preview) { file.Delete(backup.Checked); count++; } MessageBox.Show(this, count + " NFO(s) deleted." + (backup.Checked ? " Backups kept beside the originals." : " No backups created."), "Deletion complete"); DialogResult = DialogResult.OK; Close(); }
                catch (Exception ex) { MessageBox.Show(this, "Stopped after " + count + " deletion(s). " + ex.Message, "Deletion stopped", MessageBoxButtons.OK, MessageBoxIcon.Error); describe(); refresh(); }
            };
            describe(); refresh();
        }
    }
}
