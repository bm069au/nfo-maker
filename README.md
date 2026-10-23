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
- Right-click a movie or episode and choose **Create NFO file**. Filename
  suggestions and existing NFO details load automatically. Saving beside the
  selected video uses one Save click; an existing NFO receives a backup.
- Right-click a folder (or empty space inside it) and choose **NFO Maker**.
  The app opens with that folder already selected. Click **Batch** to load its
  videos, including subfolders into the movie and TV review grid. From a selected video,
  **Batch** uses that video's folder. No folder-tree navigation is needed.
- Review titles, years, types and episode numbers, then **Save selected NFOs**.
  Existing files stay unselected; replacements require explicit approval and
  backups. Ambiguous episode numbering and obvious extras stay unselected.
  Batch scans subfolders automatically, skipping sample/extra folders and directory links. Each NFO is saved beside its own video.
  Details come from filenames and existing sidecars; descriptions are not invented.
- Windows 11 may put these actions under **Show more options**. Setup installs
  them; portable copies can use **Add file and folder right-click menus**.
- Use **Enable Open with**, then choose the Windows default yourself if wanted.
  The app does not silently replace your default video player.

Videos are not renamed, moved or overwritten. NFO changes are not automatically
sent to Jellyfin: refresh library metadata yourself. Batch writes are atomic per
file, not an all-or-nothing transaction; failures report partial progress.

## Source and build

`NfoMaker.cs` and `Batch.cs` are the existing current source. Run `build.ps1`
with the installed Windows .NET Framework compiler. No external packages.
Developers can run `package-installer.ps1` to produce the setup executable.

## Licence

MIT. Free to use, modify and redistribute; see `LICENSE`. No warranty.

## Creator and support

Created by **Brett Martin Moore**. This program is free. Development funds are
often limited; voluntary donations are welcome if you can afford to help.
Open **Help > About** for Contact and Donations buttons, or email
[bm069au@gmail.com](mailto:bm069au@gmail.com) to get in touch or ask how to donate.

NFO actions request the top position in the classic Explorer menu. Windows 11
may still place them under Show more options; first-menu native integration
is not included in this release.
