using System.Text.Json;

namespace SSDExam.Authentication.Models;

public class UserAccount
{
    public string Username { get; set; }
    public string Email { get; set; }
    public Dictionary<string, JsonElement> Claims { get; set; }
}