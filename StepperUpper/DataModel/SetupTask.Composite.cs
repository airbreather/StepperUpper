using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using AirBreather;

namespace StepperUpper
{
    internal abstract partial class SetupTask
    {
        internal sealed class Composite : SetupTask
        {
            public ImmutableArray<SetupTask> Children = ImmutableArray<SetupTask>.Empty;

            public Composite() { }

            public Composite(XElement taskElement)
            {
                var children = ImmutableArray.CreateBuilder<SetupTask>();
                foreach (var el in taskElement.Elements())
                {
                    children.Add(CreateFrom(el));
                }

                this.Children = children.MoveToImmutableSafe();
            }

            protected override Task DispatchAsyncCore(SetupContext context, CancellationToken cancellationToken) =>
                Task.WhenAll(this.Children.Select(ch => ch.DispatchAsync(context, cancellationToken)));
        }
    }
}
