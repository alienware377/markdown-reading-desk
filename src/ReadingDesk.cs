// Markdown Reading Desk
//
// One executable, three jobs:
//   ReadingDesk.exe                  setup window (install, uninstall)
//   ReadingDesk.exe "notes.md"       read that file
//   ReadingDesk.exe --read           read the welcome page
//   ReadingDesk.exe --install [--dest DIR] [--silent]
//   ReadingDesk.exe --uninstall [--silent]
//
// Installing copies this same executable into the destination folder, unpacks
// the reader next to it, draws the icon, and registers the app as a handler
// for .md files. Windows 11 protects the default-app choice with a hash, so
// the final "always use this app" pick is left to the person installing.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

static class App {
    public const string Title    = "Markdown Reading Desk";
    public const string ProgId   = "ReadingDesk.md";
    public const string ExeName  = "ReadingDesk.exe";
    public const string TypeName = "Markdown Document";
    public const string Blurb    = "Read markdown files with reading-mode controls";

    public static string DefaultDir {
        get {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ReadingDesk");
        }
    }

    public static string SelfPath { get { return Application.ExecutablePath; } }

    [DllImport("shell32.dll")]
    static extern void SHChangeNotify(int eventId, uint flags, IntPtr a, IntPtr b);

    public static void RefreshShell() {
        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }

