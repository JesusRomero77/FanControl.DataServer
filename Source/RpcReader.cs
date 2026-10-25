using FanControl.IPC;
using System;
using System.Collections.Generic;

namespace FanControl.DataServer
{
    public static class RPCReader
    {
        // Decimales con los que se redondean las lecturas (limpia además el ruido de float, ej. 45,2999992).
        private const int DecimalPlaces = 1;

        // Datos en bruto obtenidos por RPC; ConfigMatcher los usa para emparejar con el JSON.
        private static readonly List<RPCSensor> _rpcSensors = new List<RPCSensor>();
        private static readonly List<RPCControl> _rpcControls = new List<RPCControl>();
        private static readonly List<SensorMessage> _rpcData = new List<SensorMessage>();

        // Objetos que han hecho correspondencia con el perfil activo; son los que se sirven.
        private static readonly List<Sensor> _sensors = new List<Sensor>();
        private static readonly List<Control> _controls = new List<Control>();

        public class RPCSensor
        {
            public string Name { get; set; }
            public string Identifier { get; set; }
        }
        public class RPCControl
        {
            public string Name { get; set; }
            public string Identifier { get; set; }
        }

        private static void SetRPCData()
        {
            _rpcData.Clear();
            var client = IPCFactory.GetSensorClient();
            var reply = client.GetAllSensors(new GetAllSensorsRequest());
            foreach (SensorMessage sensor in reply.Sensors)
            {
                _rpcData.Add(sensor);
            }
        }
        private static void SetRPCSensors()
        {
            _rpcSensors.Clear();
            foreach (SensorMessage sensor in _rpcData)
            {
                if (string.IsNullOrWhiteSpace(sensor.Name) || string.IsNullOrWhiteSpace(sensor.Identifier)) continue;
                _rpcSensors.Add(new RPCSensor { Name = sensor.Name, Identifier = sensor.Identifier });
            }
        }
        private static void SetRPCControls()
        {
            _rpcControls.Clear();
            foreach (SensorMessage control in _rpcData)
            {
                if (control.Type != SensorMessageType.Control) continue;
                if (string.IsNullOrWhiteSpace(control.Name) || string.IsNullOrWhiteSpace(control.Identifier)) continue;
                _rpcControls.Add(new RPCControl { Name = control.Name, Identifier = control.Identifier });
            }
        }
        public static void SetRPCObjects()
        {
            SetRPCData();
            SetRPCSensors();
            SetRPCControls();
        }

        // Sustituye los objetos servidos por los que ha creado ConfigMatcher para el perfil activo.
        public static void SetMatchedObjects()
        {
            _sensors.Clear();
            _controls.Clear();
            _sensors.AddRange(ConfigMatcher.GetMatchedSensors());
            _controls.AddRange(ConfigMatcher.GetMatchedControls());
        }
        public static List<RPCSensor> GetRPCSensors()
        {
            return _rpcSensors;
        }
        public static List<RPCControl> GetRPCControls()
        {
            return _rpcControls;
        }
        public static List<Sensor> GetSensors()
        {
            return _sensors;
        }
        public static List<Control> GetControls()
        {
            return _controls;
        }

        // Lee los datos del RPC y actualiza el valor de todos los sensores y controles del perfil,
        // hayan cambiado o no.
        public static void UpdateValues()
        {
            SetRPCData();

            foreach (Sensor sensor in _sensors)
                sensor.Value = GetRPCValue(sensor.Identifier);

            foreach (Control control in _controls)
                control.Value = GetRPCValue(control.Identifier);
        }

        // Busca en los datos del RPC el elemento con ese Identifier y devuelve su valor redondeado.
        // Devuelve null si el RPC no lo incluye en esta lectura.
        private static double? GetRPCValue(string identifier)
        {
            foreach (SensorMessage rpcElement in _rpcData)
                if (rpcElement.Identifier == identifier)
                    return Math.Round(rpcElement.Value, DecimalPlaces);

            return null;
        }
    }
}