using ECommons.DalamudServices;
using GraphQL;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UcobClears.Models;
using UcobClears.RawInformation;

namespace UcobClears.AdvPlate
{
    internal static class FFLogsApiSearch
    {
        private static readonly string GQL_Query =
            """
            query($name: String, $server: String, $region: String) {
                characterData{
                    character(name: $name, serverSlug: $server, serverRegion: $region) {
                        dawntrail: encounterRankings(encounterID: 1073)
                        endwalker: encounterRankings(encounterID: 1060)
                        shadowbringers: encounterRankings(encounterID: 1047)
                        stormblood: encounterRankings(encounterID: 1039)
                    }
                }
            }
            """;

        private static readonly string FFLOGS_API_ENDPOINT = "https://www.fflogs.com/api/v2";
        private static readonly string FFLOGS_API_OAUTH = "https://www.fflogs.com/oauth/token";

        private static FFLogsApiBearerToken? FFLOGS_TOKEN = null;
        private static DateTime? TOKEN_CREATED = null;

        private static Dictionary<string, FFLogsApiResponse_Data> CachedResponses = new Dictionary<string, FFLogsApiResponse_Data>();

        public static async Task<FFLogsStatus> GetUcobLogs_v2(string username, string server, bool ignoreCache = false)
        {
            if (ignoreCache || FFLOGS_TOKEN == null || (TOKEN_CREATED.HasValue && TOKEN_CREATED.Value.AddSeconds(FFLOGS_TOKEN.expires_in) < DateTime.Now))
            {
                TOKEN_CREATED = DateTime.Now;
                var newToken = await GetBearerToken();
                if (newToken == null)
                {
                    TOKEN_CREATED = null;
                    return new FFLogsStatus()
                    {
                        message = $"FFLogs Credentials Invalid.",
                        requestStatus = FFLogsRequestStatus.Failed
                    };
                }

                FFLOGS_TOKEN = newToken;
            } 
            
            var request = new GraphQLRequest()
            {
                Query = GQL_Query,
                Variables = new
                {
                    name = username,
                    server,
                    region = ConstantData.ServerRegionMap?.GetValueOrDefault(server)
                }
            };

            Svc.Log.Debug($"Creating request to FFLogs for {username} @ {server} ({ConstantData.ServerRegionMap?.GetValueOrDefault(server)})");
            Svc.Log.Debug($"Server Map: {ConstantData.ServerRegionMap?.Count ?? 0}");

            var graphQLClient = new GraphQLHttpClient(FFLOGS_API_ENDPOINT, new NewtonsoftJsonSerializer());
            graphQLClient.HttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {FFLOGS_TOKEN.access_token}");
            
            FFLogsApiResponse_Data? response = GetCachedValue(username, server);

            if (response == null || ignoreCache)
            {
                Svc.Log.Debug($"Fetching data from FFLogs.");
                var timeStarted = DateTime.Now;
                try
                {
                    var gqlResponse = await graphQLClient.SendQueryAsync<FFLogsApiResponse_Data>(request);
                    Svc.Log.Debug($"GQL Response: {gqlResponse?.Data}");

                    if (gqlResponse != null)
                    {
                        if (gqlResponse.Errors != null && gqlResponse.Errors.Length > 0)
                        {
                            Svc.Log.Debug(gqlResponse.Errors.FirstOrDefault()?.Message ?? "An error has occurred");

                            return new FFLogsStatus()
                            {
                                message = gqlResponse.Errors.FirstOrDefault()?.Message ?? "An error has occurred",
                                requestStatus = FFLogsRequestStatus.Failed
                            };
                        }

                        response = gqlResponse.Data;
                        response.timeFetched = timeStarted;

                        CachedResponses[$"{username}@{server}"] = response;
                    }
                }
                catch (Exception ex)
                {
                    Svc.Log.Error(ex.Message);
                    return new FFLogsStatus()
                    {
                        message = $"An unexpected error has occurred.",
                        requestStatus = FFLogsRequestStatus.Failed
                    };
                }
            }
            
            if (response != null)
            {
                if (response.characterData.character == null)
                {
                    return new FFLogsStatus()
                    {
                        message = $"Character not found.",
                        requestStatus = FFLogsRequestStatus.Failed
                    };
                }

                if (HiddenLogs(response))
                {
                    return new FFLogsStatus()
                    {
                        message = $"Hidden logs.",
                        requestStatus = FFLogsRequestStatus.Failed
                    };
                }

                int totalKills =
                       (response.characterData?.character?.stormblood?.totalKills ?? 0)
                       + (response.characterData?.character?.shadowbringers?.totalKills ?? 0)
                       + (response.characterData?.character?.endwalker?.totalKills ?? 0)
                       + (response.characterData?.character?.dawntrail?.totalKills ?? 0);

                return new FFLogsStatus()
                {
                    message = $"Total UCoB Kills: {totalKills}",
                    requestStatus = FFLogsRequestStatus.Success
                };
            }

            //In theory this is unreachable.
            Svc.Log.Debug("Null response.");
            return new FFLogsStatus()
            {
                requestStatus = FFLogsRequestStatus.Failed,
                message = "Null response."
            };
        }

