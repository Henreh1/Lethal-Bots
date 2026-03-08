using GameNetcodeStuff;
using LethalBots.NetworkSerializers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Unity.Netcode;

namespace LethalBots.Managers
{
    /// <summary>
    /// This is the manager than handles all of the groups whether  it be only bots or a mix of human players and bots.<br/>
    /// This manager also handles networking all of the groups automatically, even for players that join in late.<br/>
    /// </summary>
    /// <remarks>
    /// The manager keeps cached information about the active groups. This makes lookups quick as the data is only reassed if the groups change themselves.
    /// </remarks>
    public class GroupManager : NetworkBehaviour
    {
        public const int INVALID_GROUP_INDEX = -1;
        private const int DEFAULT_GROUP_INDEX = 0;

        public static GroupManager Instance { get; private set; } = null!;

        public NetworkList<LethalBotGroupMemberNetworkSerializable> LethalBotGroups = new NetworkList<LethalBotGroupMemberNetworkSerializable>(writePerm: NetworkVariableWritePermission.Server);

        private Dictionary<ulong, int> memberToGroup = new Dictionary<ulong, int>();
        private Dictionary<int, HashSet<ulong>> groupMembers = new Dictionary<int, HashSet<ulong>>();
        private Dictionary<int, ulong> groupLeaders = new Dictionary<int, ulong>();

        private int nextGroupId = DEFAULT_GROUP_INDEX;

