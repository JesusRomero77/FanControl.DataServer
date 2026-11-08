using System;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace FanControl.DataServer
{
    // Ajustes del servidor, leídos de DataServerConfig.xml (en %LocalAppData%\FanControl), el mismo
    // archivo que escribe la pestaña DataServer de WatchTower. Si no existe se crea con los valores
    // por defecto. Ante cualquier error se mantienen los valores por defecto, para que el plugin siga vivo.
    internal static class ServerSettings
    {
        private const string RootElementName = "ServerSettings";
        private const string PortElementName = "Port";
        private const string PasswordElementName = "Password";

        private const int DefaultPort = 8765;
        private const int MinPort = 1;
        private const int MaxPort = 65535;
        private const string DefaultPassword = "Password1234";

        // Puerto en el que escucha el servidor HTTP.
        public static int Port { get; private set; } = DefaultPort;

        // Contraseña con la que los clientes firman el desafío.
        public static string Password { get; private set; } = DefaultPassword;

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FanControl", "DataServerConfig.xml");

        // Lee el XML. Puerto vacío o inválido = 8765; contraseña vacía = la de por defecto.
        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    CreateDefaultFile();
                    return;
                }

                XElement root = XDocument.Load(FilePath).Root;
                if (root == null || root.Name != RootElementName)
                {
                    ErrorLogger.Write(ErrorLogger.Severity.Warning,
                        "DataServerConfig.xml does not have the expected format; using the default settings.");
                    return;
                }

                Port = ParsePort(((string)root.Element(PortElementName) ?? "").Trim());

                string password = ((string)root.Element(PasswordElementName) ?? "").Trim();
                Password = password.Length > 0 ? password : DefaultPassword;
            }
            catch (Exception ex)
            {
                ErrorLogger.Write(ErrorLogger.Severity.Error, ex);
            }
        }

        // Convierte el texto del XML en puerto. Vacío es válido (= por defecto); un valor
        // inválido se avisa en el log y también usa el de por defecto.
        private static int ParsePort(string text)
        {
            if (text.Length == 0) return DefaultPort;

            int port;
            if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out port) && port >= MinPort && port <= MaxPort)
                return port;

            ErrorLogger.Write(ErrorLogger.Severity.Warning,
                "Invalid port '" + text + "' in DataServerConfig.xml; using " + DefaultPort + ".");
            return DefaultPort;
        }

        // Crea el XML con la misma estructura y comentarios que crea WatchTower, ya con la
        // contraseña por defecto.
        private static void CreateDefaultFile()
        {
            XDocument document = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(RootElementName,
                    new XComment(" Port the server listens on (1-65535). Leave empty to use 8765. "),
                    new XElement(PortElementName, ""),
                    new XComment(" Password clients must send in the Authorization header. "),
                    new XElement(PasswordElementName, DefaultPassword)));

            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            document.Save(FilePath);
        }
    }
}