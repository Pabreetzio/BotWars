﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;
using System.Web.Mvc;
using Microsoft.AspNet.SignalR;
using NetBots.Core;
using NetBots.GameEngine;
using NetBots.Web;
using NetBots.WebServer.Data.MsSql;
using NetBots.WebServer.Host.Hubs;
using NetBots.WebServer.Host.Models;
using NetBots.WebServer.Model;
using NetBotsHostProject.Models;
using Newtonsoft.Json;

namespace NetBotsHostProject.Hubs
{
    public class WarGameManager
    {
        readonly ApplicationDbContext _db = new ApplicationDbContext();
        private const int TurnLimit = 200;

        public async Task<string> StartGame(int bot1Id, int bot2Id, string userId)
        {
            try
            {
                var bot1 = _db.PlayerBots.FirstOrDefault(x => x.Id == bot1Id);
                var bot2 = _db.PlayerBots.FirstOrDefault(x => x.Id == bot2Id);
                if (bot1 != null && bot2 != null)
                {
                    GameState startingState = GetNewGameState();
                    Game game = new Game(startingState, bot1.URL, bot2.URL);
                    game.UpdateGameState(new List<PlayerMoves>()); //Do this to get the starting bots to spawn.
                    //var hub = GlobalHost.ConnectionManager.GetHubContext<WarViewHub>();

                    int currentTurn = 1;
                    while (game.GameState.Winner == null && currentTurn < TurnLimit)
                    {

                        var delay = Task.Delay(200);
                        var myTasks = game.Players.Select(p => GetPlayerMovesAsync(p, game.GameState));
                        var httpMoves = await Task.WhenAll(myTasks);
                        game.UpdateGameState(httpMoves.Select(x => x.PlayerMoves));
                        var model = GetWarViewModel(game, bot1.Name, bot2.Name);
                        if (!ValidateMoves(httpMoves, game))
                        {
                            model.Alert = GetAlert(httpMoves);
                        }
                        WarViewHub.SendGameState(model, userId);
                        await delay;
                        currentTurn++;
                    }
                    SaveGameResult(bot1.Id, bot2.Id, game);
                    var finalModel = GetWarViewModel(game, bot1.Name, bot2.Name);
                    WarViewHub.SendGameState(finalModel, userId);
                    await Task.WhenAll(game.Players.Select(p => GetPlayerMovesAsync(p, game.GameState)));
                    return game.GameState.Winner;
                }
                return null;
            }
            catch (Exception ex)
            {
                //Do some logging here eventually. 
                //The main thing is it returns OK to the front end so it knows everything is over.
                var ex2 = ex;
                return null;
            }
        }



        private string GetAlert(HttpMove[] httpMoves)
        {
            var alert = "";
            foreach (var httpMove in httpMoves.Where(x => x.HttpMoveResponse == HttpMoveResponse.Error))
            {
                alert += httpMove.PlayerMoves.PlayerName + " returned an error:\r\n";
                alert += httpMove.Exception.Message + "\r\n";
            }
            return alert;
        }

        private bool ValidateMoves(HttpMove[] httpMoves, Game game)
        {
            if (httpMoves.Any(x => x.HttpMoveResponse == HttpMoveResponse.Error))
            {
                //If both returned an error, don't set a winner. 
                if (httpMoves.Any(x => x.HttpMoveResponse != HttpMoveResponse.Error))
                {
                    var badOne = httpMoves.First(x => x.HttpMoveResponse == HttpMoveResponse.Error);
                    var winnerName = badOne.PlayerMoves.PlayerName.ToLower() == "p1" ? "p2" : "p1";
                    game.GameState.Winner = winnerName;
                }
                return false;
            }
            return true;
        }

        private static WarViewModel GetWarViewModel(Game game, string p1Name, string p2Name)
        {
            var model = new WarViewModel()
            {
                P1Name = p1Name,
                P2Name = p2Name,
                State = game.GameState
            };
            return model;
        }

        private void SaveGameResult(int bot1Id, int bot2Id, Game game)
        {

            var p1 = _db.PlayerBots.First(x => x.Id == bot1Id);
            var p2 = _db.PlayerBots.First(x => x.Id == bot2Id);
            if (String.IsNullOrWhiteSpace(game.GameState.Winner))
            {
                //If the game progressed to the turn limit, the winner won't be set yet, so we do it here.
                SetWinnerByBotCount(game);
            }
            var gameResult = new GameSummary()
            {
                Player1 = p1,
                Player2 = p2,
                TournamentGame = false,
                Winner = game.GameState.Winner == "p1" ? p1 : game.GameState.Winner == "p2" ? p2 : null
            };
            _db.GameSummaries.Add(gameResult);
            _db.SaveChanges();
        }

