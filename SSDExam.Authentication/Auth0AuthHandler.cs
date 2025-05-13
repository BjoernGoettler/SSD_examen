using System.Net.Http.Headers;
using System.Text.Json;
using SSDExam.Authentication.Models;
using SSDExam.Authentication.Responses;
public class Auth0AuthHandler
    {
        private readonly string _domain;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _audience;
        private readonly HttpClient _httpClient;

        public Auth0AuthHandler(
            string domain,
            string clientId,
            string clientSecret = null,
            string audience = null)
        {
            _domain = domain;
            _clientId = clientId;
            _clientSecret = clientSecret;
            _audience = audience;
            _httpClient = new HttpClient();
        }

        public async Task<AuthResult> LoginWithDeviceCodeAsync(string[] scopes)
        {
            try
            {
                // Step 1: Start device authorization flow
                var deviceAuthorizationResponse = await InitiateDeviceAuthorizationFlow(scopes);

                // Step 2: Display instructions to user
                Console.WriteLine($"To sign in, use a web browser to open the page {deviceAuthorizationResponse.VerificationUriComplete}");
                Console.WriteLine("Enter the code displayed on your device when prompted.");
                Console.WriteLine($"Code: {deviceAuthorizationResponse.UserCode}");

                // Step 3: Poll for token
                return await PollForTokenAsync(deviceAuthorizationResponse.DeviceCode, deviceAuthorizationResponse.Interval);
            }
            catch (Exception ex)
            {
                return new AuthResult
                {
                    IsAuthenticated = false,
                    ErrorMessage = $"Authentication failed: {ex.Message}"
                };
            }
        }

        private async Task<DeviceAuthorizationResponse> InitiateDeviceAuthorizationFlow(string[] scopes)
        {
            // Auth0's device authorization endpoint
            var deviceAuthUrl = $"https://{_domain}/oauth/device/code";

            var requestData = new Dictionary<string, string>
            {
                {"client_id", _clientId},
                {"scope", string.Join(" ", scopes)}
            };
            

            var content = new FormUrlEncodedContent(requestData);

            try
            {
                Console.WriteLine($"Connecting to: {deviceAuthUrl}");
                var response = await _httpClient.PostAsync(deviceAuthUrl, content);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                Console.WriteLine($"Response status: {response.StatusCode}");
                
                response.EnsureSuccessStatusCode();
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                var deviceAuthResponse = JsonSerializer.Deserialize<DeviceAuthorizationResponse>(responseBody, options);

                return deviceAuthResponse;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HTTP request error: {ex.Message}");
                throw;
            }
        }

        private async Task<AuthResult> PollForTokenAsync(string deviceCode, int interval)
        {
            // Auth0's token endpoint
            var tokenUrl = $"https://{_domain}/oauth/token";
            
            var requestData = new Dictionary<string, string>
            {
                {"grant_type", "urn:ietf:params:oauth:grant-type:device_code"},
                {"device_code", deviceCode},
                {"client_id", _clientId}
            };

            if (!string.IsNullOrEmpty(_clientSecret))
            {
                requestData.Add("client_secret", _clientSecret);
            }

            var content = new FormUrlEncodedContent(requestData);

            bool pending = true;
            DateTime timeout = DateTime.Now.AddMinutes(10); // 10 min timeout

            while (pending && DateTime.Now < timeout)
            {
                await Task.Delay(interval * 1000); // Convert interval to milliseconds
                
                var response = await _httpClient.PostAsync(tokenUrl, content);
                var responseJson = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseJson, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        
                    // Get user info if required
                    var userInfo = await GetUserInfoAsync(tokenResponse.AccessToken);
                    
                    return new AuthResult
                    {
                        IsAuthenticated = true,
                        AccessToken = tokenResponse.AccessToken,
                        RefreshToken = tokenResponse.RefreshToken,
                        ExpiresIn = tokenResponse.ExpiresIn,
                        IdToken = tokenResponse.IdToken,
                        TokenType = tokenResponse.TokenType,
                        Account = userInfo
                    };
                }
                else
                {
                    var error = JsonSerializer.Deserialize<ErrorResponse>(responseJson, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        
                    if (error.Error == "authorization_pending")
                    {
                        // Still waiting for user to complete the flow
                        continue;
                    }
                    else if (error.Error == "slow_down")
                    {
                        // Increase the interval
                        interval += 5;
                        continue;
                    }
                    else
                    {
                        // Other error occurred
                        return new AuthResult
                        {
                            IsAuthenticated = false,
                            ErrorMessage = error.ErrorDescription ?? error.Error
                        };
                    }
                }
            }

            return new AuthResult
            {
                IsAuthenticated = false,
                ErrorMessage = "Authentication timed out"
            };
        }

        private async Task<UserAccount> GetUserInfoAsync(string accessToken)
        {
            var userInfoUrl = $"https://{_domain}/userinfo";
            
            using var request = new HttpRequestMessage(HttpMethod.Get, userInfoUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var userInfoJson = await response.Content.ReadAsStringAsync();
            var userInfo = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(userInfoJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            return new UserAccount
            {
                Username = GetStringValue(userInfo, "nickname") ?? GetStringValue(userInfo, "email"),
                Email = GetStringValue(userInfo, "email"),
                Claims = userInfo
            };
        }

        private string GetStringValue(Dictionary<string, JsonElement> dict, string key)
        {
            return dict.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String 
                ? value.GetString() 
                : null;
        }

        public async Task<AuthResult> RefreshTokenAsync(string refreshToken)
        {
            try
            {
                var tokenUrl = $"https://{_domain}/oauth/token";
                
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    {"grant_type", "refresh_token"},
                    {"refresh_token", refreshToken},
                    {"client_id", _clientId},
                    {"client_secret", _clientSecret}
                });
                
                var response = await _httpClient.PostAsync(tokenUrl, content);
                response.EnsureSuccessStatusCode();
                
                var responseJson = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseJson, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                var userInfo = await GetUserInfoAsync(tokenResponse.AccessToken);
                
                return new AuthResult
                {
                    IsAuthenticated = true,
                    AccessToken = tokenResponse.AccessToken,
                    RefreshToken = tokenResponse.RefreshToken ?? refreshToken,
                    ExpiresIn = tokenResponse.ExpiresIn,
                    IdToken = tokenResponse.IdToken,
                    TokenType = tokenResponse.TokenType,
                    Account = userInfo
                };
            }
            catch (Exception ex)
            {
                return new AuthResult
                {
                    IsAuthenticated = false,
                    ErrorMessage = $"Token refresh failed: {ex.Message}"
                };
            }
        }
    }