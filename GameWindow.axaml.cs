using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MySql.Data.MySqlClient;

namespace TicTacToe;

public partial class GameWindow : Window
{
    private readonly string _connectionString = $"server=46.40.3.35; user=33bakos; database = 33bakos_tictactoe";
    private readonly string _playerId;
    private string _gameCode;
    private int _gameId;
    private string _currentPlayer;
    private string _playerRole;
    private bool _gameOver;
    
    public GameWindow(string gameCode, string playerId)
    {
        InitializeComponent();
        _gameCode = gameCode;
        _playerId = playerId;
        InitializeGame();
        StartAutoRefresh();
    }

    private void InitializeGame()
    {
        using (var connection = new MySqlConnection(_connectionString))
        {
            connection.Open();

            var command = new MySqlCommand(
                    "SELECT GameId, Player1, Player2 FROM Games WHERE GameCode = @gameCode AND IsActive = TRUE",
                    connection);
            command.Parameters.AddWithValue("@gameCode", _gameCode);

            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    _gameId = reader.GetInt32(0);
                    string player1 = reader.GetString(1);
                    string? player2 = reader.IsDBNull(2) ? null : reader.GetString(2);

                    if (player1 == _playerId)
                    {
                        _playerRole = "Player1";
                        _currentPlayer = "X";
                    }
                    else if (player2 == _playerId)
                    {
                        _playerRole = "Player2";
                        _currentPlayer = "O";
                    }
                    else
                    {
                        throw new Exception("You are not part of this game");
                    }
                }
                else
                {
                    throw new Exception("Game not found or inactive.");
                }
            }
        }

        Title = $"Tic Tac Toe - {_playerRole}";
    }

    private async void StartAutoRefresh()
    {
        while (!_gameOver)
        {
            RefreshBoard();
            await Task.Delay(2000);
        }
    }

    private void RefreshBoard()
    {
        using (var connection = new MySqlConnection(_connectionString))
        {
            connection.Open();

            var command = new MySqlCommand(
                "SELECT CellIndex, Player FROM Moves Where GameId = @gameId ORDER BY MoveTime",
                connection);
            command.Parameters.AddWithValue("@gameId", _gameId);

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    int cellIndex = reader.GetInt32(0);
                    string player = reader.GetString(1);

                    var cell = this.FindControl<Button>($"Cell{cellIndex}");
                    if (cell != null && string.IsNullOrEmpty(cell.Content?.ToString()))
                    {
                        cell.Content = player;
                        cell.IsEnabled = false;
                    }
                }
            }
        }

        if (CheckWinner())
        {
            _gameOver = true;
            UpdateGameStatus(false);
            return;
        }

        if (IsBoardFull())
        {
            _gameOver = true;
            UpdateGameStatus(false);
        }
    }

    private bool CheckWinner()
    {
        var board = Enumerable.Range(0, 9)
            .Select(i => this.FindControl<Button>($"Cell{i}")?.Content?.ToString())
            .ToArray();

        int[,] winningCombinations = new int[,]
        {
            { 0, 1, 2 }, { 3, 4, 5 }, { 6, 7, 8 }, // Rows
            { 0, 3, 6 }, { 1, 4, 7 }, { 2, 5, 8 }, // Columns
            { 0, 4, 8 }, { 2, 4, 6 } // Diagonals
        };

        for (int i = 0; i < winningCombinations.GetLength(0); i++)
        {
            if (board[winningCombinations[i, 0]] == _currentPlayer &&
                board[winningCombinations[i, 1]] == _currentPlayer &&
                board[winningCombinations[i, 2]] == _currentPlayer)
            {
                return true;
            }
        }
        
        return false;
    }

    private bool IsBoardFull()
    {
        for (int i = 0; i < 9; i++)
        {
            var cell = this.FindControl<Button>($"Cell{i}");
            if (cell != null && string.IsNullOrEmpty(cell.Content?.ToString()))
            {
                return false;
            }
        }

        return true;
    }

    private void UpdateGameStatus(bool isActive)
    {
        using (var connection = new MySqlConnection(_connectionString))
        {
            var command = new MySqlCommand(
                "UPDATE Games SET IsActive = @isActive WHERE GameId = @gameId",
                connection);
            command.Parameters.AddWithValue("@gameId", _gameId);
            command.Parameters.AddWithValue("@isActive", isActive);
            connection.Open();
            command.ExecuteNonQuery();
        }
    }
    
    private void Cell_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_gameOver)
            return;

        if (sender is Button button)
        {
            int cellIndex = int.Parse(button.Name.Substring(4));

            SaveMove(cellIndex);
            
            RefreshBoard();
        }
    }

    private void SaveMove(int cellIndex)
    {
        using (var connection = new MySqlConnection(_connectionString))
        {
            var command = new MySqlCommand(
                "INSERT INTO Moves (GameId, Player, CellIndex) VALUES (@gameId, @player, @cellIndex)",
                connection);
            command.Parameters.AddWithValue("@gameId", _gameId);
            command.Parameters.AddWithValue("@player", _currentPlayer);
            command.Parameters.AddWithValue("@cellIndex", cellIndex);
            connection.Open();
            command.ExecuteNonQuery();
        }

        _currentPlayer = _currentPlayer == "X" ? "O" : "X"; //switch the turns
    }
}