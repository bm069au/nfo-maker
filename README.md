# NFO Maker

Free, offline Windows NFO creation and editing for Jellyfin-style movie and TV
metadata. Filename-led suggestions: no online metadata service, account,
subscription, advertisements or paid API.

## Download and run

**[Download NfoMaker-Setup.exe](https://github.com/bm069au/nfo-maker/releases/latest/download/NfoMaker-Setup.exe)**

Double-click the setup file, approve Windows' permission prompt and click
**Install**. Then open **NFO Maker** from the desktop or Start menu.
No extracting ZIPs, command line, compiler or manual registry setup is needed.
Setup installs into Program Files, creates shortcuts, adds the video right-click
action and registers NFO opening. It also adds an ordinary Windows uninstall
entry. Your default video player is unchanged. The installer is unsigned.

## Use

- Create or edit movie, episode and documentary NFOs: title, series, season,
  episode, year and plot. Zero is supported for specials.
- Open an existing NFO; save makes a timestamped backup and preserves XML
  metadata outside the edited fields.
- Choose **Batch folder** to review filename-derived SxxExx episode suggestions.
  Missing recognised episodes are selected by default. Review unknown or
  multi-episode numbering manually. Updating existing NFOs is explicit and
  backed up; malformed non-XML files require individual review/rebuild.
- The installer adds **Create NFO file** for common video types, including
  MKV, AVI and MP4. Windows 11 may put this under **Show more options**.
  The in-app registration action remains available for portable copies.
- Use **Enable Open with**, then choose the Windows default yourself if wanted.
  The app does not silently replace your default video player.

Videos are not renamed, moved or overwritten. NFO changes are not automatically
sent to Jellyfin: refresh library metadata yourself. Batch writes are atomic per
file, not an all-or-nothing transaction; failures report partial progress.

## Source and build

`NfoMaker.cs` and `Batch.cs` are the existing current source. Run `build.ps1`
with the installed Windows .NET Framework compiler. No external packages.
Developers can run `package-installer.ps1` to produce the setup executable.

The release executable is the existing 18 October 2026 build, SHA-256:
`339ced7edb44d320a2ef406f5dbc096fd37a81a0bd66655c1f2def7305ca8f40`.
It matches the installed copy used on Brett's machine. No media-library tests,
registry changes or new gameplay/server changes were performed for publication.

## Licence

MIT. Free to use, modify and redistribute; see `LICENSE`. No warranty.
