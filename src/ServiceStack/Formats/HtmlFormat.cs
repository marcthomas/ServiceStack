using System;
using System.IO;
using System.Linq;
using ServiceStack.Serialization;
using ServiceStack.Text;
using ServiceStack.Web;

namespace ServiceStack.Formats
{
	public class HtmlFormat : IPlugin
	{
		public static string TitleFormat
			= @"{0} Snapshot of {1}";

		public static string HtmlTitleFormat
			= @"Snapshot of <i>{0}</i> generated by <a href=""http://www.servicestack.net"">ServiceStack</a> on <b>{1}</b>";

		public static string HtmlTemplate;

		private IAppHost AppHost { get; set; }

		public void Register(IAppHost appHost)
		{
			AppHost = appHost;
			//Register this in ServiceStack with the custom formats
            appHost.ContentTypes.Register(MimeTypes.Html, SerializeToStream, null);
            appHost.ContentTypes.Register(MimeTypes.JsonReport, SerializeToStream, null);

            appHost.Config.DefaultContentType = MimeTypes.Html;
            appHost.Config.IgnoreFormatsInMetadata.Add(MimeTypes.Html.ToContentFormat());
            appHost.Config.IgnoreFormatsInMetadata.Add(MimeTypes.JsonReport.ToContentFormat());
		}

		public void SerializeToStream(IRequestContext requestContext, object response, IHttpResponse httpRes)
		{
            var httpReq = requestContext.Get<IHttpRequest>();
            var httpResult = httpReq.GetItem("HttpResult") as IHttpResult;
            if (httpResult != null && httpResult.Headers.ContainsKey(HttpHeaders.Location))
                return;

            if (httpReq != null && AppHost.ViewEngines.Any(x => x.ProcessRequest(httpReq, httpRes, response))) return;

            if (requestContext.ResponseContentType != MimeTypes.Html && httpReq != null
                && httpReq.ResponseContentType != MimeTypes.JsonReport) return;

		    var dto = response.GetDto();
		    var html = dto as string;
            if (html == null)
            {
                // Serialize then escape any potential script tags to avoid XSS when displaying as HTML
                var json = JsonDataContractSerializer.Instance.SerializeToString(dto) ?? "null";
                json = json.Replace("<", "&lt;").Replace(">", "&gt;");

                string url = string.Empty;
                if (httpReq != null)
                {
                    url = httpReq.AbsoluteUri
                                 .Replace("format=html", "")
                                 .Replace("format=shtm", "")
                                 .TrimEnd('?', '&');

                    url += url.Contains("?") ? "&" : "?";
                }

                var now = DateTime.UtcNow;
                string requestName = string.Empty;
                if (httpReq != null) requestName = httpReq.OperationName ?? dto.GetType().Name;

                html = GetHtmlTemplate()
                        .Replace("${Dto}", json)
                        .Replace("${Title}", string.Format(TitleFormat, requestName, now))
                        .Replace("${MvcIncludes}", MiniProfiler.Profiler.RenderIncludes().ToString())
                        .Replace("${Header}", string.Format(HtmlTitleFormat, requestName, now))
                        .Replace("${ServiceUrl}", url);

            }

			var utf8Bytes = html.ToUtf8Bytes();
			httpRes.OutputStream.Write(utf8Bytes, 0, utf8Bytes.Length);
		}

		private string GetHtmlTemplate()
		{
			if (string.IsNullOrEmpty(HtmlTemplate))
			{
				HtmlTemplate = LoadHtmlTemplateFromEmbeddedResource();
			}
			return HtmlTemplate;
		}

		private string LoadHtmlTemplateFromEmbeddedResource()
		{
			// ServiceStack.Formats.HtmlFormat.html
			string embeddedResourceName = GetType().Namespace + ".HtmlFormat.html";
			var stream = GetType().Assembly.GetManifestResourceStream(embeddedResourceName);
			if (stream == null)
			{
				throw new FileNotFoundException(
					"Could not load HTML template embedded resource " + embeddedResourceName,
					embeddedResourceName);
			}
			using (var streamReader = new StreamReader(stream))
			{
				return streamReader.ReadToEnd();
			}
		}
	}

}