        /// <summary>
        /// When manager awake, setup the manager instance
        /// </summary>
        private void Awake()
        {
            // Prevent multiple instances of the SaveManager
            if (Instance != null && Instance != this)
            {
                if (Instance.IsSpawned && Instance.IsServer)
                {
                    Instance.NetworkObject.Despawn(destroy: true);
                }
                else
                {
                    Destroy(Instance.gameObject);
                }
            }

            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            LethalBotGroups.OnListChanged += OnGroupListChanged;

            if (!base.NetworkManager.IsServer)
            {
                if (Instance != null && Instance != this)
                {
                    // Destory Local manager
                    Destroy(Instance.gameObject);
                }
                Instance = this;
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            LethalBotGroups.OnListChanged -= OnGroupListChanged;
        }

        #region Group Creation, Addition, and Removal Rpcs

        /// <summary>
        /// Creates a new group with <paramref name="leader"/> as the leader
        /// </summary>
        /// <remarks>
        /// This always fails if not called on the server.
        /// </remarks>
        /// <param name="leader"></param>
        private void CreateGroup(PlayerControllerB leader)
        {
            if (!IsServer)
            {
                return;
            }

            RemoveFromCurrentGroup(leader);

            int groupId = nextGroupId++;
            LethalBotGroups.Add(new LethalBotGroupMemberNetworkSerializable
            {
                GroupId = groupId,
                Member = leader,
                IsLeader = true
            });
        }

        /// <summary>
        /// <inheritdoc cref="CreateGroup(PlayerControllerB)"/>
        /// </summary>
        /// <remarks>
        /// This will automatically call <see cref="CreateGroupServerRpc(NetworkBehaviourReference)"/> if called on a client.
        /// </remarks>
        /// <param name="leader"></param>
        public void CreateGroupAndSync(PlayerControllerB leader)
        {
            if (IsServer)
            {
                CreateGroup(leader);
            }
            else
            {
                CreateGroupServerRpc(leader);
            }
        }

        /// <summary>
        /// Helper rpc that allows clients to create groups!
        /// </summary>
        /// <param name="player"></param>
        [ServerRpc(RequireOwnership = false)]
        private void CreateGroupServerRpc(NetworkBehaviourReference player)
        {
            if (player.TryGet(out PlayerControllerB groupLeader))
            {
                CreateGroup(groupLeader);
            }
        }

        /// <summary>
        /// Adds the given <paramref name="member"/> to the given <paramref name="groupId"/>
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="member"></param>
        private void AddToGroup(int groupId, PlayerControllerB member)
        {
            if (!IsServer)
                return;

            RemoveFromCurrentGroup(member);

            LethalBotGroups.Add(new LethalBotGroupMemberNetworkSerializable
            {
                GroupId = groupId,
                Member = member,
                IsLeader = false
            });
        }

        /// <summary>
        /// <inheritdoc cref="AddToGroup(int, PlayerControllerB)"/>
        /// </summary>
        /// <remarks>
        /// This will automatically call <see cref="AddToGroupServerRpc(int, NetworkBehaviourReference)"/> if called on a client.
        /// </remarks>
        /// <param name="groupId"></param>
        /// <param name="member"></param>
        public void AddToGroupAndSync(int groupId, PlayerControllerB member)
        {
            if (IsServer)
            {
                AddToGroup(groupId, member); 
            }
            else
            {
                AddToGroupServerRpc(groupId, member);
            }
        }

        /// <summary>
        /// Helper rpc that allows clients to add themselves to groups
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="player"></param>
        private void AddToGroupServerRpc(int groupId, NetworkBehaviourReference player)
        {
            if (player.TryGet(out PlayerControllerB groupLeader))
            {
                AddToGroup(groupId, groupLeader);
            }
        }

        /// <summary>
        /// Removes the given <paramref name="member"/> from every group
        /// </summary>
        /// <param name="member"></param>
        private void RemoveFromCurrentGroup(PlayerControllerB member)
        {
            if (!IsServer)
                return;

            for (int i = LethalBotGroups.Count - 1; i >= 0; i--)
            {
                var group = LethalBotGroups[i];
                if (group.Member.TryGet(out PlayerControllerB m) && m == member)
                {
                    LethalBotGroups.RemoveAt(i);
                    if (group.IsLeader)
                    {
                        HandleLeaderRemoval(group.GroupId);
                    }
                }
            }
        }

        /// <summary>
        /// <inheritdoc cref="AddToGroup(int, PlayerControllerB)"/>
        /// </summary>
        /// <remarks>
        /// This will automatically call <see cref="RemoveFromCurrentGroupServerRpc(NetworkBehaviourReference)"/> if called on a client.
        /// </remarks>
        /// <param name="member"></param>
        public void RemoveFromCurrentGroupAndSync(PlayerControllerB member)
        {
            if (IsServer)
            {
                RemoveFromCurrentGroup(member);
            }
            else
            {
                RemoveFromCurrentGroupServerRpc(member);
            }
        }

        /// <summary>
        /// Helper rpc that allows clients to remove themselves from groups
        /// </summary>
        /// <param name="player"></param>
        private void RemoveFromCurrentGroupServerRpc(NetworkBehaviourReference player)
        {
            if (player.TryGet(out PlayerControllerB groupLeader))
            {
                RemoveFromCurrentGroup(groupLeader);
            }
        }

        #endregion

        #region Group Helper functions

        /// <summary>
        /// Checks if the given <paramref name="player"/> is in a group.
        /// </summary>
        /// <remarks>
        /// If you want the actual group id use <see cref="GetGroupId(PlayerControllerB)"/> instead.
        /// </remarks>
        /// <param name="player"></param>
        /// <returns></returns>
        public bool IsPlayerInGroup(PlayerControllerB player)
        {
            return GetGroupId(player) != INVALID_GROUP_INDEX;
        }

        /// <summary>
        /// Checks if the given <paramref name="player"/> is a group leader
        /// </summary>
        /// <param name="player"></param>
        /// <param name="groupId">The id of the group the given <paramref name="player"/> is in. <see cref="INVALID_GROUP_INDEX"/> if they are not in a group.</param>
        /// <returns></returns>
        public bool IsPlayerGroupLeader(PlayerControllerB player, out int groupId)
        {
            groupId = GetGroupId(player);
            if (groupId == INVALID_GROUP_INDEX)
                return false;

            if (groupLeaders.TryGetValue(groupId, out ulong leaderId))
                return leaderId == player.NetworkObjectId;

            return false;
        }

        /// <summary>
        /// Checks if the given players, <paramref name="a"/> and <paramref name="b"/> are in the same group
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns><see langword="true"/> if both players are in the same group; otherwise <see langword="false"/></returns>
        public bool ArePlayersInSameGroup(PlayerControllerB a, PlayerControllerB b)
        {
            int groupA = GetGroupId(a);
            int groupB = GetGroupId(b);

            return groupA != INVALID_GROUP_INDEX && groupA == groupB;
        }

        /// <summary>
        /// Returns the id of the group <paramref name="player"/> is in.
        /// </summary>
        /// <param name="player"></param>
        /// <returns>The id of the group the player is in or <see cref="INVALID_GROUP_INDEX"/> if the player isn't in a group</returns>
        public int GetGroupId(PlayerControllerB player)
        {
            // Check to see if this player is in a group
            if (memberToGroup.TryGetValue(player.NetworkObjectId, out int group))
            {
                return group;
            }

            // Return the invaild group id
            return INVALID_GROUP_INDEX;
        }

        /// <summary>
        /// Checks if the group with the given Id exists
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns><see langword="true"/> if the given <paramref name="groupId"/> exists; otherwise <see langword="false"/></returns>
        public bool DoesGroupExist(int groupId)
        {
            return groupMembers.ContainsKey(groupId);
        }

        /// <summary>
        /// Returns every registed group this round.
        /// </summary>
        /// <returns>The Id's of every group in this round</returns>
        public IEnumerable<int> GetAllGroups()
        {
            return groupMembers.Keys;
        }

        /// <summary>
        /// Returns the size of the given group
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns>The size of the given <paramref name="groupId"/>; 0 if the group doesn't exist.</returns>
        public int GetGroupSize(int groupId)
        {
            if (groupMembers.TryGetValue(groupId, out var members))
                return members.Count;

            return 0;
        }

        /// <summary>
        /// Returns every player that is in the given <paramref name="groupId"/>
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns>List of all the players in the <paramref name="groupId"/></returns>
        public List<PlayerControllerB> GetGroupMembers(int groupId)
        {
            List<PlayerControllerB> result = new List<PlayerControllerB>();
            if (!groupMembers.TryGetValue(groupId, out var members))
                return result;

            var spawnedObjects = NetworkManager.SpawnManager.SpawnedObjects;
            foreach (ulong id in members)
            {
                // Find and resolve the network objects
                if (spawnedObjects.TryGetValue(id, out var obj) 
                    && obj.TryGetComponent(out PlayerControllerB member))
                {
                    result.Add(member);
                }
            }

            return result;
        }

        /// <summary>
        /// Returns every player that is in the same group as <paramref name="player"/>
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public List<PlayerControllerB> GetOtherGroupMembers(PlayerControllerB player)
        {
            int groupId = GetGroupId(player);
            if (groupId == INVALID_GROUP_INDEX)
                return new List<PlayerControllerB>();

            List<PlayerControllerB> members = GetGroupMembers(groupId);
            members.Remove(player);
            return members;
        }

        /// <summary>
        /// This returns the leader of the given <paramref name="groupId"/>
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns>The leader of the group or null</returns>
        public PlayerControllerB? GetGroupLeader(int groupId)
        {
            if (!groupLeaders.TryGetValue(groupId, out var leaderId))
                return null;

            // Find and resolve the network objects
            if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(leaderId, out var obj)
                && obj.TryGetComponent(out PlayerControllerB leader))
            {
                return leader;
            }

            return null;
        }