        private static void SetWinnerByBotCount(Game game)
        {
            var p1Count = game.GameState.Grid.Count(x => x == '1');
            var p2Count = game.GameState.Grid.Count(x => x == '2');
            if (p1Count > p2Count)
                game.GameState.Winner = "p1";
            else if (p2Count > p1Count)
                game.GameState.Winner = "p2";
        }

        public static async Task<HttpMove> GetPlayerMovesAsync(BotPlayer player, GameState gameState)
        {
            try
            {
                MoveRequest moveRequest = new MoveRequest() { State = gameState, Player = player.PlayerName };
                string jsonMoveRequest = JsonConvert.SerializeObject(moveRequest);
                HttpClient client = GetClient(player.Uri);
                var content = new StringContent(jsonMoveRequest, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(player.Uri, content);
                response.EnsureSuccessStatusCode();
                var responseJson = await response.Content.ReadAsStringAsync();
                var moves = JsonConvert.DeserializeObject<List<BotletMove>>(responseJson);
                var playerMove = new PlayerMoves() { Moves = moves, PlayerName = player.PlayerName };
                var httpMove = new HttpMove()
                {
                    HttpMoveResponse = HttpMoveResponse.OK,
                    PlayerMoves = playerMove
                };
                return httpMove;
            }
            catch (TaskCanceledException ex)
            {
                //This means the Http Client timed out. We return an empy move list instead.
                return GetEmptyHttpMove(player.PlayerName, HttpMoveResponse.Timeout);
            }
            catch (HttpRequestException ex)
            {
                //This means a 400 returned or something like that, which is a disqualification.
                return GetEmptyHttpMove(player.PlayerName, HttpMoveResponse.Error, ex);
            }
        }

        private static HttpMove GetEmptyHttpMove(string pName, HttpMoveResponse response, HttpRequestException ex = null)
        {
            var pMove = new PlayerMoves()
            {
                Moves = new BotletMove[0],
                PlayerName = pName
            };
            var httpMove = new HttpMove()
            {
                PlayerMoves = pMove,
                HttpMoveResponse = response,
                Exception = ex
            };
            return httpMove;
        }

        private static HttpClient GetClient(string botUrl)
        {
            var cache = System.Web.HttpContext.Current.Cache;
            var client = cache[botUrl] as HttpClient;
            if (client == null)
            {
                client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(3);
                cache.Add(botUrl, client, null, Cache.NoAbsoluteExpiration, TimeSpan.FromMinutes(5), CacheItemPriority.High, null);
            }
            return client;
        }

        private GameSettings GetGameSettings()
        {
            string[] userData;
            string dataFile = System.Web.HttpContext.Current.Server.MapPath("~/App_Data/GameSettings.json");
            if (System.IO.File.Exists(dataFile))
            {
                userData = System.IO.File.ReadAllLines(dataFile);
                if (userData.Length == 0)
                {
                    throw new Exception("The file is empty.");
                }
            }
            else
            {
                throw new Exception("The file does not exist.");
            }

            return JsonConvert.DeserializeObject<GameSettings>(string.Join("\n", userData));
        }

        public GameState GetNewGameState()
        {
            GameSettings settings = GetGameSettings();

            var startingGame = new GameState()
            {
                Rows = settings.boardSize,
                Cols = settings.boardSize,
                P1 = new Player() { Energy = 1, Spawn = settings.boardSize + 1 },
                P2 = new Player() { Energy = 1, Spawn = settings.boardSize * (settings.boardSize - 1) - 2 },
                Grid = new string('.', settings.boardSize * settings.boardSize),
                MaxTurns = 200,
                TurnsElapsed = 0,
                GameId = GameHelper.GenerateRandomGameId()
            };
            return startingGame;
        }

        public Game GetGame(GameState gamestate, string side1Url, string side2Url, int seed)
        {
            if (seed != 0)
            {
                return new Game(gamestate, side1Url, side2Url);
            }
            else
            {
                return new Game(gamestate, side1Url, side2Url, seed);
            }
        }

        public async Task<PlayerBot> GetPlayerBot(int id)
        {
            if (id > 0)
            {
                var bot = await _db.PlayerBots.FirstOrDefaultAsync(x => x.Id == id);
                if (bot != null)
                {
                    return bot;
                }
            }
            return null;
        }

    }
}