using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class DebugServer
    {
        private const int RequestTimeoutMs = 60000;

        internal sealed class Request
        {
            public string Command;
            public Dictionary<string, string> Args;
            public string Response;
            public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);

            public void Reply(object value)
            {
                Response = Json.Write(value);
                Done.Set();
            }

            public void Fail(string error)
            {
                Reply(new Dictionary<string, object> { ["ok"] = false, ["error"] = error });
            }

            public string Arg(string name, string fallback = null)
            {
                return Args != null && Args.TryGetValue(name, out string value) ? value : fallback;
            }

            public float Float(string name, float fallback)
            {
                return float.TryParse(Arg(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value)
                    ? value
                    : fallback;
            }
        }

        private static readonly ConcurrentQueue<Request> _queue = new ConcurrentQueue<Request>();
        private static TcpListener _listener;
        private static Thread _thread;
        private static volatile bool _running;

        public static MonoBehaviour Host { get; private set; }

        public static void Start(MonoBehaviour host)
        {
            if (!ModConfig.DebugServer.Value) return;
            Host = host;
            try
            {
                _listener = new TcpListener(IPAddress.Loopback, ModConfig.DebugPort.Value);
                _listener.Start();
            }
            catch (Exception e)
            {
                ModLog.Warning("Debug server not started: " + e.Message);
                _listener = null;
                return;
            }
            Application.runInBackground = true;
            _running = true;
            _thread = new Thread(Listen) { IsBackground = true, Name = "PlayableVehicles debug server" };
            _thread.Start();
            ModLog.Info("Debug server listening on 127.0.0.1:" + ModConfig.DebugPort.Value);
        }

        public static void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
            _listener = null;
        }

        public static void Tick()
        {
            while (_queue.TryDequeue(out Request request))
            {
                try
                {
                    DebugCommands.Execute(request);
                }
                catch (Exception e)
                {
                    request.Fail(e.GetType().Name + ": " + e.Message);
                }
            }
        }

        private static void Listen()
        {
            while (_running)
            {
                TcpClient client;
                try
                {
                    client = _listener.AcceptTcpClient();
                }
                catch
                {
                    if (!_running) return;
                    continue;
                }
                try
                {
                    Handle(client);
                }
                catch (Exception e)
                {
                    ModLog.Warning("Debug server request failed: " + e.Message);
                }
                finally
                {
                    client.Close();
                }
            }
        }

        private const string MarkerHeader = "X-PV-Debug";
        private const int MaxHeaderLines = 50;
        private const int MaxHeaderLength = 4096;

        private static void Handle(TcpClient client)
        {
            client.ReceiveTimeout = 5000;
            NetworkStream stream = client.GetStream();
            var reader = new StreamReader(stream, Encoding.ASCII);
            string line = reader.ReadLine();
            if (string.IsNullOrEmpty(line)) return;
            bool marked = false;
            bool localHost = false;
            for (int i = 0; i < MaxHeaderLines; i++)
            {
                string headerLine = reader.ReadLine();
                if (string.IsNullOrEmpty(headerLine) || headerLine.Length > MaxHeaderLength) break;
                int colon = headerLine.IndexOf(':');
                if (colon <= 0) continue;
                string name = headerLine.Substring(0, colon).Trim();
                string value = headerLine.Substring(colon + 1).Trim();
                if (name.Equals(MarkerHeader, StringComparison.OrdinalIgnoreCase)) marked = true;
                else if (name.Equals("Host", StringComparison.OrdinalIgnoreCase))
                {
                    string hostName = value.Split(':')[0];
                    localHost = hostName == "127.0.0.1" || hostName.Equals("localhost", StringComparison.OrdinalIgnoreCase);
                }
            }
            if (!marked || !localHost)
            {
                byte[] denied = Encoding.ASCII.GetBytes("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                stream.Write(denied, 0, denied.Length);
                return;
            }

            string[] parts = line.Split(' ');
            string target = parts.Length > 1 ? parts[1] : "/";
            var request = new Request { Args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) };
            int query = target.IndexOf('?');
            request.Command = Uri.UnescapeDataString((query >= 0 ? target.Substring(0, query) : target).Trim('/')).ToLowerInvariant();
            if (query >= 0)
            {
                foreach (string pair in target.Substring(query + 1).Split('&'))
                {
                    if (pair.Length == 0) continue;
                    int eq = pair.IndexOf('=');
                    string key = Uri.UnescapeDataString(eq >= 0 ? pair.Substring(0, eq) : pair);
                    string value = eq >= 0 ? Uri.UnescapeDataString(pair.Substring(eq + 1).Replace('+', ' ')) : "";
                    request.Args[key] = value;
                }
            }

            _queue.Enqueue(request);
            string body = request.Done.Wait(RequestTimeoutMs)
                ? request.Response
                : "{\"ok\":false,\"error\":\"timeout: the game did not process the command (loading, frozen or minimized?)\"}";
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: "
                + bytes.Length + "\r\nConnection: close\r\n\r\n");
            stream.Write(header, 0, header.Length);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }
    }
}
