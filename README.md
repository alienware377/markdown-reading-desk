# Markdown Reading Desk

A single-file markdown reader with the reading-mode controls iOS keeps to itself.
Double-click a `.md` file, get a properly typeset page, then tune the tint,
the typeface, the measure, and the reading aids until it's comfortable.

No build step, no dependencies to install, no server, nothing phoning home.
One HTML file plus a ~13 KB launcher.

---

## What you can change

**Page tint.** Auto (follows your system theme), Paper, Sepia, Solar, Night,
Slate, Dusk, and a high-contrast Ink.

**Typeface.** Literata, Fraunces, IBM Plex Sans, Atkinson Hyperlegible
(designed for low vision), IBM Plex Mono, or your system sans.

**Measure and rhythm.** Every one of these is a slider, not a preset:

| Control | Typographic name | Sensible range |
| --- | --- | --- |
| Text size | type size | 17–21 px for long reads |
| Line height | leading | 1.5–1.8 at body size |
| Column width | measure | 45–75 characters |
| Paragraph gap | vertical rhythm | 0.8–1.4 em |
| Letter spacing | tracking | 0 for serif, a hair for caps |
| Word spacing | word space | 0, unless justifying |

**Reading aids**

- **Bionic emphasis** bolds the opening of each word so the eye lands on the
  shape instead of spelling it out. Strength is adjustable, 20–70%.
- **Focus dimming** fades everything except the block under your pointer.
- **Reading ruler**, a band that tracks your cursor, the index-card trick.
- **Justified text** and **hyphenation**, which belong together.
- **Drop cap** marks the opening the way a printed page would.

**Along the way**, a contents sidebar built from the headings, a progress
line, live word count and reading time, read-aloud via the browser's speech
voice, print, and save-as-styled-HTML.

Everything you change is kept in the browser's local storage, so the desk looks
the same next time you sit down.

## Keyboard

```text
\   settings panel
[ ] text size down / up
b   bionic emphasis
f   focus dimming
r   reading ruler
t   contents
e   source editor (type markdown, it renders live)
```

## Install (Windows, no admin rights)

Grab **ReadingDesk.exe** from the [latest release](https://github.com/alienware377/markdown-reading-desk/releases/latest)
and double-click it. A small setup window opens: pick a folder, press Install.
It is unsigned, so SmartScreen will ask first; choose *More info*, then *Run anyway*.

That one file is the whole app. It installs a copy of itself, unpacks the
reader beside it, draws its icon, adds a Start Menu entry, and registers as a
handler for `.md`. Nothing is written outside your own user account.

**One step is yours.** Windows 11 protects the default-app choice with a hash,
so no installer, this one included, may set it for you:

> right-click any `.md` file, choose **Open with**, then *Choose another app*,
> pick **Markdown Reading Desk**, and tick **Always use this app**

To uninstall, run the setup window again and press Uninstall, or:

```powershell
& "$env:LOCALAPPDATA\ReadingDesk\ReadingDesk.exe" --uninstall
```

### From source

```powershell
git clone https://github.com/alienware377/markdown-reading-desk.git
cd markdown-reading-desk
powershell -ExecutionPolicy Bypass -File install.ps1
```

`build.ps1` alone produces `dist\ReadingDesk.exe` without installing anything.
Pass `-Dest` to `install.ps1` to choose the folder. Both need .NET Framework
4.x, which ships with Windows.

## Use it without installing

`reading-desk.html` works on its own. Open it in any browser and drop a `.md`
file onto the window, or paste into the source pane.

## How it works

`ReadingDesk.exe` takes the `.md` path Explorer hands it, reads the text,
drops it into `viewer.html` inside a `<script type="text/markdown">` block,
writes the result next to itself, and opens that in Chrome or Edge with
`--app=` so you get a window without tabs or an address bar. If neither
browser is present it falls back to your default one.

Markdown is parsed by [marked](https://github.com/markedjs/marked) and
sanitized with [DOMPurify](https://github.com/cure53/DOMPurify), both loaded
from cdnjs. Fonts come from Google Fonts. Those are the only network requests
the page makes. Offline, it falls back to system fonts and shows the raw text.

## Layout

```text
reading-desk.html   the reader: all the CSS, all the controls, one file
src/ReadingDesk.cs  the app: reader, setup window, and .md registration
build.ps1           compiles the two into dist\ReadingDesk.exe
install.ps1         builds, then runs the installer
uninstall.ps1       puts everything back
```

## License

MIT, see [LICENSE](LICENSE).
