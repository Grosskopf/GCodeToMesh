using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace GCodeToMesh
{
    class MainClass
    {
        private static async Task ChatWithServer(GCodeHandler gCodeHandler, String Serveradress, String Downloadfolder)
        {

            using (ClientWebSocket ws = new ClientWebSocket())
            {
                await ws.ConnectAsync(new Uri("ws://" + Serveradress.Substring(7) + "/sockjs/websocket"), CancellationToken.None);

                Console.WriteLine("Connected to websocket, Listening for uploads");
                while (true)
                {
                    ArraySegment<byte> bytesReceived = new ArraySegment<byte>(new byte[4096]);
                    WebSocketReceiveResult result = await ws.ReceiveAsync(
                        bytesReceived, CancellationToken.None);
                    String recieved = Encoding.UTF8.GetString(
                        bytesReceived.Array, 0, result.Count);
                    JObject resultrcv = JObject.Parse(recieved);
                    if (resultrcv.ContainsKey("event") && (String)resultrcv["event"]["type"] == "Upload")
                    {
                        Console.WriteLine("Got "+resultrcv["event"]["payload"]["name"]);
                        gCodeHandler.LoadObject(Serveradress+"/downloads/files/local/" + ((String)resultrcv["event"]["payload"]["name"]), Downloadfolder + ((String)resultrcv["event"]["payload"]["name"]).Split('.')[0] + "/");
                    }
                }
            }
        }
        public static void Main(string[] args)
        {
            GCodeHandler gCodeHandler;
            gCodeHandler = new GCodeHandler();

            int optionscount = 0;
            while (args[1 + optionscount].StartsWith("--", StringComparison.CurrentCulture))
            {
                if (args[1 + optionscount].StartsWith("--j", StringComparison.CurrentCulture))
                {
                    gCodeHandler.threadcount = int.Parse(args[1 + optionscount].Substring(3));
                }
                else if (args[1 + optionscount].StartsWith("--q", StringComparison.CurrentCulture))
                {
                    gCodeHandler.quality = float.Parse(args[1 + optionscount].Substring(3));
                }
                optionscount++;
            }
            if (args.Length > 0 && args[0] == "--autoget")
            {
                String Serveradress = "http://127.0.0.1:5000";
                String Downloadfolder = "./";
                if (args.Length > 1 + optionscount)
                {
                    Serveradress = args[1 + optionscount];
                }
                if (args.Length > 2 + optionscount)
                {
                    Downloadfolder = args[2 + optionscount];
                }
                using (var client = new WebClient())
                {
                    string json = client.DownloadString(Serveradress + "/api/files");

                    var files = JObject.Parse(json)["files"];
                    foreach (var item in files)
                    {
                        gCodeHandler.LoadObject((String)item["refs"]["download"], Downloadfolder + ((String)item["name"]).Split('.')[0] + "/");
                        Console.WriteLine("Got " + item["name"]);
                    }
                    Task t = ChatWithServer(gCodeHandler, Serveradress, Downloadfolder);
                    t.Wait();
                }

            }
            else if (args.Length > 0 && args[0] == "--get")
            {
                String Downloadfolder = "./";
                if (args.Length < 2+ optionscount)
                {
                    Console.WriteLine("This command needs at least a URL to the Exact download site");
                    Console.WriteLine("For example:");
                    Console.WriteLine("\t GCodeToMesh --get http://127.0.0.1:5000/downloads/files/local/starkstromschalter.gcode");
                }
                else if (args.Length > 2+ optionscount)
                {
                    Downloadfolder = args[2+ optionscount];
                }
                else
                {
                    if (args[1+ optionscount].Contains("http"))
                    {
                        gCodeHandler.LoadObject(args[1+ optionscount], Downloadfolder);
                    }
                    else
                    {
                        gCodeHandler.LoadObjectFolder(args[1+optionscount], Downloadfolder);
                        //Console.WriteLine("after all"+DateTimeOffset.Now);
                    }
                }
            }
            else
            {
                Console.WriteLine("Usage: GCodeToMesh Command [options] [url] [folder]");
                Console.WriteLine("");
                Console.WriteLine("Commands:");
                Console.WriteLine("\t --get [options] url [folder] \t\t gets a single file from an URL or folder, and stores it and the Decimated Meshes. If Folder is not set where the executable is, otherwise in the set Folder");
                Console.WriteLine("\t --autoget [options] [url] [folder] \t gets all of the Files from the given URL and stores them in the given Folder. Defaults to http://:5000 and ./");
                Console.WriteLine("");
                Console.WriteLine("Options:");
                Console.WriteLine("\t --j[n] amount of threads to use besides mainthread (otherwise corecount)");
                Console.WriteLine("\t --q[f] goal Quality in percent");
                Console.WriteLine("");
                Console.WriteLine("Example:");
                Console.WriteLine("");
                Console.WriteLine("\t GCodeToMesh --autoget --j8 --q0.7 http://127.0.0.1:5000/ /var/lib/octoprint/.octoprint/uploads");
                Console.WriteLine("");
                Console.WriteLine("Automatically gets any new GCODE and makes Octoprint provide the mesh files that are rendered with 8 threads plus main thread down to 70% quality");
            }

        }
    }
}
