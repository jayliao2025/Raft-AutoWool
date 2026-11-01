using BepInEx;
using BepInEx.Configuration;
using System;
using UnityEngine;

namespace AutoWool
{
    [BepInPlugin("jayliao.AutoWool", "Auto Wool And Milk", "0.4.0")]
    public class BepInExPlugin : BaseUnityPlugin
    {
        public static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;
        public static ConfigEntry<float> interval;
        public static ConfigEntry<float> range;
        public static ConfigEntry<bool> shearLlamas;
        public static ConfigEntry<bool> milkGoats;
        public static ConfigEntry<bool> faithfulMode;

        private float timer;
        private float lastNoToolLog;
        private int woolTotal;
        private int milkTotal;

        public static void Dbgl(object obj, BepInEx.Logging.LogLevel level = BepInEx.Logging.LogLevel.Info)
        {
            if (isDebug.Value)
                context.Logger.Log(level, obj);
        }

        public void Awake()
        {
            context = this;
            modEnabled = Config.Bind<bool>("General", "ModEnabled", true, "Enable mod");
            isDebug = Config.Bind<bool>("General", "IsDebug", true, "Enable debug logging");
            interval = Config.Bind<float>("Options", "Interval", 2f, "Seconds between scans");
            range = Config.Bind<float>("Options", "Range", 30f, "Max distance from the player to the animal (-1 = unlimited)");
            shearLlamas = Config.Bind<bool>("Options", "ShearLlamas", true, "Automatically collect wool from llamas");
            milkGoats = Config.Bind<bool>("Options", "MilkGoats", true, "Automatically collect milk from goats");
            faithfulMode = Config.Bind<bool>("Options", "FaithfulMode", true, "Require the vanilla tool in your inventory: shears for wool, an empty bucket for milk (one empty bucket is spent per milk)");

            Logger.LogInfo("Auto Wool And Milk loaded");
        }

        public void Update()
        {
            if (!modEnabled.Value)
                return;

            timer += Time.deltaTime;
            if (timer < interval.Value)
                return;
            timer = 0f;

            var network = ComponentManager<Raft_Network>.Value;
            if (network == null || !Raft_Network.IsHost)
                return;

            var player = network.GetLocalPlayer();
            if (player == null || player.Inventory == null)
                return;

            var animals = FindObjectsOfType<AI_NetworkBehaviour_Domestic_Resource>();
            if (animals == null || animals.Length == 0)
                return;

            int ready = 0;
            foreach (var animal in animals)
            {
                if (animal == null)
                    continue;

                bool isLlama = animal is AI_NetworkBehaviour_Llama;
                bool isGoat = animal is AI_NetworkBehaviour_Goat;

                if (!isLlama && !isGoat)
                    continue;
                if (isLlama && !shearLlamas.Value)
                    continue;
                if (isGoat && !milkGoats.Value)
                    continue;

                if (!animal.IsDomestic)
                    continue;
                if (animal.networkEntity == null || animal.networkEntity.IsDead)
                    continue;

                if (range.Value >= 0f && Vector3.Distance(animal.transform.position, player.transform.position) > range.Value)
                    continue;

                ResourceRegenerative res;
                try
                {
                    res = animal.Resource;
                }
                catch (Exception e)
                {
                    Dbgl($"Could not read Resource: {e.Message}");
                    continue;
                }

                if (res == null || !res.IsReady)
                    continue;

                ready++;

                if (faithfulMode.Value && !HasRequiredTool(res, player))
                    continue;

                Harvest(res, isLlama, isGoat, player);
            }

            if (ready > 0)
                Dbgl($"Scanned {animals.Length} animal(s), {ready} had a resource ready");
        }

        private Item_Base GetCollectionItem(ResourceRegenerative res)
        {
            try
            {
                return res.resource != null ? res.resource.collectionItem : null;
            }
            catch (Exception e)
            {
                Dbgl($"Could not read collectionItem: {e.Message}");
                return null;
            }
        }

        private bool HasRequiredTool(ResourceRegenerative res, Network_Player player)
        {
            var need = GetCollectionItem(res);
            if (need == null)
                return true;

            if (player.Inventory.GetItemCount(need) > 0)
                return true;

            if (Time.time - lastNoToolLog > 30f)
            {
                Dbgl($"Needs {need.UniqueName} in the inventory, skipping (FaithfulMode)");
                lastNoToolLog = Time.time;
            }
            return false;
        }

        private void Harvest(ResourceRegenerative res, bool isLlama, bool isGoat, Network_Player player)
        {
            Item_Base item;
            try
            {
                // vanilla flow: ResourceCollector.OnHarvest() -> HarvestResource() then add to inventory
                item = res.HarvestResource();
            }
            catch (Exception e)
            {
                Dbgl($"HarvestResource failed: {e.Message}");
                return;
            }

            if (item == null)
            {
                Dbgl("HarvestResource returned null");
                return;
            }

            int remain = player.Inventory.AddItem(item.UniqueName, 1);

            if (remain > 0)
            {
                Dbgl($"Inventory full, could not store {item.UniqueName}");
                return;
            }

            if (isLlama)
                woolTotal++;
            else
                milkTotal++;

            Dbgl($"Collected {item.UniqueName} from {(isLlama ? "llama" : "goat")} (wool {woolTotal} / milk {milkTotal})");

            // milk comes out as a filled bucket, so spend the empty one like the vanilla loop does
            if (faithfulMode.Value && isGoat)
            {
                var need = GetCollectionItem(res);
                if (need != null)
                    player.Inventory.RemoveItem(need.UniqueName, 1);
            }
        }
    }
}
