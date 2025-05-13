using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

public class RsaKeyManager
{
    private RSA _rsaKey;
    private Dictionary<string, RSAParameters> _peerPublicKeys = new Dictionary<string, RSAParameters>();
    
    public RsaKeyManager()
    {
        // Generate a new RSA key pair
        _rsaKey = RSA.Create(2048); // 2048-bit key
    }
    
    // Get our public key in a format suitable for transmission
    public string GetPublicKeyString()
    {
        var publicKey = _rsaKey.ExportParameters(false);
        
        // We'll use XML serialization for the RSA parameters
        var stringWriter = new StringWriter();
        var xmlSerializer = new XmlSerializer(typeof(RSAParameters));
        xmlSerializer.Serialize(stringWriter, publicKey);
        return stringWriter.ToString();
    }
    
    // Add a peer's public key
    public void AddPeerPublicKey(string peerId, string publicKeyXml)
    {
        try
        {
            var stringReader = new StringReader(publicKeyXml);
            var xmlSerializer = new XmlSerializer(typeof(RSAParameters));
            var publicKey = (RSAParameters)xmlSerializer.Deserialize(stringReader);
            
            _peerPublicKeys[peerId] = publicKey;
            Console.WriteLine($"Added public key for peer: {peerId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error adding peer public key: {ex.Message}");
            throw;
        }
    }
    
