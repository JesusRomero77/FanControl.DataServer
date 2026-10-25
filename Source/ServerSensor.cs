using FanControl.Plugins;

namespace FanControl.DataServer
{
    // Sensor ficticio: FanControl solo acepta un plugin que añada al menos un sensor.
    internal class ServerSensor : IPluginSensor
    {
        public string Id => "DataServer";

        public string Name => "Data Server";

        public float? Value => 0;

        public void Update()
        {
        }
    }
}