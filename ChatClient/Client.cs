using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using SSDExam.Authentication.Models;

namespace ChatClient;

public class Client
{
    private static TcpClient client = new();
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly IConfiguration _configuration;
    private readonly string _domain;
    private readonly string _serverAddress;
    private readonly int _serverPort;
    private readonly Auth0AuthHandler authHandler;
    private AuthResult authResult;

    public Client(string serverAddress, int serverPort)
    {
        _serverAddress = serverAddress;
        _serverPort = serverPort;
        _configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", true, true).AddUserSecrets<Client>().Build();
        _domain = _configuration["Auth0:Domain"];
        _clientId = _configuration["Auth0:ClientId"];
        _clientSecret = _configuration["Auth0:ClientSecret"];
        authHandler = new Auth0AuthHandler(
            _domain,
            _clientId,
            _clientSecret
        );
    }

    public void Login()
    {
        authResult = authHandler.LoginWithDeviceCodeAsync().Result;
        if (authResult.IsAuthenticated)
        {
            Console.WriteLine("Authentication successful!");
            Console.WriteLine($"Welcome, {authResult.Account.Username} ({authResult.Account.Email})");
        }
        else
        {
            Console.WriteLine($"Authentication failed: {authResult.ErrorMessage}");
        }
    }

    public void RefreshToken()
    {
        authResult = authHandler.RefreshTokenAsync(authResult.RefreshToken).Result;
    }


    public void SendMessage(string message)
    {
        if (authResult.IsAuthenticated)
        {
            //Todo: Send message to server
        }
    }
}