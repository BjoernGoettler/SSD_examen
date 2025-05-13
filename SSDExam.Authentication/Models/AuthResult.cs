namespace SSDExam.Authentication.Models;

public class AuthResult
{
    public bool IsAuthenticated { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
    public string IdToken { get; set; }
    public string TokenType { get; set; }
    public UserAccount Account { get; set; }
    public string ErrorMessage { get; set; }
}