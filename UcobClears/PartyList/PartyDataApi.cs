using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UcobClears.Models;
using System.Text.Json;
using System.Net.WebSockets;
using System.Threading;

namespace UcobClears.PartyList
{
    internal static class PartyDataApi
    {
        public static bool isWebSocketOpen = false;
        public static ClientWebSocket? webSocket;
        private static CancellationTokenSource? cancellationTokenSource;

        public static async Task<bool> SetupWebSocketConnection()
        {
            if (!P.Config.UseWebSocket || string.IsNullOrWhiteSpace(P.Config.UploadPartyDataUrl) || string.IsNullOrWhiteSpace(P.Config.WebSocketPort) || !int.TryParse(P.Config.WebSocketPort, out var WebSocketPort)) return false;

            if (webSocket != null)
            {
                if (webSocket.State == WebSocketState.Open) return true;
                else webSocket.Dispose();
                isWebSocketOpen = false;
            }

            try
            {
                webSocket = new ClientWebSocket();

                if (cancellationTokenSource != null) cancellationTokenSource.Dispose();

                cancellationTokenSource = new CancellationTokenSource();
                Svc.Log.Verbose($"Attempting WebSocket connetion to {P.Config.UploadPartyDataUrl}:{WebSocketPort}.");

                await webSocket.ConnectAsync(new Uri($"{P.Config.UploadPartyDataUrl}:{WebSocketPort}"), cancellationTokenSource.Token);

                Svc.Log.Verbose($"WebSocket connetion to {P.Config.UploadPartyDataUrl}:{WebSocketPort} opened.");

                isWebSocketOpen = true;
                return true;
            }
            catch (Exception ex) {
                Svc.Log.Error(ex.Message);
                if (ex.InnerException != null)
                {
                    Svc.Log.Error(ex.InnerException.Message);
                    if (ex.InnerException.InnerException != null)
                    {
                        Svc.Log.Error(ex.InnerException.InnerException.Message);
                    }
                }

                Svc.Chat.Print("An error has occurred with communication with the websocket.", "UcobClears");
                return false; 
            }
        }

        public static void CloseWebSocketConnection()
        {
            if (webSocket != null)
            {
                webSocket.Dispose();
                webSocket = null;

                if (cancellationTokenSource != null)
                {
                    cancellationTokenSource.Dispose();
                    cancellationTokenSource = null;
                }

                isWebSocketOpen = false;
            }
        }

        public static async Task UploadPartyData(PartyData party)
        {
            if (string.IsNullOrWhiteSpace(P.Config.UploadPartyDataUrl)) return;

            Svc.Log.Verbose($"Party: {party.party.Count}, Kill Sum: {party.party.Sum(x => x.count)}");

            using var httpClient = new HttpClient() { BaseAddress = new Uri(P.Config.UploadPartyDataUrl) };
            //ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            var data = JsonSerializer.Serialize(party);
            var content = new StringContent(data, Encoding.UTF8, "application/json");

            if (!string.IsNullOrWhiteSpace(P.Config.UploadPartyDataAPIKey))
            {
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", P.Config.UploadPartyDataAPIKey);
            }

            try
            {
                var result = await httpClient.PostAsync($"Ucob/party", content);
                Svc.Log.Verbose($"Party Data sent to: {P.Config.UploadPartyDataUrl}Ucob/party");
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex.Message);
                if (ex.InnerException != null)
                    Svc.Log.Error(ex.InnerException.Message);

                Svc.Chat.Print("An error has occurred with communication with the server.", "UcobClears");
            }
        }

        public static async Task SendViaWebsocket(PartyData party)
        {
            if (string.IsNullOrWhiteSpace(P.Config.UploadPartyDataUrl) || string.IsNullOrWhiteSpace(P.Config.WebSocketPort)) return;

            try
            {
                if (webSocket == null || webSocket.State != WebSocketState.Open)
                {
                    var webSocketOpen = await SetupWebSocketConnection();
                    if (!webSocketOpen)
                        return;
                }

                var data = JsonSerializer.Serialize(party);
                var buffer = Encoding.UTF8.GetBytes(data);
                Svc.Log.Verbose($"Sending {buffer.Length} bytes via websocket connection.");

                await webSocket!.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cancellationTokenSource!.Token);
            }
            catch (Exception ex) 
            {
                Svc.Log.Error(ex.Message);
                if (ex.InnerException != null)
                    Svc.Log.Error(ex.InnerException.Message);

                Svc.Chat.Print("An error has occurred with communication with the websocket.", "UcobClears");
            }
        }
    }
}
