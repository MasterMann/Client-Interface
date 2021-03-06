﻿using Newtonsoft.Json;
using ServerAppDemo.Models.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace ServerAppDemo.Models
{
    public class MatchCreation
    {
        public ILeagueClient League;
        public delegate void LeagueClientClosed();
        public event LeagueClientClosed CheckIfLeagueOpens;

        public async Task<bool> CheckIfLeagueIsOpen()
        {
            try
            {
                League = await LeagueClient.Connect();
                if(League != null)
                {
                    League.LeagueClosed += () => CheckIfLeagueOpens.Invoke();
                }
            }
            catch (Exception e)
            {
                return false;
            }
            return true;
        }

        public async Task<Summoner> GetSummoner()
        {
            if (League == null)
            {
                League = await LeagueClient.Connect();
            }
            try
            {
                var region = await League.MakeApiRequest(HttpMethod.Get, "/riotclient/get_region_locale");
                var locals = JsonConvert.DeserializeObject<Region>(region.Content.ReadAsStringAsync().Result);

                Summoners sum = new Summoners(League);
                var player = await sum.GetCurrentSummoner();
                if(player == null || player.SummonerId == null)
                {
                    return null;
                }

                Summoner user = new Summoner();
                user.SummonerID = player.SummonerId.ToString();
                user.SummonerName = player.DisplayName;
                user.Region = (Regions)Enum.Parse(typeof(Regions), locals.RegionRegion);
                return user;
            }

            catch
            {
                return null;
            }
       
        }

        public async Task CheckIfUserExists()
        {
            var player = await GetSummoner();
            if(player == null)
            {
                return;
            }
            var http = new HttpClient();
            var uri = "https://elorestapi.azurewebsites.net/api/Elo/PlayerExist";
            var content = new StringContent(JsonConvert.SerializeObject(player), Encoding.UTF8, "application/json");
            var r = await http.PostAsync(uri, content);
        }
        public async Task<int> GetSummonerMMR(string summonerId)
        {
            var http = new HttpClient();
            var uri = "https://elorestapi.azurewebsites.net/api/Elo/GetOneVOneMMR/" + summonerId;
            var response = await http.GetAsync(uri);
            var elo = JsonConvert.DeserializeObject<int>(await response.Content.ReadAsStringAsync());
            return elo;
        }
        public async Task CreateOneOnOneGame(string LobbyName, string enemyId)
        {
            if (League == null)
            {
                League = await LeagueClient.Connect();
            }
            ApiObject api = new ApiObject();
            var obj = api.createCustomGameOneOnOne(LobbyName);
            var response = await League.MakeApiRequest(HttpMethod.Post, "/lol-lobby/v2/lobby", obj);

            while (true)
            {
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    obj = api.createCustomGameOneOnOne(LobbyName);
                    response = League.MakeApiRequest(HttpMethod.Post, "/lol-lobby/v2/lobby", obj).Result;
                }
                else
                {
                    break;
                }
            }

            var invites = new List<LobbyInvitation>();

            invites.Add(new LobbyInvitation
            {
                ToSummonerId = enemyId
            });
            await League.MakeApiRequest(HttpMethod.Post, "/lol-lobby/v2/lobby/invitations", invites);
            bool AllIn = false;
            while (!AllIn)
            {
                LobbyPlayerInfo[] players = await League.MakeApiRequestAs<LobbyPlayerInfo[]>(HttpMethod.Get, "/lol-lobby/v2/lobby/members");
                foreach (var item in players)
                {
                    if (item.SummonerId == enemyId)
                    {
                        AllIn = true;
                    }
                }
            }
            await League.MakeApiRequest(HttpMethod.Post, "/lol-lobby/v1/lobby/custom/start-champ-select", new StartGame());
        }
        public async Task JoinGame(string enemyId, string match)
        {
            bool matchAccepted = false;
            if (League == null)
            {
                League = await LeagueClient.Connect();
            }
            while (!matchAccepted)
            {
                var response = await League.MakeApiRequest(HttpMethod.Get, "/lol-lobby/v2/received-invitations");
                var invites = JsonConvert.DeserializeObject<List<InviteModel>>(await response.Content.ReadAsStringAsync());

                foreach (var item in invites)
                {
                    if (item.FromSummonerId == enemyId && item.State == "Pending")
                    {
                        await League.MakeApiRequest(HttpMethod.Post, "/lol-lobby/v2/received-invitations/" + item.InvitationId + "/accept");
                        System.Net.Http.HttpClient http = new System.Net.Http.HttpClient();
                        matchAccepted = true;
                    }
                }
                await Task.Delay(100);
            }
        }

    }
}
