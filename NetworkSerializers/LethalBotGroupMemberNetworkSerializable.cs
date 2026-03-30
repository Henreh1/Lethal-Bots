using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Netcode;

namespace LethalBots.NetworkSerializers
{
    /// <summary>
    /// Class for serializing groups of players and/or bots
    /// </summary>
    [Serializable]
    public struct LethalBotGroupMemberNetworkSerializable : INetworkSerializable, IEquatable<LethalBotGroupMemberNetworkSerializable>
    {
        public int GroupId;
        public NetworkBehaviourReference Member;
        public bool IsLeader;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref GroupId);
            serializer.SerializeValue(ref Member);
            serializer.SerializeValue(ref IsLeader);
        }

        public bool Equals(LethalBotGroupMemberNetworkSerializable other)
        {
            return GroupId == other.GroupId 
                && Member.Equals(other.Member)
                && IsLeader == other.IsLeader;
        }

        public override bool Equals(object obj)
        {
            if (obj is LethalBotGroupMemberNetworkSerializable other)
            {
                return this.Equals(other);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(GroupId, Member, IsLeader);
        }

        public static bool operator ==(LethalBotGroupMemberNetworkSerializable? left, LethalBotGroupMemberNetworkSerializable? right)
        {
            if (left is null && right is null) return true;
            if (left is null || right is null) return false;
            return left.Equals(right);
        }

        public static bool operator !=(LethalBotGroupMemberNetworkSerializable? left, LethalBotGroupMemberNetworkSerializable? right)
        {
            return !(left == right);
        }
    }
}
