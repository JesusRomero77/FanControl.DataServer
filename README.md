# FanControl.DataServer

A [FanControl](https://github.com/Rem0o/FanControl.Releases) plugin that serves the sensors and fan controls of the active FanControl profile over HTTP (JSON/XML), protected by a password. A program or an HTML page can collect this data to log or graph it. A separate example HTML client is included.

The server is read-only: it never sends commands to FanControl or changes any fan speed.

## What it does

- Serves the active profile and the latest readings of its sensors and controls, refreshed once per second.
- Data is always sent, whether the values changed or not.
- Requires a password: clients prove they know it with a signed challenge, so the password itself is never sent over the network.
- Limits how many clients and connections can use the server at the same time, so a badly written client cannot overload FanControl.
- The port and the password are configurable.
- Includes `DataServer-client.html`, a single-file web client that graphs everything live.

## Requirements

- FanControl installed (the .NET Framework 4.8 version).

## Operation

The Fan Control interface must be open for the plugin to serve up-to-date data.

When the Fan Control interface is closed, Fan Control continues operating through its background service and keeps controlling the fans according to the configured curves. In this state, the plugin does not receive the updates required to serve readings.

When the interface is opened, control is transferred from the background service to the interface and the plugin starts receiving updates and serving readings. Closing the interface returns control to the background service and the readings stop updating.

## Installation

### Using FanControl's plugin installer (recommended)

1. Download `FanControl.DataServer.dll` from the Releases page.
2. In FanControl, open the plugin installer and select the downloaded DLL.
3. Restart the FanControl interface.

### Manual installation

1. Download `FanControl.DataServer.dll` from the Releases page.
2. Copy it to the `Plugins` folder of your FanControl installation.
3. Restart the FanControl interface.

The plugin adds a dummy sensor called "Data Server" to FanControl, because FanControl requires every plugin to add at least one sensor. It always shows 0 and can be ignored.

## Configuration

The first time the plugin starts, it creates `%LocalAppData%\FanControl\DataServerConfig.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<ServerSettings>
  <!-- Port the server listens on (1-65535). Leave empty to use 8765. -->
  <Port></Port>
  <!-- Password clients must send in the Authorization header. -->
  <Password>Password1234</Password>
</ServerSettings>
```

- **Port:** if it is empty or not a valid number, the plugin uses 8765 (an invalid value also writes a warning to FanControl's error log).
- **Password:** if it is empty, the plugin uses `Password1234`. **That default password is public, so change it.** Use only visible ASCII characters, with no spaces.

You can edit the file by hand or with the DataServer tab of [WatchTower](https://github.com/JesusRomero77/FanControl.WatchTower). Restart the FanControl interface after changing it, because the plugin only reads the file when it starts.

## How to get the data

The server listens on the configured port (8765 by default) and only answers read requests (GET). Every request except `/challenge` must be authenticated (see [Authentication](#authentication)).

- `/challenge` returns a one-time number (`nonce`) that the client must sign. It does not need authentication.
- `/profile` returns the active profile: its name and the list of sensors and controls it contains.
- `/readings` returns the latest value of every sensor and control of that profile, plus a reading counter and the time of the reading.

All addresses answer in JSON by default. Add `?format=xml` to any of them to get XML instead.

Things a client should know:

- The reading counter (`sequence`) increases with every new reading. If it stops increasing, the data is stale. It starts again from zero each time the plugin starts, so a lower number means the plugin was restarted.
- Readings carry a profile number (`profileId`). If it differs from the one in `/profile`, the profile has changed and the client should request `/profile` again.
- Values are rounded to one decimal. Sensors use the unit FanControl reports (normally °C) and controls are in percent. A value is empty (null) if FanControl did not report that element in the last reading.
- The time of each reading is UTC, formatted as day-month-year hours:minutes:seconds.

## Authentication

Authentication uses a signed challenge, so the password is never sent. For every request to `/profile` or `/readings`:

1. Request `/challenge`. It answers `{"nonce":"..."}`.
2. Compute the HMAC-SHA256 of the nonce, using the password as the key (both as UTF-8 text), and write it in lowercase hexadecimal.
3. Send the request with the header `Authorization: HMAC <nonce>:<signature>`.

Each nonce works for a single request and expires after 10 seconds, so a client must ask for a new one every time. Up to 100 nonces can be waiting to be used.

Status codes:

| Code | Meaning |
|------|---------|
| 200 | Success. |
| 401 | Wrong password, or a nonce that was already used, expired or unknown. |
| 404 | Unknown address. |
| 405 | Method not allowed (only GET and OPTIONS are accepted). |
| 429 | Too many clients, connections or pending nonces. Try again in a moment. |
| 503 | The plugin has no data yet (it is starting or loading a profile). |

Example in PowerShell:

```powershell
$base = "http://localhost:8765"
$password = "Password1234"

$nonce = (Invoke-RestMethod "$base/challenge").nonce
$hmac = [System.Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($password))
$signature = -join ($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($nonce)) | ForEach-Object { $_.ToString("x2") })

Invoke-RestMethod "$base/readings" -Headers @{ Authorization = "HMAC ${nonce}:$signature" }
```

## Limits

To protect FanControl from badly written clients, the server accepts at most 10 requests being served at the same time and at most 5 different client addresses active within the last 30 seconds. Anything above that receives a 429 response.

## Access from other computers

The plugin tries to listen on all network interfaces. If Windows does not allow it, the plugin falls back to listening on the same computer only, and writes a warning to FanControl's error log.

To allow access from other computers:

- Give your Windows user permission to listen on the network for that port (with the `netsh http add urlacl` command in an administrator console), or run FanControl as administrator.
- Allow the TCP port (8765 by default) in Windows Firewall.
- Restart the FanControl interface.

Then connect to the computer's address with the port (for example, the IP address followed by `:8765`) from another computer, authenticating as described above.

## Example client

`DataServer-client.html` is a single file with no dependencies. Open it in any browser, type the plugin address (the computer's IP address and port) and the password, and press Connect.

It draws one chart with all the sensors and another with all the controls, shows the latest value of each, and warns when the data stops updating or when the password is wrong. Click a name in the legend to hide or show its line. It detects profile changes and plugin restarts by itself.

The password is not saved by the browser: you have to type it every time you open the page. The signature uses the browser's cryptography functions, which are only available when the page is opened from a file, from localhost or over https.

## Security

Access to `/profile` and `/readings` requires the password. The password is never sent over the network, and each signature is valid for a single request.

Keep in mind:

- The traffic is plain HTTP, not encrypted. Anyone who can capture it can read the temperatures and fan speeds (together with the names of your profile, sensors and controls) and could try to guess a weak password from a captured signature. Use a long, unique password.
- There is no lockout for wrong passwords: apart from the limits described above, a client can keep trying.
- The default password (`Password1234`) is public. Change it before opening the port to other computers.
- Use this only on networks you trust. Never expose this port to the internet.
- The server allows web pages from any origin to make requests to it (this is what lets the example client work when opened from a file). A page still needs the password to read any data. The server is read-only, and the only information it exposes is temperatures, speeds and names.

## Known limitations

- When the interface is closed, FanControl can take a long time to hand control back to its service, and a FanControl process may stay alive. While it lives, the server keeps answering with its last reading, which no longer updates. Clients should treat a reading counter that does not change as stale data.
- If two copies of FanControl are open at the same time, only one of them can use the port.
- Only the profile's custom sensors and its visible, enabled controls are served, not the physical sensors that feed the custom sensors.
- The server limits the requests it is serving, but connections that never send a complete request are handled by Windows and are not counted.

## Upgrading from 1.0

Version 2.0 is not compatible with 1.0 clients: `/profile` and `/readings` now require authentication, so any program or page written for 1.0 (including the 1.0 `DataServer-client.html`) will receive 401 responses until it is updated. Use the `DataServer-client.html` from this release, or add the challenge and signature steps described in [Authentication](#authentication). Plugins that read the data, such as [FanControl.RemoteSensors](https://github.com/JesusRomero77/FanControl.RemoteSensors), must also be version 2.0.

## Building from source

Open `Source/FanControl.DataServer.csproj` in Visual Studio with the .NET desktop development workload and the .NET Framework 4.8 targeting pack, with FanControl installed (the project looks for its DLLs in `C:\Program Files (x86)\FanControl\`; if it is installed elsewhere, change the path of each FanControl reference in `FanControl.DataServer.csproj`). Build in Release for x64.

## Related projects

- [FanControl.CurveReader](https://github.com/JesusRomero77/FanControl.CurveReader): a FanControl plugin that logs local temperature and PWM changes to a file.
- [FanControl.RemoteSensors](https://github.com/JesusRomero77/FanControl.RemoteSensors): a FanControl plugin that shows the sensors served by this plugin, from another computer, inside FanControl.
- [FanControl.WatchTower](https://github.com/JesusRomero77/FanControl.WatchTower): a program to configure these plugins and to record the data of a remote DataServer into a log.

## Disclaimer

This project is not affiliated with FanControl or its author.

## License

This project is licensed under the [MIT License](LICENSE).

FanControl and the libraries it provides to plugins (such as Newtonsoft.Json) are not part of this repository and remain under their own licenses.

## Version

2.0
