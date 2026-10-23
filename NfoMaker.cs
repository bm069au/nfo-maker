using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;

namespace NfoMaker
{
    internal sealed class Metadata
    {
        internal string Kind = "Movie", Title = "", Series = "", Season = "", Episode = "", Year = "", Plot = "";
        internal bool DocumentaryEpisode;
        internal XmlDocument Original;
        internal bool IsEpisode { get { return Kind == "TV Show" || (Kind == "Documentary" && DocumentaryEpisode); } }
    }

    internal static class Nfo
    {
        internal static string Number(string text, string label, char prefix, int maximum)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return "";
            if (prefix != '\0' && char.ToUpperInvariant(text[0]) == prefix) text = text.Substring(1);
            int value;
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) || value < 0 || value > maximum)
                throw new ArgumentException(label + " must be a whole number from 0 to " + maximum + ", or left blank.");
            return value.ToString(CultureInfo.InvariantCulture);
        }

        internal static byte[] Build(Metadata m)
        {
            string year = Number(m.Year, "Year", '\0', 9999);
            string season = m.IsEpisode ? Number(m.Season, "Season", 'S', 9999) : "";
            string episode = m.IsEpisode ? Number(m.Episode, "Episode", 'E', 99999) : "";
            if (m.Original != null)
            {
                var copy = (XmlDocument)m.Original.CloneNode(true);
                Patch(copy, "title", m.Title); Patch(copy, "year", year); Patch(copy, "plot", m.Plot);
                if (copy.DocumentElement.Name == "episodedetails") {
                    Patch(copy, "showtitle", m.Series); Patch(copy, "season", season); Patch(copy, "episode", episode);
                } else if (copy.DocumentElement.Name == "tvshow") Patch(copy, "title", m.Title);
                using (var output = new MemoryStream()) {
                    using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true })) copy.Save(writer);
                    return output.ToArray();
                }
            }
            using (MemoryStream stream = new MemoryStream())
            {
                XmlWriterSettings settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true, CloseOutput = false };
                using (XmlWriter xml = XmlWriter.Create(stream, settings))
                {
                    xml.WriteStartDocument();
                    xml.WriteStartElement(m.IsEpisode ? "episodedetails" : "movie");
                    Element(xml, "title", m.Title);
                    if (m.IsEpisode)
                    {
                        Element(xml, "showtitle", m.Series);
                        Element(xml, "season", season);
                        Element(xml, "episode", episode);
                    }
                    Element(xml, "year", year);
                    Element(xml, "plot", m.Plot);
                    if (m.Kind == "Documentary") Element(xml, "genre", "Documentary");
                    xml.WriteEndElement();
                    xml.WriteEndDocument();
                }
                return stream.ToArray();
            }
        }

        internal static void Patch(XmlDocument doc, string name, string value) {
            var node = doc.DocumentElement.SelectSingleNode(name);
            if (node != null && node.InnerText == value) return;
            value = (value ?? "").Trim();
            if ((node == null ? "" : node.InnerText) == value) return;
            if (value.Length == 0) { if (node != null) doc.DocumentElement.RemoveChild(node); return; }
            if (node == null) { node = doc.CreateElement(name); doc.DocumentElement.AppendChild(node); }
            node.InnerText = value;
        }

        internal static Metadata Load(string path) {
            var doc = new XmlDocument { XmlResolver = null, PreserveWhitespace = true };
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 8388608 })) doc.Load(reader);
            string root = doc.DocumentElement.Name;
            if (root != "movie" && root != "episodedetails" && root != "tvshow") throw new ArgumentException("This is not a supported Jellyfin movie, episode or show NFO.");
            var m = new Metadata { Original = doc, Kind = root == "episodedetails" ? "TV Show" : "Movie" };
            m.Title = Value(doc, "title"); m.Series = Value(doc, "showtitle"); m.Season = Value(doc, "season"); m.Episode = Value(doc, "episode"); m.Year = Value(doc, "year"); m.Plot = Value(doc, "plot");
            if (doc.SelectSingleNode("/*/genre[text()='Documentary']") != null) { m.Kind = "Documentary"; m.DocumentaryEpisode = root == "episodedetails"; }
            return m;
        }
        internal static string Value(XmlDocument doc, string name) { var n = doc.DocumentElement.SelectSingleNode(name); return n == null ? "" : n.InnerText; }

        private static void Element(XmlWriter xml, string name, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) xml.WriteElementString(name, value.Trim());
        }

        internal static string SuggestedPath(string input)
        {
            string clean = (input ?? "").Trim().Trim('"');
            if (clean.Length == 0) return "New metadata.nfo";
            return Path.ChangeExtension(clean, ".nfo");
        }

        internal static string Save(string target, Metadata metadata)
        {
            target = Path.GetFullPath(target);
            if (!string.Equals(Path.GetExtension(target), ".nfo", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Choose an .nfo file. Video files cannot be overwritten.");
            byte[] bytes = Build(metadata);
            string temp = Path.Combine(Path.GetDirectoryName(target), ".nfo-maker-" + Guid.NewGuid().ToString("N") + ".tmp");
            string backup = null;
            try
            {
                using (FileStream file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write))
                {
                    file.Write(bytes, 0, bytes.Length);
                    file.Flush(true);
                }
                if (File.Exists(target))
                {
                    backup = target + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".bak";
                    File.Replace(temp, target, backup);
                }
                else File.Move(temp, target);
                return backup;
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly ComboBox kind = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly CheckBox documentaryEpisode = new CheckBox { Text = "This documentary is an episode", AutoSize = true };
        private readonly TextBox filename = new TextBox(), title = new TextBox(), series = new TextBox(), year = new TextBox();
        private readonly ComboBox season = new ComboBox(), episode = new ComboBox();
        private XmlDocument original;
        private string loadedPath;
        private readonly TextBox plot = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Height = 85 };
        private readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(610, 0), ForeColor = Color.DarkGreen };

        internal MainForm()
        {
            Text = "NFO Maker"; AppBranding.SetIcon(this);
            Font = new Font("Segoe UI", 10F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(690, 620);
            MinimumSize = new Size(620, 560);
            StartPosition = FormStartPosition.CenterScreen;
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, AutoScroll = true };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(table);
            AddWide(table, new Label { Text = "Create or replace Jellyfin metadata", AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
            AddWide(table, new Label { Text = "Paste any details you know. All metadata fields can be left blank.", AutoSize = true });
            kind.Items.AddRange(new object[] { "Movie", "TV Show", "Documentary" });
            AddRow(table, "Type", kind);
            AddWide(table, documentaryEpisode);
            var filePanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Margin = Padding.Empty };
            filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            filename.Dock = DockStyle.Fill;
            var browse = new Button { Text = "Browse...", AutoSize = true };
            filePanel.Controls.Add(filename, 0, 0); filePanel.Controls.Add(browse, 1, 0);
            AddRow(table, "File name or path", filePanel);
            AddWide(table, new Label { Text = "Paste a video filename/path or an existing NFO. Choose the save folder next.", AutoSize = true, MaximumSize = new Size(610, 0) });
            AddRow(table, "Title / episode title", title);
            AddRow(table, "Series name", series);
            AddRow(table, "Season (0 = special)", season);
            AddRow(table, "Episode number", episode);
            AddRow(table, "Year", year);
            AddRow(table, "Description", plot);
            var save = new Button { Text = "Save NFO...", AutoSize = true, Padding = new Padding(12, 5, 12, 5) };
            AddWide(table, save);
            AddWide(table, new Label { Text = "Existing NFOs are backed up beside the file before replacement.\r\nSave beside the video with the same filename stem. Show folders still control grouping.\r\nAfter saving, refresh the item's metadata in Jellyfin. Missing details can stay blank;\r\nprovider matches and library display are not guaranteed by an NFO alone.", AutoSize = true, MaximumSize = new Size(610, 0), ForeColor = SystemColors.GrayText });
            AddWide(table, status);
            var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            var batch = new Button { Text = "Batch", AutoSize = true };
            var load = new Button { Text = "Load pasted NFO", AutoSize = true };
            var associate = new Button { Text = "Enable Open with", AutoSize = true };
            actions.Controls.Add(batch); actions.Controls.Add(load); actions.Controls.Add(associate); AddWide(table, actions);
            batch.Click += delegate { using (var f = new BatchForm(filename.Text)) f.ShowDialog(this); };
            var menu = new MenuStrip();
            menu.Items.Add("Batch", null, delegate { using(var f = new BatchForm(filename.Text)) f.ShowDialog(this); });
            menu.Items.Add("Open NFO...", null, delegate { using(var d = new OpenFileDialog { Filter = "NFO metadata|*.nfo" }) if(d.ShowDialog(this) == DialogResult.OK) OpenPath(d.FileName); });
            menu.Items.Add("Add file and folder right-click menus", null, delegate { try { Association.RegisterVideoMenu(); MessageBox.Show(this, "Added video and folder NFO actions for your Windows account. On Windows 11, look under Show more options. Keep this executable in its current location."); } catch(Exception ex) { MessageBox.Show(this, ex.Message); } });
            var help = new ToolStripMenuItem("Help");
            help.DropDownItems.Add("About NFO Maker", null, delegate { AppBranding.About(this); });
            help.DropDownItems.Add("Contact", null, delegate { AppBranding.Contact(this, false); });
            help.DropDownItems.Add("Donations", null, delegate { AppBranding.Contact(this, true); });
            menu.Items.Add(help);
            MainMenuStrip = menu; Controls.Add(menu);
            load.Click += delegate { OpenPath(filename.Text); };
            associate.Click += delegate { try { Association.Register(); MessageBox.Show(this, "NFO Maker is registered. Right-click an .nfo, choose Open with > Choose another app > NFO Maker, then Always. Windows keeps your current default until you choose it."); } catch(Exception ex) { MessageBox.Show(this, ex.Message); } };
            season.Items.Add(""); episode.Items.Add("");
            for (int i = 0; i <= 100; i++) season.Items.Add(i.ToString());
            for (int i = 0; i <= 500; i++) episode.Items.Add(i.ToString());
            kind.SelectedIndexChanged += delegate { UpdateType(); };
            documentaryEpisode.CheckedChanged += delegate { UpdateType(); };
            kind.SelectedIndex = 0;
            browse.Click += delegate
            {
                using (var dialog = new OpenFileDialog { Title = "Choose the video or NFO", Filter = "Video or NFO|*.mkv;*.mp4;*.avi;*.m4v;*.mov;*.wmv;*.mpg;*.mpeg;*.ts;*.m2ts;*.iso;*.strm;*.nfo|All files|*.*" })
                    if (dialog.ShowDialog(this) == DialogResult.OK) OpenPath(dialog.FileName);
            };
            save.Click += delegate { SaveClicked(); };
        }

        internal void OpenPath(string path) {
            path = path.Trim().Trim('"');
            try {
                if (Directory.Exists(path)) {
                    filename.Text = Path.GetFullPath(path); loadedPath = null; original = null;
                    title.Clear(); series.Clear(); season.Text = episode.Text = ""; year.Clear(); plot.Clear();
                    kind.Enabled = documentaryEpisode.Enabled = true;
                    status.Text = "Folder selected. Click Batch to review its movies and TV episodes.";
                    return;
                }
                bool isNfo = Path.GetExtension(path).Equals(".nfo", StringComparison.OrdinalIgnoreCase);
                string sidecar = Path.ChangeExtension(path, ".nfo");
                if (!isNfo && File.Exists(sidecar)) { OpenPath(sidecar); return; }
                Metadata m = isNfo ? Nfo.Load(path) : FilenameMetadata.Read(path);
                filename.Text = path; original = m.Original; loadedPath = Path.GetFullPath(path);
                kind.SelectedItem = m.Kind; documentaryEpisode.Checked = m.DocumentaryEpisode;
                title.Text = m.Title; series.Text = m.Series; season.Text = m.Season; episode.Text = m.Episode; year.Text = m.Year; plot.Text = m.Plot;
                kind.Enabled = documentaryEpisode.Enabled = original == null;
                status.Text = original == null ? "Filename suggestions loaded. Check the type and details before saving. Unknown episode titles stay blank." : "Loaded. Unedited metadata is preserved; saving creates a backup.";
            } catch(Exception ex) {
                if (File.Exists(path) && Path.GetExtension(path).Equals(".nfo", StringComparison.OrdinalIgnoreCase) && MessageBox.Show(this, ex.Message + "\r\nRebuild this NFO? The original will be backed up on save.", "Cannot read NFO", MessageBoxButtons.YesNo) == DialogResult.Yes) {
                    original = null; loadedPath = Path.GetFullPath(path); filename.Text = path;
                    title.Clear(); series.Clear(); season.Text = episode.Text = ""; year.Clear(); plot.Clear(); kind.Enabled = documentaryEpisode.Enabled = true;
                } else status.Text = "Not opened: " + ex.Message;
            }
        }

        private static void AddRow(TableLayoutPanel table, string text, Control control)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 7, 8, 7) }, 0, row);
            control.Dock = DockStyle.Fill; control.Margin = new Padding(0, 4, 0, 4);
            table.Controls.Add(control, 1, row);
        }

        private static void AddWide(TableLayoutPanel table, Control control)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            control.Margin = new Padding(0, 4, 0, 6);
            table.Controls.Add(control, 0, row); table.SetColumnSpan(control, 2);
        }

        private void UpdateType()
        {
            bool doc = (string)kind.SelectedItem == "Documentary";
            documentaryEpisode.Visible = doc;
            bool episodic = (string)kind.SelectedItem == "TV Show" || (doc && documentaryEpisode.Checked);
            series.Enabled = season.Enabled = episode.Enabled = episodic;
        }

        private void SaveClicked()
        {
            try
            {
                if (Directory.Exists(filename.Text.Trim().Trim('"'))) { MessageBox.Show(this, "Click Batch to create NFOs for the videos in this folder."); return; }
                if (original != null && !string.Equals(Path.GetFullPath(filename.Text.Trim().Trim('"')), loadedPath, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Use Browse or Load pasted NFO to switch files before editing.");
                var data = new Metadata { Original = original, Kind = (string)kind.SelectedItem, DocumentaryEpisode = documentaryEpisode.Checked, Title = title.Text, Series = series.Text, Season = season.Text, Episode = episode.Text, Year = year.Text, Plot = plot.Text };
                Nfo.Build(data); // Validate before opening the save dialog or touching a file.
                if (loadedPath != null && File.Exists(loadedPath) && Path.GetExtension(loadedPath).Equals(".nfo", StringComparison.OrdinalIgnoreCase)) {
                    string backup = Nfo.Save(loadedPath, data); original = Nfo.Load(loadedPath).Original;
                    status.Text = "Saved: " + loadedPath + "\r\nBackup: " + backup; return;
                }
                string suggested = Nfo.SuggestedPath(filename.Text);
                if (loadedPath != null && string.Equals(Path.GetFullPath(filename.Text.Trim().Trim('"')), loadedPath, StringComparison.OrdinalIgnoreCase) && File.Exists(loadedPath) && !Path.GetExtension(loadedPath).Equals(".nfo", StringComparison.OrdinalIgnoreCase)) {
                    string target = Path.ChangeExtension(loadedPath, ".nfo");
                    if (File.Exists(target) && MessageBox.Show(this, "Replace this NFO? The original will be backed up.\r\n" + target, "Replace NFO", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                    string backup = Nfo.Save(target, data);
                    original = Nfo.Load(target).Original; loadedPath = target; filename.Text = target;
                    status.Text = "Saved: " + target + (backup == null ? "" : "\r\nBackup: " + backup); return;
                }
                using (var dialog = new SaveFileDialog { Title = "Save NFO beside the video", Filter = "NFO metadata (*.nfo)|*.nfo", DefaultExt = "nfo", AddExtension = true, OverwritePrompt = false, CheckPathExists = true, FileName = Path.GetFileName(suggested) })
                {
                    string folder = Path.GetDirectoryName(suggested);
                    if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder)) dialog.InitialDirectory = Path.GetFullPath(folder);
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    if (File.Exists(dialog.FileName) && MessageBox.Show(this, "Replace this NFO? Its original contents will be kept in a .bak file beside it.\r\n\r\n" + dialog.FileName, "Replace NFO", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    string backup = Nfo.Save(dialog.FileName, data);
                    status.Text = "Saved: " + dialog.FileName + (backup == null ? "" : "\r\nOriginal backup: " + backup);
                }
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "NFO was not saved", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }

    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 2 && args[0] == "--self-test") return TestOnce(args[1]);
            if (args.Length == 3 && args[0] == "--test-crystal") return BatchTest.Run(args[1], args[2]);
            var form = new MainForm();
            if (args.Length == 2 && args[0] == "--batch") form.OpenPath(args[1]);
            else if (args.Length == 1) form.OpenPath(args[0]);
            Application.Run(form);
            return 0;
        }

        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static XmlDocument Read(byte[] bytes)
        {
            var doc = new XmlDocument { XmlResolver = null };
            using (var stream = new MemoryStream(bytes)) doc.Load(stream);
            return doc;
        }

        private static int TestOnce(string directory)
        {
            Directory.CreateDirectory(directory);
            string report = Path.Combine(directory, "test-result.txt");
            try
            {
                using (var form = new MainForm()) { form.CreateControl(); form.PerformLayout(); Check(form.Controls.Count == 1, "Form construction failed"); }
                Check(Read(Nfo.Build(new Metadata())).DocumentElement.Name == "movie", "Blank movie");
                var tv = new Metadata { Kind = "TV Show", Title = "Brian & Stewie <Holiday>", Series = "Family Guy", Season = "S00", Episode = "E64", Plot = "Caf\u00e9 \u2014 pasted text" };
                var xml = Read(Nfo.Build(tv));
                Check(xml.DocumentElement.Name == "episodedetails" && xml.SelectSingleNode("/episodedetails/title").InnerText == tv.Title && xml.SelectSingleNode("/episodedetails/season").InnerText == "0" && xml.SelectSingleNode("/episodedetails/episode").InnerText == "64" && xml.SelectSingleNode("/episodedetails/year") == null, "TV escaping, numbering or omitted fields");
                Check(xml.SelectSingleNode("/episodedetails/plot").InnerText == tv.Plot, "Unicode");
                Check(Read(Nfo.Build(new Metadata { Kind = "TV Show" })).DocumentElement.ChildNodes.Count == 0, "Blank TV fields");
                var doc = new Metadata { Kind = "Documentary" };
                Check(Read(Nfo.Build(doc)).SelectSingleNode("/movie/genre").InnerText == "Documentary", "Documentary movie");
                doc.DocumentaryEpisode = true;
                Check(Read(Nfo.Build(doc)).DocumentElement.Name == "episodedetails", "Documentary episode");
                Check(Nfo.SuggestedPath("\"M:\\TV Shows\\Show.S01E01.mkv\"") == "M:\\TV Shows\\Show.S01E01.nfo", "Pasted path");
                string target = Path.Combine(directory, "example.nfo");
                byte[] original = Encoding.UTF8.GetBytes("bad old NFO & unchanged backup");
                File.WriteAllBytes(target, original);
                string backup = Nfo.Save(target, tv);
                Check(Convert.ToBase64String(File.ReadAllBytes(backup)) == Convert.ToBase64String(original), "Exact backup");
                Check(Read(File.ReadAllBytes(target)).DocumentElement.Name == "episodedetails", "Replacement XML");
                string fresh = Path.Combine(directory, "new.nfo");
                Check(Nfo.Save(fresh, new Metadata()) == null && File.Exists(fresh), "Create new NFO");
                bool refused = false;
                try { Nfo.Save(Path.Combine(directory, "video.mkv"), tv); } catch (ArgumentException) { refused = true; }
                Check(refused && !File.Exists(Path.Combine(directory, "video.mkv")), "Video overwrite protection");
                byte[] prior = File.ReadAllBytes(target); tv.Episode = "not a number"; refused = false;
                try { Nfo.Save(target, tv); } catch (ArgumentException) { refused = true; }
                Check(refused && Convert.ToBase64String(prior) == Convert.ToBase64String(File.ReadAllBytes(target)), "Validation preserves old NFO");
                File.WriteAllText(report, "PASS: one test pass. Form construction/layout; blank movie/TV fields; XML escaping and Unicode; season/episode parsing; documentary film/episode; pasted paths; exact backup and replacement; new file; non-NFO protection; failed validation preserves original.\r\nNo live library writes or Jellyfin ingestion/playback test. No visual UI inspection.\r\n", Encoding.UTF8);
                return 0;
            }
            catch (Exception ex) { File.WriteAllText(report, "FAIL: " + ex, Encoding.UTF8); return 1; }
        }
    }
}
