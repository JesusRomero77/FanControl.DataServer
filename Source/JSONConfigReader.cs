using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;

namespace FanControl.DataServer
{
    public static class JSONConfigReader
    {
        // Última información leída del CACHE y fecha de modificación del archivo en ese momento.
        // Mientras la fecha no cambie, se devuelve la información guardada sin volver a leer el archivo.
        private static readonly object _cacheLock = new object();
        private static DateTime _cacheLastWriteTimeUtc = DateTime.MinValue;
        private static (string FileName, string FilePath) _cachedConfigInfo = (null, null);
        private static string _fileName;
        private static string _filePath;
        private static readonly List<JsonControl> _controls = new List<JsonControl>();
        private static readonly List<JsonSensor> _sensors = new List<JsonSensor>();

        public class JsonControl
        {
            public string Name { get; set; }
            public string Identifier { get; set; }
            public string DataSource { get; set; }
        }
        public class JsonSensor
        {
            public string Name { get; set; }
            public string Identifier { get; set; }
            public string DataSource { get; set; }
        }

        public static (string FileName, string FilePath) GetCurrentConfigInfo()
        {
            // Se bloquea porque lo llaman a la vez Update (hilo de FanControl) e InitialStartup (hilo propio),
            // y la fecha y la información guardadas deben cambiar siempre juntas.
            lock (_cacheLock)
            {
                try
                {
                    string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    string configurationsFolder = Path.Combine(baseDirectory, "Configurations");
                    string cachePath = Path.Combine(configurationsFolder, "CACHE");
                    if (!File.Exists(cachePath)) return (null, null);

                    // La fecha se lee ANTES del contenido: si FanControl modifica el archivo entre las dos
                    // operaciones, se guarda la fecha antigua y la siguiente llamada volverá a leerlo.
                    DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(cachePath);
                    if (lastWriteTimeUtc == _cacheLastWriteTimeUtc) return _cachedConfigInfo;

                    JObject cache = JObject.Parse(File.ReadAllText(cachePath));
                    string fileName = cache["CurrentConfigFileName"]?.ToString();
                    string customConfigFolder = cache["CustomConfigFolder"]?.ToString();
                    if (string.IsNullOrWhiteSpace(fileName)) return (null, null);
                    string configFolder = !string.IsNullOrWhiteSpace(customConfigFolder) ? customConfigFolder : configurationsFolder;
                    string filePath = Path.Combine(configFolder, fileName);

                    // Solo se guarda el resultado si la lectura ha ido bien, para que un fallo se reintente.
                    _cachedConfigInfo = (fileName, filePath);
                    _cacheLastWriteTimeUtc = lastWriteTimeUtc;
                    return _cachedConfigInfo;
                }
                catch (Exception ex)
                {
                    ErrorLogger.Write(ErrorLogger.Severity.Error, ex);
                    return (null, null);
                }
            }
        }

        // Lee el perfil activo y rellena las listas de controles y sensores personalizados.
        public static void SetJSONObjects()
        {
            try
            {
                var configInfo = GetCurrentConfigInfo();
                _fileName = configInfo.FileName;
                _filePath = configInfo.FilePath;
                _controls.Clear();
                _sensors.Clear();

                if (string.IsNullOrWhiteSpace(_fileName) || string.IsNullOrWhiteSpace(_filePath) || !File.Exists(_filePath)) return;

                JObject configuration = JObject.Parse(File.ReadAllText(_filePath));
                JObject fanControl = configuration["FanControl"] as JObject;
                if (fanControl == null) return;

                ReadControls(fanControl);
                ReadCustomSensors(fanControl);
            }
            catch (Exception ex)
            {
                ErrorLogger.Write(ErrorLogger.Severity.Error, ex);
            }
        }

        // Lee los controles visibles y habilitados del perfil.
        private static void ReadControls(JObject fanControl)
        {
            JArray controls = fanControl["Controls"] as JArray;
            if (controls == null) return;

            foreach (JToken token in controls)
            {
                JObject control = token as JObject;
                if (control == null) continue;
                if (control["IsHidden"]?.Value<bool?>() == true || control["Enable"]?.Value<bool?>() == false) continue;
                string name = control["NickName"]?.ToString();
                string identifier = control["Identifier"]?.ToString();
                string dataSource = control["SelectedFanCurve"]?["Name"]?.ToString();
                _controls.Add(new JsonControl { Name = name, Identifier = identifier, DataSource = dataSource });
            }
        }

        // Lee los sensores personalizados visibles del perfil.
        private static void ReadCustomSensors(JObject fanControl)
        {
            JArray customSensors = fanControl["CustomSensors"] as JArray;
            if (customSensors == null) return;

            foreach (JToken token in customSensors)
            {
                JObject sensor = token as JObject;
                if (sensor == null) continue;
                if (sensor["IsHidden"]?.Value<bool?>() == true) continue;
                string name = sensor["NickName"]?.ToString();
                string identifier = sensor["Identifier"]?.ToString();
                string dataSource = sensor["SelectedTempSource"]?["Identifier"]?.ToString();
                _sensors.Add(new JsonSensor { Name = name, Identifier = identifier, DataSource = dataSource });
            }
        }

        public static string GetFileName()
        {
            return _fileName;
        }
        public static string GetFilePath()
        {
            return _filePath;
        }
        public static List<JsonControl> GetJSONControls()
        {
            return _controls;
        }
        public static List<JsonSensor> GetJSONSensors()
        {
            return _sensors;
        }
    }
}