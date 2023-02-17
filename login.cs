using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

public partial class Login : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        if (!IsPostBack)
        {
            // Check if the X.509 certificate is installed on the device and is trusted
            var certThumbprint = "example_thumbprint"; // The thumbprint of the X.509 certificate
            var cert = new X509Certificate2();
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            var certCollection = store.Certificates.Find(X509FindType.FindByThumbprint, certThumbprint, true);
            if (certCollection.Count > 0)
            {
                cert = certCollection[0];
                var chain = new X509Chain();
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                if (chain.Build(cert))
                {
                    // Certificate is valid and trusted, allow user to login
                }
                else
                {
                    // Certificate is not valid or trusted, show error message
                    ErrorMessage.Text = "Invalid or untrusted certificate.";
                }
            }
            else
            {
                // Certificate is not installed, show error message
                ErrorMessage.Text = "Certificate not found on device.";
            }
        }
    }

    protected void LoginButton_Click(object sender, EventArgs e)
    {
        var username = UsernameTextBox.Text;
        var password = PasswordTextBox.Text;

        // Generate a random nonce to use in the password hashing
        var random = new Random();
        var nonce = random.Next(0, int.MaxValue);

        // XOR the password with the nonce
        var passwordXORNonce = XOR(password, nonce.ToString());

        // Compute the SHA256 hash of the password and nonce
        var passwordHash = SHA256(passwordXORNonce);

        // Authenticate the user against the database
        var auth = new Auth();
        if (auth.AuthenticateUser(username, passwordHash))
        {
            // User is authenticated, redirect to main page
            Response.Redirect("Main.aspx");
        }
        else
        {
            // User is not authenticated, show error message
            ErrorMessage.Text = "Invalid username or password.";
        }
    }

    // Perform an XOR operation on two strings
    private string XOR(string s1, string s2)
    {
        var result = new StringBuilder();
        for (int i = 0; i < s1.Length; i++)
        {
            result.Append((char)(s1[i] ^ s2[i % s2.Length]));
        }
        return result.ToString();
    }

    // Compute the SHA256 hash of a string
    private string SHA256(string input)
    {
        using (var sha256 = SHA256.Create())
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = sha256.ComputeHash(bytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }
    }
}
