// Settings/Setting.cs
// Purpose: Options UI — simple About tab for Wrecking Ball (name, version, links).

namespace WreckingBall
{
    using System;
    using Colossal.IO.AssetDatabase;
    using Game.Modding;
    using Game.Settings;
    using UnityEngine;

    [FileLocation("ModsSettings/WreckingBall/WreckingBall")]
    [SettingsUITabOrder(kAboutTab)]
    [SettingsUIGroupOrder(kAboutInfoGroup, kAboutLinksGroup)]
    [SettingsUIShowGroupName(kAboutLinksGroup)]
    public sealed class Setting : ModSetting
    {
        // Tabs
        public const string kAboutTab = "About";

        // Groups
        public const string kAboutInfoGroup = "Info";
        public const string kAboutLinksGroup = "Links";

        // Links
        private const string kUrlDiscord =
            "https://discord.gg/HTav7ARPs2";

        private const string kUrlParadox =
            "https://mods.paradoxplaza.com/authors/kimosabe1/cities_skylines_2?games=cities_skylines_2&orderBy=desc&sortBy=best&time=alltim";

        public Setting(IMod mod)
            : base(mod)
        {
        }

        public override void SetDefaults()
        {
            // No configurable gameplay settings yet.
        }

        // ---- About: info ----

        [SettingsUISection(kAboutTab, kAboutInfoGroup)]
        public string ModName => Mod.ModName;

        [SettingsUISection(kAboutTab, kAboutInfoGroup)]
        public string ModVersion => Mod.ModVersion;

        // ---- About: links ----

        [SettingsUIButtonGroup(kAboutLinksGroup)]
        [SettingsUIButton]
        [SettingsUISection(kAboutTab, kAboutLinksGroup)]
        public bool OpenParadox
        {
            set
            {
                if (value)
                {
                    TryOpen(kUrlParadox);
                }
            }
        }

        [SettingsUIButtonGroup(kAboutLinksGroup)]
        [SettingsUIButton]
        [SettingsUISection(kAboutTab, kAboutLinksGroup)]
        public bool OpenDiscord
        {
            set
            {
                if (value)
                {
                    TryOpen(kUrlDiscord);
                }
            }
        }

        private static void TryOpen(string url)
        {
            try
            {
                Application.OpenURL(url);
            }
            catch (Exception ex)
            {
                Mod.Log.Warn($"Open URL failed: {ex.Message}");
            }
        }
    }
}
