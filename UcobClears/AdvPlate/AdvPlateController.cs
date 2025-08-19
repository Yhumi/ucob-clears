using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Utility;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Extensions;
using KamiToolKit.Nodes;
using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using UcobClears.Models;
using UcobClears.RawInformation;

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
            var setupArgs = args as AddonSetupArgs;

            if (setupArgs == null)
                return;

            username = setupArgs.AtkValueSpan[29].GetValueAsString();
            server = setupArgs.AtkValueSpan[32].GetValueAsString().Split('[')[0].Trim();

            //GetUserDetailsFromCard(args.Addon);
            if (username == null || server == null)
                return;

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
                    var usernameString = charCard->AtkValues[29].String.AsDalamudSeString().TextValue;
                    Svc.Log.Debug(usernameString);

                    var serverString = charCard->AtkValues[32].String.AsDalamudSeString().TextValue;
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
                        string progMessage = $"{tomestoneResponse.encounters?.ultimateProgressionTarget?.percent}";

                        if (tomestoneResponse.encounters?.ultimateProgressionTarget?.percent?.Contains("P3") ?? false)
                        {
                            string? progPoint = TomestoneP3ProgMap(tomestoneResponse.encounters?.ultimateProgressionTarget?.percent!);
                            if (progPoint != null)
                            {
                                progMessage += $" ({progPoint})";
                            }
                        }

                        fflogsResponse.message += $" [{progMessage}]";
                    }
                }
            }

            Svc.Log.Debug($"{fflogsResponse.requestStatus.ToString()}: {fflogsResponse.message}");
            Svc.Framework.RunOnFrameworkThread(() => AddNodeToPlate(addonInt, fflogsResponse));
        }

        private string? TomestoneP3ProgMap(string p3percent)
        {
            Svc.Log.Debug($"P3 Prog Found, checking prog point.");
            try
            {
                var split = p3percent.Split('%');
                var decimalPercentage = Convert.ToDecimal(split[0]);

                var filteredDecimals = ConstantData.TomestoneProgPercentageMap?.Where(x => x.Value > decimalPercentage).ToList() ?? null;
                if (filteredDecimals == null) return null;

                return filteredDecimals.Last().Key;
            }
            catch (Exception ex)
            {
                Svc.Log.Debug($"Could not process prog point from p3. {ex.Message}");
                return null;
            }
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
                Svc.Log.Debug($"An error has occurred: {e.Message}\n{e.StackTrace}");
                return;
            }
            FFLogsResponseNode = new TextNode
            {
                NodeFlags = NodeFlags.Enabled | NodeFlags.Visible,
                Size = new Vector2(textNodeParent->GetWidth(), textNodeParent->GetHeight()),
                Position = new Vector2(textNodeParent->GetXFloat(), textNodeParent->GetYFloat() + textNodeParent->GetHeight()),
                TextColor = textNode->TextColor.ToVector4(),
                TextOutlineColor = textNode->EdgeColor.ToVector4(),
                BackgroundColor = textNode->BackgroundColor.ToVector4(),
                FontSize = 12,
                LineSpacing = textNode->LineSpacing,
                CharSpacing = textNode->CharSpacing,
                TextFlags = TextFlags.MultiLine | TextFlags.AutoAdjustNodeSize | (TextFlags)textNode->TextFlags,
                AlignmentType = AlignmentType.TopRight
            };

            try
            {
                FFLogsResponseNode.Text = logsStatus.message;
            }
            catch (Exception ex)
            {
                Svc.Log.Debug($"An error has occurred setting the text of the TextNode object: {ex.Message}");
                Svc.Log.Debug($"{ex.StackTrace}");
                return;
            }

            Svc.Log.Debug($"Font: {FFLogsResponseNode.FontSize}");
            Svc.Log.Debug($"Attaching to Addon after Target Id: {((AtkResNode*)textNode)->NodeId}");

            P.NativeController.AttachNode(FFLogsResponseNode, (AtkResNode*)textNodeParent, KamiToolKit.Classes.NodePosition.AfterTarget);

            //AtkTextNode* createdTextNode;
            //try
            //{
            //    charCard->UldManager.UpdateDrawNodeList();

            //    Svc.Log.Debug($"Finding NodeId 1000");

            //    int idx1000 = 0;
            //    for (int i = 0; i < charCard->UldManager.NodeListCount; i++)
            //    {
            //        var node = charCard->UldManager.NodeList[i];
            //        Svc.Log.Debug(node->NodeId.ToString());
            //        if (node->NodeId == 1000)
            //            idx1000 = i;
            //    }

            //    createdTextNode = charCard->UldManager.NodeList[idx1000]->GetAsAtkTextNode();

            //    if (createdTextNode == null)
            //        Svc.Log.Debug($"Null node.");

            //    Svc.Log.Debug($"Created text node: {createdTextNode->NodeId}. Setting text to {logsStatus.message}");
            //    createdTextNode->SetText(logsStatus.message);
            //}
            //catch (Exception e)
            //{
            //    Svc.Log.Debug($"An error has occurred: {e.Message}");
            //    return;
            //}
        }

        private unsafe void RemoveNodeFromPlate(nint charCardnint)
        {
            if (FFLogsResponseNode == null) return;
            if (charCardnint == IntPtr.Zero)
                return;

            var charCard = (AtkUnitBase*)charCardnint;
            if (charCard == null)
                return;

            P.NativeController.DetachNode(FFLogsResponseNode);
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
