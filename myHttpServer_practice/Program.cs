using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace myHttpServer_practice
{
    class Program
    {
        static string path = @"C:\temp\arts";


        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            MicroHttpServer mhs = new MicroHttpServer(1688,
            (req) =>
            {
                if (req.Url == "/")
                    return ListPhoto(req);
                else if (req.Url.EndsWith(".png"))
                    return GetJpeg(
                        Path.Combine(path, req.Url.TrimStart('/')));
                else return new CompactResponse()
                {
                    StatusText = CompactResponse.HttpStatus.Http500
                };
            });
            Console.Write("Press any key to stop...");
            Console.Read();
            mhs.Stop();

        }



        static CompactResponse ListPhoto(CompactRequest req)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(@"
                        <html>
                            <head>
                                <title>Index</title>
                                    <style type='text/css'>img { width: 160px; height: 120px; float: left; margin: 10px;}</style>
                            </head>
                         <body>
                     ");
            sb.Append("");
            foreach (string file in
                Directory.GetFiles(path, "*.png"))
                sb.AppendFormat("<img src='{0}' />",
                    Path.GetFileName(file));
            sb.Append("</body></html>");
            return new CompactResponse()
            {
                ContentType = "text/html",
                Data = Encoding.UTF8.GetBytes(sb.ToString())
            };
        }
        //取得圖檔
        static CompactResponse GetJpeg(string file)
        {
            if (File.Exists(file))
                return new CompactResponse()
                {
                    ContentType = "image/jpeg",
                    Data = File.ReadAllBytes(file)
                };
            else //找不到檔案時傳回HTTP 404
                return new CompactResponse()
                {
                    StatusText = CompactResponse.HttpStatus.Http404
                };
        }


    }
    //Reuquest物件
    public class CompactRequest
    {
        public string Method, Url, Protocol;
        public Dictionary<string, string> Headers;
        //傳入StreamReader，讀取Request傳入的內容
        public CompactRequest(StreamReader sr)
        {
            //第一列格式如: GET /index.html HTTP/1.1
            string firstLine = sr.ReadLine();
            string[] p = firstLine.Split(' ');
            Method = p[0];
            Url = (p.Length > 1) ? p[1] : "NA";
            Protocol = (p.Length > 2) ? p[2] : "NA";
            //讀取其他Header，格式為HeaderName: HeaderValue
            string line = null;
            Headers = new Dictionary<string, string>();
            while (!string.IsNullOrEmpty(line = sr.ReadLine()))
            {
                int pos = line.IndexOf(":");
                if (pos > -1)
                    Headers.Add(line.Substring(0, pos),
                        line.Substring(pos + 1));
            }
        }
    }
    //Response物件
    public class CompactResponse
    {
        //預設200, 404, 500三種回應
        public class HttpStatus
        {
            public static string Http200 = "200 OK";
            public static string Http404 = "404 Not Found";
            public static string Http500 = "500 Error";
        }
        public string StatusText = HttpStatus.Http200;
        public string ContentType = "text/plain";
        //可回傳Response Header
        public Dictionary<string, string> Headers
            = new Dictionary<string, string>();
        //傳回內容，以byte[]表示
        public byte[] Data = new byte[] { };
    }
    //簡陋但堪用的HTTP Server
    public class MicroHttpServer
    {
        private Thread serverThread;
        TcpListener listener;
        //呼叫端要準備一個函數，接收CompactRequest，回傳CompactResponse
        public MicroHttpServer(int port,
            Func<CompactRequest, CompactResponse> reqProc)
        {
            IPAddress ipAddr = IPAddress.Parse("0.0.0.0");
            listener = new TcpListener(ipAddr, port);
            //另建Thread執行
            serverThread = new Thread(() =>
            {
                listener.Start();
                while (true)
                {
                    Socket s = listener.AcceptSocket();
                    NetworkStream ns = new NetworkStream(s);

                    if (ns.ReadByte() < 0)
                    {
                        return;
                    }

                    //解讀Request內容
                    StreamReader sr = new StreamReader(ns);
                    CompactRequest req = new CompactRequest(sr);
                    //呼叫自訂的處理邏輯，得到要回傳的Response
                    CompactResponse resp = reqProc(req);
                    //傳回Response
                    StreamWriter sw = new StreamWriter(ns);
                    sw.WriteLine("HTTP/1.1 {0}", resp.StatusText);
                    sw.WriteLine("Content-Type: " + resp.ContentType);
                    foreach (string k in resp.Headers.Keys)
                        sw.WriteLine("{0}: {1}", k, resp.Headers[k]);
                    sw.WriteLine("Content-Length: {0}", resp.Data.Length);
                    sw.WriteLine();
                    sw.Flush();
                    //寫入資料本體
                    s.Send(resp.Data);
                    //結束連線
                    s.Shutdown(SocketShutdown.Both);
                    ns.Close();
                }
            });
            serverThread.Start();
        }
        public void Stop()
        {
            listener.Stop();
            serverThread.Abort();
        }
    }
}
