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

    private void Start()
    {
        string path = Directory.GetCurrentDirectory();
        string dbPath = Path.Combine(path, dbSimpleName);

        if (NetworkManager.Singleton.IsServer)
        {
            if (File.Exists(dbPath))
            Debug.Log("Database exists!");
            else
                CreateDB();
        }
        
        //CreateDB();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.KeypadPlus))
        {
            AddPlayer(playerToAdd, "123", 100);
        }

        if (Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (PlayerExists(playerToLook))
                Debug.Log($"'{playerToLook}' exists in database!");
            else
                Debug.Log($"'{playerToLook}' doesn't exist in database!");
        }

        if (Input.GetKeyDown(KeyCode.KeypadMultiply))
        {
            UpdatePlayerElo(playerToLook, newElo);
        }
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
                                        Elo INT)";

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

    public void AddPlayer(string name, string password, int elo)
    {
        using (var connection = new SqliteConnection(databaseName))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO Players (Name, Password, Elo) VALUES (@name, @password, @elo)";
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@password", password);
                command.Parameters.AddWithValue("@elo", elo);
                command.ExecuteNonQuery();
            }
            connection.Close();
        }
    }

    void UpdatePlayerElo(string name, int newElo)
    {
        using (var connection = new SqliteConnection(databaseName))
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE Players SET Elo = @newElo WHERE Name = @name";
                command.Parameters.AddWithValue("@newElo", newElo);
                command.Parameters.AddWithValue("@name", name);
                int rowsAffected = command.ExecuteNonQuery();
                
                if (rowsAffected > 0)
                {
                    Debug.Log("Player's Elo updated successfully.");
                }
                else
                {
                    Debug.LogWarning("Player not found.");
                }
            }
            connection.Close();
        }
    }
}
