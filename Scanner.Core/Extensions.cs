﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Scanner
{
    public class Extensions
    {
        public class WebClientEx : WebClient
        {
            public WebClientEx(WebProxy webProxy = null, CookieContainer container = null)
            {
                _container = container ?? new CookieContainer();

                if (webProxy == null) return;

                Proxy = webProxy;

                Credentials = Proxy.Credentials;

                this.ResponseCookies = new CookieCollection();
            }

            public CookieCollection ResponseCookies { get; set; }

            public CookieContainer CookieContainer
            {
                get { return _container; }
                set { _container = value; }
            }

            private CookieContainer _container;

            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest r = base.GetWebRequest(address);
                var request = r as HttpWebRequest;

                if (request != null)
                {
                    request.CookieContainer = _container;
                    request.Timeout = 5 * 1000;
                }

                return r;
            }

            protected override WebResponse GetWebResponse(WebRequest request, IAsyncResult result)
            {
                WebResponse response = base.GetWebResponse(request, result);
                ReadCookies(response);
                return response;
            }

            protected override WebResponse GetWebResponse(WebRequest request)
            {
                WebResponse response = null;
                try
                {
                    response = base.GetWebResponse(request);

                    ReadCookies(response);
                }
                catch
                {
                    // ignored
                }

                return response;
            }

            private void ReadCookies(WebResponse r)
            {
                var response = r as HttpWebResponse;
                if (response != null)
                {
                    ResponseCookies = response.Cookies;
                    _container.Add(ResponseCookies);
                }
            }
        }
    }

    public static class EnumerableExtension
    {
        public static T PickRandom<T>(this IEnumerable<T> source)
        {
            return source.PickRandom(1).Single();
        }

        public static IEnumerable<T> PickRandom<T>(this IEnumerable<T> source, int count)
        {
            return source.Shuffle().Take(count);
        }

        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
        {
            return source.OrderBy(x => Guid.NewGuid());
        }


    }

    public static class TimeExt
    {
        public static DateTime FromUnixTime(long unixTime)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddMilliseconds(unixTime);
        }
    }

    public static class BugExt
    {
        public static void BugFix_CookieDomain(CookieContainer cookieContainer)
        {
            Type _ContainerType = typeof(CookieContainer);
            Hashtable table = (Hashtable)_ContainerType.InvokeMember("m_domainTable",
                                       System.Reflection.BindingFlags.NonPublic |
                                       System.Reflection.BindingFlags.GetField |
                                       System.Reflection.BindingFlags.Instance,
                                       null,
                                       cookieContainer,
                                       new object[] { });
            ArrayList keys = new ArrayList(table.Keys);
            foreach (string keyObj in keys)
            {
                string key = (keyObj as string);
                if (key[0] == '.')
                {
                    string newKey = key.Remove(0, 1);
                    table[newKey] = table[keyObj];
                }
            }
        }
    }

    public static class ConsoleExt
    {
        public static void ConsoleWrite(string Name, int proxyCount, int linesCount, string lastDuration)
        {
            var value = $"|{Name,13}| P {proxyCount,5} | Lines {linesCount,10} | Time {lastDuration,10}";
            Console.WriteLine(value);
        }
    }

}