﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bars.EAS.Utils.Extension;
using BM.Core;
using BM.DTO;
using BM.Entities;
using BM.Web;
using Extreme.Net;
using Favbet.Models.Line;
using mevoronin.RuCaptchaNETClient;
using Newtonsoft.Json;
using Scanner;
using Scanner.Helper;

namespace Favbet
{
    public class FavBetScanner : ScannerBase
    {
        static readonly object Lock = new object();

        public override string Name => "Favbet";

        //public override string Host => "https://www.favbet.com/";
        public override string Host => "https://favbet.ro/";
        public string DomainForCookie => ".favbet.ro";

        public static Dictionary<WebProxy, CachedArray<CookieContainer>> CookieDictionary = new Dictionary<WebProxy, CachedArray<CookieContainer>>();


        protected override LineDTO[] GetLiveLines()
        {
            var lines = new List<LineDTO>();

            var randomProxy = ProxyList.PickRandom();

            try
            {
                string response;

                using (var wc = new Extensions.WebClientEx(randomProxy, CookieDictionary[randomProxy].GetData()))
                {
                    wc.Headers["User-Agent"] = GetWebClient.DefaultUserAgent;

                    response = wc.DownloadString($"{Host}live/markets/");
                }

                var sports = JsonConvert.DeserializeObject<Market>(response).Sports;

                var tasks = new List<Task>();

                foreach (var tournament in sports.SelectMany(sport => sport.Tournaments))
                {
                    //убираем чемпионаты 
                    if (tournament.TournamentName.ContainsIgnoreCase("statistics", "crossbar", "goalpost", "fouls", "corners", "offsides", "shot"))
                        continue;

                    tasks.AddRange(tournament.Games.AsParallel()/*.WithDegreeOfParallelism(4)*/
                        .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
                        .Select(gameId => Task.Factory.StartNew(state => ParseGame(gameId, tournament, lines), gameId)));
                }

                try
                {
                    Task.WaitAll(tasks.ToArray(), 10000);
                }
                catch (Exception e)
                {
                    Log.Info("FavBet Task wait all exception, line count " + lines.Count);
                    Console.WriteLine("FavBet Task wait all exception, line count " + lines.Count);
                }

                LastUpdatedDiff = DateTime.Now - LastUpdated;

                ConsoleExt.ConsoleWrite(Name, ProxyList.Count, lines.Count, new DateTime(LastUpdatedDiff.Ticks).ToString("mm:ss"));

                return lines.ToArray();

            }
            catch (Exception e)
            {
                Log.Info($"ERROR FB {e.Message} {e.StackTrace}");
            }

            return new LineDTO[] { };
        }


        private void ParseGame(Game gameId, Tournament tournament, List<LineDTO> lines)
        {
            try
            {
                var converter = new FavBetConverter();

                var random = ProxyList.PickRandom();

                var game = ConverterHelper.GetFullGame(gameId.Id, random, CookieDictionary[random].GetData(), Host);

                if (game == null) return;

                game.TryInitProperties(converter.TeamRegex, converter.ScoreRegex);

                var lineGame = converter.CreateLineWithoutEvents(game, Host, Name);

                if (lineGame == null) return;

                var events = converter.AddEventsToLine(lineGame, game, tournament.TournamentName);

                lock (Lock) lines.AddRange(events);
            }
            catch (Exception e)
            {
                Log.Info("FB Parse event exception " + e.Message);
            }
        }


