using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.Elements;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Cache;
using ExileCore2.Shared.Enums;
using ExileCore2.Shared.Helpers;
using MinimapIcons.IconsBuilder.Icons;
using RectangleF = ExileCore2.Shared.RectangleF;
using Vector2 = System.Numerics.Vector2;

namespace MinimapIcons;

public class MinimapIcons : BaseSettingsPlugin<MapIconsSettings>
{
    private IngameUIElements? _ingameUi;
    private bool? _largeMap;
    private float _mapScale;
    private Vector2 _mapCenter;
    private SubMap LargeMapWindow => GameController.Game.IngameState.IngameUi.Map.LargeMap;
    private CachedValue<List<BaseIcon>>? _iconListCache;
    private IconsBuilder.IconsBuilder? _iconsBuilder;
    private readonly HashSet<string> _alwaysShownIngameIconPaths = new(StringComparer.Ordinal);
    private IconsBuilder.IconsBuilder IconsBuilder => _iconsBuilder ??= new IconsBuilder.IconsBuilder(this);

    public override bool Initialise()
    {
        IconsBuilder.Initialise();
        Settings.AlwaysShownIngameIcons.Content = [.. Settings.AlwaysShownIngameIcons.Content.DistinctBy(x => x.Value)];
        RefreshAlwaysShownIngameIconPaths();
        Graphics.InitImage("sprites.png");
        Graphics.InitImage("Icons.png");
        CanUseMultiThreading = true;
        _iconListCache = CreateIconListCache();
        Settings.IconListRefreshPeriod.OnValueChanged += (_, _) => _iconListCache = CreateIconListCache();
        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
        IconsBuilder.AreaChange(area);
    }

    private TimeCache<List<BaseIcon>> CreateIconListCache()
    {
        return new TimeCache<List<BaseIcon>>(() =>
        {
            var entitySource = Settings.DrawCachedEntities
                ? GameController?.EntityListWrapper.Entities
                : GameController?.EntityListWrapper?.OnlyValidEntities;

            if (entitySource == null)
                return [];

            List<BaseIcon> baseIcons = [];
            foreach (var entity in entitySource)
            {
                var icon = entity.GetHudComponent<BaseIcon>();
                if (icon == null)
                    continue;

                var path = icon.Entity.Path;
                var isBreachEntity = path.Contains("Breach/Monsters") || path.Contains("Chests/breach");
                if (isBreachEntity && !Settings.CacheBreachEntities && !icon.Entity.IsValid)
                    continue;

                baseIcons.Add(icon);
            }

            baseIcons.Sort(static (left, right) => left.Priority.CompareTo(right.Priority));
            return baseIcons;
        }, Settings.IconListRefreshPeriod);
    }

    internal bool IsAlwaysShownIngameIcon(string? path)
    {
        return path != null && _alwaysShownIngameIconPaths.Contains(path);
    }

    public override void Tick()
    {
        _ingameUi = GameController.Game.IngameState.IngameUi;

        var smallMiniMap = _ingameUi.Map.SmallMiniMap;
        if (smallMiniMap.IsValid && smallMiniMap.IsVisibleLocal)
        {
            var mapRect = smallMiniMap.GetClientRectCache;
            _mapCenter = mapRect.Center;
            _largeMap = false;
            _mapScale = smallMiniMap.MapScale;
        }
        else if (_ingameUi.Map.LargeMap.IsVisibleLocal)
        {
            var largeMapWindow = LargeMapWindow;
            _mapCenter = largeMapWindow.MapCenter;
            _largeMap = true;
            _mapScale = largeMapWindow.MapScale;
        }
        else
        {
            _largeMap = null;
        }
    }

