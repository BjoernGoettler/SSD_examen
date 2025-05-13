using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Monitoring;
using Serilog;

class Program
{
    static async Task Main(string[] args)
    {
        Console.Write("Enter your client ID: ");
        string clientId = Console.ReadLine();
        
        Console.Write("Enter server address (default: localhost): ");
        string serverAddress = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(serverAddress))
            serverAddress = "localhost";
        
        Console.Write("Enter server port (default: 5000): ");
        string portInput = Console.ReadLine();
        int port = string.IsNullOrWhiteSpace(portInput) ? 5000 : int.Parse(portInput);
        
        using (var client = new SecureChatClient(clientId, serverAddress, port))
        {
            try
            {
                await client.ConnectAsync();
                
                Console.WriteLine("\nCommands:");
                Console.WriteLine("  sendkey <recipient> - Send your public key to a recipient");
                Console.WriteLine("  msg <recipient> <message> - Send an encrypted message");
                Console.WriteLine("  quit - Exit the application");
                Console.WriteLine("\nYour public key:");
                Console.WriteLine(client.GetPublicKey());
                
                while (true)
                {
                    Console.Write("> ");
                    string input = Console.ReadLine();
                    
                    if (input.ToLower() == "quit")
                        break;
                    
                    string[] parts = input.Split(' ', 3);
                    
                    if (parts[0].ToLower() == "sendkey" && parts.Length >= 2)
                    {
                        await client.SendPublicKeyAsync(parts[1]);
                    }
                    else if (parts[0].ToLower() == "msg" && parts.Length >= 3)
                    {
                        await client.SendMessageAsync(parts[1], parts[2]);
                    }
                    else
                    {
                        swEncrypt.Write(message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}