// Mod.cs
// Purpose: Entry point for "Wrecking Ball [WB]" — registers settings/locales,
//          hooks Selected Info Panel section, and wires the WreckingBallSystem
//          into the GameSimulation update loop.

namespace WreckingBall
{
    using System.Reflection;
    using Colossal.IO.AssetDatabase;
    using Colossal.Localization;
    using Colossal.Logging;
    using Game;
    using Game.Modding;
    using Game.SceneFlow;
    using Game.UI.InGame;
    using Unity.Entities;

    public sealed class Mod : IMod
    {
        // ---- PUBLIC METADATA ----
        public const string ModName = "Wrecking Ball";
        public const string ModId = "WreckingBall";
        public const string ModTag = "[WB]";

        /// <summary>
        /// Read &lt;Version&gt; from the .csproj (3-part: major.minor.patch).
        /// </summary>
        public static readonly string ModVersion =
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        // ---- LOGGING ----
        public static readonly ILog Log =
            LogManager.GetLogger("WreckingBall")
#if DEBUG
                .SetShowsErrorsInUI(true);
#else
                .SetShowsErrorsInUI(false);
#endif

        // ---- SETTINGS HANDLE ----
        public static Setting? Settings
        {
            get; private set;
        }

        private static bool s_BannerLogged;

        public void OnLoad(UpdateSystem updateSystem)
        {
            if (!s_BannerLogged)
            {
                s_BannerLogged = true;
                Log.Info($"{ModName} {ModTag} v{ModVersion} OnLoad");
            }

            // Create settings instance
            var setting = new Setting(this);
            Settings = setting;

            // Locales (en-US for now)
            GameManager? gm = GameManager.instance;
            LocalizationManager? lm = gm?.localizationManager;
            lm?.AddSource("en-US", new LocaleEN(setting));

            // Load saved settings + register Options UI
            AssetDatabase.global.LoadSettings("WreckingBall", setting, new Setting(this));
            setting.RegisterInOptionsUI();

            // Register simulation system (handles abandon/destroy requests)
            updateSystem.UpdateAt<WreckingBallSystem>(SystemUpdatePhase.GameSimulation);

            // Hook Selected Info Panel section for building actions
            World world = World.DefaultGameObjectInjectionWorld;
            SelectedInfoUISystem? sipSystem = world.GetOrCreateSystemManaged<SelectedInfoUISystem>();
            WreckingBallSection? section = world.GetOrCreateSystemManaged<WreckingBallSection>();

            sipSystem.AddMiddleSection(section);

            Log.Info("WreckingBallSection registered in SelectedInfoUISystem.");
        }

        public void OnDispose()
        {
            Log.Info(nameof(OnDispose));

            if (Settings != null)
            {
                Settings.UnregisterInOptionsUI();
                Settings = null;
            }

            // I don't think I need to explicitly remove the SIP section; game will tear it down with the world.
        }
    }
}