    // Encrypt a message for a specific peer using their public key
    public byte[] EncryptForPeer(string peerId, string message)
    {
        if (!_peerPublicKeys.TryGetValue(peerId, out var publicKey))
        {
            throw new Exception($"No public key found for peer: {peerId}");
        }
        
        try
        {
            using (var rsaEncryptor = RSA.Create())
            {
                rsaEncryptor.ImportParameters(publicKey);
                
                var messageBytes = Encoding.UTF8.GetBytes(message);
                
                // For longer messages, we'll use a hybrid approach:
                // 1. Generate a random AES key
                // 2. Encrypt the message with AES
                // 3. Encrypt the AES key with RSA
                // 4. Send both the encrypted key and the encrypted message
                
                if (messageBytes.Length > 100) // RSA can only encrypt small amounts of data
                {
                    using (Aes aes = Aes.Create())
                    {
                        aes.GenerateKey();
                        aes.GenerateIV();
                        
                        // Encrypt the AES key with RSA
                        byte[] encryptedKey = rsaEncryptor.Encrypt(aes.Key, RSAEncryptionPadding.OaepSHA256);
                        
                        // Encrypt the message with AES
                        byte[] encryptedMessage;
                        using (var encryptor = aes.CreateEncryptor())
                        using (var memoryStream = new MemoryStream())
                        {
                            // Structure: [RSA-encrypted AES key length (4 bytes)][RSA-encrypted AES key][AES IV (16 bytes)][AES-encrypted message]
                            var keyLengthBytes = BitConverter.GetBytes(encryptedKey.Length);
                            memoryStream.Write(keyLengthBytes, 0, keyLengthBytes.Length);
                            memoryStream.Write(encryptedKey, 0, encryptedKey.Length);
                            memoryStream.Write(aes.IV, 0, aes.IV.Length);
                            
                            using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                            {
                                cryptoStream.Write(messageBytes, 0, messageBytes.Length);
                                cryptoStream.FlushFinalBlock();
                            }
                            
                            encryptedMessage = memoryStream.ToArray();
                        }
                        
                        return encryptedMessage;
                    }
                }
                else
                {
                    // For short messages, just use RSA directly
                    byte[] encryptedBytes = rsaEncryptor.Encrypt(messageBytes, RSAEncryptionPadding.OaepSHA256);
                    
                    // Prepend a flag to indicate direct RSA encryption (0 length key)
                    byte[] result = new byte[4 + encryptedBytes.Length];
                    BitConverter.GetBytes(0).CopyTo(result, 0); // 0 length key means direct RSA
                    encryptedBytes.CopyTo(result, 4);
                    
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error encrypting message: {ex.Message}");
            throw;
        }
    }
    
    // Decrypt a message using our private key
    public string DecryptMessage(byte[] encryptedData)
    {
        try
        {
            using (var memoryStream = new MemoryStream(encryptedData))
            using (var reader = new BinaryReader(memoryStream))
            {
                // Read the encrypted key length
                int keyLength = reader.ReadInt32();
                
                if (keyLength == 0)
                {
                    // Direct RSA encryption for short messages
                    byte[] encryptedMessage = reader.ReadBytes((int)(memoryStream.Length - memoryStream.Position));
                    byte[] decryptedBytes = _rsaKey.Decrypt(encryptedMessage, RSAEncryptionPadding.OaepSHA256);
                    return Encoding.UTF8.GetString(decryptedBytes);
                }
                else
                {
                    // Hybrid encryption for longer messages
                    byte[] encryptedKey = reader.ReadBytes(keyLength);
                    byte[] iv = reader.ReadBytes(16); // AES IV is 16 bytes
                    byte[] encryptedMessage = reader.ReadBytes((int)(memoryStream.Length - memoryStream.Position));
                    
                    // Decrypt the AES key with RSA
                    byte[] decryptedKey = _rsaKey.Decrypt(encryptedKey, RSAEncryptionPadding.OaepSHA256);
                    
                    // Decrypt the message with AES
                    using (Aes aes = Aes.Create())
                    {
                        aes.Key = decryptedKey;
                        aes.IV = iv;
                        
                        using (var decryptor = aes.CreateDecryptor())
                        using (var decryptStream = new MemoryStream(encryptedMessage))
                        using (var cryptoStream = new CryptoStream(decryptStream, decryptor, CryptoStreamMode.Read))
                        using (var streamReader = new StreamReader(cryptoStream))
                        {
                            return streamReader.ReadToEnd();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error decrypting message: {ex.Message}");
            throw;
        }
    }
    
    // Optional: Save keys to file for persistence
    public void SaveKeyPair(string filePath, string password)
    {
        // Export the key pair including the private key, protected with a password
        var keyParams = _rsaKey.ExportParameters(true);
        
        // Encrypt the private key with a password
        using (var aes = Aes.Create())
        {
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            
            // Derive key from password
            using (var deriveBytes = new Rfc2898DeriveBytes(passwordBytes, salt, 10000))
            {
                aes.Key = deriveBytes.GetBytes(32); // 256 bits
                aes.IV = deriveBytes.GetBytes(16);  // 128 bits
            }
            
            using (var fileStream = File.Create(filePath))
            {
                // Write salt
                fileStream.Write(salt, 0, salt.Length);
                
                // Serialize and encrypt the RSA parameters
                using (var encryptor = aes.CreateEncryptor())
                using (var cryptoStream = new CryptoStream(fileStream, encryptor, CryptoStreamMode.Write))
                {
                    var serializer = new XmlSerializer(typeof(RSAParameters));
                    serializer.Serialize(cryptoStream, keyParams);
                }
            }
        }
    }
    
    // Optional: Load keys from file
    public void LoadKeyPair(string filePath, string password)
    {
        using (var fileStream = File.OpenRead(filePath))
        {
            // Read salt
            var salt = new byte[16];
            fileStream.Read(salt, 0, salt.Length);
            
            using (var aes = Aes.Create())
            {
                var passwordBytes = Encoding.UTF8.GetBytes(password);
                
                // Derive key from password
                using (var deriveBytes = new Rfc2898DeriveBytes(passwordBytes, salt, 10000))
                {
                    aes.Key = deriveBytes.GetBytes(32);
                    aes.IV = deriveBytes.GetBytes(16);
                }
                
                // Decrypt and deserialize the RSA parameters
                using (var decryptor = aes.CreateDecryptor())
                using (var cryptoStream = new CryptoStream(fileStream, decryptor, CryptoStreamMode.Read))
                {
                    var serializer = new XmlSerializer(typeof(RSAParameters));
                    var keyParams = (RSAParameters)serializer.Deserialize(cryptoStream);
                    
                    // Import the key parameters
                    _rsaKey = RSA.Create();
                    _rsaKey.ImportParameters(keyParams);
                }
            }
        }
    }
}