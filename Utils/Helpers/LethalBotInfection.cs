using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;

namespace LethalBots.Utils.Helpers
{
    /// <summary>
    /// Small class to keep track of the infection state of a bot.
    /// </summary>
    /// <remarks>
    /// NetworkSerializable so we can easily sync it across all clients.
    /// </remarks>
    public sealed class LethalBotInfection : INetworkSerializable, IEquatable<LethalBotInfection>
    {
        public float showSignsMeter;
        public float timeAtLastHealing;
        public float setPoison;
        public float sprayOnPlayerMeter;
        public float totalTimeSpentInPlants;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref showSignsMeter);
            serializer.SerializeValue(ref timeAtLastHealing);
            serializer.SerializeValue(ref setPoison);
            serializer.SerializeValue(ref sprayOnPlayerMeter);
            serializer.SerializeValue(ref totalTimeSpentInPlants);
        }

        public bool Equals(LethalBotInfection? other)
        {
            return other != null &&
                   showSignsMeter == other.showSignsMeter &&
                   timeAtLastHealing == other.timeAtLastHealing &&
                   setPoison == other.setPoison &&
                   sprayOnPlayerMeter == other.sprayOnPlayerMeter &&
                   totalTimeSpentInPlants == other.totalTimeSpentInPlants;
        }

        public override bool Equals(object obj)
        {
            return obj is LethalBotInfection infection && Equals(infection);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(showSignsMeter, timeAtLastHealing, setPoison, sprayOnPlayerMeter, totalTimeSpentInPlants);
        }
    }
}