    [STAThread]
    static int Main(string[] argv) {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        try {
            string first = argv.Length > 0 ? argv[0] : "";

            if (first.Equals("--install", StringComparison.OrdinalIgnoreCase)) {
                string dest = ArgValue(argv, "--dest", DefaultDir);
                Installer.Install(dest);
                if (!HasFlag(argv, "--silent"))
                    MessageBox.Show(Installer.PostInstallText(dest), Title,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }

            if (first.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)) {
                string where = Installer.Uninstall();
                if (!HasFlag(argv, "--silent"))
                    MessageBox.Show(
                        "Unregistered.\r\n\r\nThe files are still in:\r\n" + where +
                        "\r\n\r\nDelete that folder when you are ready.", Title,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }

            if (first.Equals("--read", StringComparison.OrdinalIgnoreCase) || first.Length == 0) {
                // Run from the install folder it is the reader. Run it from
                // anywhere else, a fresh download say, and it offers to install.
                string here = Path.GetDirectoryName(SelfPath).TrimEnd('\\');
                string reg  = Installer.RegisteredDir().TrimEnd('\\');
                bool installed = reg.Length > 0 &&
                    string.Equals(here, reg, StringComparison.OrdinalIgnoreCase);

                if (first.Length == 0 && !installed) {
                    Application.Run(new SetupForm());
                    return 0;
                }
                Reader.Open(null);
                return 0;
            }

            // used by build.ps1 to get the .ico for the second compile pass
            if (first.Equals("--emit-icon", StringComparison.OrdinalIgnoreCase) && argv.Length > 1) {
                File.WriteAllBytes(argv[1], Artwork.PngToIco(Artwork.PagePng(256)));
                return 0;
            }

            if (first.StartsWith("--")) {
                MessageBox.Show("Unknown option: " + first, Title,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 2;
            }

            Reader.Open(first);
            return 0;

        } catch (Exception ex) {
            MessageBox.Show(ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    static bool HasFlag(string[] a, string name) {
        foreach (string s in a) if (s.Equals(name, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    static string ArgValue(string[] a, string name, string fallback) {
        for (int i = 0; i < a.Length - 1; i++)
            if (a[i].Equals(name, StringComparison.OrdinalIgnoreCase)) return a[i + 1];
        return fallback;
    }
}

// ---------------------------------------------------------------- the reader

static class Reader {
    const string Welcome =
        "# Markdown Reading Desk\n\n" +
        "Drop a **.md** file onto this window, or press **Open .md** up in the bar.\n";

    public static void Open(string path) {
        string dir = Path.GetDirectoryName(App.SelfPath);
        string md, name;

        if (string.IsNullOrEmpty(path)) {
            md = Welcome;
            name = "no file open";
        } else {
            if (!File.Exists(path)) {
                MessageBox.Show("That file isn't there any more:\r\n\r\n" + path, App.Title,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            md = File.ReadAllText(path);
            name = Path.GetFileName(path);
        }

        string tplPath = Path.Combine(dir, "viewer.html");
        string tpl = File.Exists(tplPath) ? File.ReadAllText(tplPath) : Installer.BuildViewer();

        string html = tpl
            .Replace("<!--EMBED-->", md.Replace("</script", "<\\/script"))
            .Replace("<!--NAME-->", name.Replace("&", "&amp;").Replace("\"", "&quot;"));

        string outDir = Path.Combine(dir, "open");
        try { Directory.CreateDirectory(outDir); }
        catch (Exception) {
            outDir = Path.Combine(Path.GetTempPath(), "ReadingDesk");
            Directory.CreateDirectory(outDir);
        }

        StringBuilder sb = new StringBuilder();
        foreach (char c in name)
            sb.Append((char.IsLetterOrDigit(c) || c == '-' || c == '_') ? c : '-');
        string safe = sb.ToString();
        if (safe.Length > 60) safe = safe.Substring(0, 60);
        if (safe.Length == 0) safe = "doc";

        string outPath = Path.Combine(outDir, safe + ".html");
        File.WriteAllText(outPath, html, new UTF8Encoding(false));

        string browser = FindBrowser();
        if (browser == null) {
            ProcessStartInfo psi = new ProcessStartInfo(outPath);
            psi.UseShellExecute = true;
            Process.Start(psi);
        } else {
            Process.Start(browser, "--app=\"file:///" + outPath.Replace("\\", "/") + "\"");
        }
    }

    static string FindBrowser() {
        string pf   = Environment.GetEnvironmentVariable("ProgramFiles") ?? "";
        string pf86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? "";
        string lad  = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "";
        string[] candidates = {
            Path.Combine(pf,   @"Microsoft\Edge\Application\msedge.exe"),
            Path.Combine(pf86, @"Microsoft\Edge\Application\msedge.exe"),
            Path.Combine(pf,   @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(pf86, @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(lad,  @"Google\Chrome\Application\chrome.exe")
        };
        foreach (string c in candidates) if (File.Exists(c)) return c;
        return null;
    }
}

// ------------------------------------------------------------- install logic

static class Installer {

    public static string RegisteredDir() {
        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
                   @"Software\Classes\Applications\" + App.ExeName + @"\shell\open\command")) {
            if (k == null) return "";
            string cmd = k.GetValue(null) as string;
            if (string.IsNullOrEmpty(cmd) || !cmd.StartsWith("\"")) return "";
            int close = cmd.IndexOf('"', 1);
            if (close < 0) return "";
            try { return Path.GetDirectoryName(cmd.Substring(1, close - 1)); }
            catch { return ""; }
        }
    }

    public static void Install(string dest) {
        Directory.CreateDirectory(dest);
        Directory.CreateDirectory(Path.Combine(dest, "open"));

        string exe = Path.Combine(dest, App.ExeName);
        if (!string.Equals(exe, App.SelfPath, StringComparison.OrdinalIgnoreCase))
            File.Copy(App.SelfPath, exe, true);

        byte[] png = Artwork.PagePng(256);
        string icoPath = Path.Combine(dest, "ReadingDesk.ico");
        File.WriteAllBytes(icoPath, Artwork.PngToIco(png));

        File.WriteAllText(Path.Combine(dest, "viewer.html"), BuildViewer(png), new UTF8Encoding(false));

        RememberPrevious(dest);
        Register(exe, icoPath);
        MakeShortcut(exe, icoPath);
        App.RefreshShell();
    }

    public static string Uninstall() {
        string dir = RegisteredDir();
        if (dir.Length == 0) dir = Path.GetDirectoryName(App.SelfPath);

        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + App.ProgId, false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Applications\" + App.ExeName, false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\ReadingDesk", false);

        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", true))
            if (k != null) try { k.DeleteValue(App.Title, false); } catch { }

        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Classes\.md\OpenWithProgids", true))
            if (k != null) try { k.DeleteValue(App.ProgId, false); } catch { }

        // put back whatever .md pointed at before
        string note = Path.Combine(dir, "previous-md-progid.txt");
        if (File.Exists(note)) {
            string prev = File.ReadAllText(note).Trim();
            if (prev.Length > 0)
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.md"))
                    k.SetValue(null, prev);
        }

        try { File.Delete(ShortcutPath()); } catch { }
        App.RefreshShell();
        return dir;
    }

    static void RememberPrevious(string dest) {
        string prev = "";
        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Classes\.md"))
            if (k != null) prev = (k.GetValue(null) as string) ?? "";
        if (prev == App.ProgId) return;            // already ours, keep the older note
        File.WriteAllText(Path.Combine(dest, "previous-md-progid.txt"), prev, new UTF8Encoding(false));
    }

    static void Register(string exe, string icoPath) {
        string cmd = "\"" + exe + "\" \"%1\"";

        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + App.ProgId)) {
            k.SetValue(null, App.TypeName);
            k.SetValue("FriendlyTypeName", App.TypeName);
        }
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + App.ProgId + @"\DefaultIcon"))
            k.SetValue(null, icoPath + ",0");
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + App.ProgId + @"\shell\open\command"))
            k.SetValue(null, cmd);

        string ak = @"Software\Classes\Applications\" + App.ExeName;
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(ak))
            k.SetValue("FriendlyAppName", App.Title);
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(ak + @"\DefaultIcon"))
            k.SetValue(null, icoPath + ",0");
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(ak + @"\shell\open\command"))
            k.SetValue(null, cmd);
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(ak + @"\SupportedTypes"))
            k.SetValue(".md", "");

        // listed as a real app, so Settings can offer it under Default apps
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\ReadingDesk\Capabilities")) {
            k.SetValue("ApplicationName", App.Title);
            k.SetValue("ApplicationDescription", App.Blurb);
            k.SetValue("ApplicationIcon", icoPath + ",0");
        }
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\ReadingDesk\Capabilities\FileAssociations"))
            k.SetValue(".md", App.ProgId);
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
            k.SetValue(App.Title, @"Software\ReadingDesk\Capabilities");

        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.md")) {
            k.SetValue(null, App.ProgId);
            k.SetValue("PerceivedType", "text");
        }
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.md\OpenWithProgids"))
            k.SetValue(App.ProgId, new byte[0], RegistryValueKind.None);
    }

    static string ShortcutPath() {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                            App.Title + ".lnk");
    }

    static void MakeShortcut(string exe, string icoPath) {
        Type t = Type.GetTypeFromProgID("WScript.Shell");
        if (t == null) return;
        object shell = Activator.CreateInstance(t);
        object lnk = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell,
                                    new object[] { ShortcutPath() });
        Type lt = lnk.GetType();
        lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { exe });
        lt.InvokeMember("Arguments", BindingFlags.SetProperty, null, lnk, new object[] { "--read" });
        lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk,
                        new object[] { Path.GetDirectoryName(exe) });
        lt.InvokeMember("IconLocation", BindingFlags.SetProperty, null, lnk, new object[] { icoPath + ",0" });
        lt.InvokeMember("Description", BindingFlags.SetProperty, null, lnk, new object[] { App.Blurb });
        lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
    }

    public static string PostInstallText(string dest) {
        return "Installed to:\r\n" + dest + "\r\n\r\n" +
               "One step is yours. Windows protects the default-app choice, so no " +
               "installer can set it for you.\r\n\r\n" +
               "Right-click any .md file, choose Open with, then Choose another app, " +
               "pick " + App.Title + ", and tick Always use this app.";
    }

    // ---- the reader page -------------------------------------------------

    public static string BuildViewer() { return BuildViewer(Artwork.PagePng(256)); }

    public static string BuildViewer(byte[] png) {
        string reader;
        Assembly asm = Assembly.GetExecutingAssembly();
        using (Stream s = asm.GetManifestResourceStream("reader.html"))
        using (StreamReader r = new StreamReader(s, new UTF8Encoding(false)))
            reader = r.ReadToEnd();

        const string anchor = "<div class=\"app\" id=\"app\">";
        string slot = "<script type=\"text/markdown\" id=\"embedded\" " +
                      "data-name=\"<!--NAME-->\"><!--EMBED--></script>\r\n";
        reader = reader.Replace(anchor, slot + anchor);

        // the window takes its taskbar icon from the page favicon, so inline it
        string favicon = "<link rel=\"icon\" type=\"image/png\" href=\"data:image/png;base64," +
                         Convert.ToBase64String(png) + "\">";
        reader = reader.Replace("</title>", "</title>\r\n" + favicon);

        return "<!doctype html>\r\n<html lang=\"en\">\r\n<head>\r\n" +
               "<meta charset=\"utf-8\">\r\n" +
               "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">\r\n" +
               "<style>html{color-scheme:light dark}body{margin:0;font:14px system-ui,sans-serif}" +
               "img{max-width:100%}[hidden]{display:none!important}</style>\r\n" +
               reader + "\r\n</html>\r\n";
    }
}

