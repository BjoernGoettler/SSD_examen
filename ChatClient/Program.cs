internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.Write("Enter your client ID: ");
        var clientId = Console.ReadLine();

        Console.Write("Enter server address (default: localhost): ");
        var serverAddress = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(serverAddress))
            serverAddress = "localhost";

        Console.Write("Enter server port (default: 5000): ");
        var portInput = Console.ReadLine();
        var port = string.IsNullOrWhiteSpace(portInput) ? 5000 : int.Parse(portInput);

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
                    var input = Console.ReadLine();

                    if (input.ToLower() == "quit")
                        break;

                    var parts = input.Split(' ', 3);

                    if (parts[0].ToLower() == "sendkey" && parts.Length >= 2)
                        await client.SendPublicKeyAsync(parts[1]);
                    else if (parts[0].ToLower() == "msg" && parts.Length >= 3)
                        await client.SendMessageAsync(parts[1], parts[2]);
                    else
                        Console.WriteLine("Unknown command or incorrect format.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}