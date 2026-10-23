using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NfoMaker {
    internal sealed class BatchItem {
        public bool Selected { get; set; }
        public string File { get; set; }
        public string Season { get; set; }
        public string Episode { get; set; }
        public string Status { get; set; }
        internal string Path;
        internal byte[] Expected;
        internal string Target { get { return System.IO.Path.ChangeExtension(Path, ".nfo"); } }
    }
    internal static class Batch {
        internal static List<BatchItem> Scan(string folder) {
            var result = new List<BatchItem>();
            foreach (string p in Directory.GetFiles(folder).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) {
                if (!Regex.IsMatch(System.IO.Path.GetExtension(p), @"^\.(mkv|mp4|avi|m4v|mov|wmv|mpg|mpeg|ts|m2ts|iso|strm)$", RegexOptions.IgnoreCase)) continue;
                var m = Regex.Match(System.IO.Path.GetFileNameWithoutExtension(p), @"(?i)(?<![a-z0-9])S(\d{1,4})E(\d{1,5})(?!\d|[ ._-]*E\d|[ ._-]*-\d)");
                bool known = m.Success;
                var item = new BatchItem { Path = p, File = System.IO.Path.GetFileName(p), Season = known ? m.Groups[1].Value : "", Episode = known ? m.Groups[2].Value : "" };
                item.Expected = System.IO.File.Exists(item.Target) ? System.IO.File.ReadAllBytes(item.Target) : null;
                item.Selected = known && item.Expected == null;
                item.Status = item.Expected != null ? "Existing - not selected" : known ? "Missing - ready" : "Check numbering";
                result.Add(item);
            }
            return result;
        }
        internal static int Save(List<BatchItem> items, string series, bool replace) {
            var work = new List<KeyValuePair<BatchItem, Metadata>>();
            foreach (var row in items.Where(x => x.Selected)) {
                if (row.Expected != null && !replace) { row.Status = "Skipped existing"; continue; }
                if (string.IsNullOrWhiteSpace(row.Season) || string.IsNullOrWhiteSpace(row.Episode)) throw new ArgumentException("Enter season and episode for " + row.File + " or untick it.");
                Metadata m = row.Expected == null ? new Metadata { Kind = "TV Show" } : Nfo.Load(row.Target);
                if (!m.IsEpisode) throw new ArgumentException("Not an episode NFO: " + row.Target);
                if (!string.IsNullOrWhiteSpace(series)) m.Series = series.Trim();
                m.Season = row.Season; m.Episode = row.Episode; Nfo.Build(m);
                work.Add(new KeyValuePair<BatchItem, Metadata>(row, m));
            }
            int saved = 0;
            foreach (var job in work) {
                var row = job.Key;
                try {
                    bool exists = System.IO.File.Exists(row.Target);
                    if (exists != (row.Expected != null) || (exists && !System.IO.File.ReadAllBytes(row.Target).SequenceEqual(row.Expected))) throw new IOException("NFO changed since folder selection. Reload the folder.");
                    string backup = Nfo.Save(row.Target, job.Value);
                    row.Expected = System.IO.File.ReadAllBytes(row.Target); row.Selected = false;
                    row.Status = backup == null ? "Created" : "Updated; backup: " + backup;
                    saved++;
                } catch (Exception ex) { row.Status = "FAILED: " + ex.Message; throw new IOException("Stopped after " + saved + " saved. " + row.File + ": " + ex.Message, ex); }
            }
            return saved;
        }
    }
    internal static class Association {
        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
        internal static void RegisterVideoMenu() {
            foreach (string extension in new [] { ".mkv", ".mp4", ".avi", ".m4v", ".mov", ".wmv", ".mpg", ".mpeg", ".ts", ".m2ts", ".iso", ".strm" }) {
                string key = @"Software\Classes\SystemFileAssociations\" + extension + @"\shell\NfoMaker.Create";
                using (var k = Registry.CurrentUser.CreateSubKey(key)) {
                    k.SetValue("", "Create NFO file");
                    k.SetValue("Icon", "\"" + Application.ExecutablePath + "\",0");
                    k.SetValue("MultiSelectModel", "Single");
                }
                using (var k = Registry.CurrentUser.CreateSubKey(key + @"\command")) k.SetValue("", "\"" + Application.ExecutablePath + "\" \"%1\"");
            }
            SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
        }
        internal static void Register() {
            string exe = Application.ExecutablePath;
            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\NfoMaker.Metadata")) k.SetValue("", "Jellyfin NFO metadata");
            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\NfoMaker.Metadata\shell\open\command")) k.SetValue("", "\"" + exe + "\" \"%1\"");
            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.nfo\OpenWithProgids")) k.SetValue("NfoMaker.Metadata", new byte[0], RegistryValueKind.None);
        }
    }
    internal static class FilenameMetadata {
        internal static Metadata Read(string path) {
            string stem = Path.GetFileNameWithoutExtension(path);
            var m = new Metadata { Title = Regex.Replace(stem.Replace('_', ' ').Replace('.', ' '), @"\s+", " ").Trim() };
            var match = Regex.Match(stem, @"(?i)(?<![a-z0-9])S(\d{1,4})E(\d{1,5})(?!\d|[ ._-]*E\d|[ ._-]*-\d)");
            if (match.Success && Regex.Matches(stem, @"(?i)S\d+E\d+").Count == 1) {
                m.Kind = "TV Show";
                m.Series = Regex.Replace(stem.Substring(0, match.Index).Replace('.', ' ').Replace('_', ' '), @"\s+", " ").Trim(' ', '-');
                m.Season = int.Parse(match.Groups[1].Value).ToString();
                m.Episode = int.Parse(match.Groups[2].Value).ToString();
                // A release filename rarely supplies a trustworthy episode title.
                m.Title = "";
            }
            return m;
        }
    }
    internal sealed class BatchForm : Form {
        readonly TextBox folder = new TextBox { Width = 540 }, series = new TextBox { Width = 240 };
        readonly CheckBox replace = new CheckBox { Text = "Update existing NFOs (with backups)", AutoSize = true };
        readonly DataGridView grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        readonly Label status = new Label { Dock = DockStyle.Bottom, Height = 60, Text = "Choose a folder. Missing episode NFOs are selected automatically. Unknown numbering requires review." };
        List<BatchItem> items = new List<BatchItem>();
        internal BatchForm() {
            Text = "NFO Maker - Batch TV episodes"; Size = new Size(1000, 580); MinimumSize = new Size(760, 460); Font = new Font("Segoe UI", 10); StartPosition = FormStartPosition.CenterParent;
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 112, Padding = new Padding(8), WrapContents = true };
            var browse = new Button { Text = "Choose folder", AutoSize = true }; var load = new Button { Text = "Load", AutoSize = true };
            var all = new Button { Text = "Select all", AutoSize = true }; var missing = new Button { Text = "Missing only", AutoSize = true }; var save = new Button { Text = "Save selected NFOs", AutoSize = true };
            top.Controls.Add(folder); top.Controls.Add(browse); top.Controls.Add(load); top.SetFlowBreak(load, true);
            top.Controls.Add(new Label { Text = "Series name", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }); top.Controls.Add(series); top.Controls.Add(replace); top.Controls.Add(all); top.Controls.Add(missing); top.Controls.Add(save);
            grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Save", DataPropertyName = "Selected", FillWeight = 12 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Video", DataPropertyName = "File", ReadOnly = true, FillWeight = 100 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Season", DataPropertyName = "Season", FillWeight = 15 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Episode", DataPropertyName = "Episode", FillWeight = 15 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Result", DataPropertyName = "Status", ReadOnly = true, FillWeight = 55 });
            Controls.Add(grid); Controls.Add(top); Controls.Add(status);
            browse.Click += delegate { using(var d = new FolderBrowserDialog { Description = "Choose the folder containing the episode videos" }) if(d.ShowDialog(this) == DialogResult.OK) { folder.Text = d.SelectedPath; LoadFolder(); } };
            load.Click += delegate { LoadFolder(); };
            all.Click += delegate { grid.EndEdit(); foreach(var r in items) r.Selected = true; grid.Refresh(); };
            missing.Click += delegate { grid.EndEdit(); foreach(var r in items) r.Selected = r.Expected == null && r.Season != "" && r.Episode != ""; grid.Refresh(); };
            save.Click += delegate {
                try { grid.EndEdit(); ((CurrencyManager)BindingContext[grid.DataSource]).EndCurrentEdit();
                    if(replace.Checked && items.Any(r => r.Selected && r.Expected != null) && MessageBox.Show(this, "Update selected existing NFOs? Each original will be backed up.", "Batch update", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                    int n = Batch.Save(items, series.Text, replace.Checked); status.Text = n + " NFO(s) saved. Refresh metadata in Jellyfin when ready.";
                } catch(Exception ex) { status.Text = ex.Message; MessageBox.Show(this, ex.Message, "Batch stopped", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                grid.Refresh();
            };
            grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e) { if(e.RowIndex >= 0 && e.ColumnIndex != 0 && File.Exists(items[e.RowIndex].Target)) { using(var editor = new MainForm()) { editor.OpenPath(items[e.RowIndex].Target); editor.ShowDialog(this); } LoadFolder(); } };
        }
        void LoadFolder() {
            try { items = Batch.Scan(folder.Text.Trim().Trim('"')); grid.DataSource = items;
                if(string.IsNullOrWhiteSpace(series.Text)) series.Text = Regex.Replace(Path.GetFileName(folder.Text.TrimEnd('\\')), @"(?i)[ ._-]*S\d+.*$", "").Replace('.', ' ').Trim();
                status.Text = items.Count + " videos; " + items.Count(x => x.Selected) + " missing NFOs ready. Review series and numbers, then Save selected NFOs. Double-click an existing row to edit its NFO.";
            } catch(Exception ex) { status.Text = ex.Message; }
        }
    }
    internal static class BatchTest {
        static void Check(bool value, string message) { if(!value) throw new Exception(message); }
        internal static int Run(string folder, string report) {
            try {
                using(var form = new MainForm()) form.CreateControl();
                using(var form = new BatchForm()) form.CreateControl();
                var items = Batch.Scan(folder);
                Check(items.Count == 8 && items.Count(x => x.Selected) == 2, "Expected exactly eight videos and two missing NFOs.");
                Check(items.Where(x => x.Selected).All(x => x.Season == "01" && (x.Episode == "07" || x.Episode == "08")), "Remaining files must be S01E07 and S01E08.");
                var originals = items.Where(x => x.Expected != null).ToDictionary(x => x.Target, x => x.Expected);
                var videoInfo = items.ToDictionary(x => x.Path, x => new FileInfo(x.Path).Length.ToString() + "/" + new FileInfo(x.Path).LastWriteTimeUtc.Ticks);
                // One in-memory editing check; no extra library rewrites.
                var edit = Nfo.Load(items.First(x => x.Expected != null).Target);
                var id = edit.Original.CreateElement("uniqueid"); id.SetAttribute("type", "test"); id.InnerText = "preserve-me"; edit.Original.DocumentElement.AppendChild(id);
                edit.Episode = "42"; var bytes = Nfo.Build(edit); var doc = new System.Xml.XmlDocument(); doc.LoadXml(Encoding.UTF8.GetString(bytes));
                Check(doc.SelectSingleNode("/episodedetails/uniqueid").InnerText == "preserve-me" && doc.SelectSingleNode("/episodedetails/episode").InnerText == "42", "Editing did not preserve metadata.");
                int saved = Batch.Save(items, "Crystal Lake", false);
                Check(saved == 2, "Expected two saved files.");
                foreach(var r in items) {
                    var m = Nfo.Load(r.Target);
                    Check(m.Series == "Crystal Lake" && m.Season == "1" && m.Episode == int.Parse(r.Episode).ToString(), "Wrong metadata: " + r.File);
                    Check(videoInfo[r.Path] == new FileInfo(r.Path).Length.ToString() + "/" + new FileInfo(r.Path).LastWriteTimeUtc.Ticks, "Video attributes changed.");
                }
                foreach(var original in originals) Check(File.ReadAllBytes(original.Key).SequenceEqual(original.Value), "Existing NFO changed.");
                File.WriteAllText(report, "PASS: one test execution. Batch created S01E07 and S01E08; eight episode NFOs read back correctly; six existing NFOs byte-identical; video sizes/timestamps unchanged. Form construction and in-memory field editing/preservation passed. No visual UI, Windows default-association or Jellyfin ingestion test.\r\n", Encoding.UTF8);
                return 0;
            } catch(Exception ex) { File.WriteAllText(report, "FAIL: " + ex + "\r\nNo retry performed.", Encoding.UTF8); return 1; }
        }
    }
}
