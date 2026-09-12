using System;
using Gjallarhorn.Config;
using Splatform;
using UnityEngine;

namespace Gjallarhorn.Net
{
    /// <summary>
    /// Sends announcements to every connected client.
    ///
    /// Two channels, both routed RPCs that vanilla clients already register. Both signatures
    /// were read out of the decompiled game rather than assumed -- a routed RPC whose
    /// argument list does not match what the receiver registered does not fail politely:
    /// ZRpc.HandlePackage throws EndOfStreamException, ZRpc.Update returns
    /// ErrorCode.IncompatibleVersion, and ZNet drops the client with a version error.
    /// </summary>
    public static class Broadcast
    {
        /// <summary>MessageHud.Start registers this as Register&lt;int, string&gt;.</summary>
        private const string ShowMessageRpc = "ShowMessage";

        /// <summary>Chat.Awake registers this as Register&lt;Vector3, int, UserInfo, string&gt;.</summary>
        private const string ChatMessageRpc = "ChatMessage";

        private static bool _warnedNoChatIdentity;

        /// <summary>
        /// Pushes a line into every client's message HUD.
        /// <paramref name="center"/> picks the large centre banner over the top-left feed.
        /// </summary>
        public static void Hud(string text, bool center)
        {
            if (ZRoutedRpc.instance == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var type = center ? MessageHud.MessageType.Center : MessageHud.MessageType.TopLeft;

            try
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(
                    ZRoutedRpc.Everybody, ShowMessageRpc, (int)type, text);

                GjallarhornPlugin.LogVerbose($"HUD broadcast ({type}): {text}");
            }
            catch (Exception e)
            {
                GjallarhornPlugin.LogWarning($"HUD broadcast failed: {e.Message}");
            }
        }

        /// <summary>
        /// Posts a line into every client's chat window.
        ///
        /// Sent as <see cref="Talker.Type.Normal"/>, not Shout: Chat.AddInworldText
        /// uppercases shouts, which would render the feed as yellow ALL-CAPS floating text.
        ///
        /// The UserInfo needs a <em>valid</em> PlatformUserID. Chat.OnNewChatMessage runs
        /// RelationsManager.CheckPermissionAsync on it first, and an invalid id short-circuits
        /// to Error -- the client silently drops the line and logs an error. There is no such
        /// id on a dedicated server, so we borrow a connected player's, which is real and
        /// therefore passes. Muting affects only that one player's view of the feed.
        /// </summary>
        public static void Chat(string text, Vector3 position)
        {
            if (ZRoutedRpc.instance == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (!TryGetChatIdentity(out var userId))
            {
                if (!_warnedNoChatIdentity)
                {
                    _warnedNoChatIdentity = true;
                    GjallarhornPlugin.LogWarning(
                        "No connected player with a valid platform ID, so chat lines cannot be " +
                        "sent -- clients would reject them on the permission check. Falling back " +
                        "to the top-left feed only. This resolves once someone is connected.");
                }
                return;
            }

            var sender = new UserInfo
            {
                Name = GjallarhornConfig.ChatSenderName.Value,
                UserId = userId,
            };

            try
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(
                    ZRoutedRpc.Everybody, ChatMessageRpc,
                    position, (int)Talker.Type.Normal, sender, text);

                GjallarhornPlugin.LogVerbose($"Chat broadcast: {text}");
            }
            catch (Exception e)
            {
                GjallarhornPlugin.LogWarning($"Chat broadcast failed: {e.Message}");
            }
        }

        /// <summary>
        /// Finds any valid platform ID among connected players. ZNet.UpdatePlayerList builds
        /// these from each peer's socket host name, so on a dedicated server the local entry
        /// is absent but peer entries are populated.
        /// </summary>
        private static bool TryGetChatIdentity(out PlatformUserID userId)
        {
            userId = PlatformUserID.None;

            if (ZNet.instance == null)
            {
                return false;
            }

            foreach (var player in ZNet.instance.GetPlayerList())
            {
                if (player.m_userInfo.m_id.IsValid)
                {
                    userId = player.m_userInfo.m_id;
                    return true;
                }
            }

            return false;
        }
    }
}
