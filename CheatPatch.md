# Host Override Feature - Changes Summary

This document outlines all the changes made to implement the Host Override feature, which allows the host to enforce their minimap settings on all clients.

## Overview

The Host Override feature enables the lobby host to:
- Push their minimap toggle settings to all clients
- Lock client settings so they cannot be changed while override is active
- Automatically sync settings to newly joined players
- Restore client settings when override is disabled

## Files Modified

### 1. `Plugin.cs`

#### Added Config Entry
- **New field**: `public static ConfigEntry<bool> hostOverrideActive`
  - Tracks whether host override is currently active
  - Stored in config file under section "Host Override" with key "Host Override Active"
  - Initialized to `false` on mod startup to prevent users from being locked out

#### Modified `SetBindings()`
- Added binding for `hostOverrideActive` config entry
- Sets `hostOverrideActive.Value = false` on startup and saves config

#### Modified `SyncGUIFromConfigs()`
- Added check: `if (isHost || hostOverrideActive == null || !hostOverrideActive.Value)`
- Only loads toggle settings from config if host OR host override is not active
- Prevents overwriting host-enforced settings when `RestoreFromConfig()` is called

#### Modified `SyncConfigFromGUI()`
- Added check: `if (isHost || !hostOverrideActive.Value)`
- Only saves toggle settings to config if host OR host override is not active
- Preserves client's local settings when host override is active

#### Added Static Config Access
- Added `public static ConfigFile Config` field
- Initialized in `Awake()` as `Config = base.Config`
- Allows `MinimapGUI` to save config changes

### 2. `MinimapGUI.cs`

#### Added Fields
- **New field**: `public bool hostOverride` - Toggle for host to enable/disable override
- **New field**: `private bool lastHostOverrideState` - Tracks previous host override state (host only)
- **New field**: `private bool lastClientHostOverrideActive` - Tracks previous state of `hostOverrideActive` on client
- **New fields**: `private bool? lockedEnableMinimap`, `lockedShowLoots`, `lockedShowEnemies`, etc.
  - Store locked values for enforcement when host override is active
- **New fields**: `private bool lastEnableMinimap`, `lastShowLoots`, etc.
  - Track last sent values to detect changes on host

#### Modified `Awake()`
- Updated `toggleMinimapKey.OnKey` to check host override before allowing toggle

#### Added Methods

**`HandleHostOverrideSync()`**
- Called in `Update()` to manage host override syncing
- **Host side**: Detects when `hostOverride` changes and sends settings to clients
- **Client side**: Detects when `hostOverrideActive` transitions from `true` to `false` and restores local settings

**`UpdateLastToggleValues()`**
- Updates tracking variables with current toggle values (host only)

**`HasToggleSettingsChanged()`**
- Checks if any toggle settings have changed since last update (host only)

**`SendHostSettingsToClients()`**
- Serializes current toggle settings and broadcasts them to all clients

**`SerializeToggleSettings()`**
- Converts all toggle settings into a string format for network transmission
- Includes: `enableMinimap`, `showLoots`, `showEnemies`, `showTurrets`, `showLivePlayers`, `showDeadPlayers`, `showRadarBoosters`, `showTerminalCodes`, `showShipArrow`, `showCompass`, `showHeadCam`

**`ApplyHostSettings(string settingsData)`**
- Parses received settings string and applies them to the client
- Sets `hostOverrideActive.Value = true` and saves config
- Locks all values in `locked*` fields for enforcement
- Only executes on non-host clients

**`RestoreFromConfig()`**
- Clears all locked values
- Sets `hostOverrideActive.Value = false` and saves config
- Calls `SyncGUIFromConfigs()` to restore local settings

**`EnforceHostSettings()`**
- Called in `Update()` and at end of `OnGUI()`
- Continuously enforces locked values for non-host clients when host override is active
- Prevents any changes from GUI, hotkeys, or other code paths

#### Modified `Update()`
- Added calls to `EnforceHostSettings()` before and after `HandleHostOverrideSync()`
- Ensures settings are enforced every frame

#### Modified `OnGUI()`

