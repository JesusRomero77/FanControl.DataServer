using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FanControl.DataServer
{
    // Servidor HTTP de solo lectura: responde con JSON a GET /challenge, /profile y /readings.
    // /profile y /readings exigen la cabecera Authorization con la firma HMAC del desafío.
    // Solo lee las fotos de SnapshotStore; nunca toca el RPC ni FanControl.
    internal sealed class HttpDataServer
    {
        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            DateTimeZoneHandling = DateTimeZoneHandling.Utc
        };

        private HttpListener _listener;

        // Intenta escuchar en toda la red; si Windows no lo permite (falta de permisos),
        // escucha solo en este equipo y lo avisa en el log de FanControl.
        public void Start(int port)
        {
            _listener = TryCreateListener("http://+:" + port + "/");
            if (_listener == null)
            {
                ErrorLogger.Write(ErrorLogger.Severity.Warning,
                    "Could not listen on the network (missing permissions); listening on localhost only.");
                _listener = TryCreateListener("http://localhost:" + port + "/");
            }
            if (_listener == null)
            {
                ErrorLogger.Write(ErrorLogger.Severity.Error, "Could not start the HTTP server on port " + port + ".");
                return;
            }

            Thread thread = new Thread(ListenLoop) { IsBackground = true };
            thread.Start();
        }

        public void Stop()
        {
            try
            {
                _listener?.Close();
            }
            catch (Exception ex)
            {
                ErrorLogger.Write(ErrorLogger.Severity.Warning, ex);
            }
        }

        // Crea y arranca un listener con ese prefijo; devuelve null si Windows lo rechaza.
        private static HttpListener TryCreateListener(string prefix)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            try
            {
                listener.Start();
                return listener;
            }
            catch (HttpListenerException)
            {
                listener.Close();
                return null;
            }
        }

        // Espera peticiones y cada una se atiende en un hilo del grupo, para que un cliente lento
        // no bloquee a los demás. Termina cuando se cierra el listener.
        private void ListenLoop()
        {
            while (_listener.IsListening)
            {
                try
                {
                    HttpListenerContext context = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    ErrorLogger.Write(ErrorLogger.Severity.Error, ex);
                    Thread.Sleep(100);
                }
            }
        }

        // Punto de entrada de cada petición: primero pasa por el limitador de clientes y conexiones
        // (429 si se supera) y después se procesa. El hueco del limitador se libera siempre al terminar.
        private static void HandleRequest(HttpListenerContext context)
        {
            bool admitted = false;
            try
            {
                bool asXml = string.Equals(context.Request.QueryString["format"], "xml", StringComparison.OrdinalIgnoreCase);
                string clientAddress = context.Request.RemoteEndPoint?.Address.ToString() ?? "";

                admitted = ConnectionLimiter.TryEnter(clientAddress);
                if (!admitted)
                {
                    Respond(context.Response, 429, ErrorBody("Too many clients or connections", asXml), asXml);
                    return;
                }

                ProcessRequest(context, asXml);
            }
            catch (Exception ex)
            {
                ErrorLogger.Write(ErrorLogger.Severity.Error, ex);
                try { context.Response.Abort(); } catch { }
            }
            finally
            {
                if (admitted) ConnectionLimiter.Leave();
            }
        }

        // Atiende una petición admitida. OPTIONS es la consulta previa de los navegadores (CORS);
        // el resto solo GET. /challenge es libre; /profile y /readings exigen firma válida.
        // El formato es JSON salvo que se pida XML con ?format=xml.
        private static void ProcessRequest(HttpListenerContext context, bool asXml)
        {
            HttpListenerResponse response = context.Response;

            if (context.Request.HttpMethod == "OPTIONS")
            {
                RespondPreflight(response);
                return;
            }

            if (context.Request.HttpMethod != "GET")
            {
                Respond(response, 405, ErrorBody("Method not allowed", asXml), asXml);
                return;
            }

            string path = context.Request.Url.AbsolutePath.TrimEnd('/').ToLowerInvariant();

            if (path == "/challenge")
            {
                RespondChallenge(response, asXml);
                return;
            }

            if (path != "/profile" && path != "/readings")
            {
                Respond(response, 404, ErrorBody("Not found", asXml), asXml);
                return;
            }

            if (!HmacAuth.IsAuthorized(context.Request.Headers["Authorization"], ServerSettings.Password))
            {
                Respond(response, 401, ErrorBody("Unauthorized", asXml), asXml);
                return;
            }

            object body;
            if (path == "/profile") body = SnapshotStore.Profile;
            else body = SnapshotStore.Readings;

            // Aún no hay datos (el plugin está arrancando o cargando un perfil).
            if (body == null)
            {
                Respond(response, 503, ErrorBody("Data not ready yet", asXml), asXml);
                return;
            }

            Respond(response, 200, SerializeBody(body, asXml), asXml);
        }

        // Entrega un número de un solo uso, o 429 si ya hay demasiados pendientes de usar.
        private static void RespondChallenge(HttpListenerResponse response, bool asXml)
        {
            string nonce = HmacAuth.CreateNonce();
            if (nonce == null)
            {
                Respond(response, 429, ErrorBody("Too many pending challenges", asXml), asXml);
                return;
            }

            Respond(response, 200, SerializeBody(new ChallengeInfo { Nonce = nonce }, asXml), asXml);
        }

        // Convierte los datos a JSON o a XML según el formato pedido.
        private static string SerializeBody(object body, bool asXml)
        {
            return asXml ? SerializeXml(body) : JsonConvert.SerializeObject(body, _jsonSettings);
        }

        // Serializa a XML en UTF-8, para que la cabecera del XML coincida con los bytes que se envían.
        private static string SerializeXml(object body)
        {
            // Espacios de nombres vacíos: evita que aparezcan xmlns:xsd y xmlns:xsi en la etiqueta raíz.
            XmlSerializerNamespaces namespaces = new XmlSerializerNamespaces();
            namespaces.Add(string.Empty, string.Empty);

            XmlWriterSettings settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true };
            using (MemoryStream stream = new MemoryStream())
            {
                using (XmlWriter writer = XmlWriter.Create(stream, settings))
                    new XmlSerializer(body.GetType()).Serialize(writer, body, namespaces);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        // Cuerpo de las respuestas de error, en el mismo formato que se pidió.
        private static string ErrorBody(string message, bool asXml)
        {
            if (asXml)
                return "<Error>" + System.Security.SecurityElement.Escape(message) + "</Error>";
            return JsonConvert.SerializeObject(new { error = message });
        }

        // Envía la respuesta y cierra la conexión.
        private static void Respond(HttpListenerResponse response, int statusCode, string text, bool asXml)
        {
            byte[] data = Encoding.UTF8.GetBytes(text);
            response.StatusCode = statusCode;
            response.ContentType = asXml ? "application/xml; charset=utf-8" : "application/json; charset=utf-8";
            response.Headers["Cache-Control"] = "no-store";
            response.Headers["Access-Control-Allow-Origin"] = "*";
            response.ContentLength64 = data.Length;
            response.OutputStream.Write(data, 0, data.Length);
            response.Close();
        }

        // Respuesta a la consulta previa (OPTIONS) que hace el navegador antes de enviar la
        // cabecera Authorization desde una página de otro origen. Sin cuerpo.
        private static void RespondPreflight(HttpListenerResponse response)
        {
            response.StatusCode = 204;
            response.Headers["Access-Control-Allow-Origin"] = "*";
            response.Headers["Access-Control-Allow-Methods"] = "GET, OPTIONS";
            response.Headers["Access-Control-Allow-Headers"] = "Authorization";
            response.Headers["Access-Control-Max-Age"] = "600";
            response.ContentLength64 = 0;
            response.Close();
        }
    }
}