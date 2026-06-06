using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using ExileCore2.PoEMemory.MemoryObjects;

namespace MinimapIcons.IconsBuilder.Icons;

public static class EntityValidityCache<T>
{
    public readonly struct Tag
    {
        public readonly long Value;
        public readonly Func<Entity, T> Getter;
        public readonly T DefaultValue;

        public Tag(long value, Func<Entity, T> getter, T defaultValue)
        {
            Value = value;
            Getter = getter;
            DefaultValue = defaultValue;
        }

        public T Get(Entity entity)
        {
            return EntityValidityCache<T>.Get(entity, in this);
        }
    }

    private static readonly ConditionalWeakTable<Entity, Dictionary<long, T>> _values = [];

    private static long _counter;

    public static Tag CreateTag(Func<Entity, T> getter, T defaultValue)
    {
        return new Tag(Interlocked.Increment(ref _counter), getter, defaultValue);
    }

    public static T Get(Entity entity, in Tag tag)
    {
        var dict = _values.GetValue(entity, e => new Dictionary<long, T>());
        if (entity.IsValid)
        {
            var newValue = tag.Getter(entity);
            dict[tag.Value] = newValue;
            return newValue;
        }
        else return dict.GetValueOrDefault(tag.Value, tag.DefaultValue);
    }
}