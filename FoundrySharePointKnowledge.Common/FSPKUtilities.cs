using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace FoundrySharePointKnowledge.Common
{
    /// <summary>
    /// These are system-wide extension methods.
    /// </summary>
    public static class FSPKUtilities
    {
        #region Public Methods
        /// <summary>
        /// Runs a batch of tasks concurrently and reports any exceptions.
        /// </summary>
        public static async Task<AggregateException> WhenAllAsync(params Task[] tasks)
        {
            //return
            return await tasks.WhenAllAsync();
        }

        /// <summary>
        /// Runs a batch of tasks concurrently and reports any exceptions.
        /// </summary>
        public static async Task<AggregateException> WhenAllAsync(this IEnumerable<Task> tasks)
        {
            try
            {
                //initialization
                if (!tasks?.Any() ?? true)
                    return null;

                //execute work
                await Task.WhenAll(tasks);
                List<Exception> exceptions = new List<Exception>();

                //capture exceptions
                foreach (Task task in tasks)
                    if (task.Exception != null)
                        exceptions.Add(task.Exception);

                //return
                if (exceptions.Any())
                    return new AggregateException(exceptions);
                else
                    return null;
            }
            catch (Exception ex)
            {
                //error
                return new AggregateException("Failed to run task batch.", ex);
            }
        }

        /// <summary>
        /// Combines two strings into a url.
        /// </summary>
        public static string CombineURL(this string firstPart, string secondPart)
        {
            //return
            if (string.IsNullOrWhiteSpace(firstPart))
                return secondPart;
            else if (string.IsNullOrWhiteSpace(secondPart))
                return firstPart;
            else
                return $"{firstPart.TrimEnd('/')}/{secondPart.TrimStart('/')}";
        }       
        #endregion
    }
}
