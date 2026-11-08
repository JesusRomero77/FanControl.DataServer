# What's New

> **WatchTower:** DataServer can be configured with [WatchTower](https://github.com/JesusRomero77/FanControl.WatchTower), a program that configures the whole family of plugins. Its DataServer tab edits the same `DataServerConfig.xml` that the plugin reads.

## 2.0

- Version 2.0 is not compatible with 1.0 clients: `/profile` and `/readings` now require a password, so any program or page written for 1.0 (including the 1.0 `DataServer-client.html`) will receive 401 responses until it is updated. Plugins that read the data, such as FanControl.RemoteSensors, must also be version 2.0.
- Clients now prove they know the password with a signed challenge (HMAC-SHA256), so the password itself is never sent over the network. The new `/challenge` address returns a one-time number that the client signs; each number works for a single request and expires after 10 seconds.
- The port and the password are now configurable in `DataServerConfig.xml`, in `%LocalAppData%\FanControl`. The plugin creates it the first time it starts, with the default port (8765) and the default password (`Password1234`, which is public, so change it). The plugin only reads the file when it starts, so restart the FanControl interface after changing it.
- The server now limits how many clients and connections can use it at the same time (10 requests being served and 5 different client addresses), so a badly written client cannot overload FanControl. Anything above that receives a 429 response.
- `DataServer-client.html` now asks for the password and signs its requests.

## 1.0

- Initial release.