// ------------------------------------------------------------------ artwork

static class Artwork {
    static GraphicsPath RoundRect(int x, int y, int w, int h, int r) {
        GraphicsPath p = new GraphicsPath();
        p.AddArc(x, y, r, r, 180, 90);
        p.AddArc(x + w - r, y, r, r, 270, 90);
        p.AddArc(x + w - r, y + h - r, r, r, 0, 90);
        p.AddArc(x, y + h - r, r, r, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static byte[] PagePng(int size) {
        Color teal  = Color.FromArgb(255, 47, 111, 102);
        Color deep  = Color.FromArgb(255, 32, 78, 72);
        Color cream = Color.FromArgb(255, 247, 249, 247);
        Color soft  = Color.FromArgb(255, 168, 200, 192);

        using (Bitmap bmp = new Bitmap(size, size))
        using (Graphics g = Graphics.FromImage(bmp)) {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float k = size / 256f;
            g.ScaleTransform(k, k);

            using (LinearGradientBrush grad = new LinearGradientBrush(
                       new Point(12, 12), new Point(244, 244), teal, deep))
            using (GraphicsPath bg = RoundRect(12, 12, 232, 232, 56))
                g.FillPath(grad, bg);

            using (SolidBrush b = new SolidBrush(cream))
            using (GraphicsPath page = RoundRect(62, 46, 132, 164, 18))
                g.FillPath(b, page);

            int[][] rules = {
                new int[] {82,  82, 92, 14}, new int[] {82, 110, 92, 9}, new int[] {82, 130, 70, 9},
                new int[] {82, 150, 92,  9}, new int[] {82, 170, 52, 9}
            };
            foreach (int[] r in rules)
                using (SolidBrush b = new SolidBrush(r[3] > 10 ? teal : soft))
                using (GraphicsPath bar = RoundRect(r[0], r[1], r[2], r[3], r[3]))
                    g.FillPath(b, bar);

            using (MemoryStream ms = new MemoryStream()) {
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }
    }

    // a one-frame .ico wrapping a PNG
    public static byte[] PngToIco(byte[] png) {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter w = new BinaryWriter(ms)) {
            w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)1);
            w.Write((byte)0); w.Write((byte)0); w.Write((byte)0); w.Write((byte)0);
            w.Write((ushort)1); w.Write((ushort)32);
            w.Write((uint)png.Length); w.Write((uint)22);
            w.Write(png);
            w.Flush();
            return ms.ToArray();
        }
    }
}

