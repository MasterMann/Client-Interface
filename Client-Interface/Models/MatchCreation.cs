﻿using Newtonsoft.Json;
using ServerAppDemo.Models.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ServerAppDemo.Models
{
    public class MatchCreation
    {
        public ILeagueClient League;

        public async Task<bool> CheckIfLeagueIsOpen()
        {
            try
            {
                League = await LeagueClient.Connect();
            }
            catch (Exception e)
            {
                return false;
            }
            return true;
        }

        public async Task LogIn()
        {
            League = await LeagueClient.Connect();
            var region = await League.MakeApiRequest(HttpMethod.Get, "/riotclient/region-locale");
            var locals = JsonConvert.DeserializeObject<Region>(region.Content.ReadAsStringAsync().Result);

            Summoners sum = new Summoners(League);
            var player = await sum.GetCurrentSummoner();

            Summoner user = new Summoner();
            user.SummonerID = player.SummonerId.ToString();
            user.SummonerName = player.DisplayName;
            user.Region = locals.RegionRegion;
        }

        public async Task<string> GetSummonerId()
        {
            if (League == null)
            {
                League = await LeagueClient.Connect();
            }
            Summoners sum = new Summoners(League);
            var player = await sum.GetCurrentSummoner();
            return player.SummonerId.ToString();
        }

        public async Task<int> GetSummonerMMR(string summonerId)
        {
            var http = new HttpClient();
            var uri = "https://elorestapi.azurewebsites.net/api/Elo/GetOneVOneElo/" + summonerId;
            var response = await http.GetAsync(uri);
            var elo = JsonConvert.DeserializeObject<int>(await response.Content.ReadAsStringAsync());
            return elo;
        }


        public async Task CreateOneOnOneGame(string LobbyName, string enemyId)
        {
            long Enemyid = long.Parse(enemyId);
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
                ToSummonerId = Enemyid
            });
            await League.MakeApiRequest(HttpMethod.Post, "/lol-lobby/v2/lobby/invitations", invites);
            bool AllIn = false;
            while (!AllIn)
            {
                LobbyPlayerInfo[] players = await League.MakeApiRequestAs<LobbyPlayerInfo[]>(HttpMethod.Get, "/lol-lobby/v2/lobby/members");
                foreach (var item in players)
                {
                    if (item.SummonerId == Enemyid)
                    {
                        AllIn = true;
                    }
                }
            }
            await League.MakeApiRequest(HttpMethod.Post, "/lol-lobby/v1/lobby/custom/start-champ-select", new StartGame());
        }
        public async Task JoinGame(string en, string match)
        {
            long enemy = long.Parse(en);
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
                    if (item.FromSummonerId == enemy)
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