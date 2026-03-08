using UnityEngine;

namespace LethalBots.Managers
{
    /// <summary>
    /// Manager in charge of initializing other managers for LethalBot
    /// </summary>
    public class PluginManager : MonoBehaviour
    {
        public static PluginManager Instance { get; private set; } = null!;

        /// <summary>
        /// <c>GameObject</c> prefab of the <c>SaveManager</c>, see: <see cref="SaveManager"><c>SaveManager</c></see>
        /// </summary>
        public GameObject SaveManagerPrefab = null!;
        /// <summary>
        /// <c>GameObject</c> prefab of the <c>LethalBotManager</c>, see: <see cref="LethalBotManager"><c>LethalBotManager</c></see>
        /// </summary>
        public GameObject LethalBotManagerPrefab = null!;
        /// <summary>
        /// <c>GameObject</c> prefab of the <c>TerminalManager</c>, see: <see cref="TerminalManager"><c>TerminalManager</c></see>
        /// </summary>
        public GameObject TerminalManagerPrefab = null!;
        /// <summary>
        /// <c>GameObject</c> prefab of the <c>GroupManager</c>, see: <see cref="GroupManager"><c>GroupManager</c></see>
        /// </summary>
        public GameObject GroupManagerPrefab = null!;

        private void Awake()
        {
            // Prevent multiple instances of PluginManager
            if (Instance != null && Instance != this)
            {
                Destroy(Instance.gameObject);
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Create a network prefab with <c>LethalLib</c> and add the component manager to it, which initialize it (but not spawn it network wise)
        /// </summary>
        /// <remarks>
        /// For spawning manager over the network see <see cref="Patches.GameEnginePatches.StartOfRoundPatch.Awake_Prefix"><c>StartOfRoundPatch.Awake_Prefix</c></see>
        /// </remarks>
        public void InitManagers()
        {
            LethalBotManagerPrefab = LethalLib.Modules.NetworkPrefabs.CreateNetworkPrefab("LethalBotManager");
            LethalBotManagerPrefab.AddComponent<LethalBotManager>();

            SaveManagerPrefab = LethalLib.Modules.NetworkPrefabs.CreateNetworkPrefab("SaveManager");
            SaveManagerPrefab.AddComponent<SaveManager>();

            TerminalManagerPrefab = LethalLib.Modules.NetworkPrefabs.CreateNetworkPrefab("TerminalManager");
            TerminalManagerPrefab.AddComponent<TerminalManager>();

            GroupManagerPrefab = LethalLib.Modules.NetworkPrefabs.CreateNetworkPrefab("GroupManager");
            GroupManagerPrefab.AddComponent<GroupManager>();
        }
    }
}
