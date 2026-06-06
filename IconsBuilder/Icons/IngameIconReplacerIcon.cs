using System;
using System.Linq;
using ExileCore2.PoEMemory;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared;
using ExileCore2.Shared.Enums;
using ExileCore2.Shared.Helpers;

namespace MinimapIcons.IconsBuilder.Icons;

public class IngameItemReplacerIcon : BaseIcon
{
    public IngameItemReplacerIcon(Entity entity, IconsBuilderSettings settings, MapIconsIndex mapIconsIndex)
        : base(entity)
    {
        Show = () => !entity.IsValid;

        var iconSizeMultiplier = RemoteMemoryObject.TheGame.Files.MinimapIcons.EntriesList.ElementAtOrDefault((int)mapIconsIndex)?.LargeMinimapSize ?? 1;
        MainTexture = new HudTexture("Icons.png")
        {
            UV = SpriteHelper.GetUV(mapIconsIndex),
            Size = RemoteMemoryObject.TheGame.IngameState.IngameUi.Map.LargeMapZoom * RemoteMemoryObject.TheGame.IngameState.Camera.Height * iconSizeMultiplier / 64,
        };
    }
}

public class IngameIconReplacerIcon : BaseIcon
{
    private bool _isHidden;
    private int _transitionableFlag1;
    private bool _shrineIsAvailable;
    private bool _isOpened;
    private bool _isIgnoreHidden;

    public IngameIconReplacerIcon(Entity entity, IconsBuilderSettings settings, MapIconsSettings mapIconsSettings)
        : base(entity)
    {
        _isHidden = false;
        _transitionableFlag1 = 1;
        _shrineIsAvailable = true;
        _isOpened = false;
        _isIgnoreHidden = mapIconsSettings.IgnoreHiddenStatusMinimapIcons.Content.Any(x => IconsBuilder.GetRegex(x.Value).IsMatch(entity.Path ?? string.Empty));

        T Update<T>(ref T store, Func<T> update)
        {
            if (!entity.IsValid)
                return store;
            
            try
            {
                return store = update();
            }
            catch
            {
                // GetComponent can throw IndexOutOfRangeException when entity component dictionary is corrupted
                return store;
            }
        }

        Show = () => !Update(ref _isHidden, () => !_isIgnoreHidden &&
                                                  (entity.GetComponent<MinimapIcon>()?.IsHide ?? _isHidden)) &&
                     Update(ref _transitionableFlag1, () => _isIgnoreHidden ? 1 : entity.GetComponent<Transitionable>()?.Flag1 ?? 1) == 1 &&
                     Update(ref _shrineIsAvailable, () => entity.GetComponent<Shrine>()?.IsAvailable ?? _shrineIsAvailable) &&
                     !Update(ref _isOpened, () => entity.GetComponent<Chest>()?.IsOpened ?? _isOpened) &&
                     (!entity.IsValid || mapIconsSettings.AlwaysShownIngameIcons.Content.Any(x => IconsBuilder.GetRegex(x.Value).IsMatch(entity.Path ?? string.Empty)));

        string name = "";
        try
        {
            name = entity.GetComponent<MinimapIcon>()?.Name ?? "";
        }
        catch
        {
            // GetComponent can throw IndexOutOfRangeException during entity transitions
            name = "";
        }

        var iconIndexByName = ExileCore2.Shared.Helpers.Extensions.IconIndexByName(name);

        var iconSizeMultiplier = RemoteMemoryObject.TheGame.Files.MinimapIcons.EntriesList.ElementAtOrDefault((int)iconIndexByName)?.LargeMinimapSize ?? 1;
        MainTexture = new HudTexture("Icons.png")
        {
            UV = SpriteHelper.GetUV(iconIndexByName),
            Size = RemoteMemoryObject.TheGame.IngameState.IngameUi.Map.LargeMapZoom * RemoteMemoryObject.TheGame.IngameState.Camera.Height * iconSizeMultiplier / 64,
        };
    }
}