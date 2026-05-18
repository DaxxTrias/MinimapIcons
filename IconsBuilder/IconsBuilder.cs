using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;
using GameOffsets2.Native;
using MinimapIcons.IconsBuilder.Icons;

namespace MinimapIcons.IconsBuilder;

public class IconsBuilder
{
    private readonly MinimapIcons _plugin;

    public IconsBuilder(MinimapIcons plugin)
    {
        _plugin = plugin;
    }

    public IconsBuilderSettings Settings => _plugin.Settings.IconsBuilderSettings;

    private string DefaultAlertFile => Path.Combine(_plugin.DirectoryFullName, "config", "mod_alerts.txt");
    private string CustomAlertFile => Path.Combine(_plugin.ConfigDirectory, "mod_alerts.txt");
    private string DefaultIgnoreFile => Path.Combine(_plugin.DirectoryFullName, "config", "ignored_entities.txt");
    private string CustomIgnoreFile => Path.Combine(_plugin.ConfigDirectory, "ignored_entities.txt");

    private List<string> IgnoredEntities { get; set; } = new List<string>();
    private Dictionary<string, Vector2i> AlertEntitiesWithIconSize { get; set; } = new Dictionary<string, Vector2i>();

    private static EntityType[] SkippedEntityTypes =>
    [
        EntityType.HideoutDecoration, 
        EntityType.Effect, 
        EntityType.Light, 
        EntityType.ServerObject, 
        EntityType.Daemon,
        EntityType.Error,
    ];

    private int RunCounter { get; set; }
    private int IconVersion;
        
    private void ReadAlertFile()
    {
        var customAlertFilePath = CustomAlertFile;
        var path = File.Exists(customAlertFilePath) ? customAlertFilePath : DefaultAlertFile;
        if (!File.Exists(path))
        {
            DebugWindow.LogError($"IconsBuilder -> Alert entities file does not exist. Path: {path}");
            return;
        }
        var readAllLines = File.ReadAllLines(path);

        foreach (var readAllLine in readAllLines)
        {
            if (readAllLine.StartsWith('#')) continue;
            var entityMetadata = readAllLine.Split(';');
            var iconSize = entityMetadata[2].Trim().Split(',');
            AlertEntitiesWithIconSize[entityMetadata[0]] = new Vector2i(int.Parse(iconSize[0]), int.Parse(iconSize[1]));
        }
    }

    private void ReadIgnoreFile()
    {
        var customIgnoreFilePath = CustomIgnoreFile;
        var path = File.Exists(customIgnoreFilePath) ? customIgnoreFilePath : DefaultIgnoreFile;
        if (!File.Exists(path))
        {
           _plugin.LogError($"IconsBuilder -> Ignored entities file does not exist. Path: {path}");
            IgnoredEntities = new List<string>();
            return;
        }
        IgnoredEntities = File.ReadAllLines(path).Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#')).ToList();
    }

    public void AreaChange(AreaInstance area)
    {
        ReadAlertFile();
        ReadIgnoreFile();
    }

    public bool Initialise()
    {
        Settings.ResetIcons.OnPressed = () => { IconVersion++; };
        ReadAlertFile();           
        ReadIgnoreFile();
        return true;
    }

    public void Tick()
    {
        RunCounter++;
        if (RunCounter % Settings.RunEveryXTicks.Value != 0) return;

        AddIcons();
    }

    private void AddIcons()
    {
        var entities = _plugin.GameController?.EntityListWrapper?.OnlyValidEntities;
        if (entities == null) return;

        foreach (var entity in entities.ToArray())
        {
            try
            {
                if (entity == null) continue;
                if (entity.GetHudComponent<BaseIcon>() is { Version: var version, } && version >= IconVersion) continue;
                if (SkipIcon(entity)) continue;

                var icon = GenerateIcon(entity);
                if (icon == null) continue;
                icon.Version = IconVersion;
                entity.SetHudComponent(icon);
            }
            catch (Exception ex)
            {
                _plugin.LogError($"Failed to build an icon for {entity}: {ex}");
            }
        }
    }

    private bool SkipIcon(Entity entity)
    {
        if (entity is not { IsValid: true }) return true;
        if (SkippedEntityTypes.Any(x => x == entity.Type)) return true;
        if (IgnoredEntities.Any(x => entity.Path?.Contains(x) == true)) return true;

        return false;
    }

    private readonly ConditionalWeakTable<string, Regex> _regexes = [];

