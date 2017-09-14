﻿using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using Bars.EAS.Utils.Extension;
using BM.DTO;
using Lz4Net;
using Newtonsoft.Json;

namespace Scanner.SelfHosted.Controllers
{
    public class HomeController : ApiController
    {
        public HttpResponseMessage Get(string bookmakerName)
        {
            var module = ScannerApiSelfHost.BookmakerScanners.FirstOrDefault(m => m.Name.ContainsIgnoreCase(bookmakerName));

            return module!= null ? GetResponse(module.GetLines()) : new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        #region Private

        private static HttpResponseMessage GetResponse(LineDTO[] lines)
        {
            var json = JsonConvert.SerializeObject(lines);
            var compressed = Lz4.CompressString(json);

            return new HttpResponseMessage
            {
                Content = new StringContent(compressed, Encoding.UTF8, "text/html")
            };
        }

        #endregion

    }
}
