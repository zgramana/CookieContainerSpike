using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace CookieContainerSpike
{
    class MainClass
    {
        static CookieContainer cookies = new CookieContainer();

        public static void Main (string[] args)
        {
            // Get some cookies...
            Console.WriteLine(
                @"

Downloading some URIs...

");
            var pages = new[] { "http://www.couchbase.com/", "http://www.github.com/", "http://www.apple.com/", "http://www.google.com/" };
            Task.WaitAll(pages.Select(ToDownloadTask).ToArray());

            Console.WriteLine(
                @"

Here are the cookies we received...

");

            // Show us what we found...
            PrintCookies (pages);


            var filePath = Path.GetTempFileName ();
            Console.WriteLine(
                @"

Saving them to disk at {0}
", filePath);

            JsonSerializeToDisk(filePath);

            // Null out our refs...
            cookies = null;
            Console.WriteLine(
                @"
Now loading from disk...

");

            // Now load our cookies from disk and print.
            JsonDeserializeFromDisk(filePath);
            PrintCookies (pages);

            File.Delete(filePath);
        }

        static void JsonSerializeToDisk (string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                var settings = new JsonSerializerSettings 
                {
                    ContractResolver = new CookieContainerResolver(), 
                    Converters = { new CookieCollectionJsonConverter() }
                };
                var json = JsonConvert.SerializeObject(cookies, settings);
                writer.Write(json);
            }
        }
            
        static void JsonDeserializeFromDisk (string filePath)
        {
            using (var reader = new StreamReader(filePath))
            {
                var settings = new JsonSerializerSettings 
                {
                    ContractResolver = new CookieContainerResolver(), 
                    Converters = { new CookieCollectionJsonConverter() }
                };
                var json = reader.ReadToEnd();
                cookies = JsonConvert.DeserializeObject<CookieContainer> (json, settings);
            }
        }

        static void PrintCookies (string[] pages)
        {
            foreach (var item in pages) 
            {
                foreach (var cookieEntry in cookies.GetCookies (new Uri (item))) 
                {
                    if (cookieEntry is CookieCollection)
                    {
                        foreach (var cookie in (CookieCollection)cookieEntry) 
                        {
                            Console.WriteLine ("{0,-30} {1}", item, cookie.ToString().Truncate());
                        }
                    }
                    else 
                    {
                        Console.WriteLine ("{0,-30} {1}", item, cookieEntry.ToString().Truncate());
                    }
                }
            }
        }

        static Task ToDownloadTask(string uri)
        {
            var handler = new HttpClientHandler { CookieContainer = cookies, UseCookies = true };
            return new HttpClient(handler)
                .GetStringAsync(uri)
                .ContinueWith(str => Console.WriteLine("uri: {0}, length: {1}", uri, str.Result.Length));
        }
    }

    public class CookieContainerResolver : DefaultContractResolver
    {
        static readonly MemberInfo cookieField;

        static CookieContainerResolver()
        {
            cookieField = typeof(CookieContainer)
                .GetMembers(BindingFlags.GetField | BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(mi => mi.Name.Equals("cookies"));
        }

        protected override System.Collections.Generic.IList<JsonProperty> CreateProperties (Type type, MemberSerialization memberSerialization)
        {
            var props = base.CreateProperties (type, memberSerialization);
            if (type.Equals(typeof(CookieContainer)))
            {
                var prop = CreateProperty(cookieField, memberSerialization);
                prop.Readable = true;
                prop.Writable = true;
                props.Add(prop);
            }
            return props;
        }
    }

    public class CookieCollectionJsonConverter : JsonConverter
    {
        public override bool CanRead {
            get {
                return true;
            }
        }

        public override bool CanWrite {
            get {
                return true;
            }
        }

        public override bool CanConvert (Type objectType)
        {
            var val = objectType == typeof(CookieCollection);
            return val;
        }

        public override object ReadJson (JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var cookies = JsonConvert.DeserializeObject<Cookie[]>((string)reader.Value);
            var collection = new CookieCollection();

            foreach(var cookie in cookies)
            {
                collection.Add(cookie);
            }

            return collection;
        }
            
        public override void WriteJson (JsonWriter writer, object value, JsonSerializer serializer)
        {
            var uri = (CookieCollection)value;
            var str = JsonConvert.SerializeObject(uri);

            writer.WriteValue(str);
        }
    }

    public static class StringExtension
    {
        public static string Truncate(this string str)
        {
            var availableWidth = Console.BufferWidth - 31;
            var shouldTrunc = str.Length > availableWidth;
            var truncStr = new String(str.Take(shouldTrunc ? availableWidth - 3 : str.Length).ToArray());
            return shouldTrunc ? string.Concat(truncStr, "...") : truncStr;
        }
    }
}
