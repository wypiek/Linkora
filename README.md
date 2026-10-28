# Linkora

A Windows Live Messenger style instant messenger, running on **macOS**, **Linux** and **Windows** (7 and later).

![Preview1](./Media/wlmclient1.png)

## Features
+ Add / Remove / Block Contact
+ Favourite contacts
+ Send Nudge
+ Games in the conversation window (tic-tac-toe)
+ File attachments in chat
+ Avatars / profile pictures shown in the contact list
+ Interface in your own language
+ Light and dark themes
+ Quick Message
+ Emoticons
+ Sign up page hosted by the server itself
+ Encryption
+ Save ID & Password
+ Auto Login
+ Notification Bubble
+ Automatic Away Status (AFK 150 Seconds)

## Project layout

| Project      | What it is                                                                       |
| ------------ | -------------------------------------------------------------------------------- |
| `WLMClient`  | The Linkora client UI (Avalonia).                                                     |
| `WLMServer`  | The console server. Talks to MySQL and relays messages between clients.          |
| `WLMData`    | Packet definitions shared by both.                                               |
| `WLMNet`     | The TCP transport: framing, AES encryption and packet dispatch.                  |

---

# Quick start

You need three things running: a **MySQL database**, the **server**, and one or more **clients**.

## 1. Database

Create a database called `msn` and import the schema:

```bash
mysql -u root -p -e "CREATE DATABASE msn CHARACTER SET utf8;"
mysql -u root -p msn < Database/msn.sql
```

That creates the two tables the server uses: `account` and `friend_requests`.

It is worth creating a dedicated user rather than using `root`:

```bash
mysql -u root -p -e "CREATE USER 'wlm'@'localhost' IDENTIFIED BY 'a-strong-password'; GRANT ALL ON msn.* TO 'wlm'@'localhost'; FLUSH PRIVILEGES;"
```

## 2. Server

