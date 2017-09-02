using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using AirBreather.Collections;

namespace StepperUpper
{
    internal static class Extensions
    {
        internal static bool Contains(this string haystack, string needle, StringComparison comparisonType) => haystack.IndexOf(needle, comparisonType) >= 0;

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

        internal static async Task Finally(this Task antecedent, Action callback)
        {
            try
            {
                await antecedent.ConfigureAwait(false);
            }
            finally
            {
                callback();
            }
        }

        internal static XDocument PoolStrings(this XDocument doc, StringPool pool)
        {
            Stack<XNode> stack = new Stack<XNode>();
            List<XAttribute> attributesList = new List<XAttribute>();
            stack.Push(doc.Root);
            while (stack.Count != 0)
            {
                switch (stack.Pop())
                {
                    case XElement nxt:
                        nxt.Name = pool.Pool(nxt.Name);
                        attributesList.AddRange(nxt.Attributes());
                        foreach (XAttribute attribute in attributesList)
                        {
                            nxt.SetAttributeValue(pool.Pool(attribute.Name), pool.Pool(attribute.Value));
                        }

                        attributesList.Clear();
                        break;

                    case XText txt:
                        txt.Value = pool.Pool(txt.Value);
                        break;

                    case XContainer cont:
                        foreach (XNode child in cont.Nodes())
                        {
                            stack.Push(child);
                        }

                        break;
                }
            }

            return doc;
        }

        private static XName Pool(this StringPool pool, XName name) => XName.Get(pool.Pool(name.LocalName), pool.Pool(name.NamespaceName));
    }
}
