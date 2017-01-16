using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using AirBreather;

namespace StepperUpper
{
    internal abstract partial class SetupTask
    {
        internal sealed class RunProcess : SetupTask
        {
            public DeferredAbsolutePath ExecutablePath;

            public ImmutableArray<ProcessArgument> Arguments = ImmutableArray<ProcessArgument>.Empty;

            public RunProcess(XElement taskElement)
            {
                this.ExecutablePath.BaseFolder = ParseKnownFolder(taskElement.Attribute("ExecutablePathParent")?.Value, defaultIfNotSpecified: KnownFolder.Output);
                this.ExecutablePath.RelativePath = taskElement.Attribute("ExecutablePath").Value;

                var arguments = ImmutableArray.CreateBuilder<ProcessArgument>();
                foreach (var el in taskElement.Elements("Argument"))
                {
                    var argument = new ProcessArgument { Value = el.Value };
                    arguments.Add(argument);

                    switch (el.Attribute("Type")?.Value)
                    {
                        case null:
                            break;

                        case "PathUnderOutputFolder":
                            argument.Kind = ProcessArgumentKind.PathUnderOutputFolder;
                            break;

                        default:
                            throw new NotSupportedException("Argument type " + el.Attribute("Type")?.Value + " was not recognized.");
                    }
                }

                this.Arguments = arguments.MoveToImmutableSafe();
            }

            public enum ProcessArgumentKind
            {
                Simple,

                PathUnderOutputFolder
            }

            protected override Task DispatchAsyncCore(SetupContext context, CancellationToken cancellationToken) =>
                ProcessRunner.RunProcessAsync(context.ResolveFile(this.ExecutablePath).FullName,
                                              ProcessPriorityClass.Normal,
                                              this.Arguments.Select(arg => GetArgument(arg, context)).ToArray());

            private static string GetArgument(ProcessArgument arg, SetupContext context)
            {
                switch (arg.Kind)
                {
                    case ProcessArgumentKind.Simple:
                        return arg.Value;

                    case ProcessArgumentKind.PathUnderOutputFolder:
                        return context.ResolveFile(new DeferredAbsolutePath(KnownFolder.Output, arg.Value)).FullName;
                }

                throw new NotSupportedException("Argument type " + arg.Kind + " was not recognized.");
            }

            public sealed class ProcessArgument
            {
                public ProcessArgumentKind Kind { get; set; }

                public string Value { get; set; }
            }
        }
    }
}