The server is one build for every operating system: `dist/server` from a release (see
"Producing releases"). It needs a [.NET runtime](https://dotnet.microsoft.com/download) installed,
.NET 6 or newer, and on Linux also OpenSSL.

Open `Messenger.config` next to `WLMServer.dll` and fill in your details:

```xml
<appSettings>
  <add key="server_port" value="1323" />
  <add key="server_encryption_key" value="CHANGEME" />
  <add key="database_host" value="localhost" />
  <add key="database_id" value="root" />
  <add key="database_password" value="" />
  <add key="database_password_encryption_key" value="CHANGE_ME_123456_123456_" />
  <add key="database_password_encryption_iv" value="CHANGE_ME_123456" />
  <add key="avatars_enabled" value="false" />
  <add key="avatars_address" value="" />
  <add key="avatars_address_upload" value="" />
  <add key="avatars_http_port" value="0" />
  <add key="avatars_storage_path" value="uploads" />
  <add key="http_port" value="8080" />
  <add key="registration_enabled" value="true" />
  <add key="registration_url" value="" />
  <add key="server_name" value="Linkora" />
  <add key="broadcast_interval" value="30" />
</appSettings>
```

| Setting                             | Description                                                                                                                 |
| ----------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| server_port                         | *What port to use.*                                                                                                         |
| server_encryption_key               | *Encryption key for talking to the client. Must match the client's value — see "Matching the encryption key" below.*        |
| database_host                       | *Database host holding the account information.*                                                                            |
| database_id                         | *Database user.*                                                                                                            |
| database_password                   | *Database user password.*                                                                                                   |
| database_password_encryption_key    | *Key used to encrypt stored account passwords. Change the value but **keep the length** (24 characters).*                   |
| database_password_encryption_iv     | *IV used for account password encryption. Change the value but **keep the length** (16 characters).*                        |
| avatars_enabled                     | *Whether to use avatars (true/false). Requires the registration page below.*                                                |
| avatars_address                     | *Where avatars are served from, e.g. `http://localhost/uploads/`*                                                           |
| avatars_address_upload              | *The upload endpoint the client posts to, e.g. `http://localhost/upload.php`*                                               |
| avatars_http_port                   | *Old name for `http_port`, still read when `http_port` is absent.*                                                          |
| avatars_storage_path                | *Where the built-in webserver keeps uploaded pictures. Relative paths are resolved next to the executable.*                 |
| http_port                           | *Port for the built-in website, which carries the sign up page and the avatar endpoints. `0` turns the website off.*        |
| registration_enabled                | *Whether the server hosts a sign up page (true/false).*                                                                     |
| registration_url                    | *Optional. What clients are told to open for "Sign up." Only needed behind a reverse proxy, or to point at another site.*   |
| server_name                         | *The name shown on the sign up page.*                                                                                       |
| broadcast_interval                  | *How often (seconds) the server refreshes every connected user's contact list.*                                             |

> **Important:** `database_password_encryption_key` must be exactly 24 characters and
> `database_password_encryption_iv` exactly 16. These derive the AES key used for stored
> passwords — changing them later invalidates every existing account password.

Start it from that folder:

```bash
dotnet WLMServer.dll
```

You should see `Local End Point: 0.0.0.0:1323`. Server commands:

| Command       | Description                       |
| ------------- | --------------------------------- |
| `/help`       | Show the command list             |
| `/online`     | Number of users currently online  |
| `/listonline` | List online users and their IPs   |
| `/create`     | Register a new account            |

Create your first accounts with `/create` (it prompts for a username and password), or let people
sign up themselves at `http://<your server>:8080/signup`, which the server hosts by itself.

## 3. Client

The sign in page has a **server address** box: type the address of whichever Linkora server you
want to use (`127.0.0.1`, `chat.example.com`, or `chat.example.com:1323` to override the port).
It is remembered between sessions, so most people only ever set it once.

`Messenger.config` next to the client supplies the value the box starts out with:

```xml
<appSettings>
  <add key="server_address" value="127.0.0.1" />
  <add key="server_port" value="1323" />
  <add key="registration_url" value="" />
</appSettings>
```

| Setting            | Description                                                                                       |
| ------------------ | ------------------------------------------------------------------------------------------------- |
| server_address     | *Hostname or IP of your server.*                                                                  |
| server_port        | *Must match the server's `server_port`.*                                                          |
| registration_url   | *Optional. Forces where the "Sign up." link goes. Leave empty and the client asks the server, which is what you want unless you are overriding a server you do not run.* |

On macOS that file lives inside the bundle, at
`Linkora.app/Contents/MacOS/Messenger.config`.

Then start the client and sign in.

### Matching the encryption key

All traffic between client and server is AES encrypted with a pre-shared key. The server reads
it from `server_encryption_key` in its config; **the client's copy is compiled in**, in
`WLMClient/Config/Properties.cs`:

```csharp
public static string SERVER_ENCRYPTION_KEY = "CHANGEME";
```

If you change the server's key you must change this constant and rebuild the client, otherwise
the two sides cannot decrypt each other and sign in will silently fail.

---

# Building

### Prerequisites
* [.NET SDK 8.0 or newer](https://dotnet.microsoft.com/download)
* A MySQL or MariaDB server

### Build and run from source

```bash
dotnet build                                    # everything
dotnet run --project WLMServer                  # the server
dotnet run --project WLMClient                  # the client
```

### Producing releases

```bash
./build-release.sh                       # the installers, the Linux clients and the server
./build-release.sh installers            # all three installers
./build-release.sh installer-windows7    # just one installer
./build-release.sh linux-x64             # a plain client folder
./build-release.sh server                # just the server
```

Installers land in `dist/installers/`; everything else in `dist/<target>/`, with macOS clients
packaged as a `.app` bundle. Only the targets being built are replaced; anything else already in
`dist/` stays where it is. The Windows installers need `makensis` (`brew install makensis`); the
macOS one uses `pkgbuild` and `productbuild`, which come with macOS.

| Installer                                   | For                                                                 |
| ------------------------------------------- | ------------------------------------------------------------------- |
| `Linkora-<version>-Windows7-Setup.exe`      | Windows 7 SP1 and later, 32 and 64-bit. Built on .NET 6, the last .NET Windows 7 runs. |
| `Linkora-<version>-Windows10-11-Setup.exe`  | Windows 10 and 11, x64 and ARM64. Built on .NET 8, which still gets security fixes. |
| `Linkora-<version>-macOS-Universal.pkg`     | macOS 11 and later, Apple silicon and Intel. |

Each installer carries a build per processor and installs the one that suits the machine. The
Windows ones install for the current user, with no administrator prompt, add Start menu and
desktop shortcuts and an entry in Add or Remove Programs, and keep `Messenger.config` and the saved
sign in details when installed over an earlier version. The Windows 7 installer also checks for
update KB3063858 and offers its download page when it is missing. The macOS package installs
`Linkora.app` into Applications; it is not signed, so open it with right click → Open the first
time. The installer sources are in `packaging/`.

| Target                                      | What you get                                                      |
| ------------------------------------------- | ------------------------------------------------------------------ |
| `installers`                                | The three installers above. |
| `osx-arm64`, `osx-x64`, `linux-x64`, `linux-arm64` | The self-contained client. No .NET needed, ~100 MB each. |
| `win-x64`, `win-x86`                        | The self-contained client for **Windows 7 SP1 and later**. `win-x86` is 32-bit and runs on any Windows. |
| `server`                                    | **The server, one build for every OS**, Windows 7 included. ~15 MB, needs any .NET runtime from 6 up installed. |

### One server for every OS

There is no such thing as a single *native* executable for several operating systems — macOS uses
Mach-O binaries, Linux ELF and Windows PE. So the server is not native: `dist/server` holds .NET
files that run on any of them, on Intel/AMD and ARM alike. It is built against .NET 6 and allowed to
roll forward, so it starts on whatever .NET is installed: .NET 6 on Windows 7, or 8, 9, 10 anywhere
else. Install a [.NET runtime](https://dotnet.microsoft.com/download) and run:

```bash
dotnet WLMServer.dll
```

The clients do not come with a server of their own, because **a single running server already
serves macOS, Linux and Windows clients at the same time.** They all speak the same protocol, so you
never need more than one server no matter what your users run.

### Running the macOS build

The app is not code signed, so Gatekeeper blocks it on first launch. Clear the quarantine flag:

```bash
xattr -dr com.apple.quarantine "Linkora.app"
```

### Running the Windows build

`client\WLMClient.exe` is the client. Nothing needs installing on Windows 10 or 11.

Windows 7 SP1 needs update **KB3063858** (or the older KB2533623) before the first run. If the
program then reports `api-ms-win-crt-runtime-l1-1-0.dll` as missing, also install the
[Visual C++ 2015-2019 Redistributable](https://aka.ms/vs/16/release/vc_redist.x86.exe).
`README-windows.txt` next to the build has the download links.

**Why Windows 7 works.** .NET 8 does not run on Windows 7; .NET 6 was the last .NET that did. The
Windows clients and the server are therefore built against .NET 6, switched on by
`-p:Windows7=true` (see `Directory.Build.props`), while the other clients stay on .NET 8. Microsoft no longer ships
security fixes for .NET 6, which is the cost of keeping Windows 7 supported. To build the Windows 7
flavour by hand:

```bash
dotnet publish WLMClient -c Release -r win-x86 --self-contained -p:Windows7=true
```

### Running the Linux build

The **server** needs OpenSSL on Linux, which the MySQL connector uses for TLS. Minimal images such as
`debian:12` do not ship it, and without it the server exits with
*"No usable version of libssl was found"*:

```bash
sudo apt install libssl3          # Debian/Ubuntu
sudo dnf install openssl-libs     # Fedora
```

The **client** needs a desktop session, the usual X11/Wayland client libraries and
at least one font:

```bash
sudo apt install libx11-6 libice6 libsm6 libfontconfig1 fonts-dejavu-core    # Debian/Ubuntu
sudo dnf install libX11 libICE libSM fontconfig dejavu-sans-fonts            # Fedora
```

Notification sounds are played through `paplay`, `aplay`, `pw-play` or `ffplay`, whichever is
installed. Without one of those the app runs fine, just silently.

---

# Profile pictures

Profile pictures are off by default. There are two ways to turn them on.

### Option A — the built-in webserver (no webserver needed)

The server hosts pictures on the same website as the sign up page. Set:

```xml
<add key="avatars_enabled" value="true" />
<add key="http_port" value="8080" />
<add key="avatars_address" value="http://your-server:8080/uploads/" />
<add key="avatars_address_upload" value="http://your-server:8080/upload" />
<add key="avatars_storage_path" value="uploads" />
```

On start the server reports `Website listening on port 8080`. Users then set a picture from
**Options → Browse** in the client; it is resized to 100×100, uploaded, and shown to their
contacts.

`avatars_address` must be reachable *by the clients*, so use the server's real hostname or IP
rather than `localhost` when they run on other machines. Open the port in your firewall too.

### Option B — the PHP registration page

If you already run a webserver, point the two addresses at the bundled `upload.php` instead. See
"The old PHP page" further down.

> Uploaded pictures are served over plain HTTP and are readable by anyone who can reach the port.
> Put it behind a reverse proxy with TLS if that matters to you.

---

# The contact list

Each contact shows their profile picture inside the same frame the application draws around your
own picture, which carries their status: green for available, red for busy, orange for away and
silver for offline. Contacts without a picture get the default one from the sign in page.

Underneath their name is either their personal message, or, when they have not set one, the most
recent message exchanged with them. That preview covers the current session only, since the
application keeps no message history.

Right click a contact and choose **Add to Favourites** to pin them. Favourites are listed in
their own group above everyone else, and both groups can be collapsed by clicking the heading.
The list is kept per account in `Favourites.xml`, next to the saved sign in details, so two
people sharing an installation keep separate lists.

---

# Changing your sign in name

**Options** lets you change both names an account has:

* **Display name** is what your contacts see. It changes immediately.
* **Sign in name** is what you type to sign in and what people add you by. Changing it asks the
  server, which renames the account and every reference to it — the contact list of everyone who
  has you, and any outstanding friend requests — in a single database transaction, so a failure
  part way through cannot leave people pointing at a name that no longer exists.

A sign in name can be up to 29 characters of letters, digits, dots, dashes and underscores. The
limited set is deliberate: contact lists are stored as `[name,blocked,accepted]` text, so a name
containing a bracket or a comma would corrupt every list referring to it. If the name is already
taken the server refuses and nothing changes.

Saved sign in details are updated to the new name, so "Remember me" keeps working.

---

# Dark theme

**Options → Dark theme** switches between the light and dark looks. It applies immediately to
every open window and is remembered.

The photographic header artwork is a fixed bitmap rather than something that can be recoloured,
so in the dark theme it is toned down with an overlay instead. The avatar frames and the
notification popup keep their own colours in both themes, since those carry the application's
identity.

---

# Languages

The interface language is chosen in **Options → Language**. It applies immediately and is
remembered. Out of the box Linkora speaks English, Spanish, German, French and Polish, and on
first run it follows the language the operating system is set to.

The version in use is shown at the foot of the window. Release builds add the date they were
produced, for example `Linkora 1.1.0 (build 20260911)`.

### Adding your own language

Translations are plain text files in the `Languages` folder next to the client, so adding one
needs no rebuild:

1. Copy an existing file, for example `Languages/es.lang`, to your language's code:
   `Languages/pt-BR.lang`. The file name is what the application matches against the system
   language.
2. Translate the values after each `=`. Set `language.name` to the language's own name, which is
   what the Options list shows.
3. Restart the client. The new language appears in the list.

```
language.name = Português
login.button = Entrar
main.friends = Amigos ({0}/{1})
```

Placeholders such as `{0}` are filled in by the application and must be kept. `
` starts a new
line. Lines beginning with `#` are comments. Any key you leave out falls back to English, so a
partial translation is perfectly usable.

---

# Games

The **Games** button at the top of a conversation lists the games you can play with that contact.
Pick one and they are invited; the game opens in a column on the right of the conversation on both
sides, and the window widens so the chat keeps its room.

* **Tic-tac-toe.** Whoever sends the invitation plays X and moves first. When a game ends,
  **Play again** sends a fresh invitation, so the other person opens the next one if they ask.
* An invitation that arrives with the conversation closed opens it, with the usual sound and
  notification.
* Closing the game, or the conversation, tells the other side you have left. A contact signing
  out ends the game too.
* Invitations from blocked contacts, and any invitation while you appear offline, are turned down
  without asking.

Both clients check every move against the rules, so neither can make a move the other side's
board does not allow. The server only passes the moves along and keeps nothing.

---

# File attachments

Click the paperclip in a conversation to send files. They travel over the same encrypted
connection as messages — the server relays them without writing anything to disk, so no webserver
or extra storage is involved.

* Images are shown inline in the conversation; anything else appears as a clickable link.
* Received files are saved to **`~/Downloads/Linkora`** and open in the desktop's default
  application when clicked.
* The limit is **20 MB** per file.

---

# Signing up

The server hosts its own sign up page, so a new Linkora server needs nothing else installed for
people to get accounts on it. With the default config it is at:

```
http://<your server>:8080/signup
```

That is what the **"Sign up."** link on the sign in page opens. The link does not guess: the
client asks the server where its sign up page is, and opens what it is told. Which means:

* A server that hosts the page answers with its own port, and the client puts in front of it the
  same address the person typed to connect. A name, a LAN address and a tunnel all reach the same
  server, and only the one they used will work for them, so only they can fill that part in.
* A server with `registration_enabled` set to `false` answers with nothing, and the client says
  that server does not offer sign up rather than opening a page that is not there.

The page checks that the name is free and usable and that the password is at least six characters,
then creates the account. Accounts made this way are ordinary accounts: they sign in immediately,
with no further step.

### Behind a reverse proxy, or somewhere else entirely

`registration_url` is what clients are told to open, and nothing else:

```xml
<!-- HTTPS in front of the server's own page: the page keeps running, this is just the way in -->
<add key="registration_enabled" value="true" />
<add key="registration_url" value="https://chat.example.com/signup" />

<!-- sign up handled by a different site: no page here, clients are sent there -->
<add key="registration_enabled" value="false" />
<add key="registration_url" value="https://accounts.example.com/" />
```

### Turning sign up off

Set `registration_enabled` to `false` and leave `registration_url` empty. The sign up page is
gone, the link tells people so, and accounts are made with `/create` on the server console.

### The old PHP page

`Registration Page/` is still in the repository and still works, for anyone who would rather run
it on a webserver they already have. Copy the folder to your webserver, make `uploads/` writable
if you want avatars, and point `registration_url` at it. Open `config.php` and fill in the
database details:

```php
<?php
$dbhost = 'localhost';
$dbuser = 'root';
$dbpass = '';
$dbname = 'msn';
$encryption_key = 'CHANGE_ME_123456_123456_';
$encryption_iv = 'CHANGE_ME_123456';
?>
```

`$encryption_key` and `$encryption_iv` **must match** `database_password_encryption_key` and
`database_password_encryption_iv` in the server's `Messenger.config`, otherwise accounts created
through the web page cannot sign in. The server's own page has no such setting to get wrong,
because it uses the server's.

To use its avatar endpoints instead of the built-in ones:

```xml
<add key="avatars_enabled" value="true" />
<add key="avatars_address" value="http://your-server/uploads/" />
<add key="avatars_address_upload" value="http://your-server/upload.php" />
```

---

# Running the server in the background

### Linux (systemd)

`/etc/systemd/system/wlmserver.service`:

```ini
[Unit]
Description=Linkora server
After=network.target mysql.service

[Service]
Type=simple
User=wlm
WorkingDirectory=/opt/wlmserver
ExecStart=/usr/bin/dotnet /opt/wlmserver/WLMServer.dll
Restart=on-failure

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl enable --now wlmserver
journalctl -u wlmserver -f
```

The server detects that no console is attached and keeps running; the interactive commands are
unavailable in that mode, so create accounts with `/create` beforehand or via the web page.

### macOS (launchd)

`~/Library/LaunchAgents/net.linkora.server.plist`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key><string>net.linkora.server</string>
    <key>ProgramArguments</key>
    <array>
        <string>/usr/local/share/dotnet/dotnet</string>
        <string>/Users/you/wlmserver/WLMServer.dll</string>
    </array>
    <key>WorkingDirectory</key><string>/Users/you/wlmserver</string>
    <key>RunAtLoad</key><true/>
    <key>KeepAlive</key><true/>
</dict>
</plist>
```

The first path is the .NET runtime; `which dotnet` shows where yours is if it lives elsewhere
(Homebrew puts it in `/opt/homebrew/bin/dotnet`). The same goes for `/usr/bin/dotnet` in the
systemd unit above.

```bash
launchctl load ~/Library/LaunchAgents/net.linkora.server.plist
```

### Firewall

Allow inbound TCP on your `server_port` (1323 by default) if clients connect from other machines.

---

# Troubleshooting

**"Unable to connect to any of the specified MySQL hosts"**
The database is unreachable or the credentials are wrong. Check `database_host`, `database_id`
and `database_password`, and that MySQL accepts connections from the server's address.

**"Unable to reach the server" when signing in, although the sign up page works**
Signing up is a web page, but signing in is a raw TCP connection on `server_port` (1323), and
that has to get through on its own. Check that the port is open in every firewall in front of the
server, cloud provider's included. A domain behind Cloudflare's proxy (the orange cloud) only
passes web traffic, so give clients a DNS only name or the server's IP to sign in with.

**Sign in does nothing / client immediately drops the connection**
Almost always a mismatch between the server's `server_encryption_key` and the client's compiled
`SERVER_ENCRYPTION_KEY`. See "Matching the encryption key".

**"This version of Linkora is outdated"**
The client's `Client.version` and the server's `Server.version` strings differ. Both are `1.0.0`
by default.

**Avatars do not appear**
`avatars_enabled` must be `true` and `avatars_address` must be a URL the *client* can reach —
not `localhost` if the client runs on another machine.

**The client starts but the window is blank on Linux**
Usually a missing font package. Install `fontconfig` and a font family such as
`fonts-dejavu-core`.

**"No usable version of libssl was found" when starting the server on Linux**
Install OpenSSL (`libssl3` on Debian/Ubuntu, `openssl-libs` on Fedora). The MySQL connector
needs it to negotiate TLS with the database.

---

# Notes on the cross-platform port

The original was a .NET Framework 4.5 app built on WPF, Windows Forms and the discontinued
NetworkComms.Net library — none of which run outside Windows. The port keeps the original
layout, artwork and behaviour while replacing the Windows-only foundations:

* **UI** — WPF became [Avalonia](https://avaloniaui.net/). The XAML, styling and every control
  were carried across as-is. `WLMClient/Compat/` holds small stand-ins for the WPF pieces
  Avalonia has no equivalent of, most notably a `RichTextBox` with a flow document, caret,
  selection and inline images, which the chat window and contact list are built on.
* **Dialogs** — the three Windows Forms dialogs were rebuilt as Avalonia windows at the same
  pixel coordinates, fonts and colours as the designer files specified.
* **Networking** — NetworkComms.Net was replaced by `WLMNet`, which keeps the same API surface
  the application was written against (so the packet handlers are unchanged) over a simple
  length-prefixed TCP framing with AES-256-CBC encryption.
* **Serialisation** — `BinaryFormatter` is gone from .NET; packets now serialise through
  protobuf-net, which the packet classes were already annotated for.
* **Platform services** — idle detection for the automatic away status, the window attention
  request behind Nudge, and sound playback each have per-platform implementations in
  `WLMClient/Compat/Platform.cs`.
* **Database** — the MySQL connection string no longer requests `Keepalive`, which the connector
  implements with a Windows-only socket call that throws elsewhere.
