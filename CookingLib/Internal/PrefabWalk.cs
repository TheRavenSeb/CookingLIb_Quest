using System.Collections.Generic;
using System.Linq;
using Il2CppAlta.Networking;

namespace CookingLib
{
    internal static class PrefabWalk
    {
        internal static List<NetworkPrefab> All()
        {
            PrefabManager.PrepareSpawnSetups();
            return PrefabManager.AllPrefabs.Where(prefab => prefab != null).ToList();
        }

        internal static List<NetworkPrefab> With<T>() where T : UnityEngine.Component =>
            All().Where(prefab => prefab.GetComponent<T>() != null).ToList();
    }
}
