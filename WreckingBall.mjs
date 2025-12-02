// WreckingBall.mjs
// Purpose: Adds "Abandon" and "Destroy" buttons to the Selected Info Panel
//          for buildings, wired to the WreckingBall C# triggers.

const modInfo = {
    id: "WreckingBall",  // must match Mod.ModId on the C# side for triggers
};

// CS2 modding globals
const modding = window["cs2/modding"];
const api = window["cs2/api"];
const l10n = window["cs2/l10n"];
const input = window["cs2/input"];
const ui = window["cs2/ui"];
const React = window["React"]; // Using React.createElement (no JSX)

// Re-use the vanilla info row visuals
const infoRowStyles = modding.getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.module.scss",
    "classes"
);

const InfoSection = modding.getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.tsx",
    "InfoSection"
);

const InfoRow = modding.getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.tsx",
    "InfoRow"
);

// Slightly smaller buttons with consistent width.
const buttonStyle = {
    padding: "2px 10px",
    minWidth: 100,
};

// Extends the SIP section renderer with the C# InfoSection type.
function registerSection(components) {
    console.log("[WreckingBall] registerSection called");

    // C# full type name: namespace + class
    // public sealed partial class WreckingBallSection : InfoSectionBase
    // namespace WreckingBall
    components["WreckingBall.WreckingBallSection"] =
        function WreckingBallSectionComponent() {
            const { translate } = l10n.useLocalization();

            const abandonLabel = translate(
                "WreckingBall/Buttons/Abandon",
                "Abandon"
            );
            const destroyLabel = translate(
                "WreckingBall/Buttons/Destroy",
                "Destroy"
            );

            const abandonTooltip = translate(
                "WreckingBall/Tooltips/Abandon",
                "Mark this building as abandoned (shows the standard warning icon)."
            );
            const destroyTooltip = translate(
                "WreckingBall/Tooltips/Destroy",
                "Collapse this building now so the game can spawn rubble, VFX and cleanup."
            );

            const onAbandonClick = () => {
                console.log("[WreckingBall] Abandon button clicked");
                api.trigger(modInfo.id, "AbandonBuilding");
            };

            const onDestroyClick = () => {
                console.log("[WreckingBall] Destroy button clicked");
                api.trigger(modInfo.id, "DestroyBuilding");
            };

            return React.createElement(
                InfoSection,
                { focusKey: input.FOCUS_DISABLED, disableFocus: true },
                React.createElement(InfoRow, {
                    left: "",
                    right: React.createElement(
                        ui.Button,
                        {
                            focusKey: input.FOCUS_DISABLED,
                            onSelect: onAbandonClick,
                            style: buttonStyle,
                        },
                        abandonLabel
                    ),
                    tooltip: abandonTooltip,
                    uppercase: true,
                    disableFocus: true,
                    subRow: false,
                    className: infoRowStyles.infoRow,
                }),
                React.createElement(InfoRow, {
                    left: "",
                    right: React.createElement(
                        ui.Button,
                        {
                            focusKey: input.FOCUS_DISABLED,
                            onSelect: onDestroyClick,
                            style: buttonStyle,
                        },
                        destroyLabel
                    ),
                    tooltip: destroyTooltip,
                    uppercase: true,
                    disableFocus: true,
                    subRow: false,
                    className: infoRowStyles.infoRow,
                })
            );
        };

    return components;
}

// Standard UI-mod entry used by the module registry
export default function register(registry) {
    console.log("[WreckingBall] UI register() called");

    registry.extend(
        "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx",
        "selectedInfoSectionComponents",
        registerSection
    );

    console.log("[WreckingBall] selectedInfoSectionComponents extended");
}

export const hasCSS = false;
