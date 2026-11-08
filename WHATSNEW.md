## FanControl.DataServer 2.0

**Breaking change:** `/profile` and `/readings` now require a password. Any program or page written for 1.0 (including the old `DataServer-client.html`) will receive 401 responses until it is updated. Use the `DataServer-client.html` from this release, or add the challenge and signature steps described in the README. Plugins that read the data, such as FanControl.RemoteSensors, must also be version 2.0.

### What's new

- **Password protection.** Clients prove they know the password with a signed challenge (HMAC-SHA256), so the password itself is never sent over the network. New `/challenge` endpoint.
- **Configurable port and password** through `%LocalAppData%\FanControl\DataServerConfig.xml`, which you can edit by hand or with the DataServer tab of WatchTower. Defaults: port 8765 and password `Password1234`. That default password is public, so change it. Restart the FanControl interface after changing the file.
- **Connection limits** to protect FanControl from badly written clients: at most 10 requests served at the same time and 5 different client addresses. Anything above that receives a 429 response.
- **Updated example client** (`DataServer-client.html`) with password support.

### Installation

Download `FanControl.DataServer.dll` and install it with FanControl's plugin installer, or copy it to the `Plugins` folder and restart the FanControl interface. See the README for details.
