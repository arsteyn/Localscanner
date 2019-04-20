﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Hosting;
using BM;
using BM.DTO;
using NLog;
using NLog.Fluent;
using Scanner.Helper;
using Scanner.Interface;

namespace Scanner
{
    public abstract class ScannerBase : IScanner
    {
        protected List<WebProxy> ProxyList;

        protected Dictionary<long, EventUpdateObject> _linesDictionary = new Dictionary<long, EventUpdateObject>();

        public virtual void StartScan()
        {
            ProxyList = ProxyHelper.GetHostList();

            CheckDict();

            WriteLiveProxy();

            while (true)
            {
                if (!ProxyList.Any() && Name != "Betfair")
                {
                    Log.Info($"ERROR {Name} no proxy");
                    Thread.Sleep(10000);
                    continue;
                }

                UpdateLiveLines();

                LastUpdated = DateTime.Now;
            }
        }



        protected DateTime LastUpdated { get; set; }

        protected TimeSpan LastUpdatedDiff { get; set; }

        private LineDTO[] _actualLines;

        public virtual LineDTO[] ActualLines
        {
            get
            {
                if (LastUpdated.AddSeconds(30) <= DateTime.Now) return new LineDTO[] { };

                return _linesDictionary.Any()
                    ? _linesDictionary.Where(item => item.Value.LineDtos != null && item.Value.LineDtos.Any()/* && item.Value.LastUpdated.AddSeconds(10) >= DateTime.Now*/).SelectMany(item => item.Value.LineDtos).ToArray()
                    : _actualLines;
            }
            set => _actualLines = value;
        }

        public virtual Logger Log => LogManager.GetCurrentClassLogger();


        public abstract string Name { get; }

        public abstract string Host { get; }

        protected virtual string Domain => new Uri(Host).Host;

        protected abstract void UpdateLiveLines();

        protected virtual void WriteLiveProxy()
        {

            var path = HostingEnvironment.ApplicationPhysicalPath + $"CheckedProxy/{Name}.txt";

            using (var outputFile = new StreamWriter(path, false))
            {
                foreach (var proxy in ProxyList)
                {
                    outputFile.WriteLine($"{proxy.Address.Host}");
                }


            }
        }

        protected virtual void CheckDict()
        {
            var hostsToDelete = new List<WebProxy>();

            Parallel.ForEach(ProxyList, (host, state) =>
            {
                try
                {
                    using (var webClient = new Extensions.WebClientEx(host))
                    {
                        webClient.DownloadString(Host);
                    }
                }
                catch (Exception e)
                {
                    hostsToDelete.Add(host);
                }
            });

            foreach (var host in hostsToDelete) ProxyList.Remove(host);
        }
    }

    public class EventUpdateObject
    {
        public EventUpdateObject(Func<List<LineDTO>> updateAction/*, CancellationTokenSource tokenSource*/)
        {
            UpdateAction = updateAction;

            //CancellationTokenSource = tokenSource;

            //Task.Factory.StartNew(() =>
            //{
            //    while (!tokenSource.Token.IsCancellationRequested)
            //    {
                    try
                    {
                        var result = UpdateAction().Select(l => l.Clone()).ToList();

                        LineDtos = result;

                        //LastUpdated = DateTime.Now;
                    }
                    catch (WebException e)
                    {
                        //LogManager.GetCurrentClassLogger().Info("Dafabet EventUpdateObject WebException");
                    }
                    catch (Exception e)
                    {
                        //LogManager.GetCurrentClassLogger().Info("Dafabet EventUpdateObject otherexception" + e.Message + e.InnerException + e.StackTrace);
                    }
            //    }
            //    var f = 9;
            //}, tokenSource.Token);
        }

        public CancellationTokenSource CancellationTokenSource { get; set; }

        public List<LineDTO> LineDtos { get; set; }

        private Func<List<LineDTO>> UpdateAction { get; }

        //public DateTime LastUpdated { get; set; }

    }
}
