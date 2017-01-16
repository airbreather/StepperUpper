using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using AirBreather;

namespace StepperUpper
{
    internal abstract partial class SetupTask
    {
        internal sealed class TweakINI : SetupTask
        {
            public DeferredAbsolutePath File;

            public ImmutableArray<PropertyValue> PropertyValues = ImmutableArray<PropertyValue>.Empty;

            public TweakINI(XElement taskElement)
            {
                this.File.BaseFolder = ParseKnownFolder(taskElement.Attribute("FileParent")?.Value, defaultIfNotSpecified: KnownFolder.Output);
                this.File.RelativePath = taskElement.Attribute("File").Value;

                var propertyValues = ImmutableArray.CreateBuilder<PropertyValue>();
                foreach (var el in taskElement.Elements("Set"))
                {
                    propertyValues.Add(new PropertyValue
                    {
                        Section = el.Attribute("Section").Value,
                        Property = el.Attribute("Property").Value,
                        Value = el.Attribute("Value").Value,
                    });
                }

                this.PropertyValues = propertyValues.MoveToImmutableSafe();
            }

            protected override Task DispatchAsyncCore(SetupContext context, CancellationToken cancellationToken)
            {
                FileInfo iniFile = context.ResolveFile(this.File);
                iniFile.Directory.Create();

                foreach (var propertyValue in this.PropertyValues)
                {
                    NativeMethods.WritePrivateProfileString(sectionName: propertyValue.Section,
                                                            propertyName: propertyValue.Property,
                                                            value: propertyValue.Value,
                                                            iniFilePath: iniFile.FullName);
                }

                return Task.CompletedTask;
            }

            public sealed class PropertyValue
            {
                public string Section;

                public string Property;

                public string Value;
            }
        }
    }
}
