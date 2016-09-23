using System;
using System.Text;
using System.Threading.Tasks;

namespace StepperUpper
{
    internal static class Extensions
    {
        internal static string MoveToString(this StringBuilder builder)
        {
            string result = builder.ToString();
            builder.Clear();
            return result;
        }

        internal static async Task<TResult> Finally<TResult>(this Task<TResult> antecedent, Action callback)
        {
            try
            {
                return await antecedent.ConfigureAwait(false);
            }
            finally
            {
                callback();
            }
        }
    }
}
