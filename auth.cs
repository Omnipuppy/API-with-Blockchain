using System.Security.Cryptography;
using Npgsql;
using Npgsql.Logging;
using NpgsqlTypes;
using System;
using System.Text;

namespace JsonRpcApi
{
    public class Auth
    {
        private readonly NpgsqlConnection connection;
        private readonly byte[] clientPublicKey;
        private byte[] serverPublicKey;
        private byte[] sharedSecret;

        public Auth(string connectionString, byte[] clientPublicKey)
        {
            this.connection = new NpgsqlConnection(connectionString);
            this.clientPublicKey = clientPublicKey;
            NpgsqlLogManager.Provider = new ConsoleLoggingProvider(NpgsqlLogLevel.Debug, true, true);
        }

        public void Connect()
        {
            connection.Open();
            using (var cmd = new NpgsqlCommand("SELECT public_key FROM auth", connection))
            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    serverPublicKey = (byte[])reader[0];
                }
            }

            using (var ecdh = ECDiffieHellman.Create())
            {
                ecdh.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash;
                ecdh.HashAlgorithm = SHA256.Create();

                sharedSecret = ecdh.DeriveKeyMaterial(ECDiffieHellmanPublicKey.FromByteArray(serverPublicKey, new ECParameters { Curve = ecdh.ExportParameters(false).Curve }), ECDiffieHellmanKeyDerivationFunction.Hash, SHA256.Create(), 32);

                var cmd = new NpgsqlCommand("SELECT auth_nonce FROM auth WHERE public_key = @public_key", connection);
                cmd.Parameters.AddWithValue("public_key", serverPublicKey);
                var nonce = (byte[])cmd.ExecuteScalar();

                var xorBytes = new byte[sharedSecret.Length];
                for (int i = 0; i < sharedSecret.Length; i++)
                {
                    xorBytes[i] = (byte)(sharedSecret[i] ^ nonce[i]);
                }

                var response = SHA256.Create().ComputeHash(xorBytes);

                cmd = new NpgsqlCommand("SELECT user_id FROM auth WHERE public_key = @public_key AND password_hash = @password_hash", connection);
                cmd.Parameters.AddWithValue("public_key", serverPublicKey);
                cmd.Parameters.AddWithValue("password_hash", response);
                var userId = (int?)cmd.ExecuteScalar();

                if (userId == null)
                {
                    throw new Exception("Invalid credentials");
                }
            }
        }

        public void Close()
        {
            connection.Close();
        }
    }
}
