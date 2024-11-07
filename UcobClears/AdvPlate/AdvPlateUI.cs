using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using KamiToolKit.Extensions;
using KamiToolKit.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using UcobClears.Models;
using static Dalamud.Interface.Windowing.Window;

namespace UcobClears.AdvPlate
{
    internal class AdvPlateUI : Window
    {
        public FFLogsStatus? logsStatus = null;
        private TextNode? FFLogsResponseNode;

        private string username = string.Empty;
        private string server = string.Empty;

        public AdvPlateUI() : base($"###AdventurePlate", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoNavInputs | ImGuiWindowFlags.NoNavFocus | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoFocusOnAppearing)
        {
            this.Size = new Vector2(0, 0);
            this.Position = new Vector2(0, 0);
            IsOpen = true;
            ShowCloseButton = false;
            RespectCloseHotkey = false;
            DisableWindowSounds = true;
            this.SizeConstraints = new WindowSizeConstraints()
            {
                MaximumSize = new Vector2(0, 0),
            };
        }

        public override unsafe void Draw()
        {
            if (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]) return;
            if (String.IsNullOrEmpty(P.Config.FFLogsAPI_ClientId) || String.IsNullOrEmpty(P.Config.FFLogsAPI_ClientSecret)) return;
            if (!TryGetAddonByName<AtkUnitBase>("CharaCard", out var charCard))
            {
                logsStatus = null;
                username = string.Empty;
                server = string.Empty;
                return;
            }

            if (logsStatus == null)
            {
                logsStatus = new FFLogsStatus()
                {
                    requestStatus = FFLogsRequestStatus.Searching,
                    message = "Searching logs..."
                };

                (username, server) = GetUserDetailsFromCard();
                if (username == string.Empty || server == string.Empty)
                {
                    logsStatus = new FFLogsStatus()
                    {
                        requestStatus = FFLogsRequestStatus.Failed,
                        message = "Unable to retrieve details from plate."
                    };
                }
                else
                {
                    LoadFFlogs(username, server);
                }
                    
            }
        }

        private unsafe (string uname, string server) GetUserDetailsFromCard()
        {
            var charCardnint = Svc.GameGui.GetAddonByName("CharaCard");
            if (charCardnint == IntPtr.Zero)
                return (string.Empty, string.Empty);

            var charCard = (AtkUnitBase*)charCardnint;
            if (charCard == null)
                return (string.Empty, string.Empty);

            if (charCard->UldManager.NodeListCount > 1)
            {
                if (charCard->UldManager.SearchNodeById(20)->IsVisible())
                {
                    var atkValues = charCard->AtkValues;
                    var usernameString = SeString.Parse(charCard->AtkValues[29].String).TextValue;
                    Svc.Log.Debug(usernameString);

                    var serverString = SeString.Parse(charCard->AtkValues[32].String).TextValue;
                    Svc.Log.Debug(serverString);

                    return (usernameString, serverString.Split('[')[0].Trim());
                }
            }

            return (string.Empty, string.Empty);
        }
    
        private async void LoadFFlogs(string server, string name, bool ignoreCache = false)
        {
            var fflogsResponse = await FFLogsApiSearch.GetUcobLogs_v2(server, name, ignoreCache);
            Svc.Log.Debug($"{fflogsResponse.requestStatus.ToString()}: {fflogsResponse.message}");
            logsStatus = fflogsResponse;
            AddNodeToPlate();
        }
    
        private unsafe void AddNodeToPlate()
        {
            if (logsStatus == null) return;
            if (!P.Config.ShowErrorsOnPlate && logsStatus.requestStatus != FFLogsRequestStatus.Success) return;

            var charCardnint = Svc.GameGui.GetAddonByName("CharaCard");
            if (charCardnint == IntPtr.Zero)
            {
                logsStatus = null;
                return;
            }

            var charCard = (AtkUnitBase*)charCardnint;
            if (charCard == null)
            {
                logsStatus = null;
                return;
            } 

            if (charCard->UldManager.NodeListCount <= 1 || !charCard->UldManager.SearchNodeById(20)->IsVisible())
            {
                logsStatus = null;
                return;
            }

            var textNodeParent = charCard->UldManager.SearchNodeById(5)->GetAsAtkComponentNode();
            var textNode = textNodeParent->GetComponent()->UldManager.SearchNodeById(3)->GetAsAtkTextNode();

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
    
        private unsafe void RemoveNodeFromPlate()
        {
            if (FFLogsResponseNode == null) return;
            var charCardnint = Svc.GameGui.GetAddonByName("CharaCard");
            if (charCardnint == IntPtr.Zero)
            {
                logsStatus = null;
                return;
            }

            var charCard = (AtkUnitBase*)charCardnint;
            if (charCard == null)
            {
                logsStatus = null;
                return;
            }

            P.NativeController.DetachFromAddon(FFLogsResponseNode, charCard);
        }

        public unsafe void Refresh(bool ignoreCache = false)
        {
            if (username == string.Empty || server == string.Empty) return;
            Svc.Log.Debug("Refreshing UI.");

            RemoveNodeFromPlate();
            LoadFFlogs(username, server);
        }
    
        public unsafe void Dispose()
        {
            RemoveNodeFromPlate();
        }
    }
}
