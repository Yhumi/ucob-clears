using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.ClientState.Party;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Dalamud.Bindings.ImGui;
using OtterGui.Log;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UcobClears.AdvPlate;
using UcobClears.Models;
using UcobClears.RawInformation;

namespace UcobClears.PartyList
{
    class PartyListController
    {
        private int partyListSize = -1;
        private event EventHandler OnPartyListChange;

        public PartyListController()
        {
            if (P.Config.UploadPartyClearsData)
                AddEventHandling();
        }

        public void AddEventHandling()
        {
            partyListSize = -1; //Forces an update
            Svc.Framework.Update += Tick;
            OnPartyListChange += PartyListChange;

            Svc.DutyState.DutyCompleted += DutyComplete;
            Svc.ClientState.TerritoryChanged += TerritoryChanged;
        }

        private void TerritoryChanged(ushort obj)
        {
            Svc.Log.Verbose($"T changed");
        }

        private void DutyComplete(object? sender, ushort e)
        {
            Svc.Log.Verbose($"Duty Complete: {e}");
            if (e == 280)
                HandlePartyList();
        }

        public void RemoveEventHandling()
        {
            Svc.Framework.Update -= Tick;
            OnPartyListChange -= PartyListChange;
        }

        private void PartyListChange(object? sender, EventArgs e)
        {
            Svc.Log.Verbose($"[PLC] PartyListChange");
            HandlePartyList();
        }

        private void HandlePartyList()
        {
            List<PartyDataMember> partyMembers = [];

            if (!Svc.ClientState.IsLoggedIn)
                return;

            if (InfoProxyCrossRealm.IsCrossRealmParty())
                partyMembers = HandleCrossWorldParty();
            else if (Svc.Party.Count > 0)
                partyMembers = HandleLocalParty();
            else
                partyMembers = HandleSolo();

            var localPlayerName = Svc.ClientState.LocalPlayer?.Name.ToString();
            var localPlayerWorld = Svc.ClientState.LocalPlayer?.HomeWorld.Value.Name.ToString();
            Svc.Framework.RunOnFrameworkThread(() => UploadPartyData(partyMembers, localPlayerName, localPlayerWorld));
        }

        private unsafe List<PartyDataMember> HandleLocalParty()
        {
            var group = GroupManager.Instance()->MainGroup;
            List<PartyDataMember> partyMembers = [];

            for (var i = 0; i < group.MemberCount; i++)
            {
                var member = group.GetPartyMemberByIndex(i);
                var world = LuminaData.GetWorld(member->HomeWorld);
                Svc.Log.Verbose($"{member->NameString} @ {world?.Name ?? "null"}");

                if (world.HasValue)
                {
                    partyMembers.Add(new PartyDataMember()
                    {
                        username = member->NameString,
                        world = world.Value.Name.ToString()
                    });
                }
            }

            return partyMembers;
        }

        private unsafe List<PartyDataMember> HandleCrossWorldParty()
        {
            var party = InfoProxyCrossRealm.Instance()->CrossRealmGroups[InfoProxyCrossRealm.Instance()->LocalPlayerGroupIndex];
            List<PartyDataMember> partyMembers = [];

            for (var i = 0; i < party.GroupMemberCount; i++)
            {
                var member = party.GroupMembers[i];
                var world = LuminaData.GetWorld((ushort)member.HomeWorld);
                Svc.Log.Verbose($"{member.NameString} @ {world?.Name ?? "null"}");

                if (world.HasValue)
                {
                    partyMembers.Add(new PartyDataMember()
                    {
                       username = member.NameString,
                       world = world.Value.Name.ToString()
                    });
                }   
            }

            return partyMembers;
        }

        private unsafe List<PartyDataMember> HandleSolo()
        {
            var player = Svc.ClientState.LocalPlayer;
            List<PartyDataMember> partyMembers = [];

            Svc.Log.Verbose($"{player.Name} @ {player.HomeWorld.Value.Name}");
            partyMembers.Add(new PartyDataMember() { username = player.Name.ToString(), world = player.HomeWorld.Value.Name.ToString() });

            return partyMembers;
        }

        private async void UploadPartyData(List<PartyDataMember> partyMembers, string? partyLeader, string? partyLeaderWorld)
        {
            //Get their kills (fancy parallelization)
            await Parallel.ForEachAsync(partyMembers, async (partyMember, cancellation) =>
            {
                //Force set cache validity
                var logs = await FFLogsApiSearch.GetUcobLogs_v2(partyMember.username, partyMember.world, overwriteCacheValidity: P.Config.UploadPartyCacheValidityInMinutes);
                partyMember.count = logs.kills;
            });

            try
            {
                PartyData party = new()
                {
                    username = partyLeader ?? string.Empty,
                    world = partyLeaderWorld ?? string.Empty,
                    party = partyMembers
                };

                if (!string.IsNullOrEmpty(party.username))
                {
                    if (P.Config.UseWebSocket)
                        await PartyDataApi.SendViaWebsocket(party);
                    else
                        await PartyDataApi.UploadPartyData(party);
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex.Message);
                if (ex.InnerException != null)
                    Svc.Log.Error(ex.InnerException.Message);

                Svc.Chat.Print("An error has occurred with communication with the websocket.", "UcobClears");
            }
        }

        public unsafe void Tick(object _)
        {
            if (!Svc.ClientState.IsLoggedIn)
                return;

            if (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]) return;
            if (!EzThrottler.Throttle("partyListCheck", 500)) return;

            int partyLength = 0;

            //Alliances needs fixing for cross-realm
            if (InfoProxyCrossRealm.IsCrossRealmParty())
                partyLength = InfoProxyCrossRealm.Instance()->CrossRealmGroups[InfoProxyCrossRealm.Instance()->LocalPlayerGroupIndex].GroupMemberCount;
            else if (GroupManager.Instance()->MainGroup.MemberCount > 0)
                partyLength = GroupManager.Instance()->MainGroup.MemberCount;
            else
                partyLength = 1;

            //Svc.Log.Verbose($"[PLC] Party Size: {partyLength}");
            //Svc.Log.Verbose($"[PLC] Previous Size: {partyListSize}");

            if (partyLength != partyListSize)
                OnPartyListChange?.Invoke(this, EventArgs.Empty);

            partyListSize = partyLength;
        }

        public void Dispose()
        {
            if (P.Config.UploadPartyClearsData && !string.IsNullOrWhiteSpace(P.Config.UploadPartyDataUrl))
                RemoveEventHandling();
        }
    }
}
