using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

using Azure;
using Azure.Core;
using Azure.Data.Tables;

namespace FoundrySharePointKnowledge.Common
{
    /// <summary>
    /// These are system-wide extension methods.
    /// </summary>
    public static class FSPKUtilities
    {
        #region Public Methods
        /// <summary>
        /// Parses an error string from an Azure response.
        /// </summary>
        public static async Task<string> GetResponseErrorAsync<T>(this Response<T> response, string message)
        {
            //initialization
            string error = string.Empty;

            //check response
            if (response == null)
            {
                //no response
                error = "No response was received.";
            }
            else
            {
                //get raw response
                Response rawResponse = response.GetRawResponse();
                if (rawResponse == null)
                {
                    //no metadata
                    error = "No response metadata was received.";
                }
                else if (rawResponse.IsError)
                {
                    //return error contenxt
                    using StreamReader reader = new StreamReader(rawResponse.ContentStream);
                    error = await reader.ReadToEndAsync();
                }
            }

            //return
            return error;
        }

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

        /// <summary>
        /// Gets an enum value from a string genertically.
        /// </summary>
        public static E ParseEnum<E>(this string value) where E : Enum
        {
            //return
            return (E)Enum.Parse(typeof(E), value);
        }

        /// <summary>
        /// Builds a URI from a raw string.
        /// </summary>
        public static Uri ParseURI(string uri, string propertyName)
        {
            //initialization
            if (string.IsNullOrWhiteSpace(uri))
                throw new ArgumentNullException(propertyName);

            //return
            if (Uri.TryCreate(uri, UriKind.Absolute, out Uri result))
                return result;
            else
                throw new InvalidOperationException($"{uri} is not a proper URL for setting {propertyName}.");
        }

        /// <summary>
        /// Ensures a unique SharePoint Graph list item id.
        /// </summary>
        public static string CreateUniqueId(params string[] ids)
        {
            //return
            return string.Join(FSPKConstants.Search.Fields.IdDelimiter, ids);
        }

        /// <summary>
        /// Configures Azure Storage (Blobs and Tables) client options.
        /// </summary>
        public static void ConfigureAzureStorageOptions<T>(this T options) where T : ClientOptions
        {
            //initialization
            options.Diagnostics.IsLoggingEnabled = false;
            options.Diagnostics.IsTelemetryEnabled = false;
            options.Diagnostics.IsDistributedTracingEnabled = false;

            //return
            options.Retry.Mode = RetryMode.Exponential;
            options.Retry.Delay = FSPKConstants.AzureStorage.RetryPolicy.Backoff;
            options.Retry.MaxRetries = FSPKConstants.AzureStorage.RetryPolicy.Attempts;
        }

        /// <summary>
        /// Bulk uploads entities to an Azure Storage Table.
        /// </summary>
        public static async Task PerformBulkTableTansactionAsync<T>(this TableClient client, List<T> entities) where T : ITableEntity
        {
            //return
            foreach (IGrouping<string, T> group in entities.GroupBy(e => e.PartitionKey))
                foreach (T[] batch in group.Chunk(FSPKConstants.AzureStorage.Tables.BatchSize))
                    await client.SubmitTransactionAsync(batch.Select(e => new TableTransactionAction(TableTransactionActionType.UpsertReplace, e)));
        }
        #endregion
    }
}
