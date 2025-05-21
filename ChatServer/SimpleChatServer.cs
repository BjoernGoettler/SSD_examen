using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ChatServer;

public class SimpleChatServer
{
    private readonly Dictionary<string, ClientConnection> _clients = new();

    private readonly object _clientsLock = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly TcpListener _listener;

    private readonly ConcurrentQueue<(string recipient, byte[] message)> _messageQueue = new();

    private DateTime _startTime;
    private int _totalMessagesProcessed;

    public SimpleChatServer(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _startTime = DateTime.Now;
    }

    public void Start()
    {
        _listener.Start();
        _startTime = DateTime.Now;
        Console.WriteLine($"Server started on port {((IPEndPoint)_listener.LocalEndpoint).Port} at {_startTime}");

        Task.Run(() => ProcessClients(_cts.Token));
    }

    public void PrintStatus()
    {
        var uptime = DateTime.Now - _startTime;

        Console.WriteLine("\n===== SERVER STATUS =====");
        Console.WriteLine($"Uptime: {uptime.Days} days, {uptime.Hours} hours, {uptime.Minutes} minutes");

        lock (_clientsLock)
        {
            Console.WriteLine($"Connected clients: {_clients.Count}");

            if (_clients.Count > 0)
            {
                Console.WriteLine("\nClient List:");
                foreach (var client in _clients.Keys) Console.WriteLine($"- {client}");
            }
        }

        Console.WriteLine($"Messages in queue: {_messageQueue.Count}");
        Console.WriteLine($"Total messages processed: {_totalMessagesProcessed}");
        Console.WriteLine("========================\n");
    }

    private async Task ProcessClients(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
            try
            {
                var client = await _listener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client, token);
            }
            catch (Exception ex) when (token.IsCancellationRequested)
            {
                // Expected exception when cancelling
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accepting client: {ex.Message}");
            }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        string clientId = null;
        ClientConnection clientConnection = null;

        try
        {
            var stream = client.GetStream();
            var buffer = new byte[4096];

            // First message is client ID
            var readBytes = await stream.ReadAsync(buffer, 0, buffer.Length, token);
            clientId = Encoding.UTF8.GetString(buffer, 0, readBytes);

            Console.WriteLine($"Client connected: {clientId}");

            clientConnection = new ClientConnection { TcpClient = client, Stream = stream };

            lock (_clientsLock)
            {
                _clients[clientId] = clientConnection;
            }

            // Start a task to send pending messages to this client
            _ = SendPendingMessagesAsync(clientId, stream, token);

            // Process incoming messages
            while (!token.IsCancellationRequested && client.Connected)
                try
                {
                    // Read message header: [4 bytes recipient ID length][recipient ID bytes][4 bytes message length][message bytes]
                    readBytes = await stream.ReadAsync(buffer, 0, 4, token);
                    if (readBytes < 4) break;

                    var recipientIdLength = BitConverter.ToInt32(buffer, 0);
                    readBytes = await stream.ReadAsync(buffer, 0, recipientIdLength, token);
                    if (readBytes < recipientIdLength) break;

                    var recipientId = Encoding.UTF8.GetString(buffer, 0, recipientIdLength);

                    readBytes = await stream.ReadAsync(buffer, 0, 4, token);
                    if (readBytes < 4) break;

                    var messageLength = BitConverter.ToInt32(buffer, 0);
                    var message = new byte[messageLength];

                    var totalRead = 0;
                    while (totalRead < messageLength)
                    {
                        readBytes = await stream.ReadAsync(message, totalRead, messageLength - totalRead, token);
                        if (readBytes == 0) break;
                        totalRead += readBytes;
                    }

                    if (totalRead < messageLength) break;

                    // Queue message for recipient
                    _messageQueue.Enqueue((recipientId, message));
                    Interlocked.Increment(ref _totalMessagesProcessed);

                    Console.WriteLine($"Message queued from {clientId} to {recipientId}, {message.Length} bytes");
                }
                catch (IOException)
                {
                    // Client disconnected
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing message from {clientId}: {ex.Message}");
                    break;
                }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing client {clientId}: {ex.Message}");
        }
        finally
        {
            if (clientId != null)
            {
                lock (_clientsLock)
                {
                    _clients.Remove(clientId);
                }

                Console.WriteLine($"Client disconnected: {clientId}");
            }

            clientConnection?.TcpClient.Dispose();
        }
    }

    private async Task SendPendingMessagesAsync(string clientId, NetworkStream stream, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(100, token); // Check periodically

                var messagesToSend = new List<byte[]>();
                foreach (var (recipient, message) in _messageQueue)
                    if (recipient == clientId)
                        if (_messageQueue.TryDequeue(out var queuedMessage) && queuedMessage.recipient == clientId)
                            messagesToSend.Add(queuedMessage.message);

                foreach (var message in messagesToSend)
                    try
                    {
                        // Send message length then message
                        await stream.WriteAsync(BitConverter.GetBytes(message.Length), 0, 4, token);
                        await stream.WriteAsync(message, 0, message.Length, token);

                        Console.WriteLine($"Message sent to {clientId}, {message.Length} bytes");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending to client {clientId}: {ex.Message}");
                        return; // Exit the loop if we can't send
                    }
            }
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            Console.WriteLine($"Error in message sender for {clientId}: {ex.Message}");
        }
    }

    public void Stop()
    {
        _cts.Cancel();

        try
        {
            _listener.Stop();

            lock (_clientsLock)
            {
                foreach (var client in _clients.Values)
                    try
                    {
                        client.TcpClient.Close();
                        client.TcpClient.Dispose();
                    }
                    catch
                    {
                    }

                _clients.Clear();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping server: {ex.Message}");
        }
    }

    private class ClientConnection
    {
        public TcpClient TcpClient { get; set; }
        public NetworkStream Stream { get; set; }
    }
}