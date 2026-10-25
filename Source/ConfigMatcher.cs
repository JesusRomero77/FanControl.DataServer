using System.Collections.Generic;

namespace FanControl.DataServer
{
    public static class ConfigMatcher
    {
        private static readonly List<Sensor> _matchedSensors = new List<Sensor>();
        private static readonly List<Control> _matchedControls = new List<Control>();

        public static void SetMatchObjects()
        {
            SetMatchSensors();
            SetMatchControls();
        }

        // Empareja cada sensor del JSON con su sensor del RPC (mismo Name e Identifier) y, por cada
        // pareja encontrada, crea un Sensor con los datos del JSON.
        private static void SetMatchSensors()
        {
            _matchedSensors.Clear();
            List<JSONConfigReader.JsonSensor> jsonSensors = JSONConfigReader.GetJSONSensors();
            List<RPCReader.RPCSensor> rpcSensors = RPCReader.GetRPCSensors();

            foreach (JSONConfigReader.JsonSensor jsonSensor in jsonSensors)
            {
                if (string.IsNullOrWhiteSpace(jsonSensor.Name) || string.IsNullOrWhiteSpace(jsonSensor.Identifier)) continue;
                foreach (RPCReader.RPCSensor rpcSensor in rpcSensors)
                {
                    if (string.IsNullOrWhiteSpace(rpcSensor.Name) || string.IsNullOrWhiteSpace(rpcSensor.Identifier)) continue;
                    if (jsonSensor.Name == rpcSensor.Name && jsonSensor.Identifier == rpcSensor.Identifier)
                    {
                        _matchedSensors.Add(new Sensor(jsonSensor.Identifier, jsonSensor.Name, jsonSensor.DataSource));
                        break;
                    }
                }
            }
        }

        // Empareja cada control del JSON con su control del RPC (mismo Identifier) y, por cada
        // pareja encontrada, crea un Control con los datos del JSON (el Name es el NickName).
        private static void SetMatchControls()
        {
            _matchedControls.Clear();
            List<JSONConfigReader.JsonControl> jsonControls = JSONConfigReader.GetJSONControls();
            List<RPCReader.RPCControl> rpcControls = RPCReader.GetRPCControls();
            foreach (JSONConfigReader.JsonControl jsonControl in jsonControls)
                foreach (RPCReader.RPCControl rpcControl in rpcControls)
                    if (jsonControl.Identifier == rpcControl.Identifier)
                    {
                        _matchedControls.Add(new Control(jsonControl.Identifier, jsonControl.Name, jsonControl.DataSource));
                        break;
                    }
        }

        public static List<Sensor> GetMatchedSensors()
        {
            return _matchedSensors;
        }
        public static List<Control> GetMatchedControls()
        {
            return _matchedControls;
        }
    }
}