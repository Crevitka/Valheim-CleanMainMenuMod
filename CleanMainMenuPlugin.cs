using BepInEx;
using BepInEx.Configuration;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

[BepInPlugin("crevitka.cleanmenu", "Clean Main Menu", "1.1.0")]
public class CleanMainMenuPlugin : BaseUnityPlugin
{
    // Toggles
    private ConfigEntry<bool> _hideModdedText;
    private ConfigEntry<bool> _hideShowLogButton;
    private ConfigEntry<bool> _hideTopRightMerch;
    private ConfigEntry<bool> _hideChangelog;
    private ConfigEntry<bool> _hideChangelogButton;
    private ConfigEntry<bool> _hideEulaButton;
    private ConfigEntry<bool> _hideVersionNumber;

    // Timing / reliability
    private ConfigEntry<float> _initialDelaySeconds;
    private ConfigEntry<int> _attempts;
    private ConfigEntry<float> _intervalSeconds;

    // Advanced
    private ConfigEntry<bool> _reapplyOnMenuScenes;
    private ConfigEntry<bool> _watchMenuContinuously;
    private ConfigEntry<float> _watchIntervalSeconds;

    private Coroutine _hideRoutine;
    private Coroutine _watchRoutine;
    private bool _changelogClosedOnce;

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
            "Hide the changelog panel and its 'new changes' notice (Valheim 1.0+: FejdStartup.m_changeLog / m_changeLogNotice; older: Canvas Changelog).");

        _hideChangelogButton = Config.Bind("General", "Hide Changelog button", true,
            "Hide the 'Show changelog' button added in Valheim 1.0 (FejdStartup.m_showChangelogButton). " +
            "If you keep the button, the changelog is only hidden once on menu load so you can still open it manually.");

        _hideEulaButton = Config.Bind("General", "Hide EULA button", true,
            "Hide the 'EULA' (user agreement) button in the main menu (FejdStartup.m_showEulaButton). " +
            "The game still shows the agreement window automatically when it is required.");

        _hideVersionNumber = Config.Bind("General", "Hide Version Number", true,
            "Hide Version Number (object name: version_text).");

        // Timing / reliability
        _initialDelaySeconds = Config.Bind("Timing", "Initial delay (seconds)", 1.5f,
            "Wait before first attempt (menu UI may spawn a bit later).");

        _attempts = Config.Bind("Timing", "Attempts", 10,
            "How many times to try hiding (useful when UI spawns late or gets recreated).");

        _intervalSeconds = Config.Bind("Timing", "Interval between attempts (seconds)", 1.0f,
            "Delay between attempts.");

        // Advanced
        _reapplyOnMenuScenes = Config.Bind("Advanced", "Reapply on menu scene load", true,
            "Re-apply cleanup whenever a menu scene loads (fixes UI coming back after exiting a world).");

        _watchMenuContinuously = Config.Bind("Advanced", "Watch menu continuously", true,
            "While in the menu, keep re-applying cleanup periodically (handles UI being recreated later).");

        _watchIntervalSeconds = Config.Bind("Advanced", "Watch interval (seconds)", 1.0f,
            "How often to re-apply cleanup while watching the menu.");

        if (_reapplyOnMenuScenes.Value)
            SceneManager.sceneLoaded += OnSceneLoaded;

        Logger.LogInfo("Clean Main Menu loaded (config initialized).");
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        // First run (game startup)
        StartApplyForCurrentScene();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // When returning to menu (or any scene load), re-apply if it's a menu scene.
        StartApplyForScene(scene);
    }

    private void StartApplyForCurrentScene()
    {
        StartApplyForScene(SceneManager.GetActiveScene());
    }

    private void StartApplyForScene(Scene scene)
    {
        if (!IsMenuScene(scene))
        {
            StopWatchdog();
            return;
        }

        RestartHideRoutine();

        if (_watchMenuContinuously.Value)
            StartWatchdog();
        else
            StopWatchdog();
    }

    /// <summary>
    /// Valheim main menu scene is typically "start". We also allow fallback detection by looking for StartGui/GUI.
    /// </summary>
    private bool IsMenuScene(Scene scene)
    {
        if (!string.IsNullOrEmpty(scene.name))
        {
            string lower = scene.name.ToLower();
            if (lower == "start" || lower.Contains("start"))
                return true;
        }

        // fallback check
        return GameObject.Find("StartGui") != null || GameObject.Find("GUI") != null;
    }

    private void RestartHideRoutine()
    {
        if (_hideRoutine != null)
        {
            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }
        _changelogClosedOnce = false;
        _hideRoutine = StartCoroutine(HideRoutine());
    }

    private void StartWatchdog()
    {
        if (_watchRoutine != null) return;
        _watchRoutine = StartCoroutine(MenuWatchdog());
    }

    private void StopWatchdog()
    {
        if (_watchRoutine != null)
        {
            StopCoroutine(_watchRoutine);
            _watchRoutine = null;
        }
    }

    private IEnumerator HideRoutine()
    {
        float initialDelay = Mathf.Max(0f, _initialDelaySeconds.Value);
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        int tries = Mathf.Clamp(_attempts.Value, 1, 60);
        float wait = Mathf.Clamp(_intervalSeconds.Value, 0.1f, 10f);

        for (int i = 0; i < tries; i++)
        {
            ApplyOnce(true);
            yield return new WaitForSeconds(wait);
        }

        Logger.LogInfo("Clean Main Menu applied.");
    }

    private IEnumerator MenuWatchdog()
    {
        float wait = Mathf.Clamp(_watchIntervalSeconds.Value, 0.25f, 10f);

        while (true)
        {
            // If we left the menu, stop watching.
            if (!IsMenuScene(SceneManager.GetActiveScene()))
            {
                StopWatchdog();
                yield break;
            }

            ApplyOnce(false);
            yield return new WaitForSeconds(wait);
        }
    }

    private void ApplyOnce(bool initialPass)
    {
        if (_hideModdedText.Value) HideByName("modded_text");
        if (_hideShowLogButton.Value) HideByName("showlog");
        if (_hideTopRightMerch.Value) HideByName("TopRight");
        if (_hideChangelog.Value) HideByName("Canvas Changelog"); // pre-1.0 layout
        HideChangelog(initialPass);
        if (_hideEulaButton.Value) HideStartupField(EulaButtonField, "EULA button");
        if (_hideVersionNumber.Value) HideByName("version_text");
    }

    // Valheim 1.0 moved the changelog into FejdStartup: a toggleable panel (m_changeLog),
    // a "new changes" badge (m_changeLogNotice) and a side button (m_showChangelogButton).
    // Fields are read via reflection so the mod keeps working on builds that don't have them.
    private static readonly BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly FieldInfo ChangeLogField = typeof(FejdStartup).GetField("m_changeLog", FieldFlags);
    private static readonly FieldInfo ChangeLogNoticeField = typeof(FejdStartup).GetField("m_changeLogNotice", FieldFlags);
    private static readonly FieldInfo ChangeLogButtonField = typeof(FejdStartup).GetField("m_showChangelogButton", FieldFlags);
    private static readonly FieldInfo EulaButtonField = typeof(FejdStartup).GetField("m_showEulaButton", FieldFlags);

    private void HideStartupField(FieldInfo field, string label)
    {
        var startup = FejdStartup.instance;
        if (startup != null) HideField(startup, field, label);
    }

    private void HideChangelog(bool initialPass)
    {
        var startup = FejdStartup.instance;
        if (startup == null) return;

        bool hideButton = _hideChangelogButton.Value;

        if (hideButton)
            HideField(startup, ChangeLogButtonField, "Changelog button");

        if (!_hideChangelog.Value) return;

        HideField(startup, ChangeLogNoticeField, "Changelog notice");

        // If the button is still visible, only close the panel once per menu visit,
        // otherwise the watchdog would immediately close it when the player opens it.
        if (hideButton || (initialPass && !_changelogClosedOnce))
        {
            if (ChangeLogField?.GetValue(startup) is GameObject) _changelogClosedOnce = true;
            HideField(startup, ChangeLogField, "Changelog panel");
        }
    }

    private void HideField(FejdStartup startup, FieldInfo field, string label)
    {
        if (field == null) return;

        var obj = field.GetValue(startup) as GameObject;
        if (obj == null || !obj.activeSelf) return;

        obj.SetActive(false);
        Logger.LogInfo($"Hidden: {label} ({obj.name})");
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