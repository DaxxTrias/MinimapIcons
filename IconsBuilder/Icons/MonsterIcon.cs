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

public class MonsterIcon : BaseIcon
{
    public MonsterIcon(Entity entity, IconsBuilderSettings settings, Dictionary<string, Vector2i> modIcons)
        : base(entity)
    {
        Update(entity, settings, modIcons);
    }

    public void Update(Entity entity, IconsBuilderSettings settings, Dictionary<string, Vector2i> modIcons)
    {
        Show = () => entity.IsAlive;
        if(entity.IsHidden && settings.HideBurriedMonsters)
        {
            Show = () => !entity.IsHidden && entity.IsAlive;
        }

        if (!_HasIngameIcon) MainTexture = new HudTexture("Icons.png");

        MainTexture.Size = Rarity switch
        {
            MonsterRarity.White => settings.SizeEntityWhiteIcon,
            MonsterRarity.Magic => settings.SizeEntityMagicIcon,
            MonsterRarity.Rare => settings.SizeEntityRareIcon,
            MonsterRarity.Unique => settings.SizeEntityUniqueIcon,
            _ => throw new ArgumentException($"{nameof(MonsterIcon)} wrong rarity for {entity.Path}. Dump: {(entity.TryGetComponent<ObjectMagicProperties>(out var omp) ? omp.DumpObject() : "ObjectMagicProperties=null")}")
        };

        var isMonsterWithIcon = IconsBuilder.ShouldTreatAsMonsterWithIcon(entity, settings);
        if (isMonsterWithIcon && IngameIconIndex == MapIconsIndex.BlightMonster)
        {
            MainTexture.Size *= 2;
        }

        if (_HasIngameIcon && 
            entity.TryGetComponent<MinimapIcon>(out var mI) && 
            mI.Name != "NPC" &&
            !isMonsterWithIcon)
            return;
        if (!entity.IsHostile)
        {
            if (!_HasIngameIcon)
            {
                MainTexture.UV = SpriteHelper.GetUV(MapIconsIndex.LootFilterSmallGreenCircle);
                Priority = IconPriority.Low;
                Show = () => !settings.HideMinions && entity.IsAlive;
            }
        }
        else if (Rarity == MonsterRarity.Unique && (entity.Path?.Contains("Metadata/Monsters/Spirit/") == true))
            MainTexture.UV = SpriteHelper.GetUV(MapIconsIndex.LootFilterLargeGreenHexagon);
        else
        {
            string? modName = null;
            ObjectMagicProperties? objectMagicProperties = null;
            if (entity.TryGetComponent<ObjectMagicProperties>(out var component))
            {
                objectMagicProperties = component;
            }

            var mods = objectMagicProperties?.Mods;

            if (mods != null)
            {
                if (mods.Contains("MonsterConvertsOnDeath_")) Show = () => entity.IsAlive && entity.IsHostile;

                modName = mods.FirstOrDefault(modIcons.ContainsKey);
            }

            if (modName != null)
            {
                MainTexture = new HudTexture("sprites.png");
                MainTexture.UV = SpriteHelper.GetUV(modIcons[modName], new Vector2i(7, 8));
                Priority = IconPriority.VeryHigh;
            }
            else
            { 
                var oldShow = Show;
                switch (Rarity)
                {
                    case MonsterRarity.White:
                        if (!isMonsterWithIcon)
                            MainTexture.UV = SpriteHelper.GetUV(MapIconsIndex.LootFilterLargeRedCircle);
                        if (settings.MonsterRarityNames.ShowNormalNames)
                            Text = RenderName.Split(',').FirstOrDefault() ?? string.Empty;
                        Show = () => oldShow() && settings.ShowNormalMonsters;
                        break;
                    case MonsterRarity.Magic:
                        if (!isMonsterWithIcon)
                            MainTexture.UV = SpriteHelper.GetUV(MapIconsIndex.LootFilterLargeBlueCircle);
                        if (settings.MonsterRarityNames.ShowMagicNames)
                            Text = RenderName.Split(',').FirstOrDefault() ?? string.Empty;
                        Show = () => oldShow() && settings.ShowMagicMonsters;
                        break;
                    case MonsterRarity.Rare:
                        if (!isMonsterWithIcon)
                            MainTexture.UV = SpriteHelper.GetUV(MapIconsIndex.LootFilterLargeYellowCircle);
                        if (settings.MonsterRarityNames.ShowRareNames)
                            Text = RenderName.Split(',').FirstOrDefault() ?? string.Empty;
                        break;
                    case MonsterRarity.Unique:
                        if (!isMonsterWithIcon)
                            MainTexture.UV = SpriteHelper.GetUV(MapIconsIndex.LootFilterLargeWhiteHexagon);
                        MainTexture.Color = Color.DarkOrange;
                        if (settings.MonsterRarityNames.ShowUniqueNames)
                            Text = RenderName.Split(',').FirstOrDefault() ?? string.Empty;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            $"Rarity wrong was is {Rarity}. {objectMagicProperties?.DumpObject() ?? "ObjectMagicProperties=null"}");
                }
            }
        }
    }
}