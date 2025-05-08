using System.Net;
using System.Net.Sockets;
using System.Text;

public class ChatServer
{
    private static Dictionary<string, TcpClient> connectedClients = new Dictionary<string, TcpClient>();
    private static TcpListener tcpListener;
    private static readonly int port = 6666;

    public static void Main()
    {
        tcpListener = new TcpListener(IPAddress.Any, port);
        try
        {
            tcpListener.Start();
        }
        catch (System.Net.Sockets.SocketException e)
        {
            if (e.Message == "Address already in use")
            {
                Console.WriteLine("Port {0} is already in use", port);
                Console.WriteLine("Shut anything else using that port down and try again.");
                Console.WriteLine("Or build with another port");
                return;
            }
            
            //If the something happened that was not because of the port being used, we need to throw the exception again
            throw;
        }
            
        
        
        Console.WriteLine("Server started, waiting for clients...");
        
        while (true)
        {
            var client = tcpListener.AcceptTcpClient();
            new Thread(() => HandleClient(client)).Start();
        }
    }

    private static void HandleClient(TcpClient client)
    {
        var stream = client.GetStream();
        var buffer = new byte[1024];
        int bytesRead;

        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
        {
            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Console.WriteLine("Received: " + message);

            // Register client (send unique ID or other registration logic)
            if (message.StartsWith("REGISTER"))
            {
                string clientId = Guid.NewGuid().ToString();
                connectedClients[clientId] = client;
                Console.WriteLine($"Client registered with ID: {clientId}");
                
                // Send client ID back to the client
                byte[] response = Encoding.UTF8.GetBytes(clientId);
                stream.Write(response, 0, response.Length);
            }
        }

        client.Close();
    }
}