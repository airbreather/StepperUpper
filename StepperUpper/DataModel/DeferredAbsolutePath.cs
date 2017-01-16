using System;
using System.Runtime.InteropServices;

using AirBreather;

namespace StepperUpper
{
    [StructLayout(LayoutKind.Auto)]
    internal struct DeferredAbsolutePath : IEquatable<DeferredAbsolutePath>
    {
        internal KnownFolder BaseFolder;

        internal string RelativePath;

        internal DeferredAbsolutePath(KnownFolder baseFolder, string relativePath)
        {
            this.BaseFolder = baseFolder;
            this.RelativePath = relativePath;
        }

        public static bool operator ==(DeferredAbsolutePath first, DeferredAbsolutePath second) => first.Equals(second);
        public static bool operator !=(DeferredAbsolutePath first, DeferredAbsolutePath second) => !first.Equals(second);

        public bool Equals(DeferredAbsolutePath other) => this.RelativePath == other.RelativePath && this.BaseFolder == other.BaseFolder;
        public override bool Equals(object obj) => obj is DeferredAbsolutePath other && this.Equals(other);

        public override int GetHashCode() => HashCode.Seed.HashWith(this.BaseFolder).HashWith(this.RelativePath);
    }
}
