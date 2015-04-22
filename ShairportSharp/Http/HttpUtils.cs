﻿using PlistCS;
using ShairportSharp.Airplay;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ShairportSharp.Http
{
    static class HttpUtils
    {        
        public static HttpResponse GetEmptyResponse(string status = "200 OK")
        {
            HttpResponse response = new HttpResponse("HTTP/1.1");
            response.Status = status;
            response["Date"] = RfcTimeNow();
            response["Content-Length"] = "0";
            return response;
        }

        public static HttpResponse GetPlistResponse(IPlistResponse plist, bool binary = false)
        {
            return GetPlistResponse(plist.GetPlist(), binary);
        }

        public static HttpResponse GetPlistResponse(Dictionary<string, object> plist, bool binary = false)
        {
            HttpResponse response = new HttpResponse("HTTP/1.1");
            response.Status = "200 OK";
            response["Date"] = RfcTimeNow();
            if (binary)
            {
                response["Content-Type"] = "application/x-apple-binary-plist";
                response.SetContent(Plist.writeBinary(plist));
            }
            else
            {
                response["Content-Type"] = "text/x-apple-plist+xml";
                string plistXml = Plist.writeXml(plist);
                //Logger.Debug("Created plist xml - '{0}'", plistXml);
                response.SetContent(plistXml);
            }
            return response;
        }

        public static bool TryGetPlist(HttpMessage message, out Dictionary<string, object> plist)
        {
            plist = null;
            if (message.ContentLength > 0)
            {
                try
                {
                    plist = (Dictionary<string, object>)Plist.readPlist(message.Content);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error("Exception parsing plist - {0}", ex);
                    Logger.Error("Exception message\r\n{0}", message);
                }
            }
            return false;
        }

        public static string RfcTimeNow()
        {
            return string.Format("{0:R}", DateTime.Now);
        }
    }
}
