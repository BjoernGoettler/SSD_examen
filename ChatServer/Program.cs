using System.Net;
using System.Net.Sockets;
using System.Text;
using Monitoring;

public class ChatServer
{
    private static Dictionary<string, TcpClient> connectedClients = new Dictionary<string, TcpClient>();
    private static TcpListener tcpListener;
    private static readonly int port = 6666;

    public static void Main()
    {
        MonitorService.Log.Here().Information("Starting server");
        tcpListener = new TcpListener(IPAddress.Any, port);
        try
        {
            MonitorService.Log.Here().Debug("Trying to start server");
            tcpListener.Start();
        }
        catch (System.Net.Sockets.SocketException e)
        {
            MonitorService.Log.Here().Debug("Exception thrown when starting server");
            
            // The following is just a help for panic situations when presenting. Proper logging will be introduced to take care of real exceptions
            if (e.Message == "Address already in use")
            {
                MonitorService.Log.Here().Debug(e.ToString());
                Console.WriteLine("Port {0} is already in use", port);
                Console.WriteLine("Shut anything else using that port down and try again.");
                Console.WriteLine("Or build with another port");
                return;
            }
            
            //If something happened that was not because of the port being used, we need to throw the exception again
            MonitorService.Log.Here().Error(e, "Exception thrown when starting server");
            throw;
        } 
        
        MonitorService.Log.Here().Information("Server started");
        Console.WriteLine("Server started, waiting for clients...");
        
        while (true)
        {
            var client = tcpListener.AcceptTcpClient();
            new Thread(() => HandleClient(client)).Start();
        }
    }

    private static void HandleClient(TcpClient client)
    {
        MonitorService.Log.Information("Client connected");
        var stream = client.GetStream();
        var buffer = new byte[1024];
        int bytesRead;

        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
        {
            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            
            // However tempting it might be. We can't allow the message to be logged
            Console.WriteLine("Received: " + message);

            // Register client (send unique ID or other registration logic)
            if (message.StartsWith("REGISTER"))
            {
                MonitorService.Log.Here().Debug("Registering client");
                string clientId = Guid.NewGuid().ToString();
                connectedClients[clientId] = client;
                var statusLine = $"Client registered with ID: {clientId}";
                MonitorService.Log.Information(statusLine);
                Console.WriteLine(statusLine);
                
                // Send client ID back to the client
                byte[] response = Encoding.UTF8.GetBytes(clientId);
                stream.Write(response, 0, response.Length);
            }
        }
        MonitorService.Log.Here().Debug("Disconnecting Client");
        client.Close();
        MonitorService.Log.Information($"Client disconnected");
    }
}