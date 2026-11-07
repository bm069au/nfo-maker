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
        public string Type { get; set; }
        public string Title { get; set; }
        public string Year { get; set; }
        public string Season { get; set; }
        public string Episode { get; set; }
        public string Status { get; set; }
        internal string Path;
        internal byte[] Expected;
        internal string LoadError, Series;
        internal bool CanRebuild;
        internal string DetectedType;
        internal string Target { get { return System.IO.Path.ChangeExtension(Path, ".nfo"); } }
    }
    internal static class Batch {
        internal static IEnumerable<string> VideosInTree(string folder) {
            var pending = new Stack<string>(); pending.Push(Path.GetFullPath(folder));
            while (pending.Count != 0) {
                string current = pending.Pop();
                foreach (string file in Directory.GetFiles(current).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) yield return file;
                foreach (string child in Directory.GetDirectories(current).OrderByDescending(x => x, StringComparer.OrdinalIgnoreCase)) {
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
                    if (Regex.IsMatch(Path.GetFileName(child), @"(?i)^(samples?|extras?|trailers?|featurettes?|bonus)(?:$|[ ._-])")) continue;
                    pending.Push(child);
                }
            }
        }
        internal static List<BatchItem> Scan(string folder) {
            var result = new List<BatchItem>();
            foreach (string p in VideosInTree(folder)) {
                if (!Regex.IsMatch(System.IO.Path.GetExtension(p), @"^\.(mkv|mp4|avi|m4v|mov|wmv|mpg|mpeg|ts|m2ts|iso|strm)$", RegexOptions.IgnoreCase)) continue;
                Metadata suggested = FilenameMetadata.Read(p);
                bool ambiguous = !suggested.IsEpisode && Regex.IsMatch(System.IO.Path.GetFileNameWithoutExtension(p), @"(?i)(?:S\d+E|\bseason[ ._-]*\d|\bepisode[ ._-]*\d|\d+x\d+)");
                var item = new BatchItem { Path = p, File = p.Substring(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar).Length + 1), Type = ambiguous ? "Review" : suggested.IsEpisode ? "TV Show" : "Movie", Title = suggested.Title, Year = suggested.Year, Season = suggested.Season, Episode = suggested.Episode };
                item.Expected = System.IO.File.Exists(item.Target) ? System.IO.File.ReadAllBytes(item.Target) : null;
                if (item.Expected != null) {
                    try { var existing = Nfo.Load(item.Target); item.Type = existing.Kind == "Documentary" ? (existing.IsEpisode ? "Documentary episode" : "Documentary film") : existing.IsEpisode ? "TV Show" : "Movie"; item.Title = existing.Title; item.Year = existing.Year; item.Season = existing.Season; item.Episode = existing.Episode; item.Series = existing.Series; }
                    catch (System.Xml.XmlException ex) { item.LoadError = ex.Message; item.CanRebuild = true; }
                    catch (ArgumentException ex) { item.LoadError = ex.Message; item.CanRebuild = true; }
                    catch (Exception ex) { item.LoadError = ex.Message; item.Type = "Review"; }
                }
                if (item.Expected == null || item.CanRebuild) item.Series = suggested.Series;
                item.DetectedType = item.Type;
                bool excluded = Regex.IsMatch(System.IO.Path.GetFileNameWithoutExtension(p), @"(?i)(?:^|[ ._-])(sample|trailer|featurette|bonus)(?:$|[ ._-])");
                item.Selected = item.Expected == null && item.Type != "Review" && !excluded;
                item.Status = item.Expected != null ? "Existing - not selected" : excluded ? "Extra - review" : item.Type == "Review" ? "Check type / numbering" : "Missing - ready";
                if (item.LoadError != null) item.Status = "Unreadable NFO - unchanged: " + item.LoadError;
                result.Add(item);
            }
            return result;
        }
        internal static int Save(List<BatchItem> items, string series, bool replace, bool keepBackup = true) {
            var work = new List<KeyValuePair<BatchItem, Metadata>>();
            foreach (var row in items.Where(x => x.Selected)) {
                if (row.Expected != null && !replace) { row.Status = "Kept existing"; continue; }
                if (row.LoadError != null && !row.CanRebuild) { row.Status = "Skipped inaccessible NFO: " + row.LoadError; continue; }
                bool episodic = row.Type == "TV Show" || row.Type == "Documentary episode";
                if (row.Type != "Movie" && row.Type != "TV Show" && row.Type != "Documentary film" && row.Type != "Documentary episode") throw new ArgumentException("Choose a batch type for " + row.File + " or untick it.");
                if (episodic && (string.IsNullOrWhiteSpace(row.Season) || string.IsNullOrWhiteSpace(row.Episode))) throw new ArgumentException("Enter season and episode for " + row.File + " or untick it.");
                Metadata m;
                try { m = row.Expected == null || row.CanRebuild ? FilenameMetadata.Read(row.Path) : Nfo.Load(row.Target); }
                catch (System.Xml.XmlException ex) { row.Status = "Skipped unreadable NFO - unchanged: " + ex.Message; continue; }
                if (m.Original != null && (m.IsEpisode != episodic || m.Original.DocumentElement.Name == "tvshow")) throw new ArgumentException("Edit existing NFO types individually: " + row.File);
                m.Kind = row.Type.StartsWith("Documentary") ? "Documentary" : row.Type; m.DocumentaryEpisode = row.Type == "Documentary episode"; m.Title = row.Title; m.Year = row.Year;
                if (m.IsEpisode && !string.IsNullOrWhiteSpace(series)) m.Series = series.Trim();
                m.Season = row.Season; m.Episode = row.Episode;
                try { Nfo.Build(m); } catch (ArgumentException ex) { throw new ArgumentException(row.File + ": " + ex.Message, ex); }
                work.Add(new KeyValuePair<BatchItem, Metadata>(row, m));
            }
            int saved = 0;
            foreach (var job in work) {
                var row = job.Key;
                try {
                    bool exists = System.IO.File.Exists(row.Target);
                    if (exists != (row.Expected != null) || (exists && !System.IO.File.ReadAllBytes(row.Target).SequenceEqual(row.Expected))) throw new IOException("NFO changed since folder selection. Reload the folder.");
                    bool rebuilding = row.CanRebuild;
                    string backup = Nfo.Save(row.Target, job.Value, keepBackup, exists);
                    row.Expected = System.IO.File.ReadAllBytes(row.Target); row.Selected = false;
                    row.LoadError = null; row.CanRebuild = false; row.DetectedType = row.Type; row.Series = job.Value.Series;
                    row.Status = !exists ? "Created" : (rebuilding ? "Rebuilt" : "Updated") + (backup == null ? " without backup" : "; backup: " + backup);
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
                    k.SetValue("Icon", File.Exists(Path.Combine(Application.StartupPath, "NfoMaker.ico")) ? Path.Combine(Application.StartupPath, "NfoMaker.ico") : "\"" + Application.ExecutablePath + "\",0");
                    k.SetValue("MultiSelectModel", "Single"); k.SetValue("Position", "Top");
                }
                using (var k = Registry.CurrentUser.CreateSubKey(key + @"\command")) k.SetValue("", "\"" + Application.ExecutablePath + "\" \"%1\"");
            }
            foreach (string location in new [] { @"Directory\shell\NfoMaker.Batch", @"Directory\Background\shell\NfoMaker.Batch" }) {
                using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + location)) {
                    k.SetValue("", "NFO Maker");
                    k.SetValue("Icon", File.Exists(Path.Combine(Application.StartupPath, "NfoMaker.ico")) ? Path.Combine(Application.StartupPath, "NfoMaker.ico") : "\"" + Application.ExecutablePath + "\",0");
                    k.SetValue("MultiSelectModel", "Single"); k.SetValue("Position", "Top");
                }
                string argument = location.Contains("Background") ? "%V" : "%1";
                using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + location + @"\command"))
                    k.SetValue("", "\"" + Application.ExecutablePath + "\" \"" + argument + "\"");
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
            if (!m.IsEpisode) {
                var year = Regex.Match(stem, @"(?:^|[ ._(\[-])((?:19|20)\d{2})(?=$|[ ._)\]-])");
                if (year.Success && year.Index > 0) { m.Year = year.Groups[1].Value; m.Title = stem.Substring(0, year.Index); }
                else m.Title = Regex.Split(stem, @"(?i)[ ._-](?:480p|576p|720p|1080p|2160p|bluray|bdrip|webrip|web-dl|dvdrip|x264|x265|h264|h265)(?:[ ._-]|$)")[0];
                m.Title = Regex.Replace(m.Title.Replace('_', ' ').Replace('.', ' '), @"\s+", " ").Trim(' ', '-', '(', ')', '[', ']');
            }
            return m;
        }
    }
    internal sealed class BatchForm : Form {
        readonly TextBox folder = new TextBox { Width = 540 }, series = new TextBox { Width = 240 };
        readonly DataGridView grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        readonly Label status = new Label { Dock = DockStyle.Bottom, Height = 60, Text = "Choose a folder. Review movies and TV episodes, then save. Uncertain files require review." };
        string batchType = "Auto detect";
        List<BatchItem> items = new List<BatchItem>();
        internal BatchForm() : this(null) { }
        internal BatchForm(string sourcePath) {
            AppBranding.SetIcon(this); Text = "NFO Maker - Batch"; Size = new Size(1100, 680); MinimumSize = new Size(800, 540); Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi; StartPosition = FormStartPosition.CenterParent;
            var top = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(8) };
            var pathRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            var typeRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            var detailsRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            var actionsRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            var browse = new Button { Text = "Choose folder", AutoSize = true }; var load = new Button { Text = "Load", AutoSize = true };
            var all = new Button { Text = "Select all", AutoSize = true }; var missing = new Button { Text = "Missing only", AutoSize = true }; var save = new Button { Text = "Save selected NFOs", AutoSize = true };
            var delete = new Button { Text = "Delete NFOs in folder...", AutoSize = true };
            pathRow.Controls.Add(folder); pathRow.Controls.Add(browse); pathRow.Controls.Add(load);
            typeRow.Controls.Add(new Label { Text = "Type for whole batch", AutoSize = true, Padding = new Padding(0, 9, 6, 0) });
            var typeSelector = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 270, Font = new Font(Font.FontFamily, 12), IntegralHeight = false, DropDownHeight = 180, AccessibleName = "Type for whole batch" };
            typeSelector.Items.AddRange(new object[] { "Auto detect", "Movie", "TV Show", "Documentary film", "Documentary episode" }); typeSelector.SelectedItem = batchType;
            typeSelector.SelectedIndexChanged += delegate { grid.EndEdit(); batchType = (string)typeSelector.SelectedItem; ApplyBatchType(); };
            typeRow.Controls.Add(typeSelector);
            detailsRow.Controls.Add(new Label { Text = "Series name", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }); detailsRow.Controls.Add(series);
            actionsRow.Controls.Add(all); actionsRow.Controls.Add(missing); actionsRow.Controls.Add(save); actionsRow.Controls.Add(delete);
            top.Controls.Add(pathRow); top.Controls.Add(typeRow); top.Controls.Add(detailsRow); top.Controls.Add(actionsRow);
            grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Save", DataPropertyName = "Selected", FillWeight = 12 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Video / subfolder", DataPropertyName = "File", ReadOnly = true, FillWeight = 100 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Type", DataPropertyName = "Type", ReadOnly = true, MinimumWidth = 125, FillWeight = 35 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Title", DataPropertyName = "Title", FillWeight = 45 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Year", DataPropertyName = "Year", FillWeight = 17 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Season", DataPropertyName = "Season", FillWeight = 15 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Episode", DataPropertyName = "Episode", FillWeight = 15 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Result", DataPropertyName = "Status", ReadOnly = true, FillWeight = 55 });
            Controls.Add(grid); Controls.Add(top); Controls.Add(status);
            browse.Click += delegate { using(var d = new FolderBrowserDialog { Description = "Choose the folder containing the videos", SelectedPath = folder.Text }) if(d.ShowDialog(this) == DialogResult.OK) { folder.Text = d.SelectedPath; LoadFolder(); } };
            load.Click += delegate { LoadFolder(); };
            delete.Click += delegate { try { using (var dialog = new DeleteNfoDialog(Path.GetFullPath(folder.Text.Trim().Trim('"')), true)) { dialog.ShowDialog(this); } LoadFolder(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Cannot delete NFOs"); } };
            all.Click += delegate { grid.EndEdit(); foreach(var r in items) r.Selected = true; grid.Refresh(); };
            missing.Click += delegate { grid.EndEdit(); foreach(var r in items) r.Selected = r.Expected == null && r.Type != "Review" && !r.Status.StartsWith("Extra"); grid.Refresh(); };
            save.Click += delegate {
                try {
                    grid.EndEdit();
                    if (items.Count == 0) { status.Text = "Load a folder containing videos first."; return; }
                    ((CurrencyManager)BindingContext[grid.DataSource]).EndCurrentEdit();
                    var selected = items.Where(r => r.Selected).ToList();
                    if (selected.Count == 0) { status.Text = "Select the rows you want to save first."; return; }
                    int existing = selected.Count(r => r.Expected != null);
                    int unreadable = selected.Count(r => r.CanRebuild);
                    bool replace = existing > 0 && MessageBox.Show(this,
                        "Overwrite " + existing + " selected existing NFO file(s)?" +
                        (unreadable > 0 ? "\r\nUnreadable NFOs will be rebuilt from the grid." : "") +
                        "\r\n\r\nChoose No to keep existing NFOs and save only missing ones.",
                        "Overwrite existing NFO files?", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
                    bool backup = !replace || AskForBackup();
                    int saved = Batch.Save(selected, series.Text, replace, backup);
                    int skipped = selected.Count - saved;
                    status.Text = saved + " NFO(s) saved." +
                        (skipped > 0 ? " " + skipped + " left unchanged." : "") +
                        (replace && saved > selected.Count - existing ? (backup ? " Original files backed up." : " No backups created.") : "");
                } catch(Exception ex) { status.Text = ex.Message; MessageBox.Show(this, ex.Message, "Batch stopped", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                grid.Refresh();
            };
            grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e) { if(e.RowIndex >= 0 && e.ColumnIndex != 0 && File.Exists(items[e.RowIndex].Target)) { using(var editor = new MainForm()) { editor.OpenPath(items[e.RowIndex].Target); editor.ShowDialog(this); } LoadFolder(); } };
            if (!string.IsNullOrWhiteSpace(sourcePath)) {
                try {
                    string source = Path.GetFullPath(sourcePath.Trim().Trim('"'));
                    string initial = Directory.Exists(source) ? source : Path.GetDirectoryName(source);
                    if (!string.IsNullOrEmpty(initial) && Directory.Exists(initial)) { folder.Text = initial; LoadFolder(); }
                    else status.Text = "The source folder is unavailable. Choose another folder.";
                } catch (Exception ex) { status.Text = "Cannot load source folder: " + ex.Message; }
            }
        }
        string loadedFolder;
        bool AskForBackup() {
            return MessageBox.Show(this,
                "Keep a backup of each original NFO before replacing it?\r\n\r\nYes keeps .bak files. No replaces the files without backups.",
                "Back up existing NFO files?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }
        void ApplyBatchType() {
            foreach (var item in items) item.Type = batchType == "Auto detect" ? item.DetectedType : batchType;
            bool episodes = batchType == "Auto detect" || batchType == "TV Show" || batchType == "Documentary episode";
            series.Enabled = episodes;
            grid.Columns[5].ReadOnly = grid.Columns[6].ReadOnly = !episodes;
            grid.Refresh();
        }
        void LoadFolder() {
            try { items = new List<BatchItem>(); grid.DataSource = items;
                string currentFolder = Path.GetFullPath(folder.Text.Trim().Trim('"'));
                items = Batch.Scan(currentFolder); grid.DataSource = items;
                ApplyBatchType();
                if (!string.Equals(currentFolder, loadedFolder, StringComparison.OrdinalIgnoreCase)) series.Clear();
                loadedFolder = currentFolder;
                if(string.IsNullOrWhiteSpace(series.Text)) {
                    var names = items.Where(x => x.Type == "TV Show" || x.Type == "Documentary episode").Select(x => x.Series).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    series.Text = names.Count == 1 ? names[0] : "";
                }
                status.Text = items.Count + " videos (including subfolders), " + items.Count(x => x.Selected) + " missing NFOs ready, " + items.Count(x => x.CanRebuild) + " unreadable. Review the grid, then select and save the rows you want to change.";
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
