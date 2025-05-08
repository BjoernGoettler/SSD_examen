using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Monitoring;

public class ChatClient
{
    private static string serverAddress = "127.0.0.1";
    private static int serverPort = 5000;
    private static TcpClient client = new TcpClient();

    public static void Main()
    {
        MonitorService.Log.Information("Connecting to server");
        client.Connect(serverAddress, serverPort);
        var stream = client.GetStream();

        // Register the client
        MonitorService.Log.Information("Registering client...");
        byte[] registerMessage = Encoding.UTF8.GetBytes("REGISTER");
        stream.Write(registerMessage, 0, registerMessage.Length);
        
        byte[] buffer = new byte[1024];
        int bytesRead = stream.Read(buffer, 0, buffer.Length);
        string clientId = Encoding.UTF8.GetString(buffer, 0, bytesRead);
        MonitorService.Log.Information($"Client registered with ID: {clientId}");

        // Chat loop
        while (true)
        {
            Console.Write("Enter message: ");
            string message = Console.ReadLine();
            string encryptedMessage = EncryptMessage(message);
            byte[] messageBytes = Encoding.UTF8.GetBytes(encryptedMessage);
            stream.Write(messageBytes, 0, messageBytes.Length);
        }
    }

    private static string EncryptMessage(string message)
    {
        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.GenerateKey();
            aesAlg.GenerateIV();

            ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
            using (MemoryStream msEncrypt = new MemoryStream())
            {
                using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(message);
                    }
                }
                return Convert.ToBase64String(msEncrypt.ToArray());
            }
        }
    }
}
