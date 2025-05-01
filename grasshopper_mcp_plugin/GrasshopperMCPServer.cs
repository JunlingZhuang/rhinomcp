using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using Rhino.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Text.Json;
using GrasshopperMCP.Functions;
using Rhino.DocObjects;
using JsonException = Newtonsoft.Json.JsonException;

namespace GrasshopperMCP
{
    public class GrasshopperMCPServer
    {
        private string host;
        private int port;
        private bool running;
        private TcpListener listener;
        private Thread serverThread;
        private readonly object lockObject = new object();
        private GrasshopperMCPFunctions handler;

        private ObservableCollection<string> logs;

        public GrasshopperMCPServer(ObservableCollection<string> logs, string host = "127.0.0.1", int port = 2000)
        {
            this.host = host;
            this.port = port;
            this.running = false;
            this.listener = null;
            this.serverThread = null;
            this.handler = new GrasshopperMCPFunctions();
            this.logs = logs;
        }


        public void Start()
        {
            lock (lockObject)
            {
                if (running)
                {
                    logs.Add("Server is already running");
                    return;
                }

                running = true;
            }

            try
            {
                // Create TCP listener
                IPAddress ipAddress = IPAddress.Parse(host);
                listener = new TcpListener(ipAddress, port);
                listener.Start();

                // Start server thread
                serverThread = new Thread(ServerLoop);
                serverThread.IsBackground = true;
                serverThread.Start();

                logs.Add($"GrasshopperMCP server started on {host}:{port}");
            }
            catch (Exception e)
            {
                logs.Add($"Failed to start server: {e.Message}");
                Stop();
            }
        }

        public void Stop()
        {
            lock (lockObject)
            {
                running = false;
            }

            // Close listener
            if (listener != null)
            {
                try
                {
                    listener.Stop();
                }
                catch
                {
                    // Ignore errors on closing
                }
                listener = null;
            }

            // Wait for thread to finish
            if (serverThread != null && serverThread.IsAlive)
            {
                try
                {
                    serverThread.Join(1000); // Wait up to 1 second
                }
                catch
                {
                    // Ignore errors on join
                }
                serverThread = null;
            }

            logs.Add("GrasshopperMCP server stopped");
        }

        private void ServerLoop()
        {
            logs.Add("Server thread started");

            while (IsRunning())
            {
                try
                {
                    // Set a timeout to check running condition periodically
                    listener.Server.ReceiveTimeout = 1000;
                    listener.Server.SendTimeout = 1000;

                    // Wait for client connection
                    if (listener.Pending())
                    {
                        TcpClient client = listener.AcceptTcpClient();
                        logs.Add($"Connected to client: {client.Client.RemoteEndPoint}");

                        // Handle client in a separate thread
                        Thread clientThread = new Thread(() => HandleClient(client));
                        clientThread.IsBackground = true;
                        clientThread.Start();
                    }
                    else
                    {
                        // No pending connections, sleep a bit to prevent CPU overuse
                        Thread.Sleep(100);
                    }
                }
                catch (Exception e)
                {
                    logs.Add($"Error in server loop: {e.Message}");

                    if (!IsRunning())
                        break;

                    Thread.Sleep(500);
                }
            }

            logs.Add("Server thread stopped");
        }

        private bool IsRunning()
        {
            lock (lockObject)
            {
                return running;
            }
        }

        private void HandleClient(TcpClient client)
        {
            logs.Add("Client handler started");

            byte[] buffer = new byte[8192];
            string incompleteData = string.Empty;

            try
            {
                NetworkStream stream = client.GetStream();

                while (IsRunning())
                {
                    try
                    {
                        // Check if there's data available to read
                        if (client.Available > 0 || stream.DataAvailable)
                        {
                            int bytesRead = stream.Read(buffer, 0, buffer.Length);
                            if (bytesRead == 0)
                            {
                                logs.Add("Client disconnected");
                                break;
                            }

                            string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            incompleteData += data;

                            try
                            {
                                // Try to parse as JSON
                                JObject command = JObject.Parse(incompleteData);
                                incompleteData = string.Empty;

                                // Execute command on Rhino's main thread
                                RhinoApp.InvokeOnUiThread(new Action(() =>
                                {
                                    try
                                    {
                                        JObject response = ExecuteCommand(command);
                                        string responseJson = JsonConvert.SerializeObject(response);

                                        try
                                        {
                                            byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
                                            stream.Write(responseBytes, 0, responseBytes.Length);
                                        }
                                        catch
                                        {
                                            logs.Add("Failed to send response - client disconnected");
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        logs.Add($"Error executing command: {e.Message}");
                                        try
                                        {
                                            JObject errorResponse = new JObject
                                            {
                                                ["status"] = "error",
                                                ["message"] = e.Message
                                            };

                                            byte[] errorBytes = Encoding.UTF8.GetBytes(errorResponse.ToString());
                                            stream.Write(errorBytes, 0, errorBytes.Length);
                                        }
                                        catch
                                        {
                                            // Ignore send errors
                                        }
                                    }
                                }));
                            }
                            catch (JsonException)
                            {
                                // Incomplete JSON data, wait for more
                            }
                        }
                        else
                        {
                            // No data available, sleep a bit to prevent CPU overuse
                            Thread.Sleep(50);
                        }
                    }
                    catch (Exception e)
                    {
                        logs.Add($"Error receiving data: {e.Message}");
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                logs.Add($"Error in client handler: {e.Message}");
            }
            finally
            {
                try
                {
                    client.Close();
                }
                catch
                {
                    // Ignore errors on close
                }
                logs.Add("Client handler stopped");
            }
        }

        private JObject ExecuteCommand(JObject command)
        {
            try
            {
                string cmdType = command["type"]?.ToString();
                JObject parameters = command["params"] as JObject ?? new JObject();

                logs.Add($"Executing command: {cmdType}");

                JObject result = ExecuteCommandInternal(cmdType, parameters);

                logs.Add("Command execution complete");
                return result;
            }
            catch (Exception e)
            {
                logs.Add($"Error executing command: {e.Message}");
                return new JObject
                {
                    ["status"] = "error",
                    ["message"] = e.Message
                };
            }
        }

        private JObject ExecuteCommandInternal(string cmdType, JObject parameters)
        {

            // Dictionary to map command types to handler methods
            Dictionary<string, Func<JObject, JObject>> handlers = new Dictionary<string, Func<JObject, JObject>>
            {
                ["create_slider"] = this.handler.CreateSlider,
                // Add more handlers as needed
            };

            if (handlers.TryGetValue(cmdType, out var handler))
            {
                var doc = RhinoDoc.ActiveDoc;
                var record = doc.BeginUndoRecord("Run MCP command");
                try
                {
                    JObject result = handler(parameters);
                    return new JObject
                    {
                        ["status"] = "success",
                        ["result"] = result
                    };
                }
                catch (Exception e)
                {
                    logs.Add($"Error in handler: {e.Message}");
                    return new JObject
                    {
                        ["status"] = "error",
                        ["message"] = e.Message
                    };
                }
                finally
                {
                    doc.EndUndoRecord(record);
                }
            }
            else
            {
                return new JObject
                {
                    ["status"] = "error",
                    ["message"] = $"Unknown command type: {cmdType}"
                };
            }
        }
    }
}