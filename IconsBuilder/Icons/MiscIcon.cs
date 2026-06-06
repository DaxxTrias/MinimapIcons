using System;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared;
using ExileCore2.Shared.Enums;
using ExileCore2.Shared.Helpers;

namespace MinimapIcons.IconsBuilder.Icons;

public class MiscIcon : BaseIcon
{
    public MiscIcon(Entity entity, IconsBuilderSettings settings) : base(entity)
    {
        Update(entity, settings);
    }

    public override string ToString()
    {
        return $"{Entity.Path} : ({Entity.Type}) :{Text}";
    }

    public void Update(Entity entity, IconsBuilderSettings settings)
    {
        if (_HasIngameIcon)
        {
            MainTexture.Size = settings.MiscIngameIconSize;
            Text = RenderName;
            Priority = IconPriority.VeryHigh;
            return;
        }

        MainTexture = new HudTexture();
        MainTexture.FileName = "Icons.png";
        MainTexture.Size = settings.SizeMiscIcon;

        if (entity.HasComponent<Targetable>())
        {
            Show = () =>
            {
                if (!entity.TryGetComponent<MinimapIcon>(out var minimapIcon)) return false;
                var isVisible = minimapIcon.IsVisible && !minimapIcon.IsHide;
                return entity.IsValid && isVisible && entity.IsTargetable;
            };
        }
        else
        {
            Show = () => entity.IsValid && entity.GetComponent<MinimapIcon>() is { IsVisible: true };
        }

        if (entity.HasComponent<Transitionable>() && entity.HasComponent<MinimapIcon>())
        {
            Priority = IconPriority.Critical;
            Show = () => false;
        }
        else if (entity.HasComponent<Targetable>())
        {
            if (entity.Path is "Metadata/Terrain/Leagues/Sanctum/Objects/SanctumMote")
            {
                Priority = IconPriority.High;
                Text = "";
                Show = () => entity.IsValid;
                MainTexture.UV = SpriteHelper.GetUV(MapIconsIndex.BlightPath);
                MainTexture.Size = settings.SanctumGoldIconSize;
            }
        }
    }
}