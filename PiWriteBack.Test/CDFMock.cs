using Cognite.Extractor.Utils;
using CogniteSdk;
using CogniteSdk.Login;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Cognite.PiWriteBack.Test
{
    internal class HttpMessageHandlerStub : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsync;

        public HttpMessageHandlerStub(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        {
            _sendAsync = sendAsync;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return await _sendAsync(request, cancellationToken);
        }
    }


    public class CDFMock
    {
        private readonly object _handlerLock = new object();
        public Dictionary<(string, string), Dictionary<string, JsonElement>> RawDatabases { get; } =
            new Dictionary<(string, string), Dictionary<string, JsonElement>>();
        public Dictionary<string, Dictionary<string, JsonElement>> TimeSeries { get; } =
            new Dictionary<string, Dictionary<string, JsonElement>>();
        public bool AllowRequest { get; set; } = true;
        public int RequestCount { get; private set; }

        private readonly string _project;
        private int _requestIdCounter = 0;
        private readonly ILogger _log;

        private readonly CogniteConfig _config;

        public CDFMock(CogniteConfig config, ILogger<CDFMock> log)
        {
            _project = config.Project;
            _log = log;
            _config = config;
        }

        private HttpResponseMessage GetFailedRequest(HttpStatusCode code)
        {
            var res = new HttpResponseMessage(code)
            {
                Content = new StringContent(JsonSerializer.Serialize(new ApiResponseError
                {
                    Error = new ResponseError
                    {
                        Code = (int)code,
                        Message = code.ToString()
                    },
                    RequestId = _requestIdCounter.ToString()
                }, Oryx.Cognite.Common.jsonOptions))
            };
            res.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            res.Headers.Add("x-request-id", _requestIdCounter.ToString(CultureInfo.InvariantCulture));
            return res;
        }

        public HttpMessageHandler CreateHandler()
        {
            return new HttpMessageHandlerStub(MessageHandler);
        }

        private async Task<HttpResponseMessage> MessageHandler(HttpRequestMessage req, CancellationToken token)
        {
            string content = null;
            try
            {
                content = await req.Content.ReadAsStringAsync();
            }
            catch { }
            HttpResponseMessage res;
            lock (_handlerLock)
            {
                RequestCount++;
                _requestIdCounter++;

                if (!AllowRequest) return GetFailedRequest(HttpStatusCode.BadRequest);

                if (req.RequestUri.AbsolutePath == "/login/status")
                {
                    var loginRes = HandleLoginStatus();
                    loginRes.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    loginRes.Headers.Add("x-request-id", _requestIdCounter.ToString(CultureInfo.InvariantCulture));
                    return loginRes;
                }

                string reqPath = req.RequestUri.AbsolutePath.Replace($"/api/v1/projects/{_project}", "");

                _log.LogInformation("Request to {Path}", reqPath);

                var rawRegex = new Regex("raw/dbs/([a-zA-Z0-9]+)/tables/([a-zA-Z0-9]+)/rows");
                var rawMatch = rawRegex.Match(reqPath);
                var tsRegex = new Regex("timeseries/data/list");
                var tsMatch = tsRegex.Match(reqPath);

                if (rawMatch.Success)                  
                {
                    string db = rawMatch.Groups[1].Value;
                    string table = rawMatch.Groups[2].Value;

                    _log.LogInformation("Inserting rows for {db}, {tab}", db, table);

                    res = HandleCreateRawRows(db, table, content);
                }
                else if (tsMatch.Success)
                {
                    _log.LogInformation("retriving datapoints for...");

                    res = HandleCreateTimeSeriesAndDataPoints(content);
                }
                else
                {
                    res = new HttpResponseMessage(HttpStatusCode.NotFound);
                }
            }

            res.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            res.Headers.Add("x-request-id", _requestIdCounter.ToString(CultureInfo.InvariantCulture));

            return res;
        }

        private HttpResponseMessage HandleLoginStatus()
        {
            var status = new LoginStatus
            {
                ApiKeyId = 1,
                LoggedIn = true,
                Project = _project,
                ProjectId = 1,
                User = "user"
            };

            var wrapper = new LoginDataRead
            {
                Data = status
            };

            var data = JsonSerializer.Serialize(wrapper, Oryx.Cognite.Common.jsonOptions);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(data)
            };
        }

        private HttpResponseMessage HandleCreateRawRows(string db, string table, string content)
        {
            if (!RawDatabases.TryGetValue((db, table), out var raw))
            {
                RawDatabases[(db, table)] = raw = new Dictionary<string, JsonElement>();
            }

            var toInsert = JsonSerializer.Deserialize<ItemsWithoutCursor<RawRow<JsonElement>>>(
                content, Oryx.Cognite.Common.jsonOptions);

            foreach (var row in toInsert.Items)
            {
                raw[row.Key] = row.Columns;
                _log.LogInformation("Raw row {Key}: {Columns} {Db}, {Table}", row.Key, raw.Count, db, table);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };
        }

        private HttpResponseMessage HandleCreateTimeSeriesAndDataPoints(string content)
        {

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };
        }
    }
}
