using ExileCore2.PoEMemory;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared;
using ExileCore2.Shared.Helpers;

namespace MinimapIcons.IconsBuilder.Icons;

public class CustomIcon : BaseIcon
{
    private static readonly EntityValidityCache<bool>.Tag IsHiddenTag = EntityValidityCache<bool>.CreateTag(e => e.GetComponent<MinimapIcon>()?.IsHide ?? false, false);
    private bool IsHiddenCached => IsHiddenTag.Get(Entity);
    public CustomIcon(Entity entity, IconsBuilderSettings settings, CustomIconSettings customIconSettings)
        : base(entity)
    {
        Show = () => (!customIconSettings.OnlyShowAlive || entity.IsAlive) &&
                     (!customIconSettings.OnlyShowNotOpened || !entity.IsOpened)&&
                     (!customIconSettings.OnlyShowNonHiddenIcons || !IsHiddenCached)
                     ;

        if (customIconSettings.DisableDrawingHiddenIcon)
            Hidden = () => false;
            
        MainTexture = new HudTexture("Icons.png")
        {
            UV = SpriteHelper.GetUV(customIconSettings.Icon),
            Size = RemoteMemoryObject.TheGame.IngameState.IngameUi.Map.LargeMapZoom * RemoteMemoryObject.TheGame.IngameState.Camera.Height / 64 * customIconSettings.Size.Value,
            Color = customIconSettings.Tint,
        };
    }
}