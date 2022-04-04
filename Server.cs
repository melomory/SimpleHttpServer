using System;
using System.Runtime.Loader;
using NLog;
using System.Net;
using System.Text;
using Serializer;

namespace HttpServer
{
    internal class Server
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private static HttpListener? _listener = null;

        static async Task Main(string[] args)
        {
            AssemblyLoadContext.Default.Unloading += SigTermEventHandler;
            Console.CancelKeyPress += CancelHandler;

            ConfigureLogger();

            _listener = GetConfiguredListener();

            _listener.Start();

            _logger.Info("Server is started");

            string data = string.Empty;
            string answerData = string.Empty;

            try
            {
                while (true)
                {
                    if (_listener is null)
                    {
                        break;
                    }
                    _logger.Info("Waiting for request...");

                    HttpListenerContext context = await _listener.GetContextAsync();

                    string request = GetRequest(context);

                    ExecuteRequest(context, request, ref data, ref answerData);
                }

            }
            catch (WebException e)
            {
                _logger.Error(e.Status);
            }
        }

        private static void ExecuteRequest(
            HttpListenerContext context,
            string request, ref string data, ref string answerData
        )
        {
            if (string.IsNullOrEmpty(request))
            {
                _logger.Info("Request method is not specified");
            }
            string  methodName = request.Substring(1);
            _logger.Info($"Method name is {methodName}");
            switch (methodName)
            {
                case "Ping":
                {
                    Ping(context);
                    Respond(context, "Server is OK");
                    _logger.Info($"Server was pinged");
                }
                    break;
                case "Stop":
                {
                    Respond(context, "Server was stopped");
                    Stop();
                }
                    break;
                case "PostInputData":
                {
                    PostInputData(context, ref data);
                    Respond(context, "Data was received");
                }
                    break;
                case "GetAnswer":
                    Respond(context, GetAnswer(ref data));
                    break;

                case "GetInputData":
                    Respond(context, GetInputData(ref data));
                    break;
                case "WriteAnswer":
                {
                    PostInputData(context, ref answerData);
                    Respond(context, "Data was received");
                }
                    break;
                default:
                    Respond(context, "Wrong method");
                    break;
            }
        }

        private static string GetInputData(ref string data)
        {
            _logger.Info($"Sending input data: {data}");
            return data;
        }

        private static string GetAnswer(ref string data)
        {
            _logger.Info($"Parsing data: {data}");
            Input inpt = Serializer.Serializer.DeserializeInputObject("json", data);
            Output oupt = Serializer.Serializer.GetOutputObject(inpt);
            return Serializer.Serializer.SerializeOutput(oupt, "json");
        }

        private static void PostInputData(HttpListenerContext context, ref string data)
        {
            if (context is null)
            {
                _logger.Trace($"Context is null");
                throw new ArgumentNullException(nameof(context));
            }

            Stream inputStream = context.Request.InputStream;
            Encoding encoding = context.Request.ContentEncoding;

            using (StreamReader reader = new StreamReader(inputStream, encoding))
            {
                data = reader.ReadToEnd();
            }
            _logger.Info($"Received data: {data}");
        }

        private static void Stop()
        {
            _listener?.Stop();
            _listener?.Close();
            _listener = null;
        }

        private static void Ping(HttpListenerContext context)
        {
            if (context is null)
            {
                _logger.Trace($"Context is null");
                throw new ArgumentNullException(nameof(context));
            }
            context.Response.StatusCode = (int)HttpStatusCode.OK;
        }

        private static void Respond(HttpListenerContext context, string response = "")
        {
            if (context is null)
            {
                _logger.Trace($"Context is null");
                throw new ArgumentNullException(nameof(context));
            }

            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.ContentLength64 = Encoding.UTF8.GetByteCount(response);

            using (Stream stream = context.Response.OutputStream)
            {
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    writer.Write(response);
                }
            }
        }

        private static string GetRequest(HttpListenerContext context)
        {
            if (context is null)
            {
                _logger.Trace($"Context is null");
                throw new ArgumentNullException(nameof(context));
            }

            return context.Request.RawUrl ?? string.Empty;
        }

        private static HttpListener GetConfiguredListener()
        {
            int port = GetPort();
            _logger.Info($"Port is set to {port}");

            string serverAddress = "http://127.0.0.1";

            string prefix = $"{serverAddress}:{port}/";

            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);

            return listener;
        }

        private static int GetPort()
        {
            System.Console.WriteLine("Enter a port (0..65536): ");

            int port = 0;
            while (!int.TryParse(Console.ReadLine(), out port))
            {
                System.Console.WriteLine("Wrong format. Try again. It should be 0..65536)");
            }
            if (port < 0 || port > 65535)
            {
                GetPort();
            }

            return port;
        }

        private static void ConfigureLogger()
        {
            var config = new NLog.Config.LoggingConfiguration();
            var logconsole = new NLog.Targets.ConsoleTarget("logconsole");
            config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);
            NLog.LogManager.Configuration = config;
        }

        private static void SigTermEventHandler(AssemblyLoadContext obj)
        {
            Stop();
            _logger.Info("Server was stopped");
        }

        private static void CancelHandler(object? sender, ConsoleCancelEventArgs e)
        {
            Stop();
            _logger.Info("Server was stopped");
        }
    }
}