        /// <summary>
        /// Helper function to reset and clear all groups
        /// </summary>
        public void ResetAndRemoveAllGroups()
        {
            nextGroupId = DEFAULT_GROUP_INDEX;
            memberToGroup.Clear();
            groupMembers.Clear();
            groupLeaders.Clear();
            LethalBotGroups.Clear();
        }

        #endregion

        private void HandleLeaderRemoval(int groupId)
        {
            // Find the best person to make the leader of our group
            // We prefer human players over bots
            LethalBotGroupMemberNetworkSerializable? newLeader = null;
            int groupIndex = -1;
            for (int i = 0; i < LethalBotGroups.Count; i++)
            {
                var group = LethalBotGroups[i];
                if (group.GroupId == groupId 
                    && !group.IsLeader 
                    && group.Member.TryGet(out PlayerControllerB member))
                {
                    // The first bot we find will be the leader unless we find a human player.
                    bool isHumanPlayer = !LethalBotManager.Instance.IsPlayerLethalBot(member);
                    if (newLeader == null || isHumanPlayer)
                    {
                        groupIndex = i;
                        newLeader = group;
                        if (isHumanPlayer)
                        {
                            break;
                        }
                    }
                }
            }

            if (newLeader != null)
            {
                var updated = newLeader.Value;
                updated.IsLeader = true;
                LethalBotGroups[groupIndex] = updated;
            }
        }

        private void OnGroupListChanged(NetworkListEvent<LethalBotGroupMemberNetworkSerializable> change)
        {
            RebuildLookups();
        }

        /// <summary>
        /// This rebuilds the lookup cache for all groups!
        /// </summary>
        private void RebuildLookups()
        {
            // Clean up the old tables
            memberToGroup.Clear();
            groupMembers.Clear();
            groupLeaders.Clear();

            foreach (var group in LethalBotGroups)
            {
                // NOTE: WE don't need the PlayerControllerB objects here, since we are only using the NetworkObjectIds
                if (group.Member.TryGet(out NetworkBehaviour member))
                {
                    // Attach IDs to what group they are assigned to
                    ulong id = member.NetworkObjectId;
                    memberToGroup[id] = group.GroupId;
                    if (group.IsLeader)
                    {
                        groupLeaders[group.GroupId] = id;
                    }

                    // If this is a new group, we need to setup the lookup cache
                    if (!groupMembers.TryGetValue(group.GroupId, out var set))
                    {
                        set = new HashSet<ulong>();
                        groupMembers[group.GroupId] = set;
                    }

                    // Add this ID to the set
                    set.Add(id);
                }
            }
        }

    }
}
