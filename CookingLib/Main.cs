using System;
using System.Linq;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(CookingLib.Main), "CookingLib", CookingLib.Main.Version, "Shadow")]
[assembly: MelonGame("Alta", "Att_Quest")]


namespace CookingLib
{
    public class Main : MelonMod
    {
        public const string Version = "0.2.1";

        public static event Action PreSetup;
        public static event Action Setup;
        public static event Action PostSetup;

        public static bool IsServer { get; private set; }

        internal static bool SetupDone { get; private set; }

        static bool worldUp;

        public override void OnInitializeMelon()
        {
            Log.Init(LoggerInstance);
            IsServer = Application.isBatchMode || Environment.GetCommandLineArgs().Any(arg => arg.Equals("/start_server", StringComparison.OrdinalIgnoreCase));
            Log.Info(IsServer ? "running on a server" : "running on a client");
            if (!IsServer)
                return;
            ServerPatches.Apply(HarmonyInstance);
            Safe.Run("shared recipe pages setting", StewRecipes.ReadFlag);
        }

        public override void OnLateInitializeMelon()
        {
            Safe.Run("game effects snapshot", EffectNames.TakeGameSnapshot);
            EventQueue.Fire(PreSetup, nameof(PreSetup));
            EventQueue.Fire(Setup, nameof(Setup));
            EventQueue.Fire(PostSetup, nameof(PostSetup));
            SetupDone = true;
            Safe.Run("vanilla liquids", VanillaLiquids.ApplyAll);
            Safe.Run("food walk", Edibles.AfterSetup);
        }

        public override void OnUpdate()
        {
            EventQueue.Drain();
            WorldUp();
        }

        internal static void WorldUp()
        {
            if (worldUp || !IsServer || NetworkSceneManager.Current == null)
                return;

            worldUp = true;
            StewStages.Ready();
            PotRules.ApplyAll();
            StewRecipes.ResolveAll();
            VanillaRecipes.ApplyAll();
            VanillaIngredients.ApplyAll();
            VanillaGarnishes.ApplyAll();
            VanillaStews.ApplyAll();
            Safe.Run("food sweep", Edibles.WorldUp);
            Infusions.ResolveAll();
            Safe.Run("roasting", Roasting.ResolveAll);
            Reachability.Check();
            CookingLibCommands.Register();
        }
    }
}
