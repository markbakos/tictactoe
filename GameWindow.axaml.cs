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
    private readonly string _gameCode;
    private string _playerRole;
    private int _gameId;
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
                    "SELECT GameId, Player1, Player2, CurrentTurn, IsActive FROM Games WHERE GameCode = @gameCode AND IsActive = TRUE",
                    connection);
            command.Parameters.AddWithValue("@gameCode", _gameCode);

            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    _gameId = reader.GetInt32(0);
                    string player1 = reader.GetString(1);
                    string player2 = reader.IsDBNull(2) ? null : reader.GetString(2);
                    string currentTurn = reader.GetString(3);
                    _gameOver = !reader.GetBoolean(4);

                    _playerRole = _playerId == player1 ? "Player1" : "Player2";

                    if (_playerRole == "Player2" && player2 != _playerId)
                    {
                        throw new Exception("You are not part of this game");
                    }
                    
                    Title = $"Tic Tac Toe - {_playerRole}";
                }
                else
                {
                    throw new Exception("Game not found or inactive.");
                }
            }
        }
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

            var movesCommand = new MySqlCommand("SELECT CellIndex, Player FROM Moves Where GameId = @gameId ORDER BY MoveTime", connection);
            movesCommand.Parameters.AddWithValue("@gameId", _gameId);

            using (var reader = movesCommand.ExecuteReader())
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

            var gameStateCommand = new MySqlCommand("SELECT CurrentTurn, IsActive FROM Games WHERE GameId = @gameId", connection);
            gameStateCommand.Parameters.AddWithValue("@gameId", _gameId);

            using (var reader = gameStateCommand.ExecuteReader())
            {
                if (reader.Read())
                {
                    string currentTurn = reader.GetString(0);
                    _gameOver = !reader.GetBoolean(1);
                    
                    TurnText.Text = _gameOver ? "Game Over" : (currentTurn == _playerRole ? "It's your turn" : "Waiting for opponent");
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
            TurnText.Text = "Draw";
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
            if (!string.IsNullOrEmpty(board[winningCombinations[i, 0]]) &&
                board[winningCombinations[i, 0]] == board[winningCombinations[i, 1]] &&
                board[winningCombinations[i, 1]] == board[winningCombinations[i, 2]])
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

            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();

                var checkCellCommand = new MySqlCommand("SELECT Player FROM Moves WHERE GameId = @gameId AND CellIndex = @cellIndex", connection);
                checkCellCommand.Parameters.AddWithValue("@gameId", _gameId);
                checkCellCommand.Parameters.AddWithValue("@cellIndex", cellIndex);
                
                var existingMove = checkCellCommand.ExecuteScalar()?.ToString();
                if (!string.IsNullOrEmpty(existingMove))
                {
                    return;
                }
                
                var turnCommand = new MySqlCommand("SELECT CurrentTurn FROM Games WHERE GameId = @gameId", connection);
                turnCommand.Parameters.AddWithValue("@gameId", _gameId);
                string currentTurn = turnCommand.ExecuteScalar()?.ToString();

                if (currentTurn != _playerRole)
                {
                    TurnText.Text = "Not your turn";
                    return;
                }
                
                var moveCommand = new MySqlCommand("INSERT INTO Moves (GameId, Player, CellIndex) VALUES (@gameId, @player, @cellIndex)",
                    connection);
                moveCommand.Parameters.AddWithValue("@gameId", _gameId);
                moveCommand.Parameters.AddWithValue("@player", _playerRole);
                moveCommand.Parameters.AddWithValue("@cellIndex", cellIndex);
                moveCommand.ExecuteNonQuery();
                
                var nextTurn = _playerRole == "Player1" ? "Player2" : "Player1";
                var updateTurnCommand = new MySqlCommand("UPDATE Games SET CurrentTurn = @nextTurn WHERE GameId = @gameId", connection);
                updateTurnCommand.Parameters.AddWithValue("@nextTurn", nextTurn);
                updateTurnCommand.Parameters.AddWithValue("@gameId", _gameId);
                updateTurnCommand.ExecuteNonQuery();
            }
            
            RefreshBoard();
        }
    }
}