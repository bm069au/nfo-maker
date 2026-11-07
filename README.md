# NFO Maker

Free, offline Windows NFO creation and editing for Jellyfin-style movie and TV
metadata. Filename-led suggestions: no online metadata service, account,
subscription, advertisements or paid API.

## Download and run

**[Download NfoMaker-Setup.exe](https://github.com/bm069au/nfo-maker/releases/latest/download/NfoMaker-Setup.exe)**

Double-click the setup file, approve Windows' permission prompt and click
**Install**. Then open **NFO Maker** from the desktop or Start menu.
If Setup finds an existing installation, it asks before replacing its files.
No extracting ZIPs, command line, compiler or manual registry setup is needed.
Setup installs into Program Files, creates shortcuts, adds right-click actions
for folders and videos, and registers NFO opening. On Windows 11, look under
**Show more options**. The installer is unsigned.

See [CHANGELOG.md](CHANGELOG.md) for release changes.

## Use

- Create or edit movie, episode and documentary NFOs: title, series, season,
  episode, year and plot. Zero is supported for specials.
- Open an existing NFO; save makes a timestamped backup and preserves XML
  metadata outside the edited fields.
- Right-click a movie or episode and choose **Create NFO file**.
  Filename suggestions and existing NFO details load
  automatically. Saving beside the selected video uses one Save click; an
  existing NFO receives a backup.
- Right-click a folder (or empty space inside it) and choose **NFO Maker**.
  The app opens with that folder
  already selected. Click **Batch** to load its videos, including subfolders
  into the movie and TV review grid. From a selected video, **Batch** uses that
  video's folder. No folder-tree navigation is needed.
- Choose one large **Type for whole batch** dropdown (Auto detect, Movie, TV Show, Documentary film or Documentary episode). It applies to every row; there are no per-row dropdowns. Review titles, years and episode numbers, then **Save selected NFOs**.
  Existing files, ambiguous episode numbering and obvious extras start unselected.
  Batch scans subfolders automatically, skipping sample/extra folders and directory links. Each NFO is saved beside its own video.
  Details come from filenames and existing sidecars; descriptions are not invented.
  Select unreadable NFOs with the row checkboxes or **Select all**. **Save selected NFOs** asks whether to overwrite existing files, then whether to keep .bak copies. Approved replacements for unreadable NFOs use the filename suggestions and grid values. Declining overwrite still saves selected videos that lack an NFO.
- Use **Delete NFO...** in the editor, or **Delete NFOs in folder...** in Batch. Review the exact file list. The preview includes subfolders by default. Clear **Include subfolders** for the selected folder only. The list includes show-level files and those without matching videos. Clear **Keep a backup** for permanent deletion. Videos and existing .bak files are untouched.
- Portable copies can use **Add file and folder right-click menus**.
- Use **Enable Open with**, then choose the Windows default yourself if wanted.
  The app does not silently replace your default video player.

Videos are not renamed, moved or overwritten. NFO changes are not automatically
sent to Jellyfin: refresh library metadata yourself. Batch writes are atomic per
file, not an all-or-nothing transaction; failures report partial progress.

## Source and build

Run `build.ps1` with the installed Windows .NET Framework compiler. Developers can run
`package-installer.ps1` to produce `dist/NfoMaker-Setup.exe`.
The release folder contains only that installer and its checksum; intermediate
executables and payloads stay in the ignored `.build` folder. Old portable ZIPs
are historical releases and should not be used for current installation.

## Licence

MIT. Free to use, modify and redistribute; see `LICENSE`. No warranty.

## Creator and support

Created by **Brett Martin Moore**. This program is free. Development funds are
often limited; voluntary donations are welcome if you can afford to help.
Open **Help > About** for Contact and Donations buttons, or email
[bm069au@gmail.com](mailto:bm069au@gmail.com) to get in touch or ask how to donate.