        protected override void CheckDict()
        {
            var listToDelete = new List<WebProxy>();

            foreach (var host in ProxyList)
            {
                CookieDictionary.Add(host, new CachedArray<CookieContainer>(1000 * 3600 * 3, () =>
                {
                    try
                    {
                        var cc = new CookieContainer();

                        ConsoleExt.ConsoleWriteError($"Favbet check address {host.Address}");

                        cc.Add(PassCloudFlare(host));

                        using (var wc = new Extensions.WebClientEx(host, cc))
                        {
                            wc.Headers["User-Agent"] = GetWebClient.DefaultUserAgent;

                            wc.DownloadString(Host + "en/live/");

                            var d = wc.ResponseHeaders["Set-Cookie"];

                            foreach (var match in d.Split(',').Select(singleCookie => Regex.Match(singleCookie, "(.+?)=(.+?);")).Where(match => match.Captures.Count != 0))
                            {
                                var name = match.Groups[1].ToString();
                                var value = match.Groups[2].ToString();
                                if (name == "PHPSESSID") cc.Add(new Cookie(name, value) { Domain = ProxyHelper.GetDomain(Host) });
                            }

                            cc.Add(wc.CookieContainer.GetAllCookies());
                        }

                        return cc;
                    }
                    catch (Exception)
                    {
                        listToDelete.Add(host);
                        ConsoleExt.ConsoleWriteError($"Favbet delete address {host.Address} listToDelete {listToDelete.Count}");
                    }

                    return null;
                }));
            }

            var tasks = ProxyList.AsParallel().Select(host => Task.Factory.StartNew(state => CookieDictionary[host].GetData(), host)).ToArray();

            Task.WaitAll(tasks.ToArray());

            foreach (var host in listToDelete)
            {
                CookieDictionary.Remove(host);
                ProxyList.Remove(host);
            }
        }

        private CookieCollection PassCloudFlare(WebProxy proxy)
        {
            var cookieCollection = new CookieCollection();

            #region Cloudflare wait 5 sec

            var cookies = CloudFlareNet.CloudFlareNet.GetCloudflareCookies(Host + "en/bets/", GetWebClient.DefaultUserAgent, new HttpProxyClient(proxy.Address.Host, proxy.Address.Port, proxy.Credentials.GetCredential(proxy.Address, "").UserName, proxy.Credentials.GetCredential(proxy.Address, "").Password));

            if (cookies != null && cookies.Any())
            {
                foreach (var cookie in cookies)
                {
                    cookieCollection.Add(new Cookie(cookie.Key, cookie.Value, "/", DomainForCookie));
                }
            }
            else
                //ReCaptcha
                //cookieCollection = CloudflareRecaptcha(proxy);

                cookieCollection.Add(new Cookie("LANG", "en") { Domain = DomainForCookie });

            return cookieCollection;

            #endregion
        }

        private CookieCollection CloudflareRecaptcha(WebProxy proxy)
        {
            var cookieCollection = new CookieCollection();

            string responseText;


            using (var webClient = new GetWebClient(proxy, cookieCollection))
            {
                try
                {
                    responseText = webClient.DownloadString(Host + "en/live/");
                    cookieCollection.Add(webClient.CookieCollection);
                }
                catch (WebException ex)
                {
                    var response = (HttpWebResponse)ex.Response;

                    var encoding = Encoding.ASCII;
                    using (var reader = new StreamReader(response.GetResponseStream(), encoding)) responseText = reader.ReadToEnd();

                    cookieCollection.Add(response.Cookies);
                }
            }

            if (!string.IsNullOrEmpty(responseText) && responseText.ContainsIgnoreCase("sport")) return cookieCollection;

            var ray = Regex.Match(responseText, "data-ray=\"(.+?)\"").Groups[1].Value;
            var sitekey = Regex.Match(responseText, "data-sitekey=\"(.+?)\"").Groups[1].Value;
            var stoken = Regex.Match(responseText, "data-stoken=\"(.+?)\"").Groups[1].Value;

            Log.Info($"FavBet RECAPTCHA Start {sitekey} {stoken}");

            var captchaResponse = RuCaptchaHelper.GetCaptchaResult(sitekey, stoken, Host, proxy.Credentials.GetCredential(proxy.Address, "").UserName, proxy.Address.Host, proxy.Credentials.GetCredential(proxy.Address, "").Password, proxy.Address.Port);

            Log.Info($"FavBet RECAPTCHA Result {captchaResponse}");

            if (captchaResponse.Contains("ERROR")) throw new Exception("Error on ReCaptcha resolve");

            using (var webClient = new GetWebClient(proxy, cookieCollection))
            {
                webClient.DownloadData($"{Host}cdn-cgi/l/chk_captcha?id={ray}&g-recaptcha-response={captchaResponse}");
                cookieCollection.Add(webClient.CookieCollection);
            }

            return cookieCollection;
        }
    }
}
