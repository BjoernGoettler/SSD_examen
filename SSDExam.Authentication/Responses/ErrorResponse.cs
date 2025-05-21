using System.Text.Json.Serialization;

namespace SSDExam.Authentication.Responses;

public class ErrorResponse
{
    [JsonPropertyName("error")] public string Error { get; set; }

    [JsonPropertyName("error_description")]
    public string ErrorDescription { get; set; }
}