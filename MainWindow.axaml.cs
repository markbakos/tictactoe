using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MySql.Data.MySqlClient;
using Tmds.DBus.Protocol;

namespace TicTacToe;

public partial class MainWindow : Window
{
    string connectionString = $"server=46.40.3.35; user=33bakos; database = 33bakos_tictactoe";
    private readonly string playerId;
    public MainWindow()
    {
        InitializeComponent();
        playerId = Guid.NewGuid().ToString();
    }

    private void openGame_Click(object? sender, RoutedEventArgs e)
    {
        string gameCode = GameCodeTextBox.Text.Trim();

        if (gameCode.Length != 4)
        {
            JoinStatus.Text = "Code must be 4 letters long";
            return;
        }

        try
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                var command = new MySqlCommand(
                    "SELECT GameID, Player2 FROM Games WHERE GAMECODE = @gameCode AND IsActive = TRUE",
                    connection);
                command.Parameters.AddWithValue("@gameCode", gameCode);

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        int gameId = reader.GetInt32(0);
                        string? player2 = reader.IsDBNull(1) ? null : reader.GetString(1);

                        if (player2 != null)
                        {
                            JoinStatus.Text = "Game is full";
                            return;
                        }

                        //join game as p2
                        AssignPlayerToGame(gameId, "Player2");
                    }
                    else
                    {
                        //create game and join as p1
                        CreateNewGame(gameCode);
                    }
                }
            }

            var gameWindow = new GameWindow(gameCode, playerId);
            gameWindow.Show();
            this.Close();
        }
        catch (Exception ex)
        {
            JoinStatus.Text = $"Error: {ex.Message}";
        }
    }

    private void CreateNewGame(string gameCode)
    {
        using (var connection = new MySqlConnection(connectionString))
        {
            var command = new MySqlCommand(
                "INSERT INTO Games (GameCode, Player1) VALUES (@gameCode, @playerName)",
                connection);
            command.Parameters.AddWithValue("@gameCode", gameCode);
            command.Parameters.AddWithValue("@playerName", playerId);
            connection.Open();
            command.ExecuteNonQuery();
        }
    }

    private void AssignPlayerToGame(int gameId, string playerColumn)
    {
        using (var connection = new MySqlConnection(connectionString))
        {
            var command = new MySqlCommand(
                $"UPDATE Games SET {playerColumn} = @playerName WHERE GameID = @gameID",
                connection);
            command.Parameters.AddWithValue("@playerName", playerId);
            command.Parameters.AddWithValue("@gameID", gameId);
            connection.Open();
            command.ExecuteNonQuery();
        }
    }
}