using SSDExam.Authentication;
// See https://aka.ms/new-console-template for more information


var auth0Auth = new Auth0AuthHandler(
    domain: "dev-pv47w9x8.us.auth0.com",
    clientId: "dSVzkKqWruHCuEd8Uxqa2y4gqCzNAuLC",
    clientSecret: "eF_r9urlv90-sZFdCBeozAp7ge-47aQ4DAfRSdRlrCRJyMmCPcnZuOyH0yKFS89U" // Optional depending on application type
);


        
Console.WriteLine("Starting authentication...");
var authResult = await auth0Auth.LoginWithDeviceCodeAsync();
        
if (authResult.IsAuthenticated)
{
    Console.WriteLine($"Authentication successful!");
    Console.WriteLine($"Welcome, {authResult.Account.Username} ({authResult.Account.Email})");
            
    // Store tokens securely for later use
    // TODO: Implement secure token storage
                
    // Continue with authenticated operations
    // ...
}
else
{
    Console.WriteLine($"Authentication failed: {authResult.ErrorMessage}");
}

