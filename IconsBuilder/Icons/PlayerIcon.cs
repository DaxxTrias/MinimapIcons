using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared;
using ExileCore2.Shared.Enums;
using ExileCore2.Shared.Helpers;

namespace MinimapIcons.IconsBuilder.Icons;

public class PlayerIcon : BaseIcon
{
    public PlayerIcon(Entity entity, IconsBuilderSettings settings, string playerName) :
        base(entity)
    {
        Show = () => entity.IsValid && !settings.HidePlayers;
        if (_HasIngameIcon) return;
        MainTexture = new HudTexture("Icons.png")
        {
            UV = SpriteHelper.GetUV(MapIconsIndex.OtherPlayer),
            Size = settings.PlayerIconSize,
        };
        Text = settings.ShowPlayerNames ? playerName : "";
    }
}