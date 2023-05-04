using System.Dynamic;
using System.IO.IsolatedStorage;
using System.Reflection;

namespace TimeTrack
{
    public static class Program
    {
        public static void Main()
        {
            /*
             * CLI tool that manages the Service 
             */
            CheckInstall();
            Console.WriteLine("Welcome to the TimeTrack CLI tool");
            while (true)
            {
                Console.WriteLine("What would you like to do?");
                
                string input = Console.ReadLine();
                switch (input)
                {
                    case "1":
                        
                        break;
                    default:
                        Console.WriteLine("Invalid input");
                        break;
                } // TODO manual update
            }
        }
        
        private static void CheckInstall()
        {
            var path = Directory.GetCurrentDirectory();
            if (!File.Exists(path + "/TimeTrackService.exe"))
            {
                Console.WriteLine("TimeTrackService.exe not found, installing...");
                //RunScript()
            }
            if (!File.Exists(path + "/UpdaterService.exe"))
            {
                Console.WriteLine("UpdaterService.exe not found, installing...");
                //RunScript()
            }
        }
        
        public static void RunScript(string arg)
        {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = $"/C {arg}";
            process.StartInfo = startInfo;
            process.Start();
            // curl -O https://github.com/aquasecurity/tfsec/releases/latest/download/tfsec-linux-amd64
        }
    }
}

/*
// The port number must match the port of the gRPC server.
using var channel = GrpcChannel.ForAddress("https://localhost:7042");
var client = new Greeter.GreeterClient(channel);
var reply = await client.SayHelloAsync(
    new HelloRequest { Name = "GreeterClient" });
Console.WriteLine("Greeting: " + reply.Message);
Console.WriteLine("Press any key to exit...");
Console.ReadKey();
*/ // huhhhhhh gRPC



