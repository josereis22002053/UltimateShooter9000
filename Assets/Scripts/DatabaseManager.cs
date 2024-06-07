using System.IO;
using Mono.Data.Sqlite;
using UnityEngine;

public class DatabaseManager : MonoBehaviour
{
    private string _dbName = "Players.db";
    private string _dbPath;
    private bool isServer;

    private void Start()
    {
        string path = Application.persistentDataPath;
        _dbPath = "URI=file:" + Path.Combine(path, _dbName);
        Debug.Log(_dbPath);

        var aux = FindObjectOfType<ApplicationStarter>();

        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--server" || args[i] == "--gameServer" || (aux && aux.IsServer))
            {
                isServer = true;

                InitializeDB();
                break;
            }
        }
    }

    private void InitializeDB()
    {
        using (var connection = new SqliteConnection(_dbPath))
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

        Debug.Log("Initialized database!");
    }

    public bool PlayerExists(string playerName)
    {
        bool playerExists = false;

        // Connect to database
        using (var conn = new SqliteConnection(_dbPath))
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
        using (var connection = new SqliteConnection(_dbPath))
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
        using (var connection = new SqliteConnection(_dbPath))
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
        using (var connection = new SqliteConnection(_dbPath))
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
        using (var connection = new SqliteConnection(_dbPath))
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
