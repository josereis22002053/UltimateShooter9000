using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Mono.Data.Sqlite;
using Unity.Netcode;
using UnityEngine;

public class DatabaseManager : MonoBehaviour
{
    private string databaseName = "URI=file:Players.db";

    private string dbSimpleName = "Players.db";

    [SerializeField] private string playerToLook = "PlayerName";
    [SerializeField] private string playerToAdd = "PlayerName";
    [SerializeField] private int newElo = 500;


    private bool isServer;

    private void Start()
    {
        string path = Directory.GetCurrentDirectory();
        string dbPath = Path.Combine(path, dbSimpleName);

        // if (NetworkManager.Singleton.IsServer)
        // {
        //     if (File.Exists(dbPath))
        //         Debug.Log("Database exists!");
        //     else
        //         CreateDB();
        // }


        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--server" || args[i] == "--gameServer")
            {
                isServer = true;

                if (File.Exists(dbPath))
                    Debug.Log("Database exists!");
                else
                    CreateDB();
            }
        }
        
        //CreateDB();
    }

    private void Update()
    {
        if (!isServer) return;
        
        // if (Input.GetKeyDown(KeyCode.KeypadPlus))
        // {
        //     AddPlayer(playerToAdd, "123", 100);
        // }

        if (Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (PlayerExists(playerToLook))
                Debug.Log($"'{playerToLook}' exists in database!");
            else
                Debug.Log($"'{playerToLook}' doesn't exist in database!");
        }

        // if (Input.GetKeyDown(KeyCode.KeypadMultiply))
        // {
        //     UpdatePlayerElo(playerToLook, newElo);
        // }
    }

    private void CreateDB()
    {
        using (var connection = new SqliteConnection(databaseName))
        {
            connection.Open();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"CREATE TABLE IF NOT EXISTS Players (
                                        Name VARCHAR(20),
                                        Password VARCHAR(20),
                                        Elo INT,
                                        Kills INT,
                                        Deaths INT)";

                command.ExecuteNonQuery();
            }

            connection.Close();
        }
    }

    public bool PlayerExists(string playerName)
    {
        bool playerExists = false;

        // Connect to database
        using (var conn = new SqliteConnection(databaseName))
        {
            conn.Open();

            // Create query
            string query = "SELECT 1 FROM Players WHERE Name = @playerName LIMIT 1";

            using (var cmd = new SqliteCommand(query, conn))
            {
                // Add parameter to prevent SQL injection
                cmd.Parameters.AddWithValue("@playerName", playerName);

                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        playerExists = true;
                    }
                }
            }

            conn.Close();
        }

        return playerExists;
    }

    public bool CheckPlayerCredentials(string name, string password)
    {
        using (var connection = new SqliteConnection(databaseName))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM Players WHERE Name = @name AND Password = @password";
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@password", password);

                long result = (long)command.ExecuteScalar();
                return result > 0;
            }
        }
    }

    public void AddPlayer(string name, string password, int elo, int kills, int deaths)
    {
        using (var connection = new SqliteConnection(databaseName))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO Players (Name, Password, Elo, Kills, Deaths) VALUES (@name, @password, @elo, @kills, @deaths)";
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@password", password);
                command.Parameters.AddWithValue("@elo", elo);
                command.Parameters.AddWithValue("@kills", kills);
                command.Parameters.AddWithValue("@deaths", deaths);
                command.ExecuteNonQuery();
            }
        }
    }

    public void UpdatePlayerStats(string name, int newElo, int newKills, int newDeaths)
    {
        Debug.Log("Updating database");
        using (var connection = new SqliteConnection(databaseName))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE Players SET Elo = @newElo, Kills = @newKills, Deaths = @newDeaths WHERE Name = @name";
                command.Parameters.AddWithValue("@newElo", newElo);
                command.Parameters.AddWithValue("@newKills", newKills);
                command.Parameters.AddWithValue("@newDeaths", newDeaths);
                command.Parameters.AddWithValue("@name", name);
                int rowsAffected = command.ExecuteNonQuery();
                
                if (rowsAffected > 0)
                {
                    Debug.Log("Player's stats updated successfully.");
                }
                else
                {
                    Debug.LogWarning("Player not found.");
                }
            }
        }
    }

    public (string name, int elo, int kills, int deaths) GetPlayerInfo(string name)
    {
        using (var connection = new SqliteConnection(databaseName))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Name, Elo, Kills, Deaths FROM Players WHERE Name = @name";
                command.Parameters.AddWithValue("@name", name);

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        string playerName = reader.GetString(0);
                        int playerElo = reader.GetInt32(1);
                        int playerKills = reader.GetInt32(2);
                        int playerDeaths = reader.GetInt32(3);
                        return (playerName, playerElo, playerKills, playerDeaths);
                    }
                    else
                    {
                        Debug.LogWarning("Player not found.");
                        return("", 0, 0, 0);
                    }
                }
            }
        }
    }
}
