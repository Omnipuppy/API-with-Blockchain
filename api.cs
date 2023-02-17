using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Npgsql;
using NpgsqlTypes;
using JsonRpc.Server;
using System.Net;
using System.IO;

namespace JsonRpcApi
{
class Program
{
// Define the structure of the data to be stored in the blockchain
class BlockData
{
public string Model { get; set; }
public string Version { get; set; }
public string Modifier { get; set; }
public string Description { get; set; }
}

    // Define the structure of a block in the blockchain
    class Block
    {
        public int Index { get; set; }
        public DateTime Timestamp { get; set; }
        public BlockData Data { get; set; }
        public string PreviousHash { get; set; }
        public int Nonce { get; set; }
        public string Hash { get; set; }

        // Compute the hash of the block using SHA256 and the data and previous hash
        public void ComputeHash()
        {
            var sha256 = SHA256.Create();
            var data = JsonConvert.SerializeObject(Data);
            var input = Index + Timestamp.ToString("o") + data + PreviousHash + Nonce;
            var bytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = sha256.ComputeHash(bytes);
            Hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        // Perform proof-of-work on the block by finding a nonce that generates a hash with a certain number of leading zeroes
        public void DoWork(int difficulty)
        {
            var leadingZeroes = new string('0', difficulty);
            while (!Hash.StartsWith(leadingZeroes))
            {
                Nonce++;
                ComputeHash();
            }
        }
    }

    // Define the blockchain class
    class Blockchain
    {
        private readonly List<Block> chain = new List<Block>();
        private readonly int difficulty = 3;

        // Add the genesis block to the chain
        public Blockchain()
        {
            var data = new BlockData
            {
                Model = "Model1",
                Version = "Version1",
                Modifier = "Modifier1",
                Description = "Description1"
            };
            var block = new Block
            {
                Index = 0,
                Timestamp = DateTime.UtcNow,
                Data = data,
                PreviousHash = "0",
                Nonce = 0
            };
            block.DoWork(difficulty);
            chain.Add(block);
        }

        // Add a new block to the chain with the specified data
        public void AddBlock(BlockData data)
        {
            var block = new Block
            {
                Index = chain.Count,
                Timestamp = DateTime.UtcNow,
                Data = data,
                PreviousHash = chain.Last().Hash,
                Nonce = 0
            };
            block.DoWork(difficulty);
            chain.Add(block);

            // Store the block in the database
            using (var conn = new NpgsqlConnection("Host=localhost;Username=postgres;Password=postgres;Database=mydatabase"))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand("INSERT INTO blocks (index, timestamp, data, previous_hash, nonce, hash) VALUES (@index, @timestamp, @data, @previous_hash, @nonce, @hash)", conn))
                {
                    cmd.Parameters.AddWithValue("index", block.Index);
                    cmd.Parameters.AddWithValue("timestamp", block.Timestamp);
                    cmd.Parameters.AddWithValue("data", JsonConvert.SerializeObject(block.Data));
                    cmd.Parameters.AddWithValue("previous_hash", block.PreviousHash);
                    cmd.Parameters.AddWithValue("nonce", block.Nonce);
                    cmd.Parameters.AddWithValue("hash", block.Hash);
                    cmd.ExecuteNonQuery();
                }
            }
