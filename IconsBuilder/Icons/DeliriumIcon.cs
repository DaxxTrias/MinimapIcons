using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared;
using ExileCore2.Shared.Enums;
using ExileCore2.Shared.Helpers;
using GameOffsets2.Native;

namespace MinimapIcons.IconsBuilder.Icons;

internal class DeliriumIcon : BaseIcon
{
    public DeliriumIcon(Entity entity, IconsBuilderSettings settings, Dictionary<string, Vector2i> modIcons) : base(entity)
    {
        Update(entity, settings, modIcons);
    }

    public override string ToString()
    {
        return $"{Entity.Metadata} : {Entity.Type} ({Entity.Address}) T: {Text}";
    }

    public void Update(Entity entity, IconsBuilderSettings settings, Dictionary<string, Vector2i> modIcons)
    {
        Show = () => Entity.IsAlive;

        MainTexture = new HudTexture("Icons.png");

        (MainTexture.Size, Text) = Rarity switch
        {
            MonsterRarity.White => (MainTexture.Size = settings.SizeEntityWhiteIcon, string.Empty),
            MonsterRarity.Magic => (MainTexture.Size = settings.SizeEntityMagicIcon, string.Empty),
            MonsterRarity.Rare => (MainTexture.Size = settings.SizeEntityRareIcon, string.Empty),
            MonsterRarity.Unique => (MainTexture.Size = settings.SizeEntityUniqueIcon, entity.RenderName),
            _ => throw new ArgumentException("Delirium icon rarity corrupted.")
        };

        if (_HasIngameIcon && entity.HasComponent<MinimapIcon>() && !entity.GetComponent<MinimapIcon>().Name.Equals("NPC"))
            return;

        if (entity.Path.StartsWith("Metadata/Monsters/LeagueDelirium/DoodadDaemons", StringComparison.Ordinal))
        {
            if (entity.Path.Contains("ShardPack", StringComparison.OrdinalIgnoreCase))
            {
                MainTexture.UV = SpriteHelper.GetUV(MapIconsIndex.MyPlayer);
                MainTexture.Size = settings.SizeEntityProximityMonsterIcon;
                Hidden = () => false;
                Priority = IconPriority.Medium;
                return;
            }
            else
            {
                Show = () => false;
                MainTexture.UV = SpriteHelper.GetUV(MapIconsIndex.QuestObject);
                return;
            }
        }

        if (!entity.IsHostile)
        {
            if (!_HasIngameIcon)
            {
                MainTexture.UV = SpriteHelper.GetUV(MapIconsIndex.LootFilterSmallGreenCircle);
                Priority = IconPriority.Low;
                Show = () => !settings.HideMinions && entity.IsAlive;
            }

            //Spirits icon
        }
        else if (Rarity == MonsterRarity.Unique && entity.Path.Contains("Metadata/Monsters/Spirit/"))
            MainTexture.UV = SpriteHelper.GetUV(MapIconsIndex.LootFilterLargeGreenHexagon);
        else
        {
            string? modName = null;

            if (entity.HasComponent<ObjectMagicProperties>())
            {
                var objectMagicProperties = entity.GetComponent<ObjectMagicProperties>();

                var mods = objectMagicProperties.Mods;

                if (mods != null)
                {
                    if (mods.Contains("MonsterConvertsOnDeath_")) Show = () => entity.IsAlive && entity.IsHostile;

                    modName = mods.FirstOrDefault(modIcons.ContainsKey);
                }
            }

            if (modName != null)
            {
                MainTexture = new HudTexture("sprites.png")
                {
                    UV = SpriteHelper.GetUV(modIcons[modName], new Vector2i(7, 8))
                };
                Priority = IconPriority.VeryHigh;
            }
            else
            {
                (MainTexture.UV, MainTexture.Color) = Rarity switch
                {
                    MonsterRarity.White => (SpriteHelper.GetUV(MapIconsIndex.LootFilterLargeRedCircle), Color.White),
                    MonsterRarity.Magic => (SpriteHelper.GetUV(MapIconsIndex.LootFilterLargeBlueCircle), Color.White),
                    MonsterRarity.Rare => (SpriteHelper.GetUV(MapIconsIndex.LootFilterLargeYellowCircle), Color.White),
                    MonsterRarity.Unique => (SpriteHelper.GetUV(MapIconsIndex.LootFilterLargeWhiteHexagon), Color.DarkOrange),
                    _ => throw new ArgumentOutOfRangeException($"Rarity wrong was is {Rarity}. {entity.GetComponent<ObjectMagicProperties>().DumpObject()}")
                };
            }
        }
    }
}