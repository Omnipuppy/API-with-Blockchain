using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Npgsql;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;

namespace IRCClient
{
    class Program
    {
        static TcpClient client;
        static NetworkStream stream;
        static DiffieHellman dh;
        static byte[] publicKey;
        static byte[] sharedKey;
        static string server = "rpc.aspx";
        static string apiNamespace = "JsonRpcApi";
        static string model;
        static string version;
        static string modifier;
        static string description;

        static void Main(string[] args)
        {
            ConnectToServer();
            ReadData();
        }

        static void ConnectToServer()
        {
            client = new TcpClient(server, 6667);
            stream = client.GetStream();

            // Generate Diffie Hellman key pair and send public key to server
            dh = new DiffieHellmanManaged();
            publicKey = dh.PublicKey;
            SendData($"PRIVMSG {apiNamespace} {{\"method\":\"SetPublicKey\",\"params\":[\"{Convert.ToBase64String(publicKey)}\"]}}");
        }

        static void ReadData()
        {
            byte[] data = new byte[1024];
            string responseData = string.Empty;
            int bytes = stream.Read(data, 0, data.Length);
            responseData = Encoding.ASCII.GetString(data, 0, bytes);
            Console.WriteLine(responseData);

            // Check if server sent a PING message and respond with a PONG
            if (responseData.StartsWith("PING"))
            {
                SendData(responseData.Replace("PING", "PONG"));
            }

            // Parse JSON response and update values of model, version, modifier, and description
            dynamic response = JsonConvert.DeserializeObject(responseData);
            if (response.method == "UpdateValues")
            {
                model = response.result.model;
                version = response.result.version;
                modifier = response.result.modifier;
                description = response.result.description;

                // Save updated values to PostgreSQL database using Diffie Hellman for encryption
                string connectionString = "Server=myServerAddress;Database=myDataBase;Username=myUsername;Password=myPassword;";
                using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (NpgsqlCommand command = new NpgsqlCommand())
                    {
                        command.Connection = connection;
                        command.CommandText = "INSERT INTO values(model, version, modifier, description) VALUES(:model, :version, :modifier, :description)";
                        command.Parameters.AddWithValue("model", Encrypt(model));
                        command.Parameters.AddWithValue("version", Encrypt(version));
                        command.Parameters.AddWithValue("modifier", Encrypt(modifier));
                        command.Parameters.AddWithValue("description", Encrypt(description));
                        command.ExecuteNonQuery();
                    }
                }
            }

            // Wait for 5 minutes and send request to update values again
            Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(t => SendData($"PRIVMSG {apiNamespace} {{\"method\":\"UpdateValues\",\"params\":[]}}"));
            ReadData();
        }

        static void SendData(string message)
        {
            byte[] data = Encoding.ASCII.GetBytes($"{message}\r\n");
            stream.Write(data, 0, data.Length);
}
    static byte[] Encrypt(string plaintext)
    {
        // Generate shared key using Diffie Hellman
        byte[] iv = new byte[16];
        CfbBlockCipher cipher = new CfbBlockCipher(new AesFastEngine(), 128);
        BufferedBlockCipher cipherWithPadding = new BufferedBlockCipher(cipher);
        IBufferedCipher cipherBuffer = new PaddedBufferedBlockCipher(cipherWithPadding, new Pkcs7Padding());

        DHParameters dhParams = new DHParameters(dh.Prime, dh.G);
        IBasicAgreement agreement = AgreementUtilities.GetBasicAgreement("DH");
        agreement.Init(new DHPublicKeyParameters(new BigInteger(publicKey), dhParams));
        byte[] sharedSecret = agreement.CalculateAgreement(new DHPublicKeyParameters(new BigInteger(sharedKey), dhParams)).ToByteArrayUnsigned();

        // Encrypt plaintext using shared key and return ciphertext
        KeyParameter keyParameter = ParameterUtilities.CreateKeyParameter("AES", sharedSecret);
        cipherBuffer.Init(true, keyParameter);
        byte[] inputBytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] outputBytes = new byte[cipherBuffer.GetOutputSize(inputBytes.Length)];
        int length = cipherBuffer.ProcessBytes(inputBytes, 0, inputBytes.Length, outputBytes, 0);
        cipherBuffer.DoFinal(outputBytes, length);

        return outputBytes;
    }
}

}


