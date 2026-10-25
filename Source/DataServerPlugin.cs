using FanControl.Plugins;
using System;
using System.Threading;

namespace FanControl.DataServer
{
    public class DataServerPlugin : IPlugin2
    {
        public string Name => "Data Server";
        private static bool _isInitializing = false;
        private static bool _isInitialized = false;
        private static readonly object _initLock = new object();
        private readonly HttpDataServer _server = new HttpDataServer();

        public DataServerPlugin(IPluginLogger logger)
        {
            ErrorLogger.Initialize(logger);
        }
        public void Initialize()
        {
        }
        public void Load(IPluginSensorsContainer container)
        {
            // 1. Crear el sensor para que FanControl acepte el plugin.
            container.TempSensors.Add(new ServerSensor());

            // 2. Esperar a que FanControl y RPC terminen de arrancar; después arrancar el servidor
            //    HTTP y preparar los datos del perfil, todo en segundo plano.
            Thread thread = new Thread(() => { Thread.Sleep(2000); StartServer(); InitialStartup(); }) { IsBackground = true };
            thread.Start();
        }

        // Arranca el servidor HTTP. Si falla, el plugin sigue vivo: solo se anota el error.
        private void StartServer()
        {
            try
            {
                _server.Start(ServerSettings.Port);
            }
            catch (Exception ex)
            {
                ErrorLogger.Write(ErrorLogger.Severity.Error, ex);
            }
        }

        // Prepara los sensores y controles del perfil activo y publica el perfil para el servidor.
        private void InitialStartup()
        {
            // Evita que dos InitialStartup se ejecuten a la vez y se pisen las listas estáticas.
            lock (_initLock)
            {
                if (_isInitializing) return;
                _isInitializing = true;
            }
            try
            {
                JSONConfigReader.SetJSONObjects();
                RPCReader.SetRPCObjects();
                ConfigMatcher.SetMatchObjects();
                RPCReader.SetMatchedObjects();

                SnapshotStore.PublishProfile(JSONConfigReader.GetFileName(), RPCReader.GetSensors(), RPCReader.GetControls());

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                ErrorLogger.Write(ErrorLogger.Severity.Error, ex);
            }
            finally
            {
                _isInitializing = false;
            }
        }

        public void Update()
        {
            // Mientras InitialStartup repuebla las listas (tras un cambio de perfil), no las tocamos.
            if (_isInitializing) return;
            if (!_isInitialized) return;
            try
            {
                // Se consulta una sola vez por Update, para que las dos comparaciones usen la misma lectura.
                var currentConfig = JSONConfigReader.GetCurrentConfigInfo();

                if (currentConfig.FileName != JSONConfigReader.GetFileName() ||
                    currentConfig.FilePath != JSONConfigReader.GetFilePath())
                {
                    Thread thread = new Thread(InitialStartup) { IsBackground = true };
                    thread.Start();
                    return;
                }

                // Lee todos los valores del RPC y publica una foto nueva para el servidor.
                RPCReader.UpdateValues();
                SnapshotStore.PublishReadings(RPCReader.GetSensors(), RPCReader.GetControls());
            }
            catch (Exception ex)
            {
                ErrorLogger.Write(ErrorLogger.Severity.Error, ex);
            }
        }
        public void Close()
        {
            _server.Stop();
        }
    }
}