**Case 0 (Minimap Tab)**
- Added `shouldDisableTogglesTab0` check based on `hostOverrideActive.Value`
- Disables `enableMinimap` toggle for non-hosts when host override is active
- Stores and restores old value to prevent changes when disabled

**Case 1 (Icons Tab)**
- Added `shouldDisableToggles` check based on `hostOverrideActive.Value`
- Disables all icon toggles for non-hosts when host override is active
- Stores and restores old values to prevent changes when disabled
- Disables "Show all Icons" and "Hide all Icons" buttons when host override is active
- `hostOverride` toggle is only enabled for host users

### 3. `Patches/HUDManagerPatch.cs`

#### Modified `ProcessBroadcastMessage()`
- Added handling for "HostOverrideSettings" signature
  - Calls `MinimapGUI.ApplyHostSettings(data)` on clients
- Added handling for "HostOverrideDisabled" signature
  - Calls `MinimapGUI.RestoreFromConfig()` on clients

#### Modified `DontSendMinimapMessagesPatch`
- Allows broadcast messages to pass through (for host override communication)

### 4. `Patches/PlayerControllerBPatch.cs`

#### Modified `DisplayMinimapPatch`
- Added check for host override when new player joins
- If host has `hostOverride` enabled, calls `SendSettingsToNewPlayerDelayed()` to send settings to new player

## Behavior Flow

### When Host Enables Override:
1. Host toggles `hostOverride` in GUI
2. `HandleHostOverrideSync()` detects change
3. `SendHostSettingsToClients()` serializes and broadcasts settings
4. Clients receive broadcast via `ProcessBroadcastMessage()`
5. Clients call `ApplyHostSettings()` which:
   - Sets `hostOverrideActive.Value = true` and saves config
   - Applies all host settings
   - Locks values for enforcement
6. Client GUI toggles become disabled (greyed out)

### When Host Changes Settings (Override Active):
1. Host changes any toggle setting
2. `HandleHostOverrideSync()` detects change via `HasToggleSettingsChanged()`
3. `SendHostSettingsToClients()` broadcasts updated settings
4. Clients receive and apply updated settings

### When New Player Joins (Override Active):
1. `DisplayMinimapPatch` detects new player connection
2. If host has `hostOverride` enabled, calls `SendSettingsToNewPlayerDelayed()`
3. After 0.5 second delay, host sends settings to new player
4. New player applies settings same as above

### When Host Disables Override:
1. Host toggles `hostOverride` off
2. `HandleHostOverrideSync()` detects change
3. Host broadcasts "HostOverrideDisabled" message
4. Clients receive message and call `RestoreFromConfig()`
5. `RestoreFromConfig()`:
   - Sets `hostOverrideActive.Value = false` and saves config
   - Clears locked values
   - Restores local settings from config
6. Client GUI toggles become enabled again

### Enforcement:
- `EnforceHostSettings()` runs every frame in `Update()`
- Also runs at end of `OnGUI()` after GUI interactions
- Continuously forces locked values for non-host clients
- Prevents any code path from changing settings when override is active

## Config File Changes

### New Config Entry
```ini
[Host Override]
# Indicates if host override is currently active (managed by the mod, do not edit manually)
Host Override Active = False
```

- Automatically set to `False` on mod startup
- Set to `True` when client receives host override settings
- Set to `False` when host disables override or mod restarts

## Network Communication

Settings are transmitted via hidden chat messages using the format:
```
[MinimapMod:CLIENT_ID:HostOverrideSettings:SETTINGS_DATA]
```

Where `SETTINGS_DATA` is a semicolon-separated string like:
```
enableMinimap=show;loots=hide;enemies=show;...
```

## Key Design Decisions

1. **Config-based State**: Uses `hostOverrideActive` config entry as source of truth for whether override is active
2. **Persistent Locking**: Locked values are stored in nullable bool fields and enforced every frame
3. **GUI Protection**: Multiple layers of protection (GUI.enabled, value restoration, enforcement)
4. **Automatic Cleanup**: Config resets to false on startup to prevent lockout
5. **Non-destructive**: Client's local settings are preserved and restored when override is disabled

