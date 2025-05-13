using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class SecureChatClient : IDisposable
{
    private readonly string _clientId;
    private readonly TcpClient _client;
    private NetworkStream _stream;
    private readonly RsaKeyManager _keyManager;
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    private readonly string _serverAddress;
    private readonly int _serverPort;
    
    public SecureChatClient(string clientId, string serverAddress, int serverPort)
    {
        _clientId = clientId;
        _serverAddress = serverAddress;
        _serverPort = serverPort;
        _keyManager = new RsaKeyManager();
        _client = new TcpClient();
        
    }
    
    public async Task ConnectAsync()
    {
        await _client.ConnectAsync(_serverAddress, _serverPort);
        
        _stream = _client.GetStream();
        // Send client ID as the first message
        var clientIdBytes = Encoding.UTF8.GetBytes(_clientId);
        await _stream.WriteAsync(clientIdBytes, 0, clientIdBytes.Length);
        
        Console.WriteLine($"Connected to server at {_serverAddress}:{_serverPort}");
        
        // Start receiving messages
        _ = ReceiveMessagesAsync(_cts.Token);
    }
    
    public string GetPublicKey()
    {
        return _keyManager.GetPublicKeyString();
    }
    
    public void AddPeerPublicKey(string peerId, string publicKey)
    {
        _keyManager.AddPeerPublicKey(peerId, publicKey);
    }
    
    public async Task SendMessageAsync(string recipientId, string message)
    {
        try
        {
            // Encrypt message with recipient's public key
            var encryptedMessage = _keyManager.EncryptForPeer(recipientId, message);
            
            // Send recipient ID length
            byte[] recipientIdBytes = Encoding.UTF8.GetBytes(recipientId);
            byte[] recipientIdLengthBytes = BitConverter.GetBytes(recipientIdBytes.Length);
            await _stream.WriteAsync(recipientIdLengthBytes, 0, recipientIdLengthBytes.Length);
            
            // Send recipient ID
            await _stream.WriteAsync(recipientIdBytes, 0, recipientIdBytes.Length);
            
            // Send message length
            byte[] messageLengthBytes = BitConverter.GetBytes(encryptedMessage.Length);
            await _stream.WriteAsync(messageLengthBytes, 0, messageLengthBytes.Length);
            
            // Send encrypted message
            await _stream.WriteAsync(encryptedMessage, 0, encryptedMessage.Length);
            
            Console.WriteLine($"Message sent to {recipientId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message: {ex.Message}");
        }
    }

    private async Task ReceiveMessagesAsync(CancellationToken token)
    {
        var buffer = new byte[4096];

        try
        {
            while (!token.IsCancellationRequested && _client.Connected)
            {
                // Read message length (4 bytes)
                int bytesRead = await _stream.ReadAsync(buffer, 0, 4, token);
                if (bytesRead < 4) break;

                int messageLength = BitConverter.ToInt32(buffer, 0);
                var message = new byte[messageLength];

                // Read the full message
                int totalRead = 0;
                while (totalRead < messageLength)
                {
                    bytesRead = await _stream.ReadAsync(message, totalRead,
                        Math.Min(buffer.Length, messageLength - totalRead), token);
                    if (bytesRead == 0) break;
                    totalRead += bytesRead;
                }

                if (totalRead < messageLength) break;

                // Check if it's a key exchange message
                string messageString = Encoding.UTF8.GetString(message);
                if (messageString.StartsWith("KEY_EXCHANGE:"))
                {
                    string publicKey = messageString.Substring("KEY_EXCHANGE:".Length);
                    string senderId = ""; // We need to determine the sender ID

                    // Extract sender ID from sender message or use a known mapping
                    // This is a simplification; you might need a more robust way to identify senders

                    _keyManager.AddPeerPublicKey(senderId, publicKey);
                    Console.WriteLine($"Received public key from {senderId}");

                    // Automatically send our public key back if we don't have a direct way to determine the sender
                    if (string.IsNullOrEmpty(senderId))
                    {
                        Console.WriteLine(
                            "Cannot determine sender ID. Use the 'sendkey' command to send your public key.");
                    }
                    else
                    {
                        await SendPublicKeyAsync(senderId);
                    }
                }
                else
                {
                    // Regular encrypted message
                    try
                    {
                        string decryptedMessage = _keyManager.DecryptMessage(message);
                        Console.WriteLine($"Received: {decryptedMessage}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error decrypting message: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            Console.WriteLine($"Error receiving messages: {ex.Message}");
        }
        finally
        {
            Console.WriteLine("Disconnected from server");
        }
    }
    
    public void Dispose()
    {
        _cts.Cancel();
        _stream?.Dispose();
        _client?.Dispose();
        _cts.Dispose();
    }
    
    // Add this to the SecureChatClient class

// Send a key exchange message
    public async Task SendPublicKeyAsync(string recipientId)
    {
        try
        {
            // Format: "KEY_EXCHANGE:|your_public_key_xml"
            string keyExchangeMessage = "KEY_EXCHANGE:" + GetPublicKey();

            // For key exchange, we don't encrypt since we don't have the recipient's key yet
            // We'll send it as a plain message with a special prefix

            byte[] messageBytes = Encoding.UTF8.GetBytes(keyExchangeMessage);

            // Send recipient ID length
            byte[] recipientIdBytes = Encoding.UTF8.GetBytes(recipientId);
            byte[] recipientIdLengthBytes = BitConverter.GetBytes(recipientIdBytes.Length);
            await _stream.WriteAsync(recipientIdLengthBytes, 0, recipientIdLengthBytes.Length);

            // Send recipient ID
            await _stream.WriteAsync(recipientIdBytes, 0, recipientIdBytes.Length);

            // Send message length
            byte[] messageLengthBytes = BitConverter.GetBytes(messageBytes.Length);
            await _stream.WriteAsync(messageLengthBytes, 0, messageLengthBytes.Length);

            // Send message
            await _stream.WriteAsync(messageBytes, 0, messageBytes.Length);

            Console.WriteLine($"Public key sent to {recipientId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending public key: {ex.Message}");
        }
    }

// Modify the ReceiveMessagesAsync method to handle key exchange messages

}