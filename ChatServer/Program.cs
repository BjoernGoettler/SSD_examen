using ChatServer;


Console.Write("Enter server port (default: 6666): ");
string portInput = Console.ReadLine();
int port = string.IsNullOrWhiteSpace(portInput) ? 6666 : int.Parse(portInput);

var server = new SimpleChatServer(port);
server.Start();

Console.WriteLine("Server running. Press Enter to stop.");

// Keep the server running until Enter is pressed
var stopEvent = new ManualResetEvent(false);

// Set up a console event handler to ensure the server keeps running
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true; // Prevent the process from terminating
    stopEvent.Set(); // Signal to exit gracefully
};

// Command processing loop to keep the server interactive
_ = Task.Run(() =>
{
    while (true)
    {
        string command = Console.ReadLine()?.ToLower();

        if (command == "quit" || command == "exit")
        {
            stopEvent.Set();
            break;
        }
        else if (command == "status" || command == "stats")
        {
            server.PrintStatus();
        }
        else if (command == "help")
        {
            Console.WriteLine("Available commands:");
            Console.WriteLine("  status - Show server status");
            Console.WriteLine("  quit   - Stop the server and exit");
            Console.WriteLine("  help   - Show this help message");
        }
        else if (!string.IsNullOrWhiteSpace(command))
        {
            Console.WriteLine("Unknown command. Type 'help' for available commands.");
        }
    }
});

// Wait for the stop signal
stopEvent.WaitOne();

Console.WriteLine("Stopping server...");
server.Stop();
Console.WriteLine("Server stopped.");