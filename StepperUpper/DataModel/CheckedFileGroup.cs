using System;
using System.Collections.Immutable;
using System.Xml.Linq;

using AirBreather;

namespace StepperUpper
{
    internal sealed class CheckedFileGroup
    {
        public string Name;

        public ImmutableArray<CheckedFile> CheckedFiles = ImmutableArray<CheckedFile>.Empty;

        public CheckedFileGroup() { }

        public CheckedFileGroup(XElement element)
        {
            this.Name = element.Attribute("Name")?.Value ?? String.Empty;
            var lst = ImmutableArray.CreateBuilder<CheckedFile>();
            foreach (var file in element.Elements())
            {
                if (file.Name.LocalName != "File")
                {
                    throw new NotSupportedException("Child type not supported: " + file.Name.LocalName);
                }

                lst.Add(new CheckedFile(file));
            }

            this.CheckedFiles = lst.MoveToImmutableSafe();
        }
    }
}
