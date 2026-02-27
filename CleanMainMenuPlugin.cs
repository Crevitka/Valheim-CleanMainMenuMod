using BepInEx;
using BepInEx.Configuration;
using System.Collections;
using UnityEngine;

[BepInPlugin("crevitka.cleanmenu", "Clean Main Menu", "1.0.0")]
public class CleanMainMenuPlugin : BaseUnityPlugin
{
    // Toggles
    private ConfigEntry<bool> _hideModdedText;
    private ConfigEntry<bool> _hideShowLogButton;
    private ConfigEntry<bool> _hideTopRightMerch;
    private ConfigEntry<bool> _hideChangelog;

    // Timing
    private ConfigEntry<int> _attempts;
    private ConfigEntry<float> _intervalSeconds;
    private ConfigEntry<float> _initialDelaySeconds;

    private void Awake()
    {
        // General
        _hideModdedText = Config.Bind("General", "Hide modded_text", true,
            "Hide the 'modded' warning text in main menu (object name: modded_text).");

        _hideShowLogButton = Config.Bind("General", "Hide showlog button", true,
            "Hide 'showlog' button in main menu (object name: showlog).");

        _hideTopRightMerch = Config.Bind("General", "Hide TopRight (Merch)", true,
            "Hide top-right panel (usually merch store / icons) (object name: TopRight).");

        _hideChangelog = Config.Bind("General", "Hide Changelog", true,
            "Hide left-side changelog panel (object name: Canvas Changelog).");

        // Timing / reliability
        _initialDelaySeconds = Config.Bind("Timing", "Initial delay (seconds)", 1.5f,
            "Wait before first attempt (menu UI may spawn a bit позже).");

        _attempts = Config.Bind("Timing", "Attempts", 10,
            "How many times to try hiding (useful when UI spawns late or gets recreated).");

        _intervalSeconds = Config.Bind("Timing", "Interval between attempts (seconds)", 1.0f,
            "Delay between attempts.");

        Logger.LogInfo("Clean Main Menu loaded (config initialized).");
    }

    private void Start()
    {
        StartCoroutine(HideRoutine());
    }

    private IEnumerator HideRoutine()
    {
        if (_initialDelaySeconds.Value > 0f)
            yield return new WaitForSeconds(_initialDelaySeconds.Value);

        int tries = Mathf.Max(1, _attempts.Value);
        float wait = Mathf.Max(0.1f, _intervalSeconds.Value);

        for (int i = 0; i < tries; i++)
        {
            ApplyOnce();
            yield return new WaitForSeconds(wait);
        }

        Logger.LogInfo("Clean Main Menu applied.");
    }

    private void ApplyOnce()
    {
        if (_hideModdedText.Value) HideByName("modded_text");
        if (_hideShowLogButton.Value) HideByName("showlog");
        if (_hideTopRightMerch.Value) HideByName("TopRight");
        if (_hideChangelog.Value) HideByName("Canvas Changelog");
    }

    private void HideByName(string name)
    {
        var obj = GameObject.Find(name);
        if (obj == null) return;

        if (obj.activeSelf)
        {
            obj.SetActive(false);
            Logger.LogInfo($"Hidden: {name}");
        }
    }
}