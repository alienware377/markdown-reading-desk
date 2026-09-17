using System;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;

class ReadingDesk {
    [STAThread]
    static void Main(string[] argv) {
        string dir = Path.GetDirectoryName(Application.ExecutablePath);
        string md, name;

        if (argv.Length == 0 || string.IsNullOrEmpty(argv[0])) {
            md = "# Markdown Reading Desk\n\nDrop a **.md** file onto this window, or press **Open .md** up in the bar.\n";
            name = "no file open";
        } else {
            string path = argv[0];
            if (!File.Exists(path)) {
                MessageBox.Show("That file isn't there any more:\n\n" + path,
                    "Reading Desk", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try { md = File.ReadAllText(path); }
            catch (Exception ex) {
                MessageBox.Show("Couldn't read that file:\n\n" + ex.Message,
                    "Reading Desk", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            name = Path.GetFileName(path);
        }

        string tplPath = Path.Combine(dir, "viewer.html");
        if (!File.Exists(tplPath)) {
            MessageBox.Show("Reading Desk is missing viewer.html in:\n\n" + dir,
                "Reading Desk", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        string html = File.ReadAllText(tplPath)
            .Replace("<!--EMBED-->", md.Replace("</script", "<\\/script"))
            .Replace("<!--NAME-->", name.Replace("&", "&amp;").Replace("\"", "&quot;"));

        string outDir = Path.Combine(dir, "open");
        Directory.CreateDirectory(outDir);
        StringBuilder sb = new StringBuilder();
        foreach (char c in name)
            sb.Append((char.IsLetterOrDigit(c) || c == '-' || c == '_') ? c : '-');
        string safe = sb.ToString();
        if (safe.Length > 60) safe = safe.Substring(0, 60);
        if (safe.Length == 0) safe = "doc";
        string outPath = Path.Combine(outDir, safe + ".html");
        File.WriteAllText(outPath, html, new UTF8Encoding(false));

        string browser = FindBrowser();
        try {
            if (browser == null) {
                ProcessStartInfo psi = new ProcessStartInfo(outPath);
                psi.UseShellExecute = true;
                Process.Start(psi);
            } else {
                Process.Start(browser, "--app=\"file:///" + outPath.Replace("\\", "/") + "\"");
            }
        } catch (Exception ex) {
            MessageBox.Show("Couldn't open a browser window:\n\n" + ex.Message,
                "Reading Desk", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    static string FindBrowser() {
        string pf  = Environment.GetEnvironmentVariable("ProgramFiles") ?? "";
        string pf86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? "";
        string lad = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "";
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