// -------------------------------------------------------------- setup window

class SetupForm : Form {
    TextBox dirBox;
    Label status;
    Button install, uninstall;

    public SetupForm() {
        Text = App.Title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(470, 248);
        Font = new Font("Segoe UI", 9f);
        BackColor = Color.FromArgb(247, 249, 247);

        using (MemoryStream ms = new MemoryStream(Artwork.PngToIco(Artwork.PagePng(64))))
            Icon = new Icon(ms);

        Label head = new Label();
        head.Text = App.Title;
        head.Font = new Font("Segoe UI Semibold", 15f);
        head.ForeColor = Color.FromArgb(20, 26, 24);
        head.SetBounds(22, 18, 420, 30);

        Label sub = new Label();
        sub.Text = "A markdown reader with tint, typeface and reading-mode controls.\r\n" +
                   "Installs for you only. No admin rights needed.";
        sub.ForeColor = Color.FromArgb(92, 107, 102);
        sub.SetBounds(24, 52, 424, 36);

        Label where = new Label();
        where.Text = "Install to";
        where.ForeColor = Color.FromArgb(92, 107, 102);
        where.SetBounds(24, 100, 80, 18);

        dirBox = new TextBox();
        dirBox.Text = Installed() ? Installer.RegisteredDir() : App.DefaultDir;
        dirBox.SetBounds(24, 120, 330, 24);

        Button browse = new Button();
        browse.Text = "Browse";
        browse.SetBounds(362, 119, 84, 26);
        browse.Click += delegate {
            using (FolderBrowserDialog d = new FolderBrowserDialog()) {
                d.Description = "Choose a folder for " + App.Title;
                if (d.ShowDialog() == DialogResult.OK)
                    dirBox.Text = Path.Combine(d.SelectedPath, "ReadingDesk");
            }
        };

        install = new Button();
        install.Text = Installed() ? "Reinstall" : "Install";
        install.SetBounds(24, 164, 120, 32);
        install.Click += delegate { DoInstall(); };

        uninstall = new Button();
        uninstall.Text = "Uninstall";
        uninstall.SetBounds(152, 164, 100, 32);
        uninstall.Enabled = Installed();
        uninstall.Click += delegate { DoUninstall(); };

        Button close = new Button();
        close.Text = "Close";
        close.SetBounds(346, 164, 100, 32);
        close.Click += delegate { Close(); };

        status = new Label();
        status.ForeColor = Color.FromArgb(47, 111, 102);
        status.SetBounds(24, 206, 424, 32);

        Controls.AddRange(new Control[] { head, sub, where, dirBox, browse, install, uninstall, close, status });
        AcceptButton = install;
        CancelButton = close;
    }

    static bool Installed() { return Installer.RegisteredDir().Length > 0; }

    void DoInstall() {
        try {
            string dest = dirBox.Text.Trim();
            if (dest.Length == 0) { status.Text = "Pick a folder first."; return; }
            Installer.Install(dest);
            status.ForeColor = Color.FromArgb(47, 111, 102);
            status.Text = "Installed to " + dest;
            install.Text = "Reinstall";
            uninstall.Enabled = true;
            MessageBox.Show(Installer.PostInstallText(dest), App.Title,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        } catch (Exception ex) {
            status.ForeColor = Color.FromArgb(150, 50, 40);
            status.Text = "Could not install: " + ex.Message;
        }
    }

    void DoUninstall() {
        if (MessageBox.Show("Unregister " + App.Title + " and restore the previous .md app?",
                App.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try {
            string dir = Installer.Uninstall();
            status.ForeColor = Color.FromArgb(47, 111, 102);
            status.Text = "Unregistered. Files are still in " + dir;
            install.Text = "Install";
            uninstall.Enabled = false;
        } catch (Exception ex) {
            status.ForeColor = Color.FromArgb(150, 50, 40);
            status.Text = "Could not uninstall: " + ex.Message;
        }
    }
}
