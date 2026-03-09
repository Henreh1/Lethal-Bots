using GameNetcodeStuff;
using LethalBots.AI;
using System;

namespace LethalBots.Utils.Helpers
{
    /// <summary>
    /// Helper class that represents a chat command!
    /// </summary>
    public class ChatCommand
    {
        public string Keyword;
        public Func<AIState, PlayerControllerB, string, bool, bool> Execute;

        /// <summary>
        /// Creates a new chat command
        /// </summary>
        /// <remarks>
        /// WARNING: <paramref name="keyword"/> will be forced into lower case!
        /// </remarks>
        /// <param name="keyword"></param>
        /// <param name="execute"></param>
        public ChatCommand(
            string keyword,
            Func<AIState, PlayerControllerB, string, bool, bool> execute)
        {
            Keyword = keyword.ToLower();
            Execute = execute;
        }
    }
}
