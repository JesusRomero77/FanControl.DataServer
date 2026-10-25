# FanControl.DataServer

A [FanControl](https://github.com/Rem0o/FanControl.Releases) plugin that serves the sensors and fan controls of the active FanControl profile over HTTP (JSON/XML). A program or an HTML page can collect this data to log or graph it. A separate example HTML client is included.

The server is read-only: it never sends commands to FanControl or changes any fan speed.

## What it does

- Serves the active profile and the latest readings of its sensors and controls, refreshed once per second.
- Data is always sent, whether the values changed or not.
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

## How to get the data

The server listens on port 8765 and only answers read requests (GET).

- `/profile` returns the active profile: its name and the list of sensors and controls it contains.
- `/readings` returns the latest value of every sensor and control of that profile, plus a reading counter and the time of the reading.

Both addresses answer in JSON by default. Add `?format=xml` to either of them to get XML instead.

Things a client should know:

- The reading counter (`sequence`) increases with every new reading. If it stops increasing, the data is stale. It starts again from zero each time the plugin starts, so a lower number means the plugin was restarted.
- Readings carry a profile number (`profileId`). If it differs from the one in `/profile`, the profile has changed and the client should request `/profile` again.
- Values are rounded to one decimal. Sensors use the unit FanControl reports (normally °C) and controls are in percent. A value is empty (null) if FanControl did not report that element in the last reading.
- The time of each reading is UTC, formatted as day-month-year hours:minutes:seconds.

## Access from other computers

The plugin tries to listen on all network interfaces. If Windows does not allow it, the plugin falls back to listening on the same computer only, and writes a warning to FanControl's error log.

To allow access from other computers:

- Give your Windows user permission to listen on the network for that port (with the `netsh http add urlacl` command in an administrator console), or run FanControl as administrator.
- Allow TCP port 8765 in Windows Firewall.
- Restart the FanControl interface.

Then open the computer's address with the port (for example, the IP address followed by `:8765/readings`) from another computer.

## Example client

`DataServer-client.html` is a single file with no dependencies. Open it in any browser, type the plugin address (the computer's IP address and port) and press Connect.

It draws one chart with all the sensors and another with all the controls, shows the latest value of each, and warns when the data stops updating. Click a name in the legend to hide or show its line. It detects profile changes and plugin restarts by itself.

## Security

There is no authentication. Anyone who can reach the port can read, without any access control, the temperatures and fan speeds of your sensors and controls (together with the names of your profile, sensors and controls).

The server also allows any web page opened in a browser on the same computer or network to read this data without you doing anything. This is what lets the example client work when opened from a file. It is a real but low-severity risk, because the only information exposed is temperatures and speeds of sensors and controls, and the server is read-only. Even so, never expose this port to the internet.

## Known limitations

- When the interface is closed, FanControl can take a long time to hand control back to its service, and a FanControl process may stay alive. While it lives, the server keeps answering with its last reading, which no longer updates. Clients should treat a reading counter that does not change as stale data.
- If two copies of FanControl are open at the same time, only one of them can use port 8765.
- Only the profile's custom sensors and its visible, enabled controls are served, not the physical sensors that feed the custom sensors.
- The port (8765) is fixed in the source code; changing it requires recompiling.

## Building from source

Open `Source/FanControl.DataServer.csproj` in Visual Studio with the .NET desktop development workload and the .NET Framework 4.8 targeting pack, with FanControl installed (the project looks for its DLLs in `C:\Program Files (x86)\FanControl\`; if it is installed elsewhere, change the path of each FanControl reference in `FanControl.DataServer.csproj`). Build in Release for x64.

## Related project

[FanControl.CurveReader](https://github.com/JesusRomero77/FanControl.CurveReader): a FanControl plugin that logs temperature and PWM changes to a file.

## Disclaimer

This project is not affiliated with FanControl or its author.

## License

This project is licensed under the [MIT License](LICENSE).

FanControl and the libraries it provides to plugins (such as Newtonsoft.Json) are not part of this repository and remain under their own licenses.

## Version

1.0
