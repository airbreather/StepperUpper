using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using AirBreather.Collections;

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

        internal static XDocument PoolStrings(this XDocument doc)
        {
            StringPool pool = new StringPool();
            Stack<XNode> stack = new Stack<XNode>();
            stack.Push(doc.Root);
            while (stack.Count != 0)
            {
                XNode nod = stack.Pop();
                XElement nxt = nod as XElement;
                if (nxt != null)
                {
                    nxt.Name = pool.Pool(nxt.Name);
                    foreach (XAttribute attribute in nxt.Attributes().ToArray())
                    {
                        nxt.SetAttributeValue(pool.Pool(attribute.Name), pool.Pool(attribute.Value));
                    }
                }

                XText txt = nod as XText;
                if (txt != null)
                {
                    txt.Value = pool.Pool(txt.Value);
                }

                XContainer cont = nod as XContainer;
                if (cont != null)
                {
                    foreach (XNode child in cont.Nodes())
                    {
                        stack.Push(child);
                    }
                }
            }

            return doc;
        }

        private static XName Pool(this StringPool pool, XName name) => XName.Get(pool.Pool(name.LocalName), pool.Pool(name.NamespaceName));
    }
}
