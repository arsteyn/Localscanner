﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Hosting;
using BM.DTO;
using NLog;
using Scanner.Helper;
using Scanner.Interface;

namespace Scanner
{
    public abstract class ScannerBase : IScanner
    {
        protected List<WebProxy> ProxyList;

        public virtual void StartScan()
        {
            ProxyList = ProxyHelper.GetHostList();

            CheckDict();

            WriteLiveProxy();

            while (true)
            {
                var result = GetLiveLines();

                if (result == null || !result.Any()) continue;

                ActualLines = result;

                LastUpdated = DateTime.Now;
            }
        }


        protected DateTime LastUpdated { get; set; }

        protected TimeSpan LastUpdatedDiff { get; set; }

        protected LineDTO[] _actualLines;

        public virtual LineDTO[] ActualLines
        {
            get { return LastUpdated.AddSeconds(30) > DateTime.Now ? _actualLines : new LineDTO[] { }; }
            set { _actualLines = value; }
        }

        protected virtual Logger Log => LogManager.GetCurrentClassLogger();


        public abstract string Name { get; }

        public abstract string Host { get; }

        protected virtual string Domain => new Uri(Host).Host;

        protected abstract LineDTO[] GetLiveLines();

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
}
