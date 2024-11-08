using Dalamud.Game.Addon.Events;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Extensions;
using KamiToolKit.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using UcobClears.Models;

namespace UcobClears.AdvPlate
{
    internal class AdvPlateController : IDisposable
    {
        private TextNode? FFLogsResponseNode;
        
        private nint addon = nint.Zero;
        private string username = string.Empty;
        private string server = string.Empty;

        public AdvPlateController()
        {
            Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "CharaCard", OnAddonSetup);
            Svc.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "CharaCard", OnAddonFinalize);
            //Svc.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, "CharaCard", OnAddonRefresh);
        }

        public void Dispose()
        {
            Svc.AddonLifecycle.UnregisterListener(OnAddonSetup, OnAddonFinalize);// ,OnAddonRefresh);

            var addon = Svc.GameGui.GetAddonByName("CharaCard");
            if (addon != IntPtr.Zero)
            {
                RemoveNodeFromPlate(addon);
            }
        }

        private void OnAddonSetup(AddonEvent type, AddonArgs args)
        {
            addon = args.Addon;
            GetUserDetailsFromCard(args.Addon);

            Task.Run(() => LoadFFlogs(args.Addon, username, server));
        }

        private void OnAddonFinalize(AddonEvent type, AddonArgs args)
        {
            addon = nint.Zero;
            username = string.Empty;
            server = string.Empty;
            RemoveNodeFromPlate(args.Addon);
        }

        private unsafe void GetUserDetailsFromCard(nint charCardnint)
        {
            if (charCardnint == IntPtr.Zero)
                return;

            var charCard = (AtkUnitBase*)charCardnint;
            if (charCard == null)
                return;

            if (charCard->UldManager.NodeListCount > 1)
            {
                if (charCard->UldManager.SearchNodeById(20)->IsVisible())
                {
                    var atkValues = charCard->AtkValues;
                    var usernameString = SeString.Parse(charCard->AtkValues[29].String).TextValue;
                    Svc.Log.Debug(usernameString);

                    var serverString = SeString.Parse(charCard->AtkValues[32].String).TextValue;
                    Svc.Log.Debug(serverString);

                    username = usernameString;
                    server = serverString.Split('[')[0].Trim();
                    return;
                }
            }
        }

        private async void LoadFFlogs(nint addonInt, string server, string name, bool ignoreCache = false)
        {
            var fflogsResponse = await FFLogsApiSearch.GetUcobLogs_v2(server, name, ignoreCache);

            if (fflogsResponse.checkProg.HasValue && fflogsResponse.checkProg.Value)
            {
                Svc.Log.Debug($"Checking progression logs...");
                var tomestoneResponse = await FFLogsApiSearch.GetUcobProg(server, name);
                if (tomestoneResponse != null)
                {
                    if (tomestoneResponse.encounters?.ultimateProgressionTarget?.name?.ToLower() == "ucob")
                    {
                        Svc.Log.Debug($"UCoB Prog Found, {tomestoneResponse.encounters?.ultimateProgressionTarget?.percent}");
                        fflogsResponse.message += $" [{tomestoneResponse.encounters?.ultimateProgressionTarget?.percent}]";
                    }
                }
            }

            Svc.Log.Debug($"{fflogsResponse.requestStatus.ToString()}: {fflogsResponse.message}");
            AddNodeToPlate(addonInt, fflogsResponse);
        }

        private unsafe void AddNodeToPlate(nint charCardnint, FFLogsStatus logsStatus)
        {
            if (logsStatus == null) return;
            if (!P.Config.ShowErrorsOnPlate && logsStatus.requestStatus == FFLogsRequestStatus.Failed) return;

            if (charCardnint == IntPtr.Zero)
                return;

            var charCard = (AtkUnitBase*)charCardnint;
            if (charCard == null)
                return;

            if (charCard->UldManager.NodeListCount <= 1)
                return;

            AtkComponentNode* textNodeParent;
            AtkTextNode* textNode;
            try
            {
                textNodeParent = charCard->UldManager.SearchNodeById(5)->GetAsAtkComponentNode();
                textNode = textNodeParent->GetComponent()->UldManager.SearchNodeById(3)->GetAsAtkTextNode();
            }
            catch (Exception e)
            {
                Svc.Log.Debug($"An error has occurred: {e.Message}");
                return;
            }

            FFLogsResponseNode = new TextNode
            {
                NodeID = 1000,
                NodeFlags = NodeFlags.Enabled | NodeFlags.Visible,
                Size = new Vector2(textNodeParent->GetWidth(), textNodeParent->GetHeight()),
                Position = new Vector2(textNodeParent->GetXFloat(), textNodeParent->GetYFloat() + textNodeParent->GetHeight()),
                TextColor = textNode->TextColor.ToVector4(),
                TextOutlineColor = textNode->EdgeColor.ToVector4(),
                BackgroundColor = textNode->BackgroundColor.ToVector4(),
                FontSize = 12,
                LineSpacing = textNode->LineSpacing,
                CharSpacing = textNode->CharSpacing,
                TextFlags = TextFlags.MultiLine | (TextFlags)textNode->TextFlags,
                Text = logsStatus.message,
                AlignmentType = AlignmentType.TopRight
            };

            Svc.Log.Debug($"Font: {FFLogsResponseNode.FontSize}");
            Svc.Log.Debug($"Attaching to Addon after Target Id: {((AtkResNode*)textNode)->NodeId}");

            P.NativeController.AttachToAddon(FFLogsResponseNode, charCard, (AtkResNode*)textNodeParent, KamiToolKit.Classes.NodePosition.AfterTarget);
        }

        private unsafe void RemoveNodeFromPlate(nint charCardnint)
        {
            if (FFLogsResponseNode == null) return;
            if (charCardnint == IntPtr.Zero)
                return;

            var charCard = (AtkUnitBase*)charCardnint;
            if (charCard == null)
                return;

            P.NativeController.DetachFromAddon(FFLogsResponseNode, charCard);
        }

        public unsafe void Refresh(bool ignoreCache = false)
        {
            if (addon == nint.Zero || username == string.Empty || server == string.Empty) return;

            var charCard = (AtkUnitBase*)addon;
            if (!charCard->IsVisible) return;

            RemoveNodeFromPlate(addon);
            LoadFFlogs(addon, username, server, ignoreCache);
        }
    }
}
