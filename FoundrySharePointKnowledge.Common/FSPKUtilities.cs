using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Text.Json;
using System.Reflection;
using System.ClientModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Text.Encodings.Web;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

using Microsoft.Extensions.Logging;

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
        #region Members
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        private static readonly JsonSerializerOptions _jsonConsoleOptions = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        #endregion
        #region Error Handling
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
        /// Throw an exception if a client result indicated a failed request.
        /// </summary>
        public static void EnsureSuccess<T, L>(this ClientResult<T> result, string message, ILogger<L> logger) where T : class
        {
            //initialization
            ArgumentNullException.ThrowIfNull(result, message);

            //return
            if (result.Value == null)
            {
                //error
                Exception error = new Exception(result.GetRawResponse().Content.ToString());
                logger.LogError(error, message);

                //throw
                throw new Exception(message, error);
            }
        }
        #endregion
        #region Threading
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
        #endregion
        #region URLs
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
        #endregion       
        #region Storage
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
        #region Pluralization
        /// <summary>
        /// Provides a proper pluralized representation of a collection in terms of its noun.
        /// </summary>
        public static string Pluralize<T>(this IEnumerable<T> collection, string noun, string pluralTerm = "s")
        {
            //initialization
            int count = collection?.Count() ?? 0;

            //return
            return count.Pluralize(noun, pluralTerm);
        }

        /// <summary>
        /// Provides a proper pluralized representation of an integer.
        /// </summary>
        public static string Pluralize(this int count, string noun, string pluralTerm = "s")
        {
            //return
            return Convert.ToDouble(count).Pluralize(noun, pluralTerm);
        }

        /// <summary>
        /// Provides a proper pluralized representation of a 64 bit integer.
        /// </summary>
        public static string Pluralize(this long count, string noun, string pluralTerm = "s")
        {
            //return
            return Convert.ToDouble(count).Pluralize(noun, pluralTerm);
        }

        /// <summary>
        /// Provides a proper pluralized representation of a double.
        /// </summary>
        public static string Pluralize(this double count, string noun, string pluralTerm = "s")
        {
            //initialization
            count = Math.Abs(count);
            if (count == 1)
                return noun;

            //remove plural term
            noun = noun.TrimEnd(pluralTerm.ToCharArray())
                       .TrimEnd(pluralTerm.ToLowerInvariant().ToCharArray())
                       .TrimEnd(pluralTerm.ToUpperInvariant().ToCharArray());

            //return
            return $"{count} {noun}{pluralTerm}";
        }
        #endregion
        #region Enums
        /// <summary>
        /// Gets an enum value from a string genertically.
        /// </summary>
        public static E ParseEnum<E>(this string value) where E : Enum
        {
            //return
            return (E)Enum.Parse(typeof(E), value);
        }

        /// <summary>
        /// Gets the first instance of a decorated object's attribute by type.
        /// </summary>
        public static T GetFirstAttribue<T>(object value) where T : Attribute
        {
            //initialization
            return value.GetType()
                        .GetMember(value.ToString())
                        .FirstOrDefault()
                       ?.GetCustomAttributes<T>()
                       ?.FirstOrDefault();
        }

        /// <summary>
        /// Gets the name metadata of the first display name attribute for an enumeration item.
        /// </summary>
        public static string GetDisplayName(this Enum value)
        {
            //initialization
            DisplayAttribute attribute = FSPKUtilities.GetFirstAttribue<DisplayAttribute>(value);

            //return
            if (attribute == null)
            {
                //use name as value
                return value.ToString();
            }
            else
            {
                //get attribute value
                string name = attribute.GetName();
                return string.IsNullOrWhiteSpace(name) ? value.ToString() : name;
            }
        }

        /// <summary>
        /// Gets the short name of the first display name attribute for an enumeration item.
        /// </summary>
        public static string GetDisplayShortName(this Enum value)
        {
            //initialization
            DisplayAttribute attribute = FSPKUtilities.GetFirstAttribue<DisplayAttribute>(value);

            //return
            if (attribute == null)
            {
                //not found
                return value.ToString();
            }
            else
            {
                //get attribute value
                return attribute.GetShortName();
            }
        }

        /// <summary>
        /// Gets all values from an enumeration.
        /// </summary>
        public static E[] GetEnumValues<E>() where E : struct, Enum
        {
            //return
            return Enum.GetValues(typeof(E))
                       .Cast<E>()
                       .ToArray();
        }

        /// <summary>
        /// Parses an enum value from a description.
        /// </summary>
        public static E ParseEnumByShortName<E>(this string value) where E : struct, Enum
        {
            //initialization
            Dictionary<string, E> descriptions = FSPKUtilities.GetEnumValues<E>().ToDictionary(k => k.GetDisplayShortName(), v => v);

            //return
            return descriptions[value];
        }
        #endregion
        #region JSON
        /// <summary>
        /// Converts an XML string to a formatted JSON string, mapping elements and attributes to object properties.
        /// </summary>
        public static string XMLToJSON(this string xml)
        {
            //initialization
            if (string.IsNullOrWhiteSpace(xml))
                return "{}";

            //this converts an XML element to a JSON nodes
            static JsonNode convertElement(XElement element)
            {
                //initialization
                JsonObject jsonObject = new JsonObject();

                //map attributes as @name properties, skipping namespace declarations
                foreach (XAttribute attribute in element.Attributes())
                {
                    //skip namespace
                    if (attribute.IsNamespaceDeclaration)
                        continue;

                    //collect attibute
                    jsonObject[$"@{attribute.Name.LocalName}"] = JsonValue.Create(attribute.Value);
                }

                //group child elements by local name — duplicates become arrays
                IGrouping<string, XElement>[] groups = element.Elements()
                                                              .GroupBy(e => e.Name.LocalName)
                                                              .ToArray();

                //get each group
                foreach (IGrouping<string, XElement> group in groups)
                {
                    //check for single element
                    XElement[] children = group.ToArray();
                    if (children.Length == 1)
                    {
                        //convert directly
                        jsonObject[group.Key] = convertElement(children[0]);
                    }
                    else
                    {
                        //conver to array
                        JsonArray array = new JsonArray();
                        foreach (XElement child in children)
                            array.Add(convertElement(child));

                        //capture array
                        jsonObject[group.Key] = array;
                    }
                }

                //leaf element: use text content, merging with attributes when both are present
                if (!element.HasElements)
                {
                    //get full element text
                    string text = element.Value?.Trim() ?? string.Empty;

                    //append to JSON
                    if (jsonObject.Count > 0 && !string.IsNullOrEmpty(text))
                        jsonObject["#text"] = JsonValue.Create(text);
                    else if (jsonObject.Count == 0)
                        return string.IsNullOrEmpty(text) ? null : JsonValue.Create(text);
                }

                //return
                return jsonObject;
            }

            //remove BOM and create XML document
            xml = xml.TrimStart('\uFEFF', '\u200B', '\u2060');
            XDocument document = XDocument.Parse(xml);
            JsonObject root = new JsonObject();

            //convert content
            root[document.Root.Name.LocalName] = convertElement(document.Root);

            //return
            return root.ToJsonString(FSPKUtilities._jsonOptions);
        }

        /// <summary>
        /// Converts a CSV string to a JSON array string, using the first row as property names.
        /// </summary>
        public static string CSVToJSON(this string csv)
        {
            //initialization
            string[] lines = csv.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
                return "[]";

            //this parses a row of the CSV
            static string[] parseLine(string line)
            {
                //initialization
                bool inQuotes = false;
                List<string> fields = new List<string>();
                StringBuilder field = new StringBuilder();

                for (int l = 0; l < line.Length; l++)
                {
                    //check each character
                    char c = line[l];
                    if (c == '"' && !inQuotes)
                    {
                        //open quote
                        inQuotes = true;
                    }
                    else if (c == '"' && inQuotes)
                    {
                        //check how to close this quoted content
                        if (l + 1 < line.Length && line[l + 1] == '"')
                        {
                            //close quote
                            field.Append('"');
                            l++;
                        }
                        else
                        {
                            //finish quote
                            inQuotes = false;
                        }
                    }
                    else if (c == ',' && !inQuotes)
                    {
                        //collect field
                        fields.Add(field.ToString());
                        field.Clear();
                    }
                    else
                    {
                        //collect character
                        field.Append(c);
                    }
                }

                //return
                fields.Add(field.ToString());
                return fields.ToArray();
            }

            //parse header
            JsonArray array = new JsonArray();
            string[] headers = parseLine(lines[0]);

            //get all rows
            for (int l = 1; l < lines.Length; l++)
            {
                //parse each row
                string[] values = parseLine(lines[l]);
                JsonObject row = new JsonObject();

                //append to JSON
                for (int h = 0; h < headers.Length; h++)
                    row[headers[h]] = JsonValue.Create(h < values.Length ? values[h] : string.Empty);

                //add JSON element
                array.Add(row);
            }

            //return
            return array.ToJsonString(FSPKUtilities._jsonOptions);
        }

        /// <summary>
        /// Formats a JSON string with proper indentation for console output.
        /// </summary>
        public static string PrettyPrintJSON(this string json)
        {
            //initialization
            if (string.IsNullOrWhiteSpace(json))
                return string.Empty;

            //this replaces common typographic unicode characters with ASCII equivalents for CMD rendering
            static string normalizeUnicode(string text)
            {
                //initialization
                StringBuilder outputBulder = new StringBuilder(text.Length);

                //check for unicode characters
                foreach (char character in text)
                {
                    //format output
                    outputBulder.Append(character switch
                    {
                        '‑' or '–' or '—' => '-',
                        '‘' or '’' => '\'',
                        '“' or '”' => '"',
                        '•' => '-',
                        '…' => '.',
                        ' ' => ' ',
                        _ => character
                    });
                }

                //return
                return outputBulder.ToString();
            }

            try
            {
                //serialize with relaxed encoding so unicode chars come through as real chars, not \uXXXX sequences
                using JsonDocument document = JsonDocument.Parse(json);
                string formatted = JsonSerializer.Serialize(document.RootElement, FSPKUtilities._jsonConsoleOptions);

                //return
                return normalizeUnicode(formatted).Replace("\\r\\n", Environment.NewLine)
                                                  .Replace("\\n", Environment.NewLine)
                                                  .Replace("\\r", Environment.NewLine);
            }
            catch (JsonException)
            {
                //error
                return json;
            }
        }
        #endregion
    }
}