        private static FFLogsApiResponse_Data? GetCachedValue(string username, string server)
        {
            var value = CachedResponses.GetValueOrDefault($"{username}@{server}");
            if (value == null) return null;

            if (!value.timeFetched.HasValue) return null;

            if (value.timeFetched.Value + TimeSpan.FromMinutes(P.Config.CacheValidityInMinutes) < DateTime.Now)
                return null;

            Svc.Log.Debug($"Valid cache found.");

            return value;
        }
    
        private static async Task<FFLogsApiBearerToken?> GetBearerToken()
        {
            var encodedCredentials = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes($"{P.Config.FFLogsAPI_ClientId}:{P.Config.FFLogsAPI_ClientSecret}"));

            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Basic {encodedCredentials}");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

            var nvc = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials")
            };

            var request = new HttpRequestMessage(HttpMethod.Post, FFLOGS_API_OAUTH)
            {
                Content = new FormUrlEncodedContent(nvc)
            };

            try
            {
                var response = await client.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;
                
                var json = await response.Content.ReadAsStringAsync();
                var token = JsonConvert.DeserializeObject<FFLogsApiBearerToken>(json);
                if (json == null) return null;

                Svc.Log.Debug($"Bearer token: {token.access_token}");
                return token;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    
        private static bool HiddenLogs(FFLogsApiResponse_Data response)
        {
            if (response.characterData.character.stormblood.error != null && response.characterData.character.stormblood.error.ToLower().Contains("permission"))
                return true;
            return false;
        }
    }

    internal class FFLogsApiBearerToken
    {
        public string token_type {  get; set; }
        public int expires_in {  get; set; }
        public string access_token {  get; set; }
    }

    internal class FFLogsApiResponse
    {
        public FFLogsApiResponse_Data? data { get; set; }
    }

    internal class FFLogsApiResponse_Data
    {
        public FFLogsApiResponse_CharacterData? characterData { get; set; }
        public DateTime? timeFetched { get; set; }
    }

    internal class FFLogsApiResponse_CharacterData
    {
        public FFLogsApiResponse_Character? character { get; set; }

    }

    internal class FFLogsApiResponse_Character
    {
        public FFLogsApiResponse_Encounter? dawntrail { get; set; }
        public FFLogsApiResponse_Encounter? endwalker { get; set; }
        public FFLogsApiResponse_Encounter? shadowbringers { get; set; }
        public FFLogsApiResponse_Encounter? stormblood { get; set; }

    }

    internal class FFLogsApiResponse_Encounter
    {
        public int? totalKills { get; set; } = 0;
        public string? error { get; set; } = null;
    }
}