    public override void Render()
    {
        var ingameUi = _ingameUi;
        var gameController = GameController;
        if (_largeMap == null || 
            ingameUi == null ||
            gameController == null ||
            !gameController.InGame ||
            Settings.DrawOnlyOnLargeMap && _largeMap != true) 
            return;

        if (!Settings.IgnoreFullscreenPanels &&
            ingameUi.FullscreenPanels.Any(x => x.IsVisible) ||
            !Settings.IgnoreLargePanels &&
            ingameUi.LargePanels.Any(x => x.IsVisible))
            return;

        // Run icon building on the render thread to avoid concurrent component dictionary access
        IconsBuilder.Tick();

        var playerRender = gameController.Player?.GetComponent<Render>();
        if (playerRender == null) return;
        var playerPos = playerRender.Pos.WorldToGrid();
        var playerHeight = -playerRender.UnclampedHeight;
        var ingameData = gameController.IngameState.Data;

        if (LargeMapWindow == null) return;

        var baseIcons = _iconListCache?.Value;
        if (baseIcons == null) return;
        RefreshAlwaysShownIngameIconPaths();

        foreach (var icon in baseIcons)
        {
            if (icon?.Entity == null) continue;

            if (!Settings.DrawMonsters && icon.Entity.Type == EntityType.Monster)
                continue;

            if (!icon.Show())
                continue;

            if (icon.HasIngameIcon &&
                icon is not CustomIcon &&
                (!Settings.DrawReplacementsForGameIconsWhenOutOfRange || icon.Entity.IsValid) &&
                !IsAlwaysShownIngameIcon(icon.Entity.Path))
                continue;

            var iconGridPos = icon.GridPosition();
            var position = _mapCenter +
                           DeltaInWorldToMinimapDelta(iconGridPos - playerPos,
                               (playerHeight + ingameData.GetTerrainHeightAt(iconGridPos)) * PoeMapExtension.WorldToGridConversion);

            var iconValueMainTexture = icon.MainTexture;
            var size = iconValueMainTexture.Size;
            var halfSize = size / 2f;
            icon.DrawRect = new RectangleF(position.X - halfSize, position.Y - halfSize, size, size);
            var drawRect = icon.DrawRect;
            if (_largeMap == false && !ingameUi.Map.SmallMiniMap.GetClientRectCache.Contains(drawRect)) 
                continue;

            Graphics.DrawImage(iconValueMainTexture.FileName, drawRect, iconValueMainTexture.UV, iconValueMainTexture.Color);
            if (icon.Hidden())
            {
                var s = drawRect.Width * 0.5f;
                drawRect.Inflate(-s, -s);

                Graphics.DrawImage(icon.MainTexture.FileName, drawRect,
                    SpriteHelper.GetUV(MapIconsIndex.LootFilterSmallCyanCircle), Color.White);

                drawRect.Inflate(s, s);
            }

            if (!string.IsNullOrEmpty(icon.Text))
                Graphics.DrawText(icon.Text, position.Translate(0, Settings.ZForText), FontAlign.Center);
        }
    }

    private const float CameraAngle = 38.7f * MathF.PI / 180;
    private static readonly float CameraAngleCos = MathF.Cos(CameraAngle);
    private static readonly float CameraAngleSin = MathF.Sin(CameraAngle);

    private Vector2 DeltaInWorldToMinimapDelta(Vector2 delta, float deltaZ)
    {
        return _mapScale * Vector2.Multiply(new Vector2(delta.X - delta.Y, deltaZ - (delta.X + delta.Y)), new Vector2(CameraAngleCos, CameraAngleSin));
    }

    private void RefreshAlwaysShownIngameIconPaths()
    {
        _alwaysShownIngameIconPaths.Clear();
        foreach (var iconPath in Settings.AlwaysShownIngameIcons.Content)
        {
            if (!string.IsNullOrEmpty(iconPath.Value))
                _alwaysShownIngameIconPaths.Add(iconPath.Value);
        }
    }
}

public static class Extensions
{
    public static T GetOrAdd<TKey, T>(this Dictionary<TKey, T> dictionary, TKey key, Func<T> valueFunc)
        where TKey : notnull
    {
        if (dictionary.TryGetValue(key, out var result))
        {
            return result;
        }

        result = valueFunc();
        dictionary[key] = result;
        return result;
    }
}