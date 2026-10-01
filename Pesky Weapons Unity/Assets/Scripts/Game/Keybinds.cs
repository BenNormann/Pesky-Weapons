using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pesky.Game
{
    /// <summary>
    /// The rebindable keys (docs/SETTINGS.md): which Gameplay-map bindings the KEYBINDS page lists (every
    /// keyboard or mouse binding, composite parts one by one - MOVE UP, MOVE DOWN ... - except Look, which is
    /// the mouse itself, and Pause, which is Escape: the key that cancels a rebind and the browser's own
    /// pointer-lock release), how a binding is named and shown, the Input System's interactive rebind, and
    /// the PlayerPrefs persistence of the overrides as the Input System's own JSON. Ported from ATCK's
    /// Keybinds; PeskyControls has no control schemes, so keyboard / mouse is told by the binding path.
    ///
    /// The overrides live on the InputActionAsset in memory. A scene load can unload the asset (MainMenu
    /// does not use PeskyControls), so every gameplay scene applies them again in SettingsFlow.Awake, before
    /// any gameplay script reads an action.
    /// </summary>
    public static class Keybinds
    {
        public const string PrefsKey = "Pesky.Bindings";
        public const string MapName = "Gameplay";
        static readonly string[] Skipped = { "Look", "Pause" };

        /// <summary>What the page calls each action. An action missing here is named from its own name ("Curse1" -> "CURSE 1").</summary>
        static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            { "Jump", "LAUNCH / SOUL UP" },
            { "Possess", "POSSESS" },
            { "Release", "LEAVE WEAPON" },
            { "Move", "MOVE" },
            { "Descend", "SOUL DOWN" },
            { "Map", "MAP" },
            { "Nudge", "MAGE: NUDGE" },
            { "Pull", "MAGE: PULL" },
            { "Curse1", "MAGE: CURSE 1" },
            { "Curse2", "MAGE: CURSE 2" },
            { "Curse3", "MAGE: CURSE 3" },
            { "Curse4", "MAGE: CURSE 4" },
            { "Curse5", "MAGE: CURSE 5" },
        };

        public struct Entry
        {
            public InputAction action;
            public int bindingIndex;
            public string label;
        }

        /// <summary>Applies the saved overrides, if any, to the live asset. Safe to call again: it replaces whatever overrides the asset holds.</summary>
        public static void Load(InputActionAsset asset)
        {
            if (asset == null) return;
            string json = PlayerPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                asset.LoadBindingOverridesFromJson(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[keybinds] the saved key overrides could not be read and were dropped: " + e.Message);
                PlayerPrefs.DeleteKey(PrefsKey);
                PlayerPrefs.Save();
            }
        }

        public static void Save(InputActionAsset asset)
        {
            if (asset == null) return;
            PlayerPrefs.SetString(PrefsKey, asset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        /// <summary>RESET ALL: every override goes, in memory and in PlayerPrefs.</summary>
        public static void ResetAll(InputActionAsset asset)
        {
            if (asset != null) asset.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        /// <summary>Fills the list with every rebindable Gameplay binding in asset order; returns the count.</summary>
        public static int Collect(InputActionAsset asset, List<Entry> into)
        {
            into.Clear();
            InputActionMap map = asset != null ? asset.FindActionMap(MapName, false) : null;
            if (map == null) return 0;
            StringBuilder sb = new StringBuilder(32);
            for (int a = 0; a < map.actions.Count; a++)
            {
                InputAction action = map.actions[a];
                if (IsSkipped(action.name)) continue;
                var bindings = action.bindings;
                for (int i = 0; i < bindings.Count; i++)
                {
                    InputBinding b = bindings[i];
                    if (b.isComposite) continue;
                    if (!IsKeyboardOrMouse(b.path)) continue;
                    sb.Clear();
                    sb.Append(NameOf(action.name));
                    if (b.isPartOfComposite && !string.IsNullOrEmpty(b.name)) sb.Append(' ').Append(PartName(b.name));
                    Entry e;
                    e.action = action;
                    e.bindingIndex = i;
                    e.label = sb.ToString();
                    into.Add(e);
                }
            }
            return into.Count;
        }

        /// <summary>The authored path decides the row: a keyboard or mouse binding is listed, a gamepad one is not.</summary>
        static bool IsKeyboardOrMouse(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return path.StartsWith("<Keyboard>", StringComparison.Ordinal) || path.StartsWith("<Mouse>", StringComparison.Ordinal);
        }

        /// <summary>A WASD composite's legs as a player reads them: Up is forward, Down is back.</summary>
        static string PartName(string part)
        {
            switch (part.ToLowerInvariant())
            {
                case "up": return "FORWARD";
                case "down": return "BACK";
                default: return part.ToUpperInvariant();
            }
        }

        static bool IsSkipped(string name)
        {
            for (int i = 0; i < Skipped.Length; i++) if (Skipped[i] == name) return true;
            return false;
        }

        static string NameOf(string actionName)
        {
            string name;
            if (Names.TryGetValue(actionName, out name)) return name;
            StringBuilder sb = new StringBuilder(actionName.Length + 4);
            for (int i = 0; i < actionName.Length; i++)
            {
                char c = actionName[i];
                if (i > 0)
                {
                    char prev = actionName[i - 1];
                    if ((char.IsUpper(c) && !char.IsUpper(prev)) || (char.IsDigit(c) && !char.IsDigit(prev))) sb.Append(' ');
                }
                sb.Append(char.ToUpperInvariant(c));
            }
            return sb.ToString();
        }

        /// <summary>The key's short name for a binding ("SPACE", "E", "LMB"), with the override if there is one; a dash when unbound.</summary>
        public static string Display(in Entry entry)
        {
            if (entry.action == null) return "-";
            string text = entry.action.GetBindingDisplayString(entry.bindingIndex, InputBinding.DisplayStringOptions.DontIncludeInteractions);
            return string.IsNullOrEmpty(text) ? "-" : text.ToUpperInvariant();
        }

        /// <summary>True when this binding carries an override (the page marks it).</summary>
        public static bool IsOverridden(in Entry entry)
        {
            if (entry.action == null) return false;
            var bindings = entry.action.bindings;
            return entry.bindingIndex >= 0 && entry.bindingIndex < bindings.Count && !string.IsNullOrEmpty(bindings[entry.bindingIndex].overridePath);
        }

        /// <summary>
        /// The Input System's interactive rebind for one entry: any keyboard key or mouse button (never the
        /// pointer's position, delta or wheel, never "any key"), Escape cancels. The action is disabled while it
        /// listens (the Input System requires it) and enabled again afterwards if it was on.
        /// </summary>
        public static InputActionRebindingExtensions.RebindingOperation Rebind(in Entry entry,
            Action<InputActionRebindingExtensions.RebindingOperation> onComplete,
            Action<InputActionRebindingExtensions.RebindingOperation> onCancel)
        {
            InputAction action = entry.action;
            bool wasEnabled = action.enabled;
            action.Disable();
            return action.PerformInteractiveRebinding(entry.bindingIndex)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithControlsHavingToMatchPath("<Mouse>")
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithControlsExcluding("<Mouse>/clickCount")
                .WithControlsExcluding("<Pointer>/press")
                .WithControlsExcluding("<Keyboard>/anyKey")
                .WithControlsExcluding("<Keyboard>/escape")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(op => { if (wasEnabled) action.Enable(); if (onComplete != null) onComplete(op); })
                .OnCancel(op => { if (wasEnabled) action.Enable(); if (onCancel != null) onCancel(op); })
                .Start();
        }
    }
}