    private BaseIcon? GenerateIcon(Entity entity)
    {
        // Early validation
        if (entity == null || !entity.IsValid)
            return null;

        var metadata = entity.Metadata ?? string.Empty;
        if (Settings.CustomIcons.Content
                .FirstOrDefault(x => _regexes.GetValue(x.MetadataRegex.Value, p => new Regex(p))!.IsMatch(metadata)) is { } customIconConfig)
        {
            return new CustomIcon(entity, Settings, customIconConfig);
        }

        if (entity.Type == EntityType.WorldItem)
        {
            if (Settings.UseReplacementsForItemIconsWhenOutOfRange)
            {
                try
                {
                    if (entity.TryGetComponent<WorldItem>(out var worldItem) && 
                        worldItem.Icon is var icon && 
                        icon != MapIconsIndex.None)
                    {
                        return new IngameItemReplacerIcon(entity, Settings, icon);
                    }
                }
                catch
                {
                    // TryGetComponent can throw IndexOutOfRangeException when component dictionary is corrupted
                    return null;
                }
            }
            return null;
        }

        if (Settings.UseReplacementsForGameIconsWhenOutOfRange)
        {
            try
            {
                if (entity.TryGetComponent<MinimapIcon>(out var minimapIconComponent) && 
                    !minimapIconComponent.IsHide)
                {
                    try
                    {
                        var name = minimapIconComponent.Name;
                        if (!string.IsNullOrEmpty(name))
                        {
                            return new IngameIconReplacerIcon(entity, Settings, _plugin.Settings);
                        }
                    }
                    catch
                    {
                        // minimapIconComponent.Name can throw for certain entities during transitions
                        return null;
                    }
                }
            }
            catch
            {
                // TryGetComponent can throw IndexOutOfRangeException when component dictionary is corrupted
                return null;
            }
        }

        //Monsters
        if (entity.Type == EntityType.Monster)
        {
            try
            {
                if (!entity.IsAlive) return null;

                if (entity.League == LeagueType.Delirium)
                    return new DeliriumIcon(entity, Settings, AlertEntitiesWithIconSize);

                return new MonsterIcon(entity, Settings, AlertEntitiesWithIconSize);
            }
            catch
            {
                // Entity properties can throw during state transitions
                return null;
            }
        }

        //NPC
        if (entity.Type == EntityType.Npc)
            return new NpcIcon(entity, Settings);

        //Player
        if (entity.Type == EntityType.Player)
        {
            try
            {
                Player? player = null;
                if (!entity.TryGetComponent(out player) || player == null)
                    return null;

                if (player.PlayerName is not {} playerName)
                    return null;

                if (_plugin.GameController.IngameState.Data.LocalPlayer.Address == entity.Address)
                    return null;

                // Safe check for local player render name
                try
                {
                    if (_plugin.GameController.IngameState.Data.LocalPlayer.TryGetComponent<Render>(out var localPlayerRender) && 
                        localPlayerRender.Name == entity.RenderName)
                        return null;
                }
                catch
                {
                    // TryGetComponent on local player can also throw
                }

                if (!entity.IsValid) return null;
                return new PlayerIcon(entity, Settings, playerName);
            }
            catch
            {
                // TryGetComponent can throw IndexOutOfRangeException when component dictionary is corrupted
                return null;
            }
        }

        //Chests
        if (entity.Type == EntityType.Chest)
        {
            try
            {
                if (!entity.IsOpened)
                    return new ChestIcon(entity, Settings);
            }
            catch
            {
                // IsOpened can throw for certain chest entities
                return null;
            }
        }

        //Area transition
        if (entity.Type == EntityType.AreaTransition)
            return new MiscIcon(entity, Settings);

        //Shrine
        if (entity.TryGetComponent<Shrine>(out _))
            return new ShrineIcon(entity, Settings);

        // Minimap icon with transitionable component (portals, mission markers, etc.)
        if (entity.TryGetComponent<MinimapIcon>(out var mmIcon))
        {
            try
            {
                // Safely check for mission marker
                var isMissionMarker = false;
                try
                {
                    isMissionMarker = string.Equals("Metadata/MiscellaneousObjects/MissionMarker", entity.Path, StringComparison.Ordinal) ||
                                      (!string.IsNullOrEmpty(mmIcon.Name) && string.Equals(mmIcon.Name, "MissionTarget", StringComparison.Ordinal));
                }
                catch
                {
                    // mmIcon.Name can throw for certain entities during transitions
                    isMissionMarker = false;
                }

                // Safely check for Transitionable component
                bool hasTransitionable = false;
                try
                {
                    hasTransitionable = entity.HasComponent<Transitionable>();
                }
                catch
                {
                    // HasComponent can throw when entity is in invalid state
                    hasTransitionable = false;
                }

                if (hasTransitionable)
                {
                    // Mission marker
                    if (isMissionMarker)
                        return new MissionMarkerIcon(entity, Settings);

                    return new MiscIcon(entity, Settings);
                }
            }
            catch
            {
                // If anything else goes wrong with MinimapIcon processing, skip this entity
                return null;
            }
        }

        // Targetable minimap icons and special cases
        try
        {
            if ((entity.TryGetComponent<MinimapIcon>(out _) && entity.TryGetComponent<Targetable>(out _)) ||
                entity.Path is "Metadata/Terrain/Leagues/Sanctum/Objects/SanctumMote")
                return new MiscIcon(entity, Settings);
        }
        catch
        {
            // Component access can throw during entity transitions
            return null;
        }

        return null;
    }
}