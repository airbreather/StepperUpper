using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace StepperUpper
{
    internal struct NonCapturingYield
    {
        public YieldAwaiter GetAwaiter() => new YieldAwaiter();

        internal struct YieldAwaiter : ICriticalNotifyCompletion
        {
            public bool IsCompleted => false;

            public void OnCompleted(Action continuation) => Task.CompletedTask.GetAwaiter().OnCompleted(continuation);

            public void UnsafeOnCompleted(Action continuation) => Task.CompletedTask.GetAwaiter().UnsafeOnCompleted(continuation);

            public void GetResult() { }
        }
    }
}
