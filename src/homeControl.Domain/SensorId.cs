﻿using System;
using System.Diagnostics;

namespace homeControl.Domain
{
    [DebuggerDisplay("Id")]
    public sealed class SensorId : IEquatable<SensorId>
    {
        public Guid Id { get; }

        public SensorId(Guid id)
        {
            Guard.DebugAssertArgumentNotDefault(id, nameof(id));

            Id = id;
        }

        public bool Equals(SensorId other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            var other = obj as SensorId;
            return other != null && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(SensorId left, SensorId right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(SensorId left, SensorId right)
        {
            return !Equals(left, right);
        }

        public static SensorId NewId()
        {
            return new SensorId(Guid.NewGuid());
        }

        public override string ToString()
        {
            return $"sensor-{Id:D}";
        }
    